using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class CityController : MonoBehaviour
{
    public static event Action PoweredOn;
    public static float Power { get; private set; }
    public static float Dawn { get; private set; }
    public static float Remaining { get; private set; }
    public static string DawnText => Mathf.FloorToInt(Remaining / 60f) + ":" + Mathf.FloorToInt(Remaining % 60f).ToString("00");

    private static CityController instance;

    [SerializeField] private Transform[] lamps;
    [SerializeField] private float lampRadius = 9f;
    [SerializeField] private float rampSeconds = 4f;
    [SerializeField] private float dawnLength = 360f;
    [SerializeField] private float sunriseSeconds = 35f;
    [SerializeField] private Camera view;
    [SerializeField] private SoundCue generator;
    [SerializeField] private AudioSource night;
    [SerializeField] private AudioSource powerUp;
    [SerializeField] private AudioSource cheer;
    [SerializeField] private AudioSource birds;

    private readonly Vector4[] lampData = new Vector4[16];
    private float elapsed;
    private bool started;
    private bool dawnDone;
    private int warned;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        PoweredOn = null;
    }

    private void Awake()
    {
        instance = this;
        view.GetUniversalAdditionalCameraData().renderPostProcessing = !Application.isMobilePlatform;
        Power = 0f;
        Dawn = 0f;
        Remaining = dawnLength;
        for (int i = 0; i < lamps.Length && i < lampData.Length; i++)
        {
            lampData[i] = new Vector4(lamps[i].position.x, lamps[i].position.y, lamps[i].position.z, lampRadius);
        }
        Shader.SetGlobalVectorArray("_LampPos", lampData);
        Shader.SetGlobalFloat("_LampCount", Mathf.Min(lamps.Length, lampData.Length));
        Shader.SetGlobalVector("_MarketCenter", transform.position);
        Shader.SetGlobalFloat("_CityPower", 0f);
    }

    public static void PowerOn()
    {
        if (instance != null && !instance.started)
        {
            instance.started = true;
            instance.StartCoroutine(instance.Ramp());
        }
    }

    private IEnumerator Ramp()
    {
        float duration = GameSettings.ReduceFlashing ? rampSeconds + 1.5f : rampSeconds;
        PoweredOn?.Invoke();
        generator.FadeTo(false, 2.5f);
        powerUp.Play();
        cheer.PlayDelayed(1.6f);
        birds.PlayDelayed(12f);
        GameFlow.Caption("City01", null, 5f);
        for (float time = 0f; time < duration; time += Time.deltaTime)
        {
            Power = Mathf.SmoothStep(0f, 1f, time / duration);
            Shader.SetGlobalFloat("_CityPower", Power);
            yield return null;
        }
        Power = 1f;
        Shader.SetGlobalFloat("_CityPower", 1f);
    }

    private void Update()
    {
        if (started)
        {
            Dawn = Mathf.MoveTowards(Dawn, 1f, Time.deltaTime / sunriseSeconds);
        }
        else if (GameFlow.IsPlaying && GameSettings.DawnMode)
        {
            elapsed += Time.deltaTime;
            Remaining = Mathf.Max(0f, dawnLength - elapsed);
            Dawn = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(dawnLength * 0.85f, dawnLength, elapsed));
            Warn();
        }
        Sky();
        night.volume = Mathf.MoveTowards(night.volume, 0.35f * (1f - Dawn * 0.6f), Time.deltaTime * 0.2f);
    }

    private void Warn()
    {
        float[] marks = { 180f, 60f, 0f };
        if (warned < marks.Length && Remaining <= marks[warned])
        {
            GameFlow.Caption("DawnWarn" + warned, null, 5f);
            warned++;
        }
        if (!dawnDone && Remaining <= 0f)
        {
            dawnDone = true;
            birds.Play();
            GameFlow.Finish(RunResult.Dawn);
        }
    }

    private void Sky()
    {
        Color dark = Color.Lerp(new Color(0.004f, 0.005f, 0.012f), new Color(0.07f, 0.05f, 0.06f), Power);
        view.backgroundColor = Color.Lerp(dark, new Color(0.62f, 0.42f, 0.38f), Dawn);
        Shader.SetGlobalFloat("_Dawn", Dawn);
        Shader.SetGlobalColor("_SkyAmbient", Color.Lerp(new Color(0.16f, 0.15f, 0.19f), new Color(0.55f, 0.52f, 0.55f), Dawn));
        Shader.SetGlobalColor("_SunColor", new Color(1f, 0.78f, 0.58f) * Dawn);
        Shader.SetGlobalVector("_SunDir", new Vector3(0.8f, 0.35f, 0.3f).normalized);
    }
}
