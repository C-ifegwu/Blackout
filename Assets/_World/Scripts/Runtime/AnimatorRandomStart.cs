using UnityEngine;

namespace Blackout.World
{
    /// <summary>Starts a looping idle at a random point so a crowd of townspeople does not move in lockstep.</summary>
    [RequireComponent(typeof(Animator))]
    public sealed class AnimatorRandomStart : MonoBehaviour
    {
        void Start()
        {
            var a = GetComponent<Animator>();
            if (a == null || a.runtimeAnimatorController == null) return;
            a.Play(0, 0, Random.value);
            a.speed = Random.Range(0.9f, 1.1f);
        }
    }
}
