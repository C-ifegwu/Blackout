using System.Collections.Generic;
using UnityEngine;

public enum MonsterState { Patrol, Investigate, Chase, Search, Dead }

public class Monster : MonoBehaviour
{
    public static readonly List<Monster> All = new List<Monster>();

    public MonsterState State { get; private set; }

    [SerializeField] private Transform[] route;
    [SerializeField] private int minLevel = 1;
    [SerializeField] private float walkSpeed = 1.3f, runSpeed = 3.4f, sight = 15f;
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource voice;
    [SerializeField] private AudioClip[] growls;
    [SerializeField] private AudioClip roar, fall;

    private Vector3 goal;
    private int waypoint;
    private float timer, nextGrowl;

    private float Gentle => GameSettings.MonsterLevel == 1 ? 0.7f : 1f;

    private void OnEnable()
    {
        All.Add(this);
        SonarSystem.Pinged += OnPing;
        RelayRoute.RelaySwitched += OnRelay;
        CityController.PoweredOn += Die;
    }

    private void OnDisable()
    {
        All.Remove(this);
        SonarSystem.Pinged -= OnPing;
        RelayRoute.RelaySwitched -= OnRelay;
        CityController.PoweredOn -= Die;
    }

    private void Start()
    {
        gameObject.SetActive(GameSettings.MonsterLevel >= minLevel);
        goal = route[0].position;
        nextGrowl = Time.time + Random.Range(4f, 10f);
    }

    private void OnPing(Vector3 origin) => Hear(origin, 26f);

    private void OnRelay(Vector3 origin) => Hear(origin, 40f);

    private void Hear(Vector3 origin, float range)
    {
        if (State != MonsterState.Dead && State != MonsterState.Chase && Vector3.Distance(origin, transform.position) < range)
        {
            State = MonsterState.Investigate;
            goal = origin;
        }
    }

    private void Update()
    {
        PlayerRig rig = PlayerRig.Instance;
        if (State == MonsterState.Dead || rig == null || !GameFlow.IsPlaying)
        {
            animator.SetFloat("Speed", 0f);
            return;
        }
        Vector3 player = rig.Feet;
        float distance = Vector3.Distance(transform.position, player);
        if (Sees(rig.Head.position, distance))
        {
            if (State != MonsterState.Chase)
            {
                voice.PlayOneShot(roar, 1f);
                GameFlow.Caption("MonsterSeen", transform, 3f);
            }
            State = MonsterState.Chase;
            goal = player;
            timer = 6f;
        }
        Think();
        Move((State == MonsterState.Chase ? runSpeed : walkSpeed) * Gentle);
        GameFlow.Finish(State == MonsterState.Chase && distance < 1.3f ? RunResult.Caught : RunResult.None);
        Growl(distance);
    }

    private bool Sees(Vector3 eye, float distance)
    {
        Vector3 from = transform.position + Vector3.up * 1.6f;
        float angle = Vector3.Angle(transform.forward, Vector3.ProjectOnPlane(eye - from, Vector3.up));
        bool inCone = distance < sight * Gentle * LanternLight.Visibility && (angle < 60f || distance < 3f);
        RaycastHit hit;
        return inCone && (!Physics.Linecast(from, eye, out hit, ~(1 << 5), QueryTriggerInteraction.Ignore) || hit.transform.root == PlayerRig.Instance.transform.root);
    }

    private void Think()
    {
        timer -= Time.deltaTime;
        bool arrived = Vector3.Distance(transform.position, goal) < 0.8f;
        if (State == MonsterState.Chase && timer <= 0f)
        {
            State = MonsterState.Search;
            timer = 4f;
        }
        else if ((State == MonsterState.Investigate && arrived) || (State == MonsterState.Search && timer <= 0f))
        {
            State = MonsterState.Patrol;
            goal = route[waypoint].position;
        }
        else if (State == MonsterState.Patrol && arrived)
        {
            waypoint = (waypoint + 1) % route.Length;
            goal = route[waypoint].position;
        }
    }

    private void Move(float speed)
    {
        Vector3 to = Vector3.ProjectOnPlane(goal - transform.position, Vector3.up);
        float pace = State == MonsterState.Search || to.magnitude < 0.1f ? 0f : speed;
        transform.rotation = to.sqrMagnitude > 0.01f ? Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), 220f * Time.deltaTime) : transform.rotation;
        transform.position += transform.forward * pace * Time.deltaTime;
        animator.SetFloat("Speed", pace);
    }

    private void Growl(float distance)
    {
        if (Time.time < nextGrowl)
        {
            return;
        }
        nextGrowl = Time.time + Random.Range(7f, 14f);
        voice.PlayOneShot(growls[Random.Range(0, growls.Length)], 0.8f);
        if (distance < 22f)
        {
            GameFlow.Caption("Growl", transform, 3f);
        }
    }

    private void Die()
    {
        State = MonsterState.Dead;
        voice.PlayOneShot(fall, 1f);
        animator.SetTrigger("Die");
    }
}
