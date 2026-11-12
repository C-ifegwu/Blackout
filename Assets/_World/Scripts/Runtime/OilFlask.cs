using System.Collections;
using UnityEngine;
using Blackout.Experience;

namespace Blackout.World
{
    /// <summary>An oil flask: walk up to it to refill the lantern.</summary>
    public sealed class OilFlask : RunResettable
    {
        public float amount = 0.35f;
        bool _taken;

        public override void ResetRun() { _taken = false; Show(true); }

        void Update()
        {
            if (_taken || !Playing || !PlayerRig.HasPlayer) return;
            if (PlayerRig.FlatDistance(transform.position) < 1.1f)
            {
                _taken = true; Show(false);
                if (Lantern.Instance != null) Lantern.Instance.AddFuel(amount);
                PlayerRig.Say("Lantern", "Oil. The flame steadies and grows.", 3.5f);
            }
        }
    }
}
