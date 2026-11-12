using System.Collections;
using UnityEngine;
using Blackout.Experience;

namespace Blackout.World
{
    /// <summary>One of three coloured braziers. Lit in the right order they help open the seal.</summary>
    public sealed class Brazier : MonoBehaviour
    {
        public string colorName = "Cyan";
        public Color color = Color.cyan;
        public Renderer flame;
        public Light flameLight;
        public bool Lit { get; private set; }
        [HideInInspector] public bool inside;

        Material _mat;

        void Awake()
        {
            if (flame != null) _mat = flame.material;
            Set(false);
        }

        public void Set(bool lit)
        {
            Lit = lit;
            if (_mat != null)
            {
                _mat.EnableKeyword("_EMISSION");
                _mat.SetColor("_EmissionColor", color * (lit ? 3f : 0.25f));
            }
            if (flameLight != null)
            {
                flameLight.color = color;
                flameLight.enabled = lit;
            }
        }
    }
}
