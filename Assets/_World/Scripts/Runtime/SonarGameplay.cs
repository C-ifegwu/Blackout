using System.Collections;
using UnityEngine;
using Unity.XR.CoreUtils;
using Blackout.Experience;

namespace Blackout.World
{
    /// <summary>
    /// Runs the BLACKOUT objective: follow three relay tones across the dark city, ping at each one to switch it on,
    /// then send the voice message at the radio tower. Everything also works without sonar (hold still near a relay
    /// or the tower for a few seconds), and spoken-direction captions help players who cannot rely on stereo sound.
    /// </summary>
    public sealed class SonarGameplay : MonoBehaviour
    {
        public Transform spawn;
        public RelayBeacon[] relays = new RelayBeacon[0];
        public Transform tower;
        public AudioSource towerAudio;
        public SonarPing sonar;
        public PuzzleChain chain;
        public AudioClip relayOnClip;

        public float zoneRadius = 6f;
        public float dwellSeconds = 8f;
        public float hintInterval = 45f;

        [Header("Beat the dawn (optional challenge)")]
        public float challengeSeconds = 600f;
        public float challengeHintInterval = 60f;

        /// <summary>Seconds left in a Beat the dawn run, or -1 when Story mode is on.</summary>
        public float TimeLeft { get; private set; } = -1f;
        float _runStartTime;
        int _lastWarn = int.MaxValue;
        static readonly int[] Warnings = { 240, 180, 120, 60, 30, 10 };

        /// <summary>0..relays.Length-1 = going to that relay, relays.Length = going to the tower, +1 = delivered.</summary>
        public int Stage { get; private set; }

        Camera _cam;
        XROrigin _origin;
        AudioSource _sfx;
        float _dwell, _lastPing = -99f, _nextHint;
        bool _prompted, _delivering, _towerPlayed;
        float _nextSealHint;
        Coroutine _routine;

        void Awake()
        {
            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.spatialBlend = 0f;
            _sfx.playOnAwake = false;
            _sfx.ignoreListenerPause = true;
        }

        void OnEnable()
        {
            GameFlow.RunStarted += ResetRun;
            GameFlow.StateChanged += OnState;
            if (sonar != null) { sonar.Pinged += OnPing; sonar.Interacted += OnInteracted; }
            SonarPing.InteractContext = () => GameFlow.State == GameState.Playing && !_delivering && InInteractRange;
        }

        void OnDisable()
        {
            GameFlow.RunStarted -= ResetRun;
            GameFlow.StateChanged -= OnState;
            if (sonar != null) { sonar.Pinged -= OnPing; sonar.Interacted -= OnInteracted; }
            SonarPing.InteractContext = null;
        }

        void Start() { SilenceAll(); }

        void OnState(GameState s)
        {
            if (s == GameState.MainMenu) SilenceAll();
        }

        void OnPing(Vector3 _) { _lastPing = Time.time; }
        void OnInteracted() { Interact(Stage); }

        Vector3 InteractPos
        {
            get
            {
                if (Stage < relays.Length) return relays[Stage] != null ? relays[Stage].transform.position : transform.position;
                return tower != null ? tower.position : transform.position;
            }
        }

        /// <summary>True when the player stands close enough to the current relay or the tower radio to use it.</summary>
        public bool InInteractRange => Stage <= relays.Length && PlayerRig.HasPlayer && PlayerRig.FlatDistance(InteractPos) < zoneRadius;

        /// <summary>Grab or press on a console. idx 0..relays.Length-1 are relays, relays.Length is the tower radio.</summary>
        public void Interact(int idx)
        {
            if (GameFlow.State != GameState.Playing || _delivering) return;
            Vector3 pos = idx < relays.Length && relays[idx] != null ? relays[idx].transform.position : (tower != null ? tower.position : transform.position);
            if (PlayerRig.FlatDistance(pos) > zoneRadius + 2f) { SayText("Radio", "Get closer to use it.", 3f); return; }
            if (idx == Stage && Stage < relays.Length) { Advance(); return; }
            if (idx == relays.Length && Stage == relays.Length)
            {
                if (chain == null || chain.Solved) { Advance(); return; }
                SayText("Seal", chain.SealHint(), 6f);
                if (FeedbackFx.Instance != null) FeedbackFx.Instance.Denied(pos + Vector3.up);
                return;
            }
            string msg = idx < Stage ? "This one is already online." : idx < relays.Length ? "Not this one yet. Follow the glow to the next relay." : "Switch on all three relays first.";
            SayText("Radio", msg, 4f);
            if (FeedbackFx.Instance != null && idx >= Stage) FeedbackFx.Instance.Denied(pos + Vector3.up);
        }

        void SilenceAll()
        {
            foreach (var r in relays) if (r != null) r.ResetState();
            if (towerAudio != null) towerAudio.Stop();
            if (sonar != null) sonar.ClearEchoes();
            Stage = 0; _delivering = false; _prompted = false; _dwell = 0f; _towerPlayed = false; _nextSealHint = 0f;
        }

        public void ResetRun()
        {
            if (_routine != null) { StopCoroutine(_routine); _routine = null; }
            SilenceAll();
            Teleport();
            Stage = 0;
            if (relays.Length > 0 && relays[0] != null) relays[0].SetCurrent(true);
            _nextHint = Time.time + CurrentHintInterval;
            TimeLeft = GameSettings.Challenge ? challengeSeconds : -1f;
            _runStartTime = Time.time;
            _lastWarn = int.MaxValue;
            _routine = StartCoroutine(Intro());
        }

        float CurrentHintInterval => GameSettings.Challenge ? challengeHintInterval : hintInterval;

        void TickChallenge()
        {
            if (TimeLeft < 0f) return;
            TimeLeft = Mathf.Max(0f, challengeSeconds - (Time.time - _runStartTime));
            foreach (int w in Warnings)
            {
                if (TimeLeft <= w && _lastWarn > w)
                {
                    _lastWarn = w;
                    string when = w >= 60 ? (w / 60) + (w == 60 ? " minute" : " minutes") : w + " seconds";
                    SayText("Radio", "Dawn in " + when + ".", 3.5f);
                    break;
                }
            }
            if (TimeLeft <= 0f) GameFlow.OnTimeUp();
        }

        void Teleport()
        {
            if (spawn == null) return;
            if (_origin == null) _origin = FindFirstObjectByType<XROrigin>();
            if (_origin == null) return;
            var cc = _origin.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            _origin.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
            if (cc != null) cc.enabled = true;
            PlayerRig.CheckpointPos = spawn.position; PlayerRig.CheckpointRot = spawn.rotation;
        }

        IEnumerator Intro()
        {
            yield return new WaitForSeconds(1.2f);
            Say("intro_1"); yield return new WaitForSeconds(8.5f);
            Say("intro_2"); yield return new WaitForSeconds(8.5f);
            Say("intro_3"); yield return new WaitForSeconds(9.5f);
            if (GameSettings.Challenge)
            {
                SayText("Radio", "Beat the dawn: you have " + Mathf.RoundToInt(challengeSeconds / 60f) + " minutes.", 4f);
                yield return new WaitForSeconds(6.5f);
            }
            if (Time.time - _lastPing > 30f) { Say("intro_4"); }
            _routine = null;
        }

        static void Say(string id)
        {
            if (CaptionSystem.Instance != null) CaptionSystem.Instance.Show(id);
        }

        static void SayText(string who, string text, float seconds)
        {
            if (CaptionSystem.Instance != null) CaptionSystem.Instance.ShowText(who, text, seconds);
        }

        Vector3 TargetPosition()
        {
            if (Stage < relays.Length && relays[Stage] != null) return relays[Stage].transform.position;
            Vector3 towerPos = tower != null ? tower.position : transform.position;
            return chain != null ? chain.NextTarget(towerPos) : towerPos;
        }

        /// <summary>Where the current goal is (used by the guide spirit and the radio hints).</summary>
        public Vector3 CurrentTarget => TargetPosition();

        public bool IsDelivering => _delivering;

        /// <summary>A short name for the current goal.</summary>
        public string CurrentTargetName
        {
            get
            {
                if (Stage < relays.Length) return "the relay";
                if (chain != null && !chain.Solved) return chain.NextName();
                return "the tower";
            }
        }

        void Update()
        {
            if (GameFlow.State != GameState.Playing || _delivering) return;
            if (_cam == null)
            {
                _cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
                if (_cam == null) return;
            }

            TickChallenge();
            if (GameFlow.State != GameState.Playing) return;

            Vector3 p = _cam.transform.position;
            Vector3 t = TargetPosition();
            var flat = new Vector3(t.x - p.x, 0f, t.z - p.z);
            bool atChainStep = Stage >= relays.Length && chain != null && !chain.Solved;
            bool inZone = !atChainStep && flat.magnitude < zoneRadius;

            if (atChainStep && tower != null && Time.time >= _nextSealHint && PlayerRig.FlatDistance(tower.position) < 10f)
            {
                _nextSealHint = Time.time + 25f;
                SayText("Seal", chain.SealHint(), 5f);
            }
            if (Stage >= relays.Length && (chain == null || chain.Solved) && !_towerPlayed && towerAudio != null)
            {
                _towerPlayed = true;
                towerAudio.loop = true; towerAudio.volume = 1f; towerAudio.Play();
            }

            if (inZone)
            {
                _dwell += Time.deltaTime;
                if (!_prompted) _prompted = true;   // the blue glow explains what to do
                if (_dwell >= dwellSeconds) Advance();   // accessibility fallback: wait beside it
            }
            else
            {
                _dwell = 0f;
                _prompted = false;
            }

            if (Time.time >= _nextHint && !inZone)
            {
                _nextHint = Time.time + CurrentHintInterval;
                Hint(p, flat);
            }
        }

        void Advance()
        {
            // Story captions from the intro must never hide objective feedback.
            if (_routine != null && !_delivering) { StopCoroutine(_routine); _routine = null; }
            _dwell = 0f; _prompted = false;
            _nextHint = Time.time + CurrentHintInterval;
            if (Stage < relays.Length)
            {
                var r = relays[Stage];
                if (r != null)
                {
                    r.Activate();
                    if (FeedbackFx.Instance != null) FeedbackFx.Instance.Success(r.transform.position, r.color, 1f);
                    // A relay switching on is loud: monsters nearby come to look.
                    if (MonsterDirector.Instance != null) MonsterDirector.Instance.NoiseAt(r.transform.position, MonsterDirector.Instance.relayNoiseRadius);
                }
                if (relayOnClip != null) _sfx.PlayOneShot(relayOnClip, 0.8f);
                Say("relay_on_" + (Stage + 1));
                { var cp = _cam != null ? _cam.transform.position : r.transform.position; PlayerRig.CheckpointPos = new Vector3(cp.x, spawn != null ? spawn.position.y : 0f, cp.z); }
                Stage++;
                if (Stage < relays.Length)
                {
                    if (relays[Stage] != null) relays[Stage].SetCurrent(true);
                }
                else if (chain != null)
                {
                    SayText("Radio", "All relays are on. Now find the brass key and the three braziers near the tower.", 6f);
                }
            }
            else
            {
                Deliver();
            }
        }

        void Deliver()
        {
            _delivering = true;
            Stage = relays.Length + 1;
            if (FeedbackFx.Instance != null && tower != null) FeedbackFx.Instance.Success(tower.position, new Color(1f, 0.85f, 0.4f), 2.2f);
            if (towerAudio != null) towerAudio.Stop();
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(DeliverRoutine());
        }

        IEnumerator DeliverRoutine()
        {
            Say("deliver_1");
            yield return new WaitForSeconds(5.5f);
            Say("deliver_2");
            yield return new WaitForSeconds(5.0f);
            GameFlow.OnDelivered();
            _routine = null;
        }

        void Hint(Vector3 camPos, Vector3 flatToTarget)
        {
            var fwd = _cam.transform.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) return;
            float angle = Vector3.SignedAngle(fwd, flatToTarget, Vector3.up);
            string dir = DirectionWord(angle);
            int metres = Mathf.Max(10, Mathf.RoundToInt(flatToTarget.magnitude / 10f) * 10);
            string what = Stage < relays.Length ? "the relay tone" : chain != null ? chain.NextName() : "the tower signal";
            SayText("Radio", $"Head for {what}: it is {dir}, about {metres} metres away.", 5f);
        }

        public static string DirectionWord(float signedAngle)
        {
            float a = Mathf.Abs(signedAngle);
            if (a < 35f) return "ahead of you";
            if (a > 145f) return "behind you";
            return signedAngle > 0f ? "to your right" : "to your left";
        }
    }
}
