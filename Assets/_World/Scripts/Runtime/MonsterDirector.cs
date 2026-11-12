using UnityEngine;
using UnityEngine.XR;
using Unity.XR.CoreUtils;
using Blackout.Experience;

namespace Blackout.World
{
    /// <summary>
    /// Owns every monster: spawns the right number for the chosen difficulty when a run starts, passes sounds (sonar pings,
    /// relays switching on) to them, turns "a monster is close" into a heartbeat, captions and a soft haptic pulse,
    /// ends the run when one catches the player, and kills them all when the message is delivered at dawn.
    /// </summary>
    public sealed class MonsterDirector : MonoBehaviour
    {
        public static MonsterDirector Instance { get; private set; }

        public Monster[] monsters = new Monster[0];
        public SonarPing sonar;
        public Vector2 roadOrigin = new Vector2(-168f, -168f);
        public float roadSpacing = 48f;
        public int roadCount = 8;

        [Header("Noise (metres a monster can hear it, before difficulty scaling)")]
        public float pingNoiseRadius = 30f;
        public float relayNoiseRadius = 22f;

        public RoadGraph Graph { get; private set; }
        public bool HasPlayer => _cam != null;
        public Vector3 PlayerEye => _cam != null ? _cam.transform.position : Vector3.zero;
        public Vector3 PlayerPos { get { var p = PlayerEye; return p; } }
        public bool PlayerStill => _smoothSpeed < 0.3f;
        /// <summary>How visible the player is: lantern state times hiding-spot concealment.</summary>
        public float PlayerVisibility => (Lantern.Instance != null ? Lantern.Instance.Visibility : 1f) * HidingSpot.Concealment;
        public MonsterTuning Tuning { get; private set; }

        Camera _cam;
        XROrigin _origin;
        Vector3 _lastCam;
        float _smoothSpeed;
        bool _caught;
        AudioSource _heart;
        AudioClip _beatClip;
        float _nextBeat, _nextWarn;

        void Awake()
        {
            Instance = this;
            Graph = new RoadGraph(roadOrigin, roadSpacing, roadCount);
            _heart = gameObject.AddComponent<AudioSource>();
            _heart.spatialBlend = 0f;
            _heart.playOnAwake = false;
            _beatClip = MakeBeat();
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void OnEnable()
        {
            GameFlow.RunStarted += Begin;
            GameFlow.Delivered += Dawn;
            GameFlow.StateChanged += OnState;
            if (sonar != null) sonar.Pinged += OnPing;
        }

        void OnDisable()
        {
            GameFlow.RunStarted -= Begin;
            GameFlow.Delivered -= Dawn;
            GameFlow.StateChanged -= OnState;
            if (sonar != null) sonar.Pinged -= OnPing;
        }

        void Start() { HideAll(); }

        void OnState(GameState s)
        {
            if (s == GameState.MainMenu || s == GameState.HealthNotice) HideAll();
        }

        void HideAll()
        {
            foreach (var m in monsters) if (m != null) m.Hide();
        }

        public void Begin()
        {
            _caught = false;
            Tuning = MonsterTuning.For(GameSettings.MonsterLevel);
            FindPlayer();
            for (int i = 0; i < monsters.Length; i++)
            {
                var m = monsters[i];
                if (m == null) continue;
                if (i < Tuning.count)
                {
                    m.Configure(Tuning, Graph, this, i + 1);
                    m.Spawn(m.home);
                }
                else m.Hide();
            }
            _nextBeat = 0f; _nextWarn = Time.time + 20f;
        }

        void Dawn()
        {
            foreach (var m in monsters) if (m != null) m.Die();
            if (Tuning.count > 0 && CaptionSystem.Instance != null)
                CaptionSystem.Instance.ShowText("City", "At the first light, the creatures cry out and fall still.", 4.5f);
        }

        void OnPing(Vector3 origin) { NoiseAt(origin, pingNoiseRadius); }

        /// <summary>Anything loud (a relay switching on) calls this so nearby monsters come to look.</summary>
        public void NoiseAt(Vector3 pos, float radius)
        {
            if (GameFlow.State != GameState.Playing) return;
            foreach (var m in monsters) if (m != null) m.Hear(pos, radius);
        }

        public bool IsPlayer(Transform t) => _origin != null && t.IsChildOf(_origin.transform);

        void FindPlayer()
        {
            if (_origin == null) _origin = FindFirstObjectByType<XROrigin>();
            if (_cam == null) _cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            if (_cam != null) _lastCam = _cam.transform.position;
            _smoothSpeed = 0f;
        }

        public void Catch(Monster by)
        {
            if (_caught || GameFlow.State != GameState.Playing) return;
            _caught = true;
            if (by != null && by.attackClip != null) _heart.PlayOneShot(by.attackClip, 0.9f);
            foreach (var m in monsters) if (m != null) m.Freeze();
            KeepClearOfMenu();
            GameFlow.OnCaught();
        }

        /// <summary>The end screen floats in front of the player; any monster that close would hide it, so step it back.</summary>
        void KeepClearOfMenu()
        {
            if (_cam == null) return;
            var camPos = _cam.transform.position;
            foreach (var m in monsters)
            {
                if (m == null || !m.gameObject.activeSelf) continue;
                var d = m.transform.position - camPos; d.y = 0f;
                if (d.magnitude >= 4f) continue;
                if (d.sqrMagnitude < 1e-4f) d = _cam.transform.forward;
                m.PlaceFacing(camPos + d.normalized * 4.2f, camPos);
            }
        }

        public void NotifySeen(Monster m)
        {
            Say("A monster has seen you. It is " + Where(m.transform.position) + ".", 3.5f);
            Haptic(0.6f, 0.25f);
        }

        string Where(Vector3 pos)
        {
            if (_cam == null) return "close";
            var fwd = _cam.transform.forward; fwd.y = 0f;
            var to = pos - _cam.transform.position; to.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) return "close";
            return SonarGameplay.DirectionWord(Vector3.SignedAngle(fwd, to, Vector3.up));
        }

        static void Say(string text, float seconds)
        {
            if (CaptionSystem.Instance != null) CaptionSystem.Instance.ShowText("Danger", text, seconds);
        }

        void Update()
        {
#if UNITY_EDITOR
            // Editor-only test keys: F9 = deliver the message now (dawn), F8 = make the nearest monster notice you.
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && GameFlow.State == GameState.Playing)
            {
                if (kb.f9Key.wasPressedThisFrame) GameFlow.OnDelivered();
                if (kb.f8Key.wasPressedThisFrame) NoiseAt(PlayerPos, 9999f);
            }
#endif
            if (GameFlow.State != GameState.Playing) return;
            if (_cam == null) FindPlayer();
            if (_cam == null) return;

            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            var p = _cam.transform.position;
            float v = new Vector2(p.x - _lastCam.x, p.z - _lastCam.z).magnitude / dt;
            _lastCam = p;
            _smoothSpeed = Mathf.Lerp(_smoothSpeed, Mathf.Min(v, 6f), 1f - Mathf.Exp(-dt / 0.35f));

            UpdateDanger(dt);
        }

        void UpdateDanger(float dt)
        {
            Monster nearest = null; float best = 1e9f;
            foreach (var m in monsters)
            {
                if (m == null || !m.IsAwake) continue;
                var d = m.transform.position - _cam.transform.position; d.y = 0f;
                float dist = d.magnitude;
                if (dist < best) { best = dist; nearest = m; }
            }
            if (nearest == null) return;

            float closeness = Mathf.Clamp01(1f - (best - 3f) / 17f);
            if (nearest.State == MonsterState.Chase) closeness = Mathf.Max(closeness, 0.5f);

            if (closeness > 0.05f)
            {
                _nextBeat -= dt;
                if (_nextBeat <= 0f)
                {
                    _heart.PlayOneShot(_beatClip, 0.2f + 0.6f * closeness);
                    Haptic(0.1f + 0.3f * closeness, 0.08f);
                    _nextBeat = Mathf.Lerp(1.15f, 0.45f, closeness);
                }
            }

            if (nearest.State != MonsterState.Chase && best < 22f && Time.time >= _nextWarn)
            {
                _nextWarn = Time.time + 9f;
                Say("Heavy footsteps " + Where(nearest.transform.position) + ". Hold still.", 3.5f);
            }
        }

        // ------------------------------------------------------------------ feedback helpers

        static void Haptic(float amplitude, float seconds)
        {
            try
            {
                SendTo(XRNode.LeftHand, amplitude, seconds);
                SendTo(XRNode.RightHand, amplitude, seconds);
            }
            catch { /* haptics are a bonus; never break gameplay */ }
        }

        static void SendTo(XRNode node, float amplitude, float seconds)
        {
            var device = InputDevices.GetDeviceAtXRNode(node);
            if (device.isValid && device.TryGetHapticCapabilities(out var caps) && caps.supportsImpulse)
                device.SendHapticImpulse(0u, amplitude, seconds);
        }

        /// <summary>A soft two-part heartbeat built in code, so there is nothing to import.</summary>
        static AudioClip MakeBeat()
        {
            const int rate = 22050;
            int n = (int)(rate * 0.42f);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                data[i] = Thump(t, 0f) + 0.7f * Thump(t, 0.17f);
            }
            var clip = AudioClip.Create("Heartbeat", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Thump(float t, float start)
        {
            float x = t - start;
            if (x < 0f) return 0f;
            float env = Mathf.Exp(-x * 22f) * Mathf.Clamp01(x * 400f);
            return Mathf.Sin(2f * Mathf.PI * (52f - 90f * x) * x) * env * 0.9f;
        }
    }
}
