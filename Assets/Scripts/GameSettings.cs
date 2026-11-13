using System;
using UnityEngine;

public enum SettingKey { ReduceFlashing, HighContrast, Seated, OneHanded, CaptionsOn, Teleport, SmoothTurn, Vignette, DawnMode, Hud }

public static class GameSettings
{
    public static event Action Changed;

    private static readonly bool[] defaults = { true, false, false, false, true, false, false, true, false, true };
    private static bool[] flags;
    private static float brightness = 0.5f;
    private static int monsterLevel = 2;

    public static bool ReduceFlashing => Get(SettingKey.ReduceFlashing);
    public static bool HighContrast => Get(SettingKey.HighContrast);
    public static bool Seated => Get(SettingKey.Seated);
    public static bool OneHanded => Get(SettingKey.OneHanded);
    public static bool CaptionsOn => Get(SettingKey.CaptionsOn);
    public static bool Teleport => Get(SettingKey.Teleport);
    public static bool SmoothTurn => Get(SettingKey.SmoothTurn);
    public static bool Vignette => Get(SettingKey.Vignette);
    public static bool DawnMode => Get(SettingKey.DawnMode);

    public static float Brightness
    {
        get
        {
            Load();
            return brightness;
        }
    }

    public static int MonsterLevel
    {
        get
        {
            Load();
            return monsterLevel;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Changed = null;
        flags = null;
    }

    public static bool Get(SettingKey key)
    {
        Load();
        return flags[(int)key];
    }

    public static void Toggle(SettingKey key)
    {
        Load();
        flags[(int)key] = !flags[(int)key];
        PlayerPrefs.SetInt("blackout." + key, flags[(int)key] ? 1 : 0);
        Save();
    }

    public static void SetBrightness(float value)
    {
        Load();
        brightness = Mathf.Clamp01(Mathf.Round(value * 10f) / 10f);
        PlayerPrefs.SetFloat("blackout.Brightness", brightness);
        Save();
    }

    public static void CycleMonsters()
    {
        Load();
        monsterLevel = (monsterLevel + 2) % 3;
        PlayerPrefs.SetInt("blackout.Monsters", monsterLevel);
        Save();
    }

    private static void Save()
    {
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    private static void Load()
    {
        if (flags != null)
        {
            return;
        }
        flags = new bool[defaults.Length];
        for (int i = 0; i < flags.Length; i++)
        {
            flags[i] = PlayerPrefs.GetInt("blackout." + (SettingKey)i, defaults[i] ? 1 : 0) == 1;
        }
        brightness = PlayerPrefs.GetFloat("blackout.Brightness", 0.5f);
        monsterLevel = PlayerPrefs.GetInt("blackout.Monsters", 2);
    }
}
