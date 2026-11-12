using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.XR.CoreUtils;
using Blackout.Experience;

namespace Blackout.World
{
    /// <summary>Small helpers to find the player and move them, shared by the quest objects.</summary>
    public static class PlayerRig
    {
        static Camera _cam;
        static XROrigin _origin;

        public static Vector3 CheckpointPos;
        public static Quaternion CheckpointRot = Quaternion.identity;

        public static Camera Cam
        {
            get
            {
                if (_cam == null) _cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
                return _cam;
            }
        }

        public static XROrigin Origin
        {
            get
            {
                if (_origin == null) _origin = Object.FindFirstObjectByType<XROrigin>();
                return _origin;
            }
        }

        public static bool HasPlayer => Cam != null;
        public static Vector3 Position => Cam != null ? Cam.transform.position : Vector3.zero;

        public static float FlatDistance(Vector3 p)
        {
            var c = Position;
            float dx = c.x - p.x, dz = c.z - p.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        public static void Teleport(Vector3 pos, Quaternion rot)
        {
            var o = Origin;
            if (o == null) return;
            var cc = o.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            o.transform.SetPositionAndRotation(pos, rot);
            if (cc != null) cc.enabled = true;
        }

        public static void Say(string who, string text, float seconds = 4f)
        {
            if (CaptionSystem.Instance != null) CaptionSystem.Instance.ShowText(who, text, seconds);
        }

        public static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var r = FindDeep(root.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }
    }

    /// <summary>
    /// The single handheld light of BLACKOUT. Everything else in the city is dark. The lantern burns fuel, can be
    /// covered (hold grip, or F on a keyboard) to hide from monsters, and is topped up with oil flasks.
    /// </summary>
    public sealed class Lantern : MonoBehaviour
    {
        public static Lantern Instance { get; private set; }

        public Material bodyMaterial;
        public float brightRange = 11f;
        public float brightIntensity = 4.2f;
        public float coveredRange = 2.4f;
        public float coveredIntensity = 0.9f;
        public float burnSeconds = 330f;
        public float coveredBurnSeconds = 1200f;

        /// <summary>Raised when a button press needed oil and there was none.</summary>
        public event System.Action Denied;
        public void NotifyDenied() { Haptics.Impulse(0.5f, 0.06f); Denied?.Invoke(); }

        public float Fuel { get; private set; } = 1f;
        public bool Covered { get; private set; }
        /// <summary>How easily monsters can see the player: 1 bright, about 0.3 covered, lower when the fuel is nearly gone.</summary>
        public float Visibility { get; private set; } = 1f;

        Light _light;
        Transform _body;
        InputAction _cover;
        float _range, _intensity, _nextAttach;
        bool _attached, _warn1, _warn2, _emptyWarned;
        float _noise;

        void Awake()
        {
            Instance = this;
            var lg = new GameObject("LanternLight");
            lg.transform.SetParent(transform, false);
            lg.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            _light = lg.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = new Color(1f, 0.72f, 0.4f);
            _light.shadows = LightShadows.None;
            _light.range = brightRange;
            _light.intensity = brightIntensity;
            _range = brightRange; _intensity = brightIntensity;

            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = "LanternBody";
            Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(transform, false);
            body.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            body.transform.localScale = Vector3.one * 0.085f;
            var r = body.GetComponent<MeshRenderer>();
            if (bodyMaterial != null) r.sharedMaterial = bodyMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _body = body.transform;
            _noise = Random.value * 100f;
        }

        void OnEnable()
        {
            _cover = new InputAction("Cover", InputActionType.Button);
            _cover.AddBinding("<XRController>{LeftHand}/{GripButton}");
            _cover.AddBinding("<XRController>{RightHand}/{GripButton}");
            _cover.AddBinding("<Keyboard>/f");
            _cover.Enable();
            GameFlow.RunStarted += ResetRun;
        }

        void OnDisable()
        {
            GameFlow.RunStarted -= ResetRun;
            if (_cover != null) { _cover.Disable(); _cover.Dispose(); _cover = null; }
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public void ResetRun()
        {
            Fuel = 1f; _warn1 = _warn2 = _emptyWarned = false;
        }

        public void AddFuel(float amount)
        {
            Fuel = Mathf.Clamp01(Fuel + amount);
            if (Fuel > 0.3f) { _warn1 = _warn2 = _emptyWarned = false; }
        }

        void Attach()
        {
            if (Time.unscaledTime < _nextAttach) return;
            _nextAttach = Time.unscaledTime + 0.5f;
            var o = PlayerRig.Origin;
            if (o == null) return;
            var hand = PlayerRig.FindDeep(o.transform, GameSettings.OneHanded ? "Right Controller" : "Left Controller");
            if (hand != null)
            {
                transform.SetParent(hand, false);
                transform.localPosition = new Vector3(0f, 0.02f, 0.10f);
                transform.localRotation = Quaternion.identity;
                _attached = true;
                return;
            }
            var cam = PlayerRig.Cam;
            if (cam != null && Time.unscaledTime > 3f)
            {
                transform.SetParent(cam.transform, false);
                transform.localPosition = new Vector3(-0.25f, -0.35f, 0.45f);
                transform.localRotation = Quaternion.identity;
                _attached = true;
            }
        }

        void Update()
        {
            if (!_attached || transform.parent == null) { _attached = false; Attach(); }

            Covered = _cover != null && _cover.IsPressed();
            bool playing = GameFlow.State == GameState.Playing;
            if (playing)
            {
                Fuel = Mathf.Max(0f, Fuel - Time.deltaTime / (Covered ? coveredBurnSeconds : burnSeconds));
                if (!_warn1 && Fuel < 0.3f) { _warn1 = true; PlayerRig.Say("Lantern", "The flame is getting low. Look for oil flasks.", 4f); }
                if (!_warn2 && Fuel < 0.12f) { _warn2 = true; PlayerRig.Say("Lantern", "Almost out of oil.", 3.5f); }
                if (!_emptyWarned && Fuel <= 0f) { _emptyWarned = true; PlayerRig.Say("Lantern", "Only an ember is left.", 4f); }
            }

            float fuelFactor = Mathf.Lerp(0.45f, 1f, Mathf.Clamp01(Fuel / 0.3f));
            float tr, ti;
            if (Fuel <= 0f) { tr = 3f; ti = 0.8f; }
            else if (Covered) { tr = coveredRange; ti = coveredIntensity; }
            else { tr = brightRange * fuelFactor; ti = brightIntensity * fuelFactor; }
            float b = Mathf.Clamp(GameSettings.Brightness, 0.6f, 2f);
            ti *= b;

            float k = 1f - Mathf.Exp(-8f * Time.deltaTime);
            _range = Mathf.Lerp(_range, tr, k);
            _intensity = Mathf.Lerp(_intensity, ti, k);
            float flicker = GameSettings.ReduceFlashing ? 1f : 1f + 0.05f * (Mathf.PerlinNoise(_noise, Time.time * 2.2f) - 0.5f) * 2f;
            _light.range = _range;
            _light.intensity = _intensity * flicker;

            float vis = Covered ? 0.3f : 1f;
            vis *= Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(Fuel / 0.2f));
            Visibility = vis;

            if (_body != null && bodyMaterial != null)
            {
                float glow = Mathf.Clamp01(_intensity / brightIntensity);
                bodyMaterial.EnableKeyword("_EMISSION");
                bodyMaterial.SetColor("_EmissionColor", new Color(1f, 0.6f, 0.25f) * (0.4f + 2.2f * glow));
            }
        }
    }
}
