using System.Collections;
using UnityEngine;
using Blackout.Experience;

namespace Blackout.World
{
    /// <summary>A stone plaque beside a relay. Walk up to read its clue about the brazier order.</summary>
    public sealed class Plaque : MonoBehaviour
    {
        [HideInInspector] public string text = "";
        [HideInInspector] public float lastShown = -99f;
    }
}
