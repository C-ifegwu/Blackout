using System;
using UnityEngine;

public enum PulseKind { Player = 0, Sound = 1, Touch = 2 }

public class SonarSystem : MonoBehaviour
{
    public static event Action<Vector3> Pinged;
    public static int Pings { get; private set; }

    private const int Slots = 12;
    private static readonly Vector4[] origins = new Vector4[Slots];
    private static readonly Vector4[] shapes = new Vector4[Slots];

    [SerializeField] private Transform head;
    [SerializeField] private AudioSource source;
    [SerializeField] private AudioClip pingClip;
    [SerializeField] private float cooldown = 1.2f;
    [SerializeField] private float radius = 26f;
    [SerializeField] private float maxAmbient = 0.003f;

    private float lastPing = -100f;
    private float hintTimer;
    private float attract;
    private bool listening;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Pinged = null;
        Pings = 0;
    }

    private void Awake()
    {
        Pings = 0;
        for (int i = 0; i < Slots; i++)
        {
            origins[i] = new Vector4(0f, -1000f, 0f, -1000f);
            shapes[i] = Vector4.zero;
        }
    }

    private void OnEnable() => ControllerInput.PingPressed += Ping;

    private void OnDisable() => ControllerInput.PingPressed -= Ping;

    public static void Emit(Vector3 origin, PulseKind kind, float reach, float strength)
    {
        float speed = kind == PulseKind.Player ? 10f : 4.5f;
        if (GameSettings.ReduceFlashing)
        {
            strength *= 0.7f;
            speed *= 0.8f;
        }
        int best = 0;
        float bestScore = float.MaxValue;
        for (int i = 0; i < Slots; i++)
        {
            float left = origins[i].w + shapes[i].x / Mathf.Max(shapes[i].y, 0.01f) + 3f - Time.time;
            float score = left + (shapes[i].z < 0.5f && shapes[i].x > 0f && kind != PulseKind.Player ? 100f : 0f);
            if (score < bestScore)
            {
                bestScore = score;
                best = i;
            }
        }
        origins[best] = new Vector4(origin.x, origin.y, origin.z, Time.time);
        shapes[best] = new Vector4(reach, speed, (float)kind, strength);
    }

    private void Update()
    {
        Shader.SetGlobalVectorArray("_PulseOrigin", origins);
        Shader.SetGlobalVectorArray("_PulseShape", shapes);
        Shader.SetGlobalFloat("_RevealTime", Time.time);
        Shader.SetGlobalFloat("_Ambient", GameSettings.Brightness * maxAmbient);
        Shader.SetGlobalFloat("_HighContrast", GameSettings.HighContrast ? 1f : 0f);
        Shader.SetGlobalFloat("_SoftPulse", GameSettings.ReduceFlashing ? 1f : 0f);
        attract -= Time.unscaledDeltaTime;
        if (attract <= 0f && (GameFlow.State == FlowState.Notice || GameFlow.State == FlowState.Title))
        {
            attract = 4.5f;
            Emit(head.position + Vector3.down * 1.2f, PulseKind.Player, 16f, 0.55f);
        }
        Listen();
    }

    public void Ping()
    {
        float wait = GameSettings.ReduceFlashing ? cooldown * 1.5f : cooldown;
        bool lockClick = !PlayerRig.XRMode && Cursor.lockState != CursorLockMode.Locked;
        if (!GameFlow.IsPlaying || Time.time - lastPing < wait || lockClick)
        {
            return;
        }
        lastPing = Time.time;
        Pings++;
        Vector3 origin = head.position + Vector3.down * 0.4f;
        Emit(origin, PulseKind.Player, radius, 1f);
        source.PlayOneShot(pingClip, 0.8f);
        GameFlow.Haptic(0.3f, 0.08f);
        Pinged?.Invoke(origin);
    }

    private void Listen()
    {
        bool held = ControllerInput.ListenHeld && GameFlow.IsPlaying && SoundCue.Beacon != null;
        SoundCue.Focus = Mathf.MoveTowards(SoundCue.Focus, held ? 1f : 0f, Time.deltaTime * 3f);
        if (held != listening)
        {
            listening = held;
            hintTimer = 0f;
            GameFlow.Prompt("");
        }
        if (!held)
        {
            return;
        }
        Vector3 beacon = SoundCue.Beacon.transform.position;
        GameFlow.Prompt("Listening... the signal is " + GameFlow.Describe(head, beacon) + (GameSettings.DawnMode ? ".   Dawn in " + CityController.DawnText : "."));
        hintTimer -= Time.deltaTime;
        if (hintTimer <= 0f)
        {
            hintTimer = 1.3f;
            Vector3 toward = Vector3.ProjectOnPlane(beacon - head.position, Vector3.up).normalized;
            Emit(head.position + toward * 2.4f + Vector3.down * 0.7f, PulseKind.Touch, 1.4f, 0.8f);
        }
    }
}
