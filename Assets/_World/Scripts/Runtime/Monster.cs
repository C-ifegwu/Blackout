using UnityEngine;
using Blackout.Experience;

namespace Blackout.World
{
    /// <summary>
    /// One creature that roams the dark city. It patrols the road grid, notices the player by sight (cone, range, line of
    /// sight, shorter range when the player holds still) or by sound (sonar pings), charges when it has noticed them, and
    /// dies when the message is delivered at dawn. Sight takes a moment to build up; the glowing eyes show how alert it is.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class Monster : MonoBehaviour
    {
        public Animator animator;
        public AudioSource voice;
        public AudioSource feet;
        public AudioClip[] idleClips = new AudioClip[0];
        public AudioClip[] alertClips = new AudioClip[0];
        public AudioClip[] stepClips = new AudioClip[0];
        public AudioClip deathClip;
        public AudioClip attackClip;
        public Renderer[] eyes = new Renderer[0];
        public Vector2Int home;
        public float speedScale = 1f;
        public bool hasDeathClip;
        public float eyeHeight = 1.7f;

        public MonsterState State { get; private set; } = MonsterState.Dormant;
        public float Alert => _alert;
        public bool IsAwake => State != MonsterState.Dormant && State != MonsterState.Dead;

        static readonly int SpeedHash = Animator.StringToHash("Speed");
        static readonly int DeadHash = Animator.StringToHash("Dead");
        static readonly int AttackHash = Animator.StringToHash("Attack");
        static readonly int BaseColorHash = Shader.PropertyToID("_BaseColor");
        static readonly RaycastHit[] Hits = new RaycastHit[10];

        MonsterTuning _t;
        RoadGraph _graph;
        MonsterDirector _dir;
        CharacterController _cc;
        System.Random _rng = new System.Random(1);
        MaterialPropertyBlock _mpb;

        Vector2Int _node, _prevNode;
        Vector3 _target, _lastKnown;
        float _alert, _graceLeft, _cooldown, _wait, _perceiveTimer, _lost, _chaseTime, _searchLeft, _stuckTimer;
        float _speed, _stepDist, _nextIdle, _nextGrowl, _deathT, _glowShown = -1f, _stuckCheck;
        Vector3 _stuckPos, _deathStartPos;
        Quaternion _deathStartRot;
        bool _routeDirect;
        bool _fallen;

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _mpb = new MaterialPropertyBlock();
        }

        public void Configure(MonsterTuning t, RoadGraph graph, MonsterDirector director, int seed)
        {
            _t = t; _graph = graph; _dir = director;
            _rng = new System.Random(seed * 7919 + 13);
        }

        public void Spawn(Vector2Int node)
        {
            gameObject.SetActive(true);
            State = MonsterState.Patrol;
            _alert = 0f; _lost = 0f; _chaseTime = 0f; _cooldown = 0f; _wait = 0f; _searchLeft = 0f; _stuckTimer = 0f;
            _graceLeft = _t.graceSeconds; _speed = 0f; _stepDist = 0f; _fallen = false; _glowShown = -1f;
            _nextIdle = Time.time + 3f + (float)_rng.NextDouble() * 6f;
            _nextGrowl = 0f;
            _node = node; _prevNode = node;

            var p = _graph.WorldPos(node);
            _cc.enabled = false;
            transform.SetPositionAndRotation(p, Quaternion.Euler(0f, _rng.Next(360), 0f));
            _cc.enabled = true;
            _stuckPos = p; _stuckCheck = 0f;

            if (animator != null)
            {
                animator.Rebind();
                animator.Update(0f);
                animator.SetBool(DeadHash, false);
            }
            ChooseNextNode();
            ApplyEyes(0.2f);
        }

        /// <summary>Moves the monster to <paramref name="pos"/> facing <paramref name="lookAt"/> (used to keep it clear of the end screen).</summary>
        public void PlaceFacing(Vector3 pos, Vector3 lookAt)
        {
            var look = lookAt - pos; look.y = 0f;
            _cc.enabled = false;
            transform.SetPositionAndRotation(new Vector3(pos.x, transform.position.y, pos.z),
                look.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(look.normalized, Vector3.up) : transform.rotation);
            _cc.enabled = true;
        }

        public void Hide()
        {
            State = MonsterState.Dormant;
            if (voice != null) voice.Stop();
            if (feet != null) feet.Stop();
            gameObject.SetActive(false);
        }

        /// <summary>Stops moving but stays visible (used when the player has been caught).</summary>
        public void Freeze()
        {
            if (State == MonsterState.Dead) return;
            State = MonsterState.Dormant;
            if (voice != null) voice.Stop();
            _speed = 0f;
            if (animator != null) animator.SetFloat(SpeedHash, 0f);
        }

        public void Die()
        {
            if (State == MonsterState.Dead || !gameObject.activeSelf) return;
            State = MonsterState.Dead;
            _deathT = 0f;
            _deathStartPos = transform.position;
            _deathStartRot = transform.rotation;
            if (voice != null) { voice.Stop(); if (deathClip != null) voice.PlayOneShot(deathClip, 0.9f); }
            if (animator != null) animator.SetBool(DeadHash, true);
        }

        /// <summary>A noise at <paramref name="pos"/> that carries <paramref name="radius"/> metres.</summary>
        public void Hear(Vector3 pos, float radius)
        {
            if (!IsAwake || State == MonsterState.Chase) return;
            if (!MonsterPerception.CanHear(transform.position, pos, radius, _t.hearingScale)) return;
            _point = pos;
            _point.y = 0f;
            State = MonsterState.Investigate;
            _alert = Mathf.Max(_alert, 0.45f);
            _searchLeft = 0f;
        }

        Vector3 _point;

        // ------------------------------------------------------------------ update

        void Update()
        {
            if (State == MonsterState.Dead) { TickDeath(Time.deltaTime); return; }
            if (!IsAwake || GameFlow.State != GameState.Playing) { TickIdleVisuals(Time.deltaTime); return; }

            float dt = Time.deltaTime;
            if (_graceLeft > 0f) _graceLeft -= dt;
            if (_cooldown > 0f) _cooldown -= dt;

            _perceiveTimer -= dt;
            if (_perceiveTimer <= 0f)
            {
                _perceiveTimer = 0.16f + (float)_rng.NextDouble() * 0.04f;
                Perceive(0.18f);
            }

            switch (State)
            {
                case MonsterState.Patrol: UpdatePatrol(dt); break;
                case MonsterState.Investigate: UpdateInvestigate(dt); break;
                case MonsterState.Chase: UpdateChase(dt); break;
                case MonsterState.Search: UpdateSearch(dt); break;
            }

            CheckCatch();
            DetectStuck(dt);
            Audio(dt);
            Visuals(dt);
        }

        // ------------------------------------------------------------------ senses

        void Perceive(float step)
        {
            if (_dir == null || !_dir.HasPlayer) return;
            Vector3 eye = transform.position + Vector3.up * eyeHeight;
            Vector3 target = _dir.PlayerEye;
            var flat = target - eye; flat.y = 0f;
            float dist = flat.magnitude;
            float range = MonsterPerception.SightRange(_t, _dir.PlayerStill) * _dir.PlayerVisibility;
            if (State == MonsterState.Chase) range *= 1.6f;   // a hunting monster keeps track more easily

            bool sees = false;
            if (_graceLeft <= 0f && _cooldown <= 0f && dist <= range)
            {
                bool clear = LineClear(eye, target);
                sees = MonsterPerception.CanSee(eye, transform.forward, target, range,
                    State == MonsterState.Chase ? 360f : _t.fovDegrees, _t.closeRange, clear);
                _routeDirect = clear;
            }

            if (State == MonsterState.Chase)
            {
                if (sees) { _lastKnown = _dir.PlayerPos; _lost = 0f; }
                else _lost += step;
                _alert = 1f;
                return;
            }

            if (sees)
            {
                _alert += step / MonsterPerception.NoticeSeconds(dist, range);
                _lastKnown = _dir.PlayerPos;
                if (_alert >= 1f) StartChase();
            }
            else
            {
                _alert = Mathf.Max(0f, _alert - step * 0.5f);
            }
        }

        bool LineClear(Vector3 eye, Vector3 target)
        {
            var d = target - eye;
            float dist = d.magnitude;
            if (dist < 0.05f) return true;
            int n = Physics.RaycastNonAlloc(eye, d / dist, Hits, dist, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var t = Hits[i].collider.transform;
                if (t.IsChildOf(transform)) continue;
                if (_dir.IsPlayer(t)) continue;
                if (t.GetComponentInParent<Monster>() != null) continue;
                return false;
            }
            return true;
        }

        // ------------------------------------------------------------------ behaviours

        void StartChase()
        {
            State = MonsterState.Chase;
            _alert = 1f; _lost = 0f; _chaseTime = 0f;
            _lastKnown = _dir.PlayerPos;
            if (voice != null && alertClips.Length > 0) voice.PlayOneShot(alertClips[_rng.Next(alertClips.Length)], 1f);
            if (animator != null) animator.SetTrigger(AttackHash);
            _nextGrowl = Time.time + 3f;
            _dir.NotifySeen(this);
        }

        void UpdatePatrol(float dt)
        {
            if (_wait > 0f)
            {
                _wait -= dt;
                _speed = 0f;
                // look around while pausing
                transform.Rotate(0f, Mathf.Sin(Time.time * 1.3f + _node.x) * 25f * dt, 0f);
                return;
            }
            var to = _target - transform.position; to.y = 0f;
            if (to.magnitude < 1.4f)
            {
                _wait = 1.2f + (float)_rng.NextDouble() * 2.5f;
                _prevNode = _node;
                _node = _graph.Nearest(_target);
                ChooseNextNode();
                return;
            }
            MoveTo(_target, _t.patrolSpeed * speedScale, dt);
        }

        void ChooseNextNode()
        {
            var next = _graph.RandomNeighbour(_node, _prevNode, _rng);
            _target = _graph.WorldPos(next);
        }

        void UpdateInvestigate(float dt)
        {
            var to = _point - transform.position; to.y = 0f;
            if (to.magnitude < 2f) { EnterSearch(4f); return; }
            MoveRoute(_point, _t.investigateSpeed * speedScale, dt);
        }

        void UpdateChase(float dt)
        {
            _chaseTime += dt;
            if (_chaseTime > _t.maxChaseSeconds || _lost > _t.giveUpSeconds)
            {
                _cooldown = 6f;            // too tired to start another chase straight away
                _point = _lastKnown;
                EnterSearch(5f);
                return;
            }
            Vector3 goal = _lost <= 0.4f && _dir.HasPlayer ? _dir.PlayerPos : _lastKnown;
            MoveRoute(goal, _t.chaseSpeed * speedScale, dt);
        }

        void EnterSearch(float seconds)
        {
            State = MonsterState.Search;
            _searchLeft = seconds;
            _alert = Mathf.Min(_alert, 0.3f);
            _speed = 0f;
        }

        void UpdateSearch(float dt)
        {
            _searchLeft -= dt;
            _speed = 0f;
            transform.Rotate(0f, Mathf.Sin(Time.time * 1.8f) * 55f * dt, 0f);
            if (_searchLeft <= 0f)
            {
                State = MonsterState.Patrol;
                _node = _graph.Nearest(transform.position);
                _prevNode = _node;
                _target = _graph.WorldPos(_node);
                _wait = 0f;
            }
        }

        // ------------------------------------------------------------------ movement

        void MoveRoute(Vector3 goal, float speed, float dt)
        {
            var from = transform.position;
            var flat = goal - from; flat.y = 0f;
            var goalNode = _graph.Nearest(goal);
            var curNode = _graph.Nearest(from);
            Vector3 wp = goal;
            if (!_routeDirect && curNode != goalNode)
            {
                var curPos = _graph.WorldPos(curNode);
                var tc = curPos - from; tc.y = 0f;
                wp = tc.magnitude > 2.5f ? curPos : _graph.WorldPos(_graph.StepToward(curNode, goalNode));
            }
            MoveTo(wp, speed, dt);
        }

        void MoveTo(Vector3 point, float speed, float dt)
        {
            var d = point - transform.position; d.y = 0f;
            float dist = d.magnitude;
            if (dist < 0.02f) { _speed = 0f; return; }
            var dir = d / dist;
            var want = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, 260f * dt);
            // only advance quickly once roughly facing the way we are going
            float facing = Mathf.Clamp01(Vector3.Dot(transform.forward, dir) * 0.5f + 0.6f);
            float step = Mathf.Min(speed * facing * dt, dist);
            var before = transform.position;
            _cc.Move(dir * step);   // the ground is flat at y = 0, so no gravity is needed
            _speed = Mathf.Lerp(_speed, (transform.position - before).magnitude / Mathf.Max(dt, 1e-4f), 0.25f);
        }

        void DetectStuck(float dt)
        {
            _stuckCheck += dt;
            if (_stuckCheck < 0.6f) return;
            bool moving = _speed > 0.01f || State == MonsterState.Patrol && _wait <= 0f || State == MonsterState.Chase || State == MonsterState.Investigate;
            float moved = (transform.position - _stuckPos).magnitude;
            _stuckPos = transform.position;
            float want = Mathf.Max(0.3f, _t.patrolSpeed * 0.5f) * _stuckCheck;
            _stuckCheck = 0f;
            if (State == MonsterState.Search || (State == MonsterState.Patrol && _wait > 0f) || !moving) { _stuckTimer = 0f; return; }
            if (moved < want * 0.35f) _stuckTimer += 0.6f; else _stuckTimer = 0f;
            if (_stuckTimer < 2.4f) return;
            _stuckTimer = 0f;
            if (State == MonsterState.Chase) { _cooldown = 3f; _point = transform.position; EnterSearch(3f); }
            else if (State == MonsterState.Investigate) EnterSearch(2f);
            else { _prevNode = _node; ChooseNextNode(); }
        }

        void CheckCatch()
        {
            if (_dir == null || !_dir.HasPlayer || _graceLeft > 0f) return;
            var d = _dir.PlayerPos - transform.position; d.y = 0f;
            if (d.magnitude <= _t.catchDistance && Mathf.Abs(_dir.PlayerEye.y - transform.position.y) < 2.6f)
                _dir.Catch(this);
        }

        // ------------------------------------------------------------------ feedback

        void Audio(float dt)
        {
            if (voice == null) return;
            float now = Time.time;
            if (State == MonsterState.Chase)
            {
                if (now >= _nextGrowl && alertClips.Length > 0)
                {
                    _nextGrowl = now + 3.5f + (float)_rng.NextDouble() * 2f;
                    voice.PlayOneShot(alertClips[_rng.Next(alertClips.Length)], 0.8f);
                }
            }
            else if (now >= _nextIdle && idleClips.Length > 0)
            {
                _nextIdle = now + 6f + (float)_rng.NextDouble() * 8f;
                voice.PlayOneShot(idleClips[_rng.Next(idleClips.Length)], State == MonsterState.Patrol ? 0.55f : 0.8f);
            }

            if (feet != null && stepClips.Length > 0 && _speed > 0.3f)
            {
                _stepDist += _speed * dt;
                float stride = State == MonsterState.Chase ? 1.7f : 1.1f;
                if (_stepDist >= stride)
                {
                    _stepDist = 0f;
                    feet.pitch = 0.85f + (float)_rng.NextDouble() * 0.25f;
                    feet.PlayOneShot(stepClips[_rng.Next(stepClips.Length)], State == MonsterState.Chase ? 0.9f : 0.55f);
                }
            }
        }

        void Visuals(float dt)
        {
            if (animator != null) animator.SetFloat(SpeedHash, _speed, 0.12f, dt);
            float glow = State == MonsterState.Chase ? 1f : Mathf.Max(_alert, State == MonsterState.Investigate ? 0.6f : 0.2f);
            ApplyEyes(glow);
        }

        void TickIdleVisuals(float dt)
        {
            if (animator != null && animator.isActiveAndEnabled) animator.SetFloat(SpeedHash, 0f, 0.1f, dt);
        }

        void ApplyEyes(float glow)
        {
            if (Mathf.Abs(glow - _glowShown) < 0.02f) return;
            _glowShown = glow;
            var c = new Color(1f, 0.08f, 0.03f, 1f) * Mathf.Lerp(0.15f, 1.4f, glow);
            c.a = 1f;
            foreach (var r in eyes)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorHash, c);
                r.SetPropertyBlock(_mpb);
            }
        }

        void TickDeath(float dt)
        {
            _deathT += dt;
            ApplyEyes(Mathf.Max(0f, 0.2f - _deathT));
            if (animator != null && animator.isActiveAndEnabled) animator.SetFloat(SpeedHash, 0f);
            if (!hasDeathClip)
            {
                float k = Mathf.Clamp01(_deathT / 0.9f);
                transform.rotation = _deathStartRot * Quaternion.Euler(-90f * k * k, 0f, 0f);
            }
            if (_deathT > 2.2f)
            {
                if (!_fallen) { _fallen = true; _cc.enabled = false; }
                transform.position = _deathStartPos + Vector3.down * ((_deathT - 2.2f) * 0.7f);
            }
            if (_deathT > 4.6f) { gameObject.SetActive(false); State = MonsterState.Dormant; }
        }
    }
}
