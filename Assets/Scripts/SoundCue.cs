using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class SoundCue : MonoBehaviour
{
    public static float Focus;
    public static SoundCue Beacon { get; private set; }

    [SerializeField] private string captionId;
    [SerializeField] private float triggerRadius = 6f;
    [SerializeField] private bool playOnApproach;
    [SerializeField] private float captionRepeat = 35f;
    [SerializeField] private float pulseRadius = 5f;
    [SerializeField] private float pulseStrength = 0.7f;

    private AudioSource source;
    private float baseVolume;
    private float fade = 1f;
    private float fadeSpeed;
    private bool spoken;
    private float nextCaption;
    private float echoAt = -1f;
    private float talkingUntil;

    public bool Active => source.isPlaying || Time.time < talkingUntil;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Focus = 0f;
        Beacon = null;
    }

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        baseVolume = source.volume;
    }

    private void OnEnable() => SonarSystem.Pinged += OnPing;

    private void OnDisable() => SonarSystem.Pinged -= OnPing;

    private void OnPing(Vector3 origin)
    {
        float distance = Vector3.Distance(origin, transform.position);
        echoAt = distance <= 26f ? Time.time + distance / 10f : echoAt;
    }

    private void Update()
    {
        fade = Mathf.MoveTowards(fade, fadeSpeed < 0f ? 0f : 1f, Mathf.Abs(fadeSpeed) * Time.deltaTime);
        float focusScale = this == Beacon ? Mathf.Lerp(1f, 1.7f, Focus) : Mathf.Lerp(1f, 0.25f, Focus);
        source.volume = Mathf.Clamp01(baseVolume * focusScale) * fade;
        if (PlayerRig.Instance == null || GameFlow.State == FlowState.Paused)
        {
            return;
        }
        float distance = Vector3.Distance(PlayerRig.Instance.Head.position, transform.position);
        if (echoAt > 0f && Time.time >= echoAt)
        {
            echoAt = -1f;
            SonarSystem.Emit(transform.position, PulseKind.Sound, pulseRadius, pulseStrength);
        }
        if (!GameFlow.IsPlaying)
        {
            return;
        }
        if (playOnApproach && !spoken && distance < triggerRadius)
        {
            spoken = true;
            Speak(source.clip, captionId);
        }
        else if (!playOnApproach && source.isPlaying && distance < triggerRadius && Time.time >= nextCaption)
        {
            nextCaption = Time.time + captionRepeat;
            GameFlow.Caption(captionId, transform, 4f);
        }
    }

    public float Speak(AudioClip clip, string id)
    {
        fadeSpeed = 0f;
        fade = 1f;
        source.loop = false;
        source.clip = clip;
        float length = GameFlow.Caption(id, transform, clip != null ? clip.length : 0f);
        talkingUntil = Time.time + length;
        if (clip != null)
        {
            source.Play();
        }
        return length;
    }

    public void SetPlaying(bool playing, bool beacon)
    {
        if (beacon)
        {
            Beacon = this;
        }
        if (playing && !source.isPlaying)
        {
            source.Play();
        }
        else if (!playing)
        {
            source.Stop();
        }
    }

    public void Silence()
    {
        spoken = true;
        source.Stop();
    }

    public void FadeTo(bool audible, float seconds)
    {
        fadeSpeed = (audible ? 1f : -1f) / Mathf.Max(seconds, 0.01f);
    }

    public void Play(AudioClip clip, float volume) => source.PlayOneShot(clip, volume);
}
