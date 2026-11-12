using UnityEngine;

namespace Blackout.World
{
    /// <summary>
    /// Quest 2 cannot afford hundreds of real-time lights. This keeps a small pool of point lights and moves
    /// them to the lamps nearest the player, while every lamp still shows a glowing bulb.
    /// </summary>
    public sealed class LampLightPool : MonoBehaviour
    {
        public Transform[] lamps = new Transform[0];
        [Range(1, 8)] public int poolSize = 6;
        public float range = 18f;
        public float maxIntensity = 4f;
        public Color color = new Color(1f, 0.78f, 0.45f);

        Light[] _lights;
        Transform _cam;
        float _power;
        float _next;
        readonly int[] _pick = new int[8];
        readonly float[] _pickDist = new float[8];

        void Awake()
        {
            _lights = new Light[poolSize];
            for (int i = 0; i < poolSize; i++)
            {
                var go = new GameObject("PooledLampLight" + i);
                go.transform.SetParent(transform, false);
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = range;
                l.color = color;
                l.shadows = LightShadows.None;
                l.intensity = 0f;
                l.enabled = false;
                _lights[i] = l;
            }
        }

        public void SetPower(float p)
        {
            _power = Mathf.Clamp01(p);
            if (_lights == null) return;
            foreach (var l in _lights)
            {
                l.intensity = maxIntensity * _power;
                l.enabled = _power > 0.01f;
            }
            _next = 0f;
        }

        void Update()
        {
            if (_power <= 0.01f || lamps == null || lamps.Length == 0) return;
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.3f;

            if (_cam == null)
            {
                var c = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
                if (c == null) return;
                _cam = c.transform;
            }

            int n = Mathf.Min(poolSize, 8);
            for (int i = 0; i < n; i++) { _pick[i] = -1; _pickDist[i] = float.MaxValue; }

            Vector3 p = _cam.position;
            for (int li = 0; li < lamps.Length; li++)
            {
                if (lamps[li] == null) continue;
                float d = (lamps[li].position - p).sqrMagnitude;
                if (d >= _pickDist[n - 1]) continue;
                int k = n - 1;
                while (k > 0 && _pickDist[k - 1] > d) { _pickDist[k] = _pickDist[k - 1]; _pick[k] = _pick[k - 1]; k--; }
                _pickDist[k] = d; _pick[k] = li;
            }

            for (int i = 0; i < n; i++)
            {
                if (_pick[i] < 0) { _lights[i].enabled = false; continue; }
                _lights[i].transform.position = lamps[_pick[i]].position + Vector3.down * 0.5f;
                _lights[i].enabled = true;
            }
        }
    }
}
