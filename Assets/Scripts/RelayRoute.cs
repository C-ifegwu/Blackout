using System.Collections;
using UnityEngine;

public class RelayRoute : MonoBehaviour
{
    public static event System.Action<Vector3> RelaySwitched;

    [SerializeField] private SoundCue[] stops;
    [SerializeField] private SoundCue tunde;
    [SerializeField] private AudioSource phone;
    [SerializeField] private AudioClip relayOn;
    [SerializeField] private AudioClip chime;
    [SerializeField] private AudioClip message;
    [SerializeField] private AudioClip reply;
    [SerializeField] private float reach = 3f;
    [SerializeField] private float hintEvery = 30f;

    private int current;
    private float nextHint;
    private bool introPlayed;
    private bool inRange;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        RelaySwitched = null;
    }

    private void OnEnable()
    {
        GameFlow.StateChanged += OnState;
        SonarSystem.Pinged += OnPing;
    }

    private void OnDisable()
    {
        GameFlow.StateChanged -= OnState;
        SonarSystem.Pinged -= OnPing;
    }

    private void Start()
    {
        for (int i = 0; i < stops.Length; i++)
        {
            stops[i].SetPlaying(i == 0, i == 0);
        }
        if (GameFlow.IsPlaying)
        {
            OnState(FlowState.Playing);
        }
    }

    private void OnState(FlowState state)
    {
        if (state == FlowState.Playing && !introPlayed)
        {
            introPlayed = true;
            nextHint = Time.time + hintEvery + 20f;
            StartCoroutine(Intro());
        }
    }

    private IEnumerator Intro()
    {
        yield return new WaitForSeconds(1f);
        GameFlow.Prompt(PlayerRig.XRMode ? "Trigger: ping   |   A/X: listen   |   Grip: cover lantern" : "Click to look   |   Space: ping   |   Shift: listen   |   C: cover lantern");
        for (int i = 1; i <= 4 && GameFlow.State == FlowState.Playing; i++)
        {
            yield return new WaitForSeconds(GameFlow.Caption("Intro0" + i, null, 0f) + 0.4f);
        }
        GameFlow.Prompt("");
    }

    private void Update()
    {
        PlayerRig rig = PlayerRig.Instance;
        if (!GameFlow.IsPlaying || rig == null || current >= stops.Length)
        {
            return;
        }
        Transform target = stops[current].transform;
        bool near = Vector3.Distance(rig.Feet, new Vector3(target.position.x, rig.Feet.y, target.position.z)) < reach;
        if (near != inRange)
        {
            inRange = near;
            GameFlow.Prompt(near ? GameFlow.Line(current == stops.Length - 1 ? "TowerPrompt" : "RelayPrompt") : "");
        }
        if (near && PlayerRig.Stillness >= 1f)
        {
            Activate();
        }
        if (Time.time > nextHint)
        {
            nextHint = Time.time + hintEvery;
            GameFlow.Caption(current == stops.Length - 1 ? "HintTower" : "HintRelay", target, 5f);
        }
    }

    private void OnPing(Vector3 origin)
    {
        if (inRange && GameFlow.IsPlaying)
        {
            Activate();
        }
    }

    private void Activate()
    {
        inRange = false;
        GameFlow.Prompt("");
        SoundCue stop = stops[current];
        nextHint = Time.time + hintEvery;
        if (current == stops.Length - 1)
        {
            StartCoroutine(Deliver(stop));
            return;
        }
        stop.SetPlaying(false, false);
        stop.Play(relayOn, 1f);
        SonarSystem.Emit(stop.transform.position, PulseKind.Sound, 30f, 1f);
        GameFlow.Haptic(0.6f, 0.2f);
        RelaySwitched?.Invoke(stop.transform.position);
        current++;
        GameFlow.Caption("RelayOn" + current, null, 0f);
        stops[current].SetPlaying(true, true);
    }

    private IEnumerator Deliver(SoundCue tower)
    {
        GameFlow.BeginDelivery();
        SoundCue.Focus = 0f;
        tower.FadeTo(false, 1.2f);
        tunde.Silence();
        phone.PlayOneShot(chime, 0.7f);
        yield return new WaitForSeconds(0.8f);
        phone.clip = message;
        if (message != null)
        {
            phone.Play();
        }
        yield return new WaitForSeconds(GameFlow.Caption("Message01", null, message != null ? message.length : 0f) + 0.6f);
        float said = tunde.Speak(reply, "Tunde02");
        yield return new WaitForSeconds(said * 0.8f);
        CityController.PowerOn();
        yield return new WaitForSeconds(GameFlow.Caption("Deliver02", tower.transform, 0f) + 4f);
        GameFlow.Finish(RunResult.Delivered);
    }
}
