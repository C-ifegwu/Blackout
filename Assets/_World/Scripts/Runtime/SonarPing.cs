using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using Blackout.Experience;

namespace Blackout.World
{
    /// <summary>
    /// The player's sonar. A ping sends an expanding ring and a soft light pulse across the city, and every surface
    /// the wave reaches shows up as a short-lived glowing echo. Cooldown is longer than one second, the light pulse
    /// fades in and out smoothly and is dimmed when Reduce Flashing is on.
    /// Input: trigger or primary button on either controller, Space, or gamepad south button.
    /// </summary>
    public sealed class SonarPing : MonoBehaviour
    {
        public Material ringMaterial;
        public Material echoMaterial;
        public AudioClip pingClip;

        public float cooldown = 3f;
        public float fuelCost = 0.07f;
        public bool showEchoes = false;
        public float range = 45f;
        public float waveSpeed = 22f;
        public int azimuths = 96;
        public float[] elevationsDeg = { -50f, -34f, -22f, -12f, -4f, 4f, 12f, 22f, 36f, 55f, 72f };
        public Color color = new Color(0.35f, 0.95f, 1f);

        /// <summary>Raised with the ping origin each time a ping is sent.</summary>
        public event Action<Vector3> Pinged;
        /// <summary>Raised instead of Pinged when the trigger is pressed next to an objective: it uses the objective, it does not call the guide globe.</summary>
        public event Action Interacted;
        public static Func<bool> InteractContext;

        public float ReadyFraction => Mathf.Clamp01((Time.time - _last) / cooldown);

        InputAction _action;
        float _last = -99f;
        float _ringStart = -99f;
        Vector3 _origin;
        Transform _cam;
        LineRenderer _ring;
        ParticleSystem _echo;
        Light _light;
        AudioSource _audio;
        bool _wasReady = true;

        struct Pending { public float time; public Vector3 pos; public float dist; }
        readonly List<Pending> _pending = new List<Pending>(512);
        const int RingPoints = 96;

        void Awake()
        {
            // Input
            _action = new InputAction("Ping", InputActionType.Button);
            _action.AddBinding("<XRController>{RightHand}/{TriggerButton}");
            _action.AddBinding("<XRController>{LeftHand}/{TriggerButton}");
            _action.AddBinding("<XRController>{RightHand}/{PrimaryButton}");
            _action.AddBinding("<Keyboard>/space");
            _action.AddBinding("<Gamepad>/buttonSouth");
            _action.performed += _ => TryPing();

            // Ring
            var ringGo = new GameObject("SonarRing");
            ringGo.transform.SetParent(transform, false);
            _ring = ringGo.AddComponent<LineRenderer>();
            _ring.useWorldSpace = true;
            _ring.loop = true;
            _ring.positionCount = RingPoints;
            _ring.widthMultiplier = 0.14f;
            _ring.material = ringMaterial;
            _ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _ring.receiveShadows = false;
            _ring.enabled = false;

            // Echo particles (one draw call for all echoes)
            var echoGo = new GameObject("SonarEcho");
            echoGo.transform.SetParent(transform, false);
            _echo = echoGo.AddComponent<ParticleSystem>();
            var main = _echo.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 4200;
            main.startLifetime = 3.2f;
            main.startSpeed = 0f;
            main.startSize = 0.22f;
            var emission = _echo.emission; emission.enabled = false;
            var shape = _echo.shape; shape.enabled = false;
            var col = _echo.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.06f), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            var rend = echoGo.GetComponent<ParticleSystemRenderer>();
            var echoInst = new Material(echoMaterial);
            var dotTex = MakeDot();
            echoInst.mainTexture = dotTex;
            if (echoInst.HasProperty("_BaseMap")) echoInst.SetTexture("_BaseMap", dotTex);
            rend.material = echoInst;
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
            _echo.Play();

            // Light pulse
            var lightGo = new GameObject("SonarLight");
            lightGo.transform.SetParent(transform, false);
            _light = lightGo.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = color;
            _light.shadows = LightShadows.None;
            _light.intensity = 0f;
            _light.enabled = false;

            // Audio
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.spatialBlend = 0f;
            _audio.playOnAwake = false;
            _audio.ignoreListenerPause = true;
        }

        static Texture2D MakeDot()
        {
            const int n = 32;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                float a = Mathf.Clamp01((1f - d) * 2.4f);
                a = a * a * (3f - 2f * a);
                tex.SetPixel(x, y, new Color(a, a, a, a));
            }
            tex.Apply();
            return tex;
        }

        void OnEnable() { _action.Enable(); }
        void OnDisable() { _action.Disable(); }

        public void ClearEchoes()
        {
            _pending.Clear();
            if (_echo != null) _echo.Clear();
            if (_ring != null) _ring.enabled = false;
            if (_light != null) _light.enabled = false;
        }

        void TryPing()
        {
            if (GameFlow.State != GameState.Playing) return;
            if (Time.time - _last < cooldown) return;
            if (_cam == null)
            {
                var c = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
                if (c == null) return;
                _cam = c.transform;
            }

            if (InteractContext != null && InteractContext())
            {
                _last = Time.time - cooldown + 0.6f;
                Interacted?.Invoke();
                return;
            }
            var lan = Lantern.Instance;
            if (lan != null)
            {
                if (lan.Fuel < 0.03f)
                {
                    lan.NotifyDenied();
                    _last = Time.time - cooldown + 2f;
                    PlayerRig.Say("Lantern", "No oil left for the guide globe. Find an oil flask.", 5f);
                    return;
                }
                lan.AddFuel(-fuelCost);
            }
            _last = Time.time;
            _wasReady = false;
            _origin = _cam.position;
            if (showEchoes) _ringStart = Time.time;
            if (pingClip != null) _audio.PlayOneShot(pingClip, 0.7f);
            Haptic();

            // Cast the fan of rays now; each echo appears when the wave reaches it.
            _pending.Clear();
            float step = 360f / azimuths;
            if (showEchoes)
            foreach (float el in elevationsDeg)
            {
                for (int a = 0; a < azimuths; a++)
                {
                    var dir = Quaternion.Euler(-el, a * step + (el * 3.7f), 0f) * Vector3.forward;
                    if (Physics.Raycast(_origin, dir, out var hit, range, ~0, QueryTriggerInteraction.Ignore))
                        _pending.Add(new Pending { time = Time.time + hit.distance / waveSpeed, pos = hit.point + hit.normal * 0.04f, dist = hit.distance });
                }
            }
            Pinged?.Invoke(_origin);
        }

        static void Haptic()
        {
            try
            {
                var d = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
                if (d.isValid) d.SendHapticImpulse(0, 0.25f, 0.08f);
            }
            catch (Exception) { /* haptics are optional */ }
        }

        void Update()
        {
            // Release echoes as the wave arrives.
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                var p = _pending[i];
                if (Time.time < p.time) continue;
                float near = 1f - Mathf.Clamp01(p.dist / range);
                var c = color; c.a = Mathf.Lerp(0.35f, 1f, near);
                _echo.Emit(new ParticleSystem.EmitParams
                {
                    position = p.pos,
                    velocity = Vector3.zero,
                    startColor = c,
                    startSize = Mathf.Lerp(0.75f, 0.4f, near),
                    startLifetime = 3.2f
                }, 1);
                _pending[i] = _pending[_pending.Count - 1];
                _pending.RemoveAt(_pending.Count - 1);
            }

            // Ring and light pulse.
            float t = Time.time - _ringStart;
            float dur = range / waveSpeed;
            if (t >= 0f && t < dur)
            {
                float r = t * waveSpeed;
                float y = _origin.y - 1.2f;
                for (int i = 0; i < RingPoints; i++)
                {
                    float ang = i * Mathf.PI * 2f / RingPoints;
                    _ring.SetPosition(i, new Vector3(_origin.x + Mathf.Cos(ang) * r, y, _origin.z + Mathf.Sin(ang) * r));
                }
                var rc = color; rc.a = 1f - t / dur;
                _ring.startColor = rc; _ring.endColor = rc;
                _ring.enabled = true;

                float peak = GameSettings.ReduceFlashing ? 1.1f : 2.4f;
                float fadeIn = Mathf.Clamp01(t / 0.25f);
                float fadeOut = 1f - Mathf.Clamp01(t / dur);
                _light.intensity = peak * fadeIn * fadeOut * fadeOut;
                _light.range = r + 8f;
                _light.transform.position = _origin;
                _light.enabled = true;
            }
            else if (_ring.enabled)
            {
                _ring.enabled = false;
                _light.enabled = false;
            }

            // Quiet tick when the sonar is ready again.
            if (!_wasReady && Time.time - _last >= cooldown)
            {
                _wasReady = true;
                if (GameFlow.State == GameState.Playing) UISounds.Play(UISounds.Kind.Hover);
            }
        }
    }
}
