using UnityEngine;

namespace Blackout.Experience
{
    /// <summary>
    /// Short UI feedback sounds (Kenney Interface Sounds, CC0). Plays even while the game is paused.
    /// Sounds are quiet and non-startling by design.
    /// </summary>
    public static class UISounds
    {
        public enum Kind { Hover, Click, Back, Confirm }

        static AudioSource _source;
        static AudioClip[] _clips;
        const float Volume = 0.5f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _source = null; _clips = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            GameFlow.Delivered -= OnDelivered;
            GameFlow.Delivered += OnDelivered;
        }

        static void OnDelivered() { Play(Kind.Confirm); }

        public static void Play(Kind kind)
        {
            if (!Ensure()) return;
            var clip = _clips[(int)kind];
            if (clip != null) _source.PlayOneShot(clip, Volume);
        }

        static bool Ensure()
        {
            if (_source != null) return _clips != null;
            var go = new GameObject("UISounds");
            Object.DontDestroyOnLoad(go);
            _source = go.AddComponent<AudioSource>();
            _source.spatialBlend = 0f;
            _source.playOnAwake = false;
            _source.ignoreListenerPause = true;
            _clips = new[]
            {
                Resources.Load<AudioClip>("UISounds/hover"),
                Resources.Load<AudioClip>("UISounds/click"),
                Resources.Load<AudioClip>("UISounds/back"),
                Resources.Load<AudioClip>("UISounds/confirm"),
            };
            return true;
        }
    }
}
