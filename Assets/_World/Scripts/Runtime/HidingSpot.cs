using System.Collections;
using UnityEngine;
using Blackout.Experience;

namespace Blackout.World
{
    /// <summary>A stone alcove for hiding. Stand inside and stay still or cover the lantern: monsters lose you.</summary>
    public sealed class HidingSpot : RunResettable
    {
        public float radius = 1.5f;
        static int _hiddenFrame = -10;
        bool _inside, _told;

        /// <summary>Multiplier on monster sight range: 1 normally, small while hidden.</summary>
        public static float Concealment => _hiddenFrame >= Time.frameCount - 2 ? 0.12f : 1f;

        public override void ResetRun() { _inside = false; _told = false; }

        void Update()
        {
            if (!Playing || !PlayerRig.HasPlayer) { _inside = false; return; }
            bool inside = PlayerRig.FlatDistance(transform.position) < radius;
            if (inside)
            {
                bool still = MonsterDirector.Instance != null && MonsterDirector.Instance.PlayerStill;
                bool covered = Lantern.Instance != null && Lantern.Instance.Covered;
                if (still || covered) _hiddenFrame = Time.frameCount;
                if (!_inside && !_told)
                {
                    _told = true;
                    PlayerRig.Say("Tip", "A good hiding place. Cover the lantern (grip) and stay still.", 4.5f);
                }
            }
            _inside = inside;
        }
    }
}
