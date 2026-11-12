using System.Collections;
using UnityEngine;
using Blackout.Experience;

namespace Blackout.World
{
    /// <summary>Common behaviour for things that reset at the start of each run.</summary>
    public abstract class RunResettable : MonoBehaviour
    {
        protected virtual void OnEnable() { GameFlow.RunStarted += ResetRun; }
        protected virtual void OnDisable() { GameFlow.RunStarted -= ResetRun; }
        public abstract void ResetRun();
        protected bool Playing => GameFlow.State == GameState.Playing;

        protected void Show(bool visible)
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
            foreach (var l in GetComponentsInChildren<Light>(true)) l.enabled = visible;
        }
    }
}
