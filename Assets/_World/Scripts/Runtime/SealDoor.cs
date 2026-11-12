using System.Collections;
using UnityEngine;
using Blackout.Experience;

namespace Blackout.World
{
    /// <summary>The sealed door at the tower. Glows red until the key and the braziers have opened it.</summary>
    public sealed class SealDoor : MonoBehaviour
    {
        public Renderer glow;
        Material _mat;
        void Awake() { if (glow != null) _mat = glow.material; SetOpen(false); }

        public void SetOpen(bool open)
        {
            if (_mat == null) return;
            _mat.EnableKeyword("_EMISSION");
            _mat.SetColor("_EmissionColor", open ? new Color(0.2f, 1f, 0.5f) * 2f : new Color(1f, 0.1f, 0.08f) * 1.2f);
        }
    }
}
