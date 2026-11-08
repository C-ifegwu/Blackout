using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Blackout.Experience
{
    /// <summary>Small helpers that build world-space UI to the Blackout UI standard.</summary>
    internal static class UIFactory
    {
        public static Canvas CreateWorldCanvas(string name, Transform parent, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = size;
            rt.localScale = Vector3.one * UIStyle.WorldScale;
            go.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 3f; // crisp text in a headset
            go.AddComponent<TrackedDeviceGraphicRaycaster>();           // lets XR controller rays hit the UI
            return canvas;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public static Image Panel(string name, Transform parent, Color color)
        {
            var image = Rect(name, parent).gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        public static TextMeshProUGUI Label(string name, Transform parent, string text, float size,
            TextAlignmentOptions align, Color color, FontStyles style = FontStyles.Normal)
        {
            var t = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = Mathf.Max(size, UIStyle.MinFontSize);
            t.alignment = align;
            t.color = color;
            t.fontStyle = style;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static void Height(GameObject go, float height)
        {
            if (!go.TryGetComponent<LayoutElement>(out var le)) le = go.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight = height;
        }

        public static Button MakeButton(string name, Transform parent, string label, Action onClick,
            out TextMeshProUGUI text, out ButtonVisual visual, float fontSize = UIStyle.ButtonFontSize)
        {
            var bg = Panel(name, parent, UIStyle.Surface);
            var button = bg.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            button.transition = Selectable.Transition.None; // ButtonVisual draws the states
            var nav = button.navigation; nav.mode = Navigation.Mode.Automatic; button.navigation = nav;
            visual = bg.gameObject.AddComponent<ButtonVisual>();
            visual.Init(bg);

            text = Label("Label", bg.transform, label, fontSize, TextAlignmentOptions.Center, UIStyle.Text, FontStyles.Bold);
            Stretch(text.rectTransform);
            text.margin = new Vector4(12f, 4f, 12f, 4f);
            if (onClick != null) button.onClick.AddListener(() => onClick());
            return button;
        }
    }
}
