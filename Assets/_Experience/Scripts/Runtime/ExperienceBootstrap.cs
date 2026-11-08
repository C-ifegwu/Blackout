using UnityEngine;
using UnityEngine.InputSystem;

namespace Blackout.Experience
{
    /// <summary>
    /// Drop this on one GameObject in the main scene. It creates the captions, UI, gaze selection and comfort
    /// systems and starts the flow (health notice, then main menu).
    /// Development builds and the Editor also get debug keys: F9 test caption, F10 simulate delivery.
    /// </summary>
    public sealed class ExperienceBootstrap : MonoBehaviour
    {
        public ExperienceUI UI { get; private set; }

        void Awake()
        {
            gameObject.AddComponent<CaptionSystem>();          // before the UI, which binds to it
            UI = gameObject.AddComponent<ExperienceUI>();
            gameObject.AddComponent<GazeDwellSelector>();
            gameObject.AddComponent<ComfortApplier>();
        }

        void Start() => GameFlow.Begin();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.f9Key.wasPressedThisFrame) CaptionSystem.Instance.ShowText("Test", "This is a test caption for Blackout.", 3f);
            if (kb.f10Key.wasPressedThisFrame) GameFlow.OnDelivered();
        }
#endif
    }
}
