using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Blackout.Experience;

namespace Blackout.World
{
    /// <summary>
    /// The glowing console on each relay and at the tower radio. Reach out and grab it (grip), or press the trigger
    /// beside it. It breathes softly when it is the next goal, brightens when your hand is over it, and glows green
    /// once it is done.
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public sealed class RelayConsole : MonoBehaviour
    {
        public int index;
        public SonarGameplay gameplay;
        public Renderer panel;
        public Color color = new Color(0.35f, 0.85f, 1f);

        XRSimpleInteractable _x;
        Material _mat;
        bool _hover;

        void Awake()
        {
            _x = GetComponent<XRSimpleInteractable>();
            if (panel != null) _mat = panel.material;
            _x.selectEntered.AddListener(_ => Use());
            _x.activated.AddListener(_ => Use());
            _x.hoverEntered.AddListener(_ => { _hover = true; Haptics.Impulse(0.15f, 0.04f); });
            _x.hoverExited.AddListener(_ => { _hover = false; });
        }

        void Use()
        {
            if (gameplay != null) gameplay.Interact(index);
        }

        void Update()
        {
            if (_mat == null || gameplay == null) return;
            bool done = index < gameplay.Stage;
            bool current = index == gameplay.Stage && !gameplay.IsDelivering;
            Color c;
            if (done) c = new Color(0.3f, 1f, 0.55f) * 2f;
            else if (current)
            {
                float pulse = GameSettings.ReduceFlashing ? 1.8f : 1.7f + 0.5f * Mathf.Sin(Time.time * 3f);
                c = color * pulse;
            }
            else c = color * 0.25f;
            if (_hover && !done) c *= 1.6f;
            _mat.EnableKeyword("_EMISSION");
            _mat.SetColor("_EmissionColor", c);
        }
    }
}
