using UnityEngine;
using Blackout.Experience;

namespace Blackout.World
{
    /// <summary>
    /// A small pale spirit that drifts ahead of the player toward the current goal. It does not stay: it appears
    /// for a few seconds every so often, or when the player has stood still for a while, then fades away. The
    /// player still has to read the dark and find the way.
    /// </summary>
    public sealed class GuideSpirit : MonoBehaviour
    {
        public SonarGameplay gameplay;
        public Material glowMaterial;
        public float firstDelay = 20f;
        public float interval = 32f;
        public float showSeconds = 7f;
        public float leadDistance = 4f;

        Renderer _orb;
        Light _light;
        float _nextShow, _showUntil, _alpha, _idle;
        Vector3 _pos;

        void Awake()
        {
            var o = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            o.name = "SpiritOrb";
            Destroy(o.GetComponent<Collider>());
            o.transform.SetParent(transform, false);
            o.transform.localScale = Vector3.one * 0.2f;
            _orb = o.GetComponent<MeshRenderer>();
            if (glowMaterial != null) _orb.sharedMaterial = glowMaterial;
            _orb.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var lg = new GameObject("SpiritLight");
            lg.transform.SetParent(transform, false);
            _light = lg.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = new Color(0.6f, 0.85f, 1f);
            _light.range = 4.5f;
            _light.intensity = 0f;
            _light.shadows = LightShadows.None;
            _orb.enabled = false;
        }

        void OnEnable() { GameFlow.RunStarted += ResetRun; }
        void OnDisable() { GameFlow.RunStarted -= ResetRun; }

        void ResetRun()
        {
            _nextShow = Time.time + firstDelay;
            _showUntil = 0f; _alpha = 0f; _idle = 0f;
        }

        void Update()
        {
            bool playing = GameFlow.State == GameState.Playing && gameplay != null && PlayerRig.HasPlayer;
            if (playing)
            {
                bool still = MonsterDirector.Instance != null && MonsterDirector.Instance.PlayerStill;
                _idle = still ? _idle + Time.deltaTime : 0f;
                if (Time.time >= _nextShow || (_idle > 12f && Time.time > _showUntil + 6f))
                {
                    _showUntil = Time.time + showSeconds;
                    _nextShow = Time.time + interval;
                    _idle = 0f;
                    var c = PlayerRig.Position;
                    _pos = new Vector3(c.x, 1.5f, c.z);
                }
            }

            bool show = playing && Time.time < _showUntil;
            _alpha = Mathf.MoveTowards(_alpha, show ? 1f : 0f, Time.deltaTime * 0.8f);
            bool vis = _alpha > 0.01f;
            _orb.enabled = vis;
            _light.enabled = vis;
            if (!vis) return;

            var cam = PlayerRig.Position;
            Vector3 t = gameplay.CurrentTarget;
            var dir = new Vector3(t.x - cam.x, 0f, t.z - cam.z);
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
            dir.Normalize();
            float lead = Mathf.Min(leadDistance, Mathf.Max(1.5f, Vector3.Distance(new Vector3(cam.x, 0, cam.z), new Vector3(t.x, 0, t.z)) - 1f));
            Vector3 want = new Vector3(cam.x, 1.5f + Mathf.Sin(Time.time * 2f) * 0.12f, cam.z) + dir * lead;
            _pos = Vector3.Lerp(_pos, want, 1f - Mathf.Exp(-2.5f * Time.deltaTime));
            transform.position = _pos;
            _light.intensity = 1.6f * _alpha * Mathf.Clamp(GameSettings.Brightness, 0.6f, 2f);
            if (glowMaterial != null)
            {
                glowMaterial.EnableKeyword("_EMISSION");
                glowMaterial.SetColor("_EmissionColor", new Color(0.6f, 0.85f, 1f) * (2.5f * _alpha));
            }
        }
    }
}
