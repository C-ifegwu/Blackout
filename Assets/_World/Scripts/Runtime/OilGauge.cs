using UnityEngine;
using Blackout.Experience;

namespace Blackout.World
{
    /// <summary>
    /// Shows how much lantern oil is left, in two places: a bar low in your view and a small bar floating above the
    /// lantern in your hand. The bar turns orange then red as the oil runs low. If you press the trigger with no
    /// oil, the bar swells and flushes red so you know why nothing happened.
    /// </summary>
    public sealed class OilGauge : MonoBehaviour
    {
        public Material barMaterial;

        class Bar
        {
            public Transform root, pivot, fill, back;
            public Material fillMat, backMat;
            public float width;
        }

        Bar _hud, _hand;
        float _flash;
        bool _hooked;

        void Awake()
        {
            _hud = MakeBar("OilHud", 0.28f, 0.014f);
            _hand = MakeBar("OilHand", 0.11f, 0.012f);
        }

        Bar MakeBar(string name, float w, float h)
        {
            var b = new Bar { width = w };
            b.root = new GameObject(name).transform;
            b.root.SetParent(transform, false);
            b.back = Quad("Back", b.root, new Vector3(w * 1.08f, h * 1.6f, 1f), Vector3.zero, out b.backMat);
            b.pivot = new GameObject("Pivot").transform;
            b.pivot.SetParent(b.root, false);
            b.pivot.localPosition = new Vector3(-w * 0.5f, 0f, -0.002f);
            b.fill = Quad("Fill", b.pivot, new Vector3(w, h, 1f), new Vector3(w * 0.5f, 0f, 0f), out b.fillMat);
            return b;
        }

        Transform Quad(string name, Transform parent, Vector3 scale, Vector3 pos, out Material mat)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = name;
            Destroy(q.GetComponent<Collider>());
            q.transform.SetParent(parent, false);
            q.transform.localPosition = pos;
            q.transform.localScale = scale;
            var r = q.GetComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mat = barMaterial != null ? new Material(barMaterial) : r.material;
            r.sharedMaterial = mat;
            return q.transform;
        }

        void OnDisable()
        {
            if (_hooked && Lantern.Instance != null) Lantern.Instance.Denied -= OnDenied;
            _hooked = false;
        }

        void OnDenied() { _flash = 1f; }

        void LateUpdate()
        {
            var lan = Lantern.Instance;
            var cam = PlayerRig.Cam;
            bool show = lan != null && cam != null && GameFlow.State == GameState.Playing;
            _hud.root.gameObject.SetActive(show);
            _hand.root.gameObject.SetActive(show);
            if (!show) return;
            if (!_hooked) { lan.Denied += OnDenied; _hooked = true; }

            _flash = Mathf.MoveTowards(_flash, 0f, Time.deltaTime / 1.4f);

            // head-locked bar, low in view
            _hud.root.position = cam.transform.position + cam.transform.forward * 0.75f - cam.transform.up * 0.31f;
            _hud.root.rotation = cam.transform.rotation;
            _hud.root.localScale = Vector3.one * (1f + 0.35f * _flash);

            // small bar above the lantern, turned toward you
            _hand.root.position = lan.transform.position + Vector3.up * 0.2f;
            _hand.root.rotation = cam.transform.rotation;

            float f = lan.Fuel;
            Color c = f > 0.3f ? new Color(1f, 0.72f, 0.2f) : f > 0.12f ? new Color(1f, 0.45f, 0.1f) : new Color(1f, 0.15f, 0.1f);
            float pulse = (f <= 0.12f && !GameSettings.ReduceFlashing) ? 0.75f + 0.25f * Mathf.Sin(Time.time * 4f) : 1f;
            Color fc = Color.Lerp(c, new Color(1f, 0.1f, 0.08f), _flash) * pulse;
            fc.a = 1f;
            Color bc = Color.Lerp(new Color(0.03f, 0.03f, 0.04f, 0.75f), new Color(0.6f, 0.05f, 0.05f, 0.9f), Mathf.Max(_flash, f <= 0f ? 0.6f : 0f));

            Apply(_hud, f, fc, bc);
            Apply(_hand, f, fc, bc);
        }

        static void Apply(Bar b, float fuel, Color fill, Color back)
        {
            float w = Mathf.Max(0.0005f, b.width * Mathf.Clamp01(fuel));
            var s = b.fill.localScale; s.x = w; b.fill.localScale = s;
            var p = b.fill.localPosition; p.x = w * 0.5f; b.fill.localPosition = p;
            b.fill.gameObject.SetActive(fuel > 0.001f);
            b.fillMat.color = fill;
            b.backMat.color = back;
        }
    }
}
