using System.Collections.Generic;
using UnityEngine;

namespace Blackout.World
{
    public enum MonsterState { Dormant, Patrol, Investigate, Chase, Search, Dead }

    /// <summary>Difficulty numbers for the creatures that roam the dark. Level 0 = off, 1 = gentle, 2 = normal.</summary>
    public struct MonsterTuning
    {
        public int count;
        public float sightRange, sightRangeStill, fovDegrees, closeRange;
        public float hearingScale;
        public float patrolSpeed, investigateSpeed, chaseSpeed;
        public float giveUpSeconds, maxChaseSeconds, graceSeconds, catchDistance;

        public static MonsterTuning For(int level)
        {
            switch (level)
            {
                case 0:
                    return new MonsterTuning { count = 0 };
                case 1:
                    return new MonsterTuning
                    {
                        count = 3, sightRange = 10f, sightRangeStill = 4.5f, fovDegrees = 90f, closeRange = 2.5f, hearingScale = 0.6f,
                        patrolSpeed = 1.1f, investigateSpeed = 1.6f, chaseSpeed = 2.1f,
                        giveUpSeconds = 4f, maxChaseSeconds = 9f, graceSeconds = 40f, catchDistance = 1.0f
                    };
                default:
                    return new MonsterTuning
                    {
                        count = 6, sightRange = 22f, sightRangeStill = 10f, fovDegrees = 130f, closeRange = 4.5f, hearingScale = 1.4f,
                        patrolSpeed = 1.7f, investigateSpeed = 2.5f, chaseSpeed = 3.3f,
                        giveUpSeconds = 9f, maxChaseSeconds = 20f, graceSeconds = 12f, catchDistance = 1.2f
                    };
            }
        }
    }

    /// <summary>Pure perception rules, kept free of scene state so they can be unit tested.</summary>
    public static class MonsterPerception
    {
        /// <summary>How far a monster can see a player who is moving or holding still.</summary>
        public static float SightRange(MonsterTuning t, bool playerStill) => playerStill ? t.sightRangeStill : t.sightRange;

        public static bool CanSee(Vector3 eye, Vector3 forward, Vector3 target, float range, float fovDegrees, float closeRange, bool lineClear)
        {
            var d = target - eye; d.y = 0f;
            float dist = d.magnitude;
            if (dist > range || !lineClear) return false;
            if (dist <= closeRange) return true;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f) return true;
            return Vector3.Angle(forward, d) <= fovDegrees * 0.5f;
        }

        public static bool CanHear(Vector3 monster, Vector3 noise, float noiseRadius, float hearingScale)
        {
            var d = noise - monster; d.y = 0f;
            return d.magnitude <= noiseRadius * hearingScale;
        }

        /// <summary>Seconds of sight needed before a monster commits to a chase: closer players are noticed faster.</summary>
        public static float NoticeSeconds(float distance, float range)
        {
            float k = Mathf.Clamp01(distance / Mathf.Max(0.01f, range));
            return Mathf.Lerp(0.5f, 1.6f, k);
        }
    }

    /// <summary>
    /// The city's roads form a regular grid, so monsters can navigate by intersection with no NavMesh baking.
    /// </summary>
    public sealed class RoadGraph
    {
        public readonly Vector2 origin;
        public readonly float spacing;
        public readonly int count;

        public RoadGraph(Vector2 origin, float spacing, int count)
        {
            this.origin = origin; this.spacing = spacing; this.count = Mathf.Max(2, count);
        }

        public Vector3 WorldPos(Vector2Int n) => new Vector3(origin.x + n.x * spacing, 0f, origin.y + n.y * spacing);

        public Vector2Int Nearest(Vector3 p)
        {
            int a = Mathf.Clamp(Mathf.RoundToInt((p.x - origin.x) / spacing), 0, count - 1);
            int b = Mathf.Clamp(Mathf.RoundToInt((p.z - origin.y) / spacing), 0, count - 1);
            return new Vector2Int(a, b);
        }

        /// <summary>The neighbouring intersection to walk to next on the way to <paramref name="to"/> (Manhattan route).</summary>
        public Vector2Int StepToward(Vector2Int from, Vector2Int to)
        {
            int dx = to.x - from.x, dz = to.y - from.y;
            if (dx == 0 && dz == 0) return from;
            if (Mathf.Abs(dx) >= Mathf.Abs(dz)) return new Vector2Int(from.x + (int)Mathf.Sign(dx), from.y);
            return new Vector2Int(from.x, from.y + (int)Mathf.Sign(dz));
        }

        static readonly Vector2Int[] Dirs = { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };

        /// <summary>A random adjacent intersection, avoiding the one just left when another choice exists.</summary>
        public Vector2Int RandomNeighbour(Vector2Int from, Vector2Int avoid, System.Random rng)
        {
            var options = new List<Vector2Int>(4);
            foreach (var d in Dirs)
            {
                var n = from + d;
                if (n.x < 0 || n.y < 0 || n.x >= count || n.y >= count) continue;
                options.Add(n);
            }
            if (options.Count > 1) options.Remove(avoid);
            return options[rng.Next(options.Count)];
        }
    }
}
