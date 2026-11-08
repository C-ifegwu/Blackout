using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;

namespace Blackout.Experience
{
    /// <summary>
    /// Applies the comfort and accessibility settings to the scene: volume, ambient light (brightness),
    /// seated tracking mode, and snap versus smooth turning.
    /// </summary>
    public sealed class ComfortApplier : MonoBehaviour
    {
        public const float SeatedEyeHeight = 1.2f;
        const float BaseGlow = 0.02f;

        /// <summary>Set false if the World track wants full control over ambient lighting.</summary>
        public static bool ControlAmbient = true;

        XROrigin _origin;
        float _defaultYOffset;
        XROrigin.TrackingOriginMode _defaultMode;
        bool _cached;

        void OnEnable() => GameSettings.Changed += Apply;
        void OnDisable() => GameSettings.Changed -= Apply;

        void Start()
        {
            _origin = FindFirstObjectByType<XROrigin>();
            if (_origin != null)
            {
                _defaultYOffset = _origin.CameraYOffset;
                _defaultMode = _origin.RequestedTrackingOriginMode;
                _cached = true;
            }
            Apply();
        }

        public void Apply()
        {
            AudioListener.volume = GameSettings.MasterVolume;

            if (ControlAmbient)
            {
                float lift = Mathf.Max(0f, GameSettings.Brightness - 1f) * 0.2f;
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = Color.white * (BaseGlow * GameSettings.Brightness + lift);
            }

            if (_origin != null && _cached)
            {
                if (GameSettings.Seated)
                {
                    _origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;
                    _origin.CameraYOffset = SeatedEyeHeight;
                }
                else
                {
                    _origin.RequestedTrackingOriginMode = _defaultMode;
                    _origin.CameraYOffset = _defaultYOffset;
                }
            }

            foreach (var snap in FindObjectsByType<SnapTurnProvider>(FindObjectsSortMode.None)) snap.enabled = GameSettings.SnapTurn;
            foreach (var smooth in FindObjectsByType<ContinuousTurnProvider>(FindObjectsSortMode.None)) smooth.enabled = !GameSettings.SnapTurn;
        }
    }
}
