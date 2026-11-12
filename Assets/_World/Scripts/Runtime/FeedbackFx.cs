using System.Collections;
using UnityEngine;
using Blackout.Experience;

namespace Blackout.World
{
    /// <summary>
    /// Rewarding feedback for interactions: a rolling light ring on the ground, a burst of drifting sparks, a soft
    /// light swell, a chime and a short vibration pattern. Everything ramps smoothly (no strobing), and is calmer
    /// when Reduce Flashing is on.
    /// </summary>
    public sealed class FeedbackFx : MonoBehaviour
    {
        public static FeedbackFx Instance { get; private set; }

        public Material fxMaterial;
        public AudioClip successClip;

        Texture2D _dot;

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        public void Success(Vector3 pos, Color color, float scale = 1f)
        {
            StartCoroutine(Ring(pos, color, scale, 0f));
            StartCoroutine(Ring(pos, Color.Lerp(color, Color.white, 0.5f), scale * 0.7f, 0.25f));
            Sparks(pos, color, scale);
            StartCoroutine(Flash(pos + Vector3.up * 0.5f, color, 6f * scale, 14f * scale));
            if (successClip != null) AudioSource.PlayClipAtPoint(successClip, pos, 0.9f);
            StartCoroutine(SuccessHaptics(scale));
        }

        public void Denied(Vector3 pos)
        {
            StartCoroutine(Flash(pos + Vector3.up * 0.5f, new Color(1f, 0.15f, 0.1f), 2.5f, 7f));
            StartCoroutine(DeniedHaptics());
        }

        IEnumerator SuccessHaptics(float scale)
        {
            Haptics.Impulse(0.9f, 0.12f * scale); yield return new WaitForSeconds(0.14f);
            Haptics.Impulse(0.6f, 0.10f); yield return new WaitForSeconds(0.14f);
            Haptics.Impulse(0.4f, 0.08f); yield return new WaitForSeconds(0.14f);
            Haptics.Impulse(0.25f, 0.08f);
        }

        IEnumerator DeniedHaptics()
        {
            Haptics.Impulse(0.5f, 0.06f); yield return new WaitForSeconds(0.1f);
            Haptics.Impulse(0.5f, 0.06f);
        }

        IEnumerator Ring(Vector3 pos, Color color, float scale, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            var go = new GameObject("FxRing");
            go.transform.position = new Vector3(pos.x, 0.06f, pos.z);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            const int n = 64;
            lr.positionCount = n;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)));
            }
            if (fxMaterial != null) lr.sharedMaterial = fxMaterial;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.widthMultiplier = 0.16f;
            float dur = GameSettings.ReduceFlashing ? 1.6f : 1.1f;
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);
                float e = 1f - (1f - k) * (1f - k);
                go.transform.localScale = Vector3.one * Mathf.Lerp(0.4f, 10f * scale, e);
                var c = color; c.a = (1f - k) * 0.9f;
                lr.startColor = c; lr.endColor = c;
                lr.widthMultiplier = Mathf.Lerp(0.2f, 0.04f, k);
                yield return null;
            }
            Destroy(go);
        }

        void Sparks(Vector3 pos, Color color, float scale)
        {
            var go = new GameObject("FxSparks");
            go.transform.position = pos + Vector3.up * 0.8f;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f * scale, 4.5f * scale);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
            main.startColor = color;
            main.gravityModifier = -0.06f;
            main.maxParticles = 200;
            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.RoundToInt(70 * scale)) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            var rend = go.GetComponent<ParticleSystemRenderer>();
            if (fxMaterial != null)
            {
                var m = new Material(fxMaterial);
                m.mainTexture = Dot();
                rend.sharedMaterial = m;
            }
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            Destroy(go, 4f);
        }

        IEnumerator Flash(Vector3 pos, Color color, float peak, float range)
        {
            var go = new GameObject("FxFlash");
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = range;
            l.shadows = LightShadows.None;
            l.intensity = 0f;
            float up = GameSettings.ReduceFlashing ? 0.6f : 0.3f;
            float down = GameSettings.ReduceFlashing ? 2.2f : 1.4f;
            float t = 0f;
            while (t < up) { t += Time.deltaTime; float k = t / up; l.intensity = peak * k * k * (3f - 2f * k); yield return null; }
            t = 0f;
            while (t < down) { t += Time.deltaTime; float k = 1f - t / down; l.intensity = peak * k * k; yield return null; }
            Destroy(go);
        }

        Texture2D Dot()
        {
            if (_dot != null) return _dot;
            const int n = 32;
            _dot = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                float a = Mathf.Clamp01((1f - d) * 2.4f);
                a = a * a * (3f - 2f * a);
                _dot.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            _dot.Apply();
            return _dot;
        }
    }
}
