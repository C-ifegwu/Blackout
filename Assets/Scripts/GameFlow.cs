using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

public enum FlowState { Notice, Title, Playing, Paused, Delivering, Ending }

public enum RunResult { None, Delivered, Dawn, Caught }

public static class GameFlow
{
    public static event Action<FlowState> StateChanged;
    public static event Action<string, string, Transform, float> CaptionShown;
    public static event Action<string> PromptChanged;

    public static FlowState State { get; private set; }
    public static RunResult Result { get; private set; }
    public static float StartTime { get; private set; }
    public static float EndTime { get; private set; }
    public static bool IsPlaying => State == FlowState.Playing;

    private static readonly Dictionary<string, string[]> lines = new Dictionary<string, string[]>();
    private static bool noticeSeen;
    private static bool skipTitle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        StateChanged = null;
        CaptionShown = null;
        PromptChanged = null;
        State = FlowState.Notice;
        noticeSeen = false;
        skipTitle = false;
    }

    public static void LoadLines(TextAsset file)
    {
        lines.Clear();
        foreach (string row in file.text.Split('\n'))
        {
            string[] cells = row.TrimEnd('\r').Split(new[] { ',' }, 3);
            if (cells.Length == 3)
            {
                lines[cells[0]] = new[] { cells[1], cells[2].Trim('"').Replace("\\n", "\n") };
            }
        }
    }

    public static string Line(string id) => lines.ContainsKey(id) ? lines[id][1] : id;

    public static float Caption(string id, Transform source, float duration)
    {
        string[] line = lines.ContainsKey(id) ? lines[id] : new[] { "", id };
        float shown = duration > 0f ? duration : 1.5f + line[1].Length * 0.06f;
        CaptionShown?.Invoke(line[0], line[1], source, shown);
        return shown;
    }

    public static void Prompt(string text) => PromptChanged?.Invoke(text);

    public static string Describe(Transform head, Vector3 target)
    {
        Vector3 to = target - head.position;
        float angle = Vector3.SignedAngle(Vector3.ProjectOnPlane(head.forward, Vector3.up), Vector3.ProjectOnPlane(to, Vector3.up), Vector3.up);
        string side = Mathf.Abs(angle) > 140f ? "behind you" : (angle < -35f ? "to your left" : (angle > 35f ? "to your right" : "ahead"));
        return (to.magnitude < 1.8f ? "right beside you" : side) + ", " + Mathf.Max(1, Mathf.RoundToInt(to.magnitude)) + " m";
    }

    public static void Haptic(float amplitude, float duration)
    {
        if (XRSettings.isDeviceActive)
        {
            InputDevices.GetDeviceAtXRNode(XRNode.LeftHand).SendHapticImpulse(0u, amplitude, duration);
            InputDevices.GetDeviceAtXRNode(XRNode.RightHand).SendHapticImpulse(0u, amplitude, duration);
        }
    }

    public static void Begin()
    {
        FlowState first = noticeSeen ? (skipTitle ? FlowState.Playing : FlowState.Title) : FlowState.Notice;
        skipTitle = false;
        StartTime = Time.time;
        Result = RunResult.None;
        SetState(first, true);
    }

    public static void AcceptNotice()
    {
        noticeSeen = true;
        SetState(FlowState.Title, false);
    }

    public static void StartGame()
    {
        StartTime = Time.time;
        SetState(FlowState.Playing, false);
    }

    public static void Pause() => SetState(State == FlowState.Playing ? FlowState.Paused : State, false);

    public static void Resume() => SetState(State == FlowState.Paused ? FlowState.Playing : State, false);

    public static void BeginDelivery() => SetState(FlowState.Delivering, false);

    public static void Finish(RunResult result)
    {
        if (result == RunResult.None || State == FlowState.Ending || (result != RunResult.Delivered && State != FlowState.Playing))
        {
            return;
        }
        Result = result;
        EndTime = Time.time;
        SetState(FlowState.Ending, false);
    }

    public static void Restart(bool playNow)
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        skipTitle = playNow;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private static void SetState(FlowState next, bool force)
    {
        if (State == next && !force)
        {
            return;
        }
        State = next;
        StateChanged?.Invoke(next);
    }
}
