using UnityEngine;

public class LanternLight : MonoBehaviour
{
    public static float Visibility { get; private set; } = 1f;
    public static float Danger { get; private set; }

    [SerializeField] private Transform model;
    [SerializeField] private Vector3 handOffset = new Vector3(0f, -0.12f, 0.05f);
    [SerializeField] private Vector3 desktopOffset = new Vector3(-0.28f, -0.32f, 0.45f);
    [SerializeField] private float litRadius = 2.5f;
    [SerializeField] private float coveredRadius = 0.7f;
    [SerializeField] private AudioSource source;
    [SerializeField] private AudioClip coverClip;
    [SerializeField] private AudioSource heartbeat;

    private float radius;
    private bool covered;

    private void Start()
    {
        radius = litRadius;
    }

    private void LateUpdate()
    {
        PlayerRig rig = PlayerRig.Instance;
        if (rig == null)
        {
            return;
        }
        Transform hold = rig.LeftHand;
        model.position = hold.TransformPoint(PlayerRig.XRMode ? handOffset : desktopOffset);
        model.rotation = Quaternion.Euler(0f, hold.eulerAngles.y, 0f);
        bool cover = ControllerInput.CoverHeld && GameFlow.IsPlaying;
        if (cover != covered)
        {
            covered = cover;
            source.PlayOneShot(coverClip, 0.5f);
            GameFlow.Haptic(0.25f, 0.06f);
        }
        float flicker = 1f + Mathf.Sin(Time.time * 9f) * 0.03f + Mathf.Sin(Time.time * 23f) * 0.02f;
        radius = Mathf.MoveTowards(radius, covered ? coveredRadius : litRadius, Time.deltaTime * 12f);
        float power = Mathf.Max(CityController.Power, CityController.Dawn);
        Shader.SetGlobalVector("_Lantern", new Vector4(model.position.x, model.position.y, model.position.z, radius * flicker * (1f - power)));
        Danger = 0f;
        foreach (Monster monster in Monster.All)
        {
            float near = Mathf.Clamp01(1f - Vector3.Distance(monster.transform.position, rig.Feet) / 22f);
            Danger = GameFlow.IsPlaying && monster.State != MonsterState.Dead ? Mathf.Max(Danger, monster.State == MonsterState.Chase ? 1f : near * 0.6f) : Danger;
        }
        heartbeat.volume = Mathf.MoveTowards(heartbeat.volume, Danger * 0.8f, Time.deltaTime);
        heartbeat.pitch = 0.9f + Danger * 0.4f;
        Visibility = Mathf.Lerp(0.35f, 1f, Mathf.InverseLerp(coveredRadius, litRadius, radius)) * (1f - PlayerRig.Stillness * 0.5f);
    }
}
