using System.Collections;
using UnityEngine;
using Blackout.Experience;

namespace Blackout.World
{
    /// <summary>A thin wire across the street. Touch it and a bell rings, calling monsters to the spot.</summary>
    public sealed class Tripwire : RunResettable
    {
        public float halfLength = 3f;
        public float noiseRadius = 34f;
        bool _fired;

        public override void ResetRun() { _fired = false; Show(true); }

        void Update()
        {
            if (_fired || !Playing || !PlayerRig.HasPlayer) return;
            var c = transform.position;
            var axis = transform.right; axis.y = 0f; axis.Normalize();
            var p = PlayerRig.Position;
            var d = new Vector3(p.x - c.x, 0f, p.z - c.z);
            float along = Mathf.Clamp(Vector3.Dot(d, axis), -halfLength, halfLength);
            float dist = (d - axis * along).magnitude;
            if (dist < 0.35f)
            {
                _fired = true;
                Show(false);
                if (MonsterDirector.Instance != null) MonsterDirector.Instance.NoiseAt(c, noiseRadius);
                PlayerRig.Say("Trap", "You hit a wire. A bell clangs through the streets!", 4f);
            }
        }
    }
}
