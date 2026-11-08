using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Blackout.Experience
{
    /// <summary>
    /// Hover, focus and gaze-dwell feedback for a UI button. Uses an outline as well as a colour change
    /// so state is never communicated by colour alone.
    /// </summary>
    public sealed class ButtonVisual : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        Image _background;
        Outline _outline;
        bool _hover;
        bool _selected;
        float _dwell;

        public void Init(Image background)
        {
            _background = background;
            _outline = background.gameObject.AddComponent<Outline>();
            _outline.effectDistance = new Vector2(5f, -5f);
            Refresh();
        }

        public void SetDwell(float progress01)
        {
            _dwell = Mathf.Clamp01(progress01);
            Refresh();
        }

        public void Refresh()
        {
            if (_background == null) return;
            bool active = _hover || _selected || _dwell > 0f;
            var baseColor = active ? UIStyle.SurfaceHover : UIStyle.Surface;
            _background.color = _dwell > 0f ? Color.Lerp(baseColor, UIStyle.Accent * 0.75f, _dwell) : baseColor;
            _outline.effectColor = active ? UIStyle.Accent : new Color(1f, 1f, 1f, GameSettings.HighContrast ? 0.9f : 0.18f);
        }

        public void OnPointerEnter(PointerEventData eventData) { _hover = true; UISounds.Play(UISounds.Kind.Hover); Refresh(); }
        public void OnPointerExit(PointerEventData eventData) { _hover = false; Refresh(); }
        public void OnSelect(BaseEventData eventData) { _selected = true; Refresh(); }
        public void OnDeselect(BaseEventData eventData) { _selected = false; Refresh(); }
        void OnDisable() { _hover = false; _selected = false; _dwell = 0f; Refresh(); }
    }
}
