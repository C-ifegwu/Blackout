using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class MenuController : MonoBehaviour
{
    public static bool Open { get; private set; }

    [SerializeField] private Transform head;
    [SerializeField] private TextAsset captions;
    [SerializeField] private float distance = 1.8f;

    private static readonly SettingKey[] toggles = { SettingKey.HighContrast, SettingKey.ReduceFlashing, SettingKey.CaptionsOn, SettingKey.Seated, SettingKey.OneHanded, SettingKey.Teleport, SettingKey.SmoothTurn, SettingKey.Vignette, SettingKey.Hud };
    private static readonly string[] rows = { "High contrast|On|Off", "Reduce flashing|On|Off", "Captions|On|Off", "Seated mode|On|Off", "One-handed mode|On|Off", "Movement|Teleport|Smooth walk", "Turning|Smooth|Snap", "Comfort vignette|On|Off", "Caption panel (H)|On|Off" };
    private static readonly string[] monsters = { "Monsters: Off", "Monsters: Gentle", "Monsters: Normal" };

    private RectTransform panel;
    private bool settingsOpen;

    private void Awake() => GameFlow.LoadLines(captions);

    private void OnEnable()
    {
        GameFlow.StateChanged += OnState;
        GameSettings.Changed += Show;
        ControllerInput.PausePressed += OnPause;
    }

    private void OnDisable()
    {
        GameFlow.StateChanged -= OnState;
        GameSettings.Changed -= Show;
        ControllerInput.PausePressed -= OnPause;
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    private void Start() => GameFlow.Begin();

    private void OnState(FlowState state)
    {
        Time.timeScale = state == FlowState.Paused ? 0f : 1f;
        AudioListener.pause = state == FlowState.Paused;
        settingsOpen = false;
        Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        forward = forward.sqrMagnitude < 0.01f ? Vector3.forward : forward.normalized;
        transform.SetPositionAndRotation(head.position + forward * distance + Vector3.down * 0.1f, Quaternion.LookRotation(forward));
        Show();
    }

    private void OnPause()
    {
        if (settingsOpen)
        {
            Settings(false);
        }
        else if (GameFlow.IsPlaying)
        {
            GameFlow.Pause();
        }
        else
        {
            GameFlow.Resume();
        }
    }

    private void Settings(bool open)
    {
        settingsOpen = open;
        Show();
    }

    private void Show()
    {
        if (panel != null)
        {
            Destroy(panel.gameObject);
        }
        HudController.Buttons.Clear();
        panel = null;
        FlowState state = GameFlow.State;
        string controls = "\n\n" + GameFlow.Line(PlayerRig.XRMode ? "MenuControlsVR" : "MenuControlsDesktop");
        UnityAction openSettings = () => Settings(true);
        if (settingsOpen)
        {
            panel = SettingsPanel();
        }
        else if (state == FlowState.Notice)
        {
            panel = Frame("BLACKOUT", GameFlow.Line("MenuNotice"), 860f, new[] { "I understand", "Settings" }, new[] { (UnityAction)GameFlow.AcceptNotice, openSettings });
        }
        else if (state == FlowState.Title)
        {
            string mode = GameSettings.DawnMode ? "Mode: Beat the dawn" : "Mode: Story";
            panel = Frame("BLACKOUT", GameFlow.Line("MenuTitle") + controls, 960f, new[] { "Start", mode, monsters[GameSettings.MonsterLevel], "Settings" }, new UnityAction[] { GameFlow.StartGame, () => GameSettings.Toggle(SettingKey.DawnMode), GameSettings.CycleMonsters, openSettings });
        }
        else if (state == FlowState.Paused)
        {
            panel = Frame("Paused", GameFlow.Line("MenuPaused") + controls, 760f, new[] { "Resume", "Settings", "Restart" }, new UnityAction[] { GameFlow.Resume, openSettings, () => GameFlow.Restart(true) });
        }
        else if (state == FlowState.Ending)
        {
            float seconds = GameFlow.EndTime - GameFlow.StartTime;
            string time = "\n\nTime " + Mathf.FloorToInt(seconds / 60f) + ":" + Mathf.FloorToInt(seconds % 60f).ToString("00") + ", " + SonarSystem.Pings + " pings.";
            panel = Frame(GameFlow.Line("Title" + GameFlow.Result), GameFlow.Line("Menu" + GameFlow.Result) + time, 640f, new[] { "Play again", "Title screen" }, new UnityAction[] { () => GameFlow.Restart(true), () => GameFlow.Restart(false) });
        }
        Open = panel != null;
    }

    private RectTransform Frame(string title, string body, float height, string[] labels, UnityAction[] clicks)
    {
        RectTransform root = HudController.Canvas(transform, title, new Vector2(1400f, height), 20, true);
        float top = height * 0.5f;
        HudController.Label(root, "<b>" + title.ToUpperInvariant() + "</b>", 72, new Vector2(0f, top - 80f), new Vector2(1300f, 100f), TextAlignmentOptions.Center, HudController.Accent);
        HudController.Label(root, body, 36, new Vector2(0f, top - 150f - (height - 330f) * 0.5f), new Vector2(1280f, height - 330f), TextAlignmentOptions.TopLeft, HudController.Ink);
        for (int i = 0; i < labels.Length; i++)
        {
            HudController.Button(root, labels[i], new Vector2((i - (labels.Length - 1) * 0.5f) * 330f, -top + 85f), new Vector2(310f, 100f), clicks[i]);
        }
        return root;
    }

    private RectTransform SettingsPanel()
    {
        RectTransform root = HudController.Canvas(transform, "Settings", new Vector2(1300f, 1250f), 20, true);
        HudController.Label(root, "<b>SETTINGS</b>", 70, new Vector2(0f, 545f), new Vector2(1200f, 90f), TextAlignmentOptions.Center, HudController.Accent);
        HudController.Label(root, "Brightness  " + Mathf.RoundToInt(GameSettings.Brightness * 10f) + " / 10", 42, new Vector2(-140f, 400f), new Vector2(840f, 76f), TextAlignmentOptions.Left, HudController.Ink);
        HudController.Button(root, "-", new Vector2(370f, 400f), new Vector2(110f, 76f), () => GameSettings.SetBrightness(GameSettings.Brightness - 0.1f));
        HudController.Button(root, "+", new Vector2(500f, 400f), new Vector2(110f, 76f), () => GameSettings.SetBrightness(GameSettings.Brightness + 0.1f));
        for (int i = 0; i < toggles.Length; i++)
        {
            SettingKey key = toggles[i];
            string[] row = rows[i].Split('|');
            HudController.Label(root, row[0], 42, new Vector2(-280f, 312f - i * 88f), new Vector2(560f, 76f), TextAlignmentOptions.Left, HudController.Ink);
            HudController.Button(root, GameSettings.Get(key) ? row[1] : row[2], new Vector2(320f, 312f - i * 88f), new Vector2(450f, 76f), () => GameSettings.Toggle(key));
        }
        HudController.Button(root, "Back", new Vector2(0f, -560f), new Vector2(360f, 100f), () => Settings(false));
        return root;
    }
}
