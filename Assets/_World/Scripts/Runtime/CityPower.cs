using UnityEngine;
using Blackout.Experience;

namespace Blackout.World
{
    /// <summary>
    /// Controls the city's power: pitch dark during the run, then a slow, smooth restore when the message is
    /// delivered (GameFlow.Delivered). Never flashes; the restore takes 3.5 s, or 5 s when Reduce Flashing is on.
    /// Works with the brightness setting.
    /// </summary>
    public sealed class CityPower : MonoBehaviour
    {
        public Light moon;
        public LampLightPool lampPool;
        public Material lampMaterial;
        public Material[] signMaterials = new Material[0];
        public Color[] signColors = new Color[0];
        public Color lampColor = new Color(1f, 0.74f, 0.4f);
        public float lampIntensity = 2.5f;
        public float signIntensity = 1.8f;

        public Color darkAmbient = new Color(0.004f, 0.005f, 0.012f);
        public Color litAmbient = new Color(0.34f, 0.29f, 0.27f);
        public Color darkFog = new Color(0.003f, 0.004f, 0.008f);
        public Color litFog = new Color(0.09f, 0.075f, 0.085f);
        public float darkFogDensity = 0.03f;
        public float litFogDensity = 0.005f;
        public float moonDark = 0.02f;
        public float moonLit = 0.4f;

        public float Power { get; private set; }
        public float RestoreSeconds => GameSettings.ReduceFlashing ? 5f : 3.5f;

        float _from, _to, _t = 1f, _dur = 1f;
        bool _dirty = true;

        void Awake()
        {
            ComfortApplier.ControlAmbient = false;   // this component owns ambient light, but still honours Brightness
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            Power = 0f;
        }

        void OnEnable()
        {
            GameFlow.Delivered += Restore;
            GameFlow.RunStarted += Cut;
            GameSettings.Changed += MarkDirty;
        }

        void OnDisable()
        {
            GameFlow.Delivered -= Restore;
            GameFlow.RunStarted -= Cut;
            GameSettings.Changed -= MarkDirty;
            ComfortApplier.ControlAmbient = true;
        }

        void MarkDirty() { _dirty = true; }

        public void Cut() { _from = Power = 0f; _to = 0f; _t = 1f; _dirty = true; }

        public void Restore()
        {
            _from = Power; _to = 1f; _t = 0f; _dur = RestoreSeconds;
        }

        void LateUpdate()
        {
            if (_t < 1f)
            {
                _t = Mathf.Min(1f, _t + Time.unscaledDeltaTime / _dur);
                float e = _t * _t * (3f - 2f * _t);   // smoothstep: no sudden change
                Power = Mathf.Lerp(_from, _to, e);
                _dirty = true;
            }
            if (_dirty) { Apply(); _dirty = false; }
        }

        void Apply()
        {
            float p = Power;
            float b = GameSettings.Brightness;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            var dark = darkAmbient * b + Color.white * (Mathf.Max(0f, b - 1f) * 0.05f);
            RenderSettings.ambientLight = Color.Lerp(dark, litAmbient, p);
            RenderSettings.fogColor = Color.Lerp(darkFog, litFog, p);
            RenderSettings.fogDensity = Mathf.Lerp(darkFogDensity, litFogDensity, p);
            if (moon != null) moon.intensity = Mathf.Lerp(moonDark * b, moonLit, p);
            if (lampPool != null) lampPool.SetPower(p);
            SetEmission(lampMaterial, lampColor * (lampIntensity * p));
            for (int i = 0; i < signMaterials.Length && i < signColors.Length; i++)
                SetEmission(signMaterials[i], signColors[i] * (signIntensity * p));
        }

        static void SetEmission(Material m, Color c)
        {
            if (m == null) return;
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c);
        }
    }
}
