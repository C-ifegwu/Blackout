using System;
using UnityEngine;

namespace Blackout.Experience
{
    public enum GameState { Boot, HealthNotice, MainMenu, Playing, Paused, Ended }

    /// <summary>
    /// Small state machine for the overall experience. Track 2 (Sonar) calls GameFlow.OnDelivered()
    /// when the message is delivered. Track 3 (World) can listen to GameFlow.Delivered to trigger the power-on finale.
    /// </summary>
    public static class GameFlow
    {
        public static GameState State { get; private set; } = GameState.Boot;

        public static event Action<GameState> StateChanged;
        public static event Action Delivered;
        public static event Action RunStarted;
        public static event Action TimedOut;
        public static event Action Caught;

        const string BestKey = "blackout.bestSeconds";
        static float s_RunStart;

        /// <summary>True when the last run ended because the Beat the dawn countdown ran out.</summary>
        public static bool LastRunFailed { get; private set; }
        /// <summary>True when the last run ended because a monster caught the player.</summary>
        public static bool LastRunCaught { get; private set; }
        /// <summary>Seconds of play in the last finished run (pauses excluded).</summary>
        public static float LastRunSeconds { get; private set; }
        /// <summary>Fastest successful run on this device, or 0 if none yet.</summary>
        public static float BestSeconds { get { float v = PlayerPrefs.GetFloat(BestKey, 0f); return v >= 60f ? v : 0f; } }
        /// <summary>True if the last successful run set a new best time.</summary>
        public static bool LastRunWasBest { get; private set; }

        public static string FormatTime(float seconds)
        {
            int t = Mathf.Max(0, Mathf.RoundToInt(seconds));
            return (t / 60) + ":" + (t % 60).ToString("00");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            State = GameState.Boot;
            StateChanged = null;
            Delivered = null;
            RunStarted = null;
            TimedOut = null;
            Caught = null;
            Time.timeScale = 1f;
        }

        public static void Begin() => Set(GameState.HealthNotice);

        public static void AcceptHealthNotice()
        {
            if (State == GameState.HealthNotice) Set(GameState.MainMenu);
        }

        public static void StartRun()
        {
            if (State != GameState.MainMenu && State != GameState.Ended) return;
            LastRunFailed = false; LastRunCaught = false; LastRunWasBest = false;
            s_RunStart = Time.time;
            Set(GameState.Playing);
            RunStarted?.Invoke();
        }

        public static void Pause()
        {
            if (State == GameState.Playing) Set(GameState.Paused);
        }

        public static void Resume()
        {
            if (State == GameState.Paused) Set(GameState.Playing);
        }

        public static void ReturnToMenu()
        {
            if (State == GameState.Paused || State == GameState.Ended) Set(GameState.MainMenu);
        }

        /// <summary>Called by gameplay when the voice message reaches its recipient.</summary>
        public static void OnDelivered()
        {
            if (State != GameState.Playing) return;
            LastRunFailed = false; LastRunCaught = false;
            LastRunSeconds = Time.time - s_RunStart;
            float best = BestSeconds;
            // Runs under a minute are developer shortcuts (F10), never a real time.
            LastRunWasBest = LastRunSeconds >= 60f && (best <= 0f || LastRunSeconds < best);
            if (LastRunWasBest) { PlayerPrefs.SetFloat(BestKey, LastRunSeconds); PlayerPrefs.Save(); }
            Delivered?.Invoke();
            Set(GameState.Ended);
        }

        /// <summary>Called by gameplay when the Beat the dawn countdown reaches zero.</summary>
        public static void OnTimeUp()
        {
            if (State != GameState.Playing) return;
            LastRunFailed = true; LastRunCaught = false;
            LastRunSeconds = Time.time - s_RunStart;
            TimedOut?.Invoke();
            Set(GameState.Ended);
        }

        /// <summary>Called by gameplay when a monster reaches the player. A gentle fail: no flash, just the end screen.</summary>
        public static void OnCaught()
        {
            if (State != GameState.Playing) return;
            LastRunFailed = true; LastRunCaught = true;
            LastRunSeconds = Time.time - s_RunStart;
            Caught?.Invoke();
            Set(GameState.Ended);
        }

        static void Set(GameState next)
        {
            if (State == next) return;
            State = next;
            Time.timeScale = next == GameState.Paused ? 0f : 1f;
            AudioListener.pause = next == GameState.Paused;
            StateChanged?.Invoke(next);
        }
    }
}
