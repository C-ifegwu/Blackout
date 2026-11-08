using UnityEngine;
using UnityEngine.UI;

namespace Blackout.Experience
{
    /// <summary>
    /// Alternative to controller selection for players who cannot press buttons: looking at a button for
    /// DwellSeconds activates it. Enabled by GameSettings.GazeSelect. Progress is shown on the button.
    /// </summary>
    public sealed class GazeDwellSelector : MonoBehaviour
    {
        public float DwellSeconds = 1.5f;

        ExperienceUI _ui;
        Button _target;
        ButtonVisual _targetVisual;
        float _elapsed;

        void Awake() => _ui = GetComponent<ExperienceUI>();

        void Update()
        {
            var cam = Camera.main;
            if (!GameSettings.GazeSelect || _ui == null || cam == null || !_ui.MenuVisible)
            {
                ResetTarget();
                return;
            }

            var ray = new Ray(cam.transform.position, cam.transform.forward);
            Button hit = null;
            foreach (var b in _ui.ActiveButtons())
            {
                var rt = (RectTransform)b.transform;
                var plane = new Plane(-rt.forward, rt.position); // canvas forward points away from the viewer
                if (!plane.Raycast(ray, out float distance)) continue;
                var local = rt.InverseTransformPoint(ray.GetPoint(distance));
                if (rt.rect.Contains(local)) { hit = b; break; }
            }

            if (hit != _target)
            {
                ResetTarget();
                _target = hit;
                if (_target != null) _targetVisual = _target.GetComponent<ButtonVisual>();
            }

            if (_target == null) return;

            _elapsed += Time.unscaledDeltaTime;
            if (_targetVisual != null) _targetVisual.SetDwell(_elapsed / DwellSeconds);
            if (_elapsed >= DwellSeconds)
            {
                var button = _target;
                ResetTarget();
                button.onClick.Invoke();
            }
        }

        void ResetTarget()
        {
            if (_targetVisual != null) _targetVisual.SetDwell(0f);
            _target = null;
            _targetVisual = null;
            _elapsed = 0f;
        }
    }
}
