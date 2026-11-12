using UnityEngine;
using Blackout.Experience;

namespace Blackout.World
{
    /// <summary>
    /// A blue glow on whatever you are looking for, shown only when you are close. It tells you what the thing is and
    /// what to do once, in a caption, so no one has to guess.
    /// </summary>
    public sealed class ObjectiveGlow : MonoBehaviour
    {
        public SonarGameplay gameplay;
        public Material glowMaterial;
        public float showDistance = 16f;

        Renderer _orb;
        Light _light;
        string _toldKey = "";

        void Awake()
        {
            var o = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            o.name = "ObjectiveOrb";
            Destroy(o.GetComponent<Collider>());
            o.transform.SetParent(transform, false);
            _orb = o.GetComponent<MeshRenderer>();
            if (glowMaterial != null) _orb.sharedMaterial = glowMaterial;
            _orb.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var lg = new GameObject("ObjectiveLight");
            lg.transform.SetParent(transform, false);
            _light = lg.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = new Color(0.3f, 0.6f, 1f);
            _light.range = 8f;
            _light.shadows = LightShadows.None;
            _orb.enabled = false;
            _light.enabled = false;
        }

        void OnEnable() { GameFlow.RunStarted += Reset; }
        void OnDisable() { GameFlow.RunStarted -= Reset; }
        void Reset() { _toldKey = ""; }

        static string Instruction(string name)
        {
            switch (name)
            {
                case "the relay": return "The relay console is right here. Grab it with your hand (grip) or press the trigger to switch the relay on.";
                case "the brass key": return "The brass key is right here. Walk into the blue glow to pick it up.";
                case "the three braziers": return "The braziers are right here. Walk up to each one to light it, in the order the plaques gave.";
                default: return "The radio console is right here. Grab it with your hand (grip) or press the trigger to send your message.";
            }
        }

        void Update()
        {
            bool playing = GameFlow.State == GameState.Playing && gameplay != null && PlayerRig.HasPlayer && !gameplay.IsDelivering;
            if (!playing) { _orb.enabled = false; _light.enabled = false; return; }

            Vector3 t = gameplay.CurrentTarget;
            float d = PlayerRig.FlatDistance(t);
            bool near = d < showDistance;
            _orb.enabled = near;
            _light.enabled = near;
            if (!near) return;

            transform.position = new Vector3(t.x, Mathf.Max(t.y, 0f) + 1.4f, t.z);
            float pulse = GameSettings.ReduceFlashing ? 1f : 1f + 0.15f * Mathf.Sin(Time.time * 3f);
            _orb.transform.localScale = Vector3.one * 0.6f * pulse;
            _light.intensity = 3.2f * pulse * Mathf.Clamp(GameSettings.Brightness, 0.6f, 2f);

            string name = gameplay.CurrentTargetName;
            string key = gameplay.Stage + name;
            if (key != _toldKey && d < showDistance - 2f)
            {
                _toldKey = key;
                PlayerRig.Say("Goal", Instruction(name), 7f);
            }
        }
    }
}
