using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

public class PlayerRig : MonoBehaviour
{
    public static PlayerRig Instance { get; private set; }
    public static bool XRMode { get; private set; }
    public static float Stillness { get; private set; }

    [SerializeField] private Transform cameraOffset;
    [SerializeField] private Transform head;
    [SerializeField] private Transform leftHand;
    [SerializeField] private GameObject[] controllers;
    [SerializeField] private MonoBehaviour[] comfortManagers;
    [SerializeField] private Renderer vignette;
    [SerializeField] private AudioSource feet;
    [SerializeField] private AudioClip[] steps;
    [SerializeField] private float desktopSpeed = 1.8f;

    private CharacterController body;
    private Material shade;
    private Vector3 lastHead;
    private float stride;
    private float motion;
    private float black = 1f;
    private float pitch;
    private bool wasLocked;

    public Transform Head => head;
    public Transform LeftHand => XRMode ? leftHand : head;
    public Vector3 Feet => new Vector3(head.position.x, transform.position.y, head.position.z);

    private void Awake()
    {
        Instance = this;
        body = GetComponent<CharacterController>();
        shade = vignette.material;
        XRMode = XRSettings.isDeviceActive;
    }

    private void OnEnable() => GameSettings.Changed += Apply;

    private void OnDisable() => GameSettings.Changed -= Apply;

    private void Start()
    {
        Apply();
    }

    private void Update()
    {
        bool xr = XRSettings.isDeviceActive;
        if (xr != XRMode)
        {
            XRMode = xr;
            Apply();
        }
        if (!XRMode)
        {
            Desktop();
        }
        Vector3 moved = Vector3.ProjectOnPlane(head.position - lastHead, Vector3.up);
        lastHead = head.position;
        float speed = moved.magnitude / Mathf.Max(Time.deltaTime, 0.001f);
        Stillness = speed < 0.15f ? Mathf.MoveTowards(Stillness, 1f, Time.deltaTime / 2.5f) : 0f;
        motion = Mathf.MoveTowards(motion, Mathf.Clamp01(speed / 1.5f), Time.deltaTime * 4f);
        stride += GameFlow.IsPlaying ? moved.magnitude : 0f;
        if (stride > 0.75f && steps.Length > 0)
        {
            stride = 0f;
            feet.PlayOneShot(steps[Random.Range(0, steps.Length)], Random.Range(0.3f, 0.45f));
        }
        black = Mathf.MoveTowards(black, 0f, Time.unscaledDeltaTime * 2.5f);
        float strength = GameSettings.Vignette && XRMode ? motion * 0.75f : 0f;
        shade.SetFloat("_Strength", strength);
        shade.SetFloat("_Black", black);
        vignette.enabled = strength > 0.01f || black > 0.001f;
    }

    public void Fade()
    {
        black = 1f;
    }

    private void Desktop()
    {
        bool playing = GameFlow.IsPlaying;
        if (!playing || Cursor.lockState != CursorLockMode.Locked)
        {
            if (playing && wasLocked)
            {
                GameFlow.Pause();
            }
            wasLocked = false;
            bool click = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
            Cursor.lockState = playing && click ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = Cursor.lockState == CursorLockMode.None;
            return;
        }
        wasLocked = true;
        Vector2 look = ControllerInput.Look * 0.1f;
        transform.Rotate(0f, look.x + ControllerInput.KeyTurn * 110f * Time.deltaTime, 0f, Space.World);
        pitch = Mathf.Clamp(pitch - look.y, -80f, 80f);
        head.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        Vector3 move = (Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized * ControllerInput.Keys.y + Vector3.ProjectOnPlane(head.right, Vector3.up).normalized * ControllerInput.Keys.x) * desktopSpeed;
        if (body != null)
        {
            body.SimpleMove(move);
        }
        else
        {
            transform.position += move * Time.deltaTime;
        }
    }

    private void Apply()
    {
        foreach (GameObject controller in controllers)
        {
            controller.SetActive(XRMode);
        }
        foreach (MonoBehaviour manager in comfortManagers)
        {
            bool right = manager.name.Contains("Right");
            Set(manager, "smoothMotionEnabled", !GameSettings.Teleport && (!right || GameSettings.OneHanded));
            Set(manager, "smoothTurnEnabled", GameSettings.SmoothTurn);
        }
        if (!XRMode)
        {
            cameraOffset.localPosition = Vector3.up * 1.65f;
            head.localPosition = Vector3.zero;
            return;
        }
        cameraOffset.localPosition = Vector3.up * (GameSettings.Seated ? 0.5f : 0f);
        List<XRInputSubsystem> systems = new List<XRInputSubsystem>();
        SubsystemManager.GetSubsystems(systems);
        systems.ForEach(system => system.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor));
    }

    private void Set(MonoBehaviour target, string property, bool value)
    {
        System.Reflection.PropertyInfo info = target.GetType().GetProperty(property);
        if (info != null)
        {
            info.SetValue(target, value);
        }
    }
}
