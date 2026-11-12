using System.Collections;
using UnityEngine;
using Blackout.Experience;

namespace Blackout.World
{
    /// <summary>A hole in the road. Falling in fades to black and puts you back at the last checkpoint with less oil.</summary>
    public sealed class Pit : RunResettable
    {
        public float radius = 1.0f;
        bool _busy;

        public override void ResetRun() { _busy = false; }

        void Update()
        {
            if (_busy || !Playing || !PlayerRig.HasPlayer) return;
            if (PlayerRig.FlatDistance(transform.position) < radius) StartCoroutine(Fall());
        }

        IEnumerator Fall()
        {
            _busy = true;
            PlayerRig.Say("Trap", "You fall into a pit!", 3f);
            var f = ScreenFader.Instance;
            if (f != null) yield return f.FadeTo(1f, 0.6f);
            PlayerRig.Teleport(PlayerRig.CheckpointPos, PlayerRig.CheckpointRot);
            if (Lantern.Instance != null) Lantern.Instance.AddFuel(-0.06f);
            yield return new WaitForSecondsRealtime(0.4f);
            if (f != null) yield return f.FadeTo(0f, 1.0f);
            PlayerRig.Say("Tip", "Pits show as a dark ring on the ground. Watch the floor.", 4f);
            yield return new WaitForSecondsRealtime(1.5f);
            _busy = false;
        }
    }
}
