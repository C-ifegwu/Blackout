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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            State = GameState.Boot;
            StateChanged = null;
            Delivered = null;
            RunStarted = null;
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
            Delivered?.Invoke();
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
