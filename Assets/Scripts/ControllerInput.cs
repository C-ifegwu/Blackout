using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR;

public class ControllerInput : MonoBehaviour
{
    public static event Action PingPressed;
    public static event Action PausePressed;

    public static Vector2 Keys { get; private set; }
    public static Vector2 Look { get; private set; }
    public static float KeyTurn { get; private set; }
    public static bool ListenHeld { get; private set; }
    public static bool CoverHeld { get; private set; }

    private InputAction[] actions;
    private Button gazed;
    private float dwell;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        PingPressed = null;
        PausePressed = null;
    }

    private void Awake()
    {
        InputAction keys = new InputAction("Keys", InputActionType.Value);
        keys.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s").With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
        keys.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow");
        InputAction turn = new InputAction("KeyTurn", InputActionType.Value);
        turn.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/leftArrow").With("Positive", "<Keyboard>/rightArrow");
        actions = new[]
        {
            keys,
            Make(InputActionType.Value, "<Mouse>/delta"),
            turn,
            Make(InputActionType.Button, "<XRController>/{PrimaryButton}", "<Keyboard>/leftShift", "<Keyboard>/f", "<Gamepad>/leftShoulder"),
            Make(InputActionType.Button, "<XRController>/{GripButton}", "<Keyboard>/c", "<Mouse>/rightButton", "<Gamepad>/rightShoulder"),
            Make(InputActionType.Button, "<XRController>/{TriggerButton}", "<Keyboard>/space", "<Mouse>/leftButton", "<Gamepad>/rightTrigger"),
            Make(InputActionType.Button, "<XRController>/{SecondaryButton}", "<XRController>{LeftHand}/{MenuButton}", "<Keyboard>/escape", "<Keyboard>/p", "<Keyboard>/tab", "<Gamepad>/start"),
            Make(InputActionType.Button, "<XRController>{LeftHand}/{Primary2DAxisClick}", "<Keyboard>/h", "<Gamepad>/select")
        };
        actions[5].performed += context => PingPressed?.Invoke();
        actions[6].performed += context => PausePressed?.Invoke();
        actions[7].performed += context => GameSettings.Toggle(SettingKey.Hud);
    }

    private void OnEnable()
    {
        Array.ForEach(actions, action => action.Enable());
    }

    private void OnDisable()
    {
        Array.ForEach(actions, action => action.Disable());
        ListenHeld = false;
        CoverHeld = false;
    }

    private void Update()
    {
        Keys = Vector2.ClampMagnitude(actions[0].ReadValue<Vector2>(), 1f);
        Look = actions[1].ReadValue<Vector2>();
        KeyTurn = actions[2].ReadValue<float>();
        ListenHeld = actions[3].IsPressed();
        CoverHeld = actions[4].IsPressed();
        Gaze();
    }

    private void Gaze()
    {
        bool gaze = MenuController.Open && PlayerRig.XRMode && PlayerRig.Instance != null && !InputDevices.GetDeviceAtXRNode(XRNode.LeftHand).isValid && !InputDevices.GetDeviceAtXRNode(XRNode.RightHand).isValid;
        Button hit = null;
        foreach (Button button in HudController.Buttons)
        {
            RectTransform rect = button != null ? (RectTransform)button.transform : null;
            float along;
            if (gaze && rect != null && new Plane(rect.forward, rect.position).Raycast(new Ray(PlayerRig.Instance.Head.position, PlayerRig.Instance.Head.forward), out along))
            {
                hit = rect.rect.Contains(rect.InverseTransformPoint(PlayerRig.Instance.Head.position + PlayerRig.Instance.Head.forward * along)) ? button : hit;
            }
        }
        dwell = hit != null && hit == gazed ? dwell + Time.unscaledDeltaTime : 0f;
        gazed = hit;
        if (hit != null && dwell >= 1.5f)
        {
            dwell = -1000f;
            hit.onClick.Invoke();
        }
    }

    private InputAction Make(InputActionType type, params string[] paths)
    {
        InputAction action = new InputAction(null, type);
        foreach (string path in paths)
        {
            action.AddBinding(path);
        }
        return action;
    }
}
