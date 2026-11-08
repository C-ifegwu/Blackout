using System;
using UnityEngine;

namespace Blackout.Experience
{
    /// <summary>
    /// Player-facing accessibility and comfort settings. Single source of truth for the project.
    /// Other tracks only READ these values (Sonar reads ReduceFlashing, World reads Brightness, etc.).
    /// Values persist between sessions through PlayerPrefs.
    /// </summary>
    public static class GameSettings
    {
        const string Prefix = "blackout.settings.";

        public static event Action Changed;

        static bool s_Loaded;
        static bool s_CaptionsOn = true;
        static bool s_HighContrast;
        static bool s_ReduceFlashing = true;
        static bool s_Seated;
        static bool s_OneHanded;
        static bool s_SnapTurn = true;
        static bool s_GazeSelect;
        static float s_Brightness = 1f;
        static float s_CaptionScale = 1f;
        static float s_MasterVolume = 1f;

        public const float MinBrightness = 0.5f, MaxBrightness = 2f;
        public const float MinCaptionScale = 0.75f, MaxCaptionScale = 2f;

        public static bool CaptionsOn { get { EnsureLoaded(); return s_CaptionsOn; } set { SetBool(ref s_CaptionsOn, value, "captions"); } }
        public static bool HighContrast { get { EnsureLoaded(); return s_HighContrast; } set { SetBool(ref s_HighContrast, value, "highContrast"); } }
        public static bool ReduceFlashing { get { EnsureLoaded(); return s_ReduceFlashing; } set { SetBool(ref s_ReduceFlashing, value, "reduceFlashing"); } }
        public static bool Seated { get { EnsureLoaded(); return s_Seated; } set { SetBool(ref s_Seated, value, "seated"); } }
        public static bool OneHanded { get { EnsureLoaded(); return s_OneHanded; } set { SetBool(ref s_OneHanded, value, "oneHanded"); } }
        public static bool SnapTurn { get { EnsureLoaded(); return s_SnapTurn; } set { SetBool(ref s_SnapTurn, value, "snapTurn"); } }
        public static bool GazeSelect { get { EnsureLoaded(); return s_GazeSelect; } set { SetBool(ref s_GazeSelect, value, "gazeSelect"); } }

        public static float Brightness { get { EnsureLoaded(); return s_Brightness; } set { SetFloat(ref s_Brightness, value, "brightness", MinBrightness, MaxBrightness); } }
        public static float CaptionScale { get { EnsureLoaded(); return s_CaptionScale; } set { SetFloat(ref s_CaptionScale, value, "captionScale", MinCaptionScale, MaxCaptionScale); } }
        public static float MasterVolume { get { EnsureLoaded(); return s_MasterVolume; } set { SetFloat(ref s_MasterVolume, value, "masterVolume", 0f, 1f); } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            // Keeps behaviour correct when Enter Play Mode Options skips the domain reload.
            s_Loaded = false;
            Changed = null;
        }

        /// <summary>Restores factory defaults and clears saved values.</summary>
        public static void ResetToDefaults()
        {
            foreach (var key in new[] { "captions", "highContrast", "reduceFlashing", "seated", "oneHanded", "snapTurn", "gazeSelect", "brightness", "captionScale", "masterVolume" })
                PlayerPrefs.DeleteKey(Prefix + key);
            PlayerPrefs.Save();
            s_Loaded = false;
            EnsureLoaded();
            Changed?.Invoke();
        }

        /// <summary>Forces a reload from PlayerPrefs (used by tests).</summary>
        public static void Reload()
        {
            s_Loaded = false;
            EnsureLoaded();
        }

        static void EnsureLoaded()
        {
            if (s_Loaded) return;
            s_Loaded = true;
            s_CaptionsOn = GetBool("captions", true);
            s_HighContrast = GetBool("highContrast", false);
            s_ReduceFlashing = GetBool("reduceFlashing", true);
            s_Seated = GetBool("seated", false);
            s_OneHanded = GetBool("oneHanded", false);
            s_SnapTurn = GetBool("snapTurn", true);
            s_GazeSelect = GetBool("gazeSelect", false);
            s_Brightness = Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "brightness", 1f), MinBrightness, MaxBrightness);
            s_CaptionScale = Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "captionScale", 1f), MinCaptionScale, MaxCaptionScale);
            s_MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "masterVolume", 1f));
        }

        static bool GetBool(string key, bool fallback) => PlayerPrefs.GetInt(Prefix + key, fallback ? 1 : 0) != 0;

        static void SetBool(ref bool field, bool value, string key)
        {
            EnsureLoaded();
            if (field == value) return;
            field = value;
            PlayerPrefs.SetInt(Prefix + key, value ? 1 : 0);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        static void SetFloat(ref float field, float value, string key, float min, float max)
        {
            EnsureLoaded();
            value = Mathf.Clamp(value, min, max);
            if (Mathf.Approximately(field, value)) return;
            field = value;
            PlayerPrefs.SetFloat(Prefix + key, value);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
