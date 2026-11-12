using UnityEngine;

namespace Blackout.World
{
    /// <summary>
    /// A relay post along the route. Only the current relay is audible: the player follows its tone, reaches it
    /// and pings to switch it on. An activated relay glows steadily.
    /// </summary>
    public sealed class RelayBeacon : MonoBehaviour
    {
        public AudioSource tone;
        public Renderer glow;
        public Color color = new Color(0.35f, 0.95f, 1f);

        public bool Activated { get; private set; }
        public bool IsCurrent { get; private set; }

        Material _mat;
        float _level;
        float _target;

        void Awake()
        {
            if (glow != null) _mat = glow.material;   // instance, so each relay glows on its own
            Apply(0.12f);
            _level = _target = 0.12f;
        }

        public void ResetState()
        {
            Activated = false;
            IsCurrent = false;
            _target = 0.12f;
            _level = 0.12f;
            if (tone != null) tone.Stop();
            Apply(_level);
        }

        public void SetCurrent(bool current)
        {
            IsCurrent = current;
            if (tone == null) return;
            if (current)
            {
                tone.loop = true;
                tone.volume = 1f;
                if (!tone.isPlaying) tone.Play();
            }
            else if (!Activated) tone.Stop();
        }

        public void Activate()
        {
            Activated = true;
            IsCurrent = false;
            _target = 1f;
            if (tone != null) tone.Stop();
        }

        void Update()
        {
            if (Mathf.Approximately(_level, _target)) return;
            _level = Mathf.MoveTowards(_level, _target, Time.unscaledDeltaTime * 0.5f);   // gradual, never a flash
            Apply(_level);
        }

        void Apply(float level)
        {
            if (_mat == null) return;
            _mat.SetColor("_EmissionColor", color * (0.15f + level * 2.6f));
        }
    }
}
