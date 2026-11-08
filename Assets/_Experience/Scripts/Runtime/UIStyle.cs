using UnityEngine;

namespace Blackout.Experience
{
    /// <summary>
    /// The Blackout UI standard (see Docs/UI-Standard.md). All UI colours, sizes and distances live here
    /// so every track can match the look and the accessibility rules.
    /// </summary>
    public static class UIStyle
    {
        // Placement (VR comfort): world-space panels only, 1.5-2 m away, slightly below eye level.
        public const float PanelDistance = 1.8f;
        public const float PanelDrop = 0.12f;
        public const float WorldScale = 0.0013f;          // 1 canvas pixel = 1.3 mm
        public const float CaptionDistance = 1.6f;
        public const float CaptionDrop = 0.42f;

        // Text: body text must never go below MinFontSize (about 1.6 degrees at 1.8 m).
        public const float MinFontSize = 36f;
        public const float BodyFontSize = 40f;
        public const float ButtonFontSize = 42f;
        public const float TitleFontSize = 80f;

        // Touch targets: at least 80 px tall (about 10 cm at the panel distance).
        public const float ButtonHeight = 88f;

        public static Color Background => GameSettings.HighContrast ? new Color(0f, 0f, 0f, 1f) : new Color(0.04f, 0.04f, 0.05f, 0.94f);
        public static Color Surface => GameSettings.HighContrast ? new Color(0.05f, 0.05f, 0.05f, 1f) : new Color(0.12f, 0.12f, 0.14f, 1f);
        public static Color SurfaceHover => GameSettings.HighContrast ? new Color(0.35f, 0.3f, 0f, 1f) : new Color(0.3f, 0.26f, 0.05f, 1f);
        public static Color Accent => new Color(1f, 0.85f, 0.1f, 1f);
        public static Color Text => Color.white;
        public static Color TextMuted => GameSettings.HighContrast ? Color.white : new Color(0.78f, 0.78f, 0.8f, 1f);
        public static Color CaptionBackground => GameSettings.HighContrast ? Color.black : new Color(0f, 0f, 0f, 0.82f);

        /// <summary>WCAG relative luminance contrast ratio between two colours (used by tests).</summary>
        public static float ContrastRatio(Color a, Color b)
        {
            float la = Luminance(a), lb = Luminance(b);
            float hi = Mathf.Max(la, lb), lo = Mathf.Min(la, lb);
            return (hi + 0.05f) / (lo + 0.05f);
        }

        static float Luminance(Color c)
        {
            static float Lin(float v) => v <= 0.03928f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
            return 0.2126f * Lin(c.r) + 0.7152f * Lin(c.g) + 0.0722f * Lin(c.b);
        }
    }
}
