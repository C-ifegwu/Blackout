using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.XR.CoreUtils;
using Blackout.Experience;

namespace Blackout.World
{
    /// <summary>Fades the view to black and back (falling in a pit). Slow and gentle, never a flash.</summary>
    public sealed class ScreenFader : MonoBehaviour
    {
        public static ScreenFader Instance { get; private set; }
        public Material fadeMaterial;

        Renderer _quad;
        float _alpha;

        void Awake()
        {
            Instance = this;
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "FadeQuad";
            Destroy(q.GetComponent<Collider>());
            _quad = q.GetComponent<MeshRenderer>();
            if (fadeMaterial != null) _quad.sharedMaterial = fadeMaterial;
            _quad.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            q.transform.SetParent(transform, false);
            q.transform.localScale = new Vector3(3f, 3f, 1f);
            _quad.enabled = false;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void LateUpdate()
        {
            var cam = PlayerRig.Cam;
            if (cam == null) return;
            transform.SetPositionAndRotation(cam.transform.position + cam.transform.forward * 0.25f, cam.transform.rotation);
        }

        public IEnumerator FadeTo(float target, float seconds)
        {
            float from = _alpha;
            float t = 0f;
            _quad.enabled = true;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                SetAlpha(Mathf.Lerp(from, target, Mathf.Clamp01(t / seconds)));
                yield return null;
            }
            SetAlpha(target);
            if (target <= 0.001f) _quad.enabled = false;
        }

        void SetAlpha(float a)
        {
            _alpha = a;
            if (_quad != null && _quad.sharedMaterial != null)
            {
                var m = _quad.sharedMaterial;
                if (m.HasProperty("_Color")) { var c = m.color; c.a = a; m.color = c; }
            }
        }
    }
}
