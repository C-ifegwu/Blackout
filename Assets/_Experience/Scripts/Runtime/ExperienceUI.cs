using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Blackout.Experience
{
    public enum MenuPanel { None, Health, Main, Settings, Pause, End }

    /// <summary>
    /// Builds and drives every menu, the settings screen and the caption bar, all as world-space UI that follows
    /// the Blackout UI standard (see UIStyle). Reacts to GameFlow state changes.
    /// </summary>
    public sealed class ExperienceUI : MonoBehaviour
    {
        const float MenuWidth = 1000f;
        const float MenuHeight = 780f;

        Canvas _menu;
        Canvas _captionCanvas;
        Image _menuBackground;
        Image _captionBackground;
        MenuPanel _current = MenuPanel.None;
        MenuPanel _settingsReturn = MenuPanel.Main;
        InputAction _pauseAction;

        readonly Dictionary<MenuPanel, RectTransform> _panels = new Dictionary<MenuPanel, RectTransform>();
        readonly List<(Button button, MenuPanel panel)> _buttons = new List<(Button, MenuPanel)>();
        readonly List<ButtonVisual> _visuals = new List<ButtonVisual>();
        readonly List<TextMeshProUGUI> _mutedTexts = new List<TextMeshProUGUI>();
        readonly List<Action> _refreshers = new List<Action>();

        public MenuPanel Current => _current;
        public bool MenuVisible => _menu != null && _menu.gameObject.activeSelf;
        public bool CaptionVisible => _captionCanvas != null && CaptionSystem.Instance != null && CaptionSystem.Instance.IsShowing;

        void Awake()
        {
            BuildMenu();
            BuildCaptionBar();

            GameSettings.Changed += OnSettingsChanged;
            GameFlow.StateChanged += OnStateChanged;

            _pauseAction = new InputAction("Pause", InputActionType.Button);
            _pauseAction.AddBinding("<Keyboard>/escape");
            _pauseAction.AddBinding("<Gamepad>/start");
            _pauseAction.AddBinding("<XRController>{LeftHand}/{Menu}");
            _pauseAction.performed += _ => TogglePause();
            _pauseAction.Enable();
        }

        void OnDestroy()
        {
            GameSettings.Changed -= OnSettingsChanged;
            GameFlow.StateChanged -= OnStateChanged;
            if (_pauseAction != null) { _pauseAction.Disable(); _pauseAction.Dispose(); }
        }

        // ---------------------------------------------------------------- state

        void OnStateChanged(GameState state)
        {
            switch (state)
            {
                case GameState.HealthNotice: Show(MenuPanel.Health); break;
                case GameState.MainMenu: Show(MenuPanel.Main); break;
                case GameState.Paused: Show(MenuPanel.Pause); break;
                case GameState.Ended: Show(MenuPanel.End); break;
                case GameState.Playing: Hide(); break;
            }
        }

        static void TogglePause()
        {
            if (GameFlow.State == GameState.Playing) GameFlow.Pause();
            else if (GameFlow.State == GameState.Paused) GameFlow.Resume();
        }

        public void Show(MenuPanel panel)
        {
            _current = panel;
            foreach (var kv in _panels) kv.Value.gameObject.SetActive(kv.Key == panel);
            _menu.gameObject.SetActive(true);
            PlaceMenu();
            RestyleAll();
            RefreshAll();
        }

        public void Hide()
        {
            _current = MenuPanel.None;
            _menu.gameObject.SetActive(false);
        }

        public IEnumerable<Button> ActiveButtons()
        {
            if (!MenuVisible) yield break;
            foreach (var (button, panel) in _buttons)
                if (panel == _current && button != null && button.isActiveAndEnabled && button.interactable) yield return button;
        }

        // ---------------------------------------------------------------- build

        void BuildMenu()
        {
            _menu = UIFactory.CreateWorldCanvas("MenuCanvas", transform, new Vector2(MenuWidth, MenuHeight));
            _menuBackground = UIFactory.Panel("Background", _menu.transform, UIStyle.Background);
            UIFactory.Stretch(_menuBackground.rectTransform);

            var health = NewPanel(MenuPanel.Health, "Before you start");
            AddBody(health, "Blackout uses darkness, sound and brief light effects. Take a break if you feel dizzy, uncomfortable or unwell. " +
                            "Flashing is limited by default and every comfort option is in Settings.", 230f);
            AddButton(health, MenuPanel.Health, "I understand", GameFlow.AcceptHealthNotice);
            AddButton(health, MenuPanel.Health, "Settings", () => OpenSettings(MenuPanel.Health));

            var main = NewPanel(MenuPanel.Main, "BLACKOUT");
            AddBody(main, "A city you hear before you see.", 70f, muted: true);
            AddButton(main, MenuPanel.Main, "Start", GameFlow.StartRun);
            AddButton(main, MenuPanel.Main, "Settings", () => OpenSettings(MenuPanel.Main));
            AddButton(main, MenuPanel.Main, "Quit", QuitApp);

            var pause = NewPanel(MenuPanel.Pause, "Paused");
            AddButton(pause, MenuPanel.Pause, "Resume", GameFlow.Resume);
            AddButton(pause, MenuPanel.Pause, "Settings", () => OpenSettings(MenuPanel.Pause));
            AddButton(pause, MenuPanel.Pause, "Main menu", GameFlow.ReturnToMenu);

            var end = NewPanel(MenuPanel.End, "Message delivered");
            AddBody(end, "The lights are back. Thank you for listening.", 100f);
            AddButton(end, MenuPanel.End, "Play again", GameFlow.StartRun);
            AddButton(end, MenuPanel.End, "Main menu", GameFlow.ReturnToMenu);

            BuildSettingsPanel();
            _menu.gameObject.SetActive(false);
        }

        RectTransform NewPanel(MenuPanel id, string title)
        {
            var rt = UIFactory.Rect(id + "Panel", _menu.transform);
            UIFactory.Stretch(rt);
            var layout = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(56, 56, 44, 44);
            layout.spacing = 18f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var t = UIFactory.Label("Title", rt, title, UIStyle.TitleFontSize, TextAlignmentOptions.Center, UIStyle.Accent, FontStyles.Bold);
            UIFactory.Height(t.gameObject, 112f);
            _panels[id] = rt;
            rt.gameObject.SetActive(false);
            return rt;
        }

        void AddBody(RectTransform panel, string text, float height, bool muted = false)
        {
            var t = UIFactory.Label("Body", panel, text, UIStyle.BodyFontSize, TextAlignmentOptions.Center,
                muted ? UIStyle.TextMuted : UIStyle.Text);
            UIFactory.Height(t.gameObject, height);
            if (muted) _mutedTexts.Add(t);
        }

        Button AddButton(Transform parent, MenuPanel panel, string label, Action onClick)
        {
            var b = UIFactory.MakeButton(label + "Button", parent, label, onClick, out _, out var visual);
            UIFactory.Height(b.gameObject, UIStyle.ButtonHeight);
            _buttons.Add((b, panel));
            _visuals.Add(visual);
            return b;
        }

        void BuildSettingsPanel()
        {
            var panel = NewPanel(MenuPanel.Settings, "Settings");
            var gridRt = UIFactory.Rect("Grid", panel);
            var grid = gridRt.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(430f, 76f);
            grid.spacing = new Vector2(16f, 12f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            UIFactory.Height(gridRt.gameObject, 5 * 76f + 4 * 12f);

            AddToggle(gridRt, "Captions", () => GameSettings.CaptionsOn, v => GameSettings.CaptionsOn = v);
            AddToggle(gridRt, "High contrast", () => GameSettings.HighContrast, v => GameSettings.HighContrast = v);
            AddToggle(gridRt, "Reduce flashing", () => GameSettings.ReduceFlashing, v => GameSettings.ReduceFlashing = v);
            AddToggle(gridRt, "Seated mode", () => GameSettings.Seated, v => GameSettings.Seated = v);
            AddToggle(gridRt, "One-handed", () => GameSettings.OneHanded, v => GameSettings.OneHanded = v);
            AddToggle(gridRt, "Snap turn", () => GameSettings.SnapTurn, v => GameSettings.SnapTurn = v);
            AddToggle(gridRt, "Gaze select", () => GameSettings.GazeSelect, v => GameSettings.GazeSelect = v);
            AddStepper(gridRt, "Bright", () => GameSettings.Brightness, v => GameSettings.Brightness = v, 0.25f, 1f);
            AddStepper(gridRt, "Text", () => GameSettings.CaptionScale, v => GameSettings.CaptionScale = v, 0.25f, 1f);
            AddStepper(gridRt, "Volume", () => GameSettings.MasterVolume, v => GameSettings.MasterVolume = v, 0.1f, 1f);

            AddButton(panel, MenuPanel.Settings, "Back", () => Show(_settingsReturn));
        }

        void AddToggle(Transform parent, string name, Func<bool> get, Action<bool> set)
        {
            var b = UIFactory.MakeButton(name + "Toggle", parent, name, () => set(!get()), out var text, out var visual, UIStyle.MinFontSize);
            _buttons.Add((b, MenuPanel.Settings));
            _visuals.Add(visual);
            void Refresh() => text.text = $"{name}: {(get() ? "ON" : "OFF")}";
            _refreshers.Add(Refresh);
            Refresh();
        }

        void AddStepper(Transform parent, string name, Func<float> get, Action<float> set, float step, float shown)
        {
            var cell = UIFactory.Rect(name + "Stepper", parent);
            var row = cell.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 8f;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = true;

            var minus = UIFactory.MakeButton("Minus", cell, "-", () => set(get() - step), out _, out var v1);
            var labelBg = UIFactory.Panel("Value", cell, new Color(0f, 0f, 0f, 0f));
            var value = UIFactory.Label("Text", labelBg.transform, name, UIStyle.MinFontSize, TextAlignmentOptions.Center, UIStyle.Text, FontStyles.Bold);
            UIFactory.Stretch(value.rectTransform);
            var plus = UIFactory.MakeButton("Plus", cell, "+", () => set(get() + step), out _, out var v2);

            SetWidth(minus.gameObject, 76f);
            SetWidth(plus.gameObject, 76f);
            var le = labelBg.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;

            _buttons.Add((minus, MenuPanel.Settings));
            _buttons.Add((plus, MenuPanel.Settings));
            _visuals.Add(v1);
            _visuals.Add(v2);
            void Refresh() => value.text = $"{name} {Mathf.RoundToInt(get() * 100f / shown)}%";
            _refreshers.Add(Refresh);
            Refresh();
        }

        static void SetWidth(GameObject go, float width)
        {
            if (!go.TryGetComponent<LayoutElement>(out var le)) le = go.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.minWidth = width;
        }

        void BuildCaptionBar()
        {
            _captionCanvas = UIFactory.CreateWorldCanvas("CaptionCanvas", transform, new Vector2(1000f, 170f));
            var root = UIFactory.Rect("CaptionRoot", _captionCanvas.transform);
            UIFactory.Stretch(root);
            _captionBackground = root.gameObject.AddComponent<Image>();
            _captionBackground.color = UIStyle.CaptionBackground;
            _captionBackground.raycastTarget = false;
            var text = UIFactory.Label("CaptionText", root, string.Empty, CaptionSystem.BaseFontSize,
                TextAlignmentOptions.Center, UIStyle.Text, FontStyles.Bold);
            UIFactory.Stretch(text.rectTransform);
            text.margin = new Vector4(28f, 12f, 28f, 12f);

            var captions = GetComponent<CaptionSystem>();
            if (captions != null) captions.Bind(text, root.gameObject);
        }

        // ---------------------------------------------------------------- placement and style

        void PlaceMenu()
        {
            var cam = Camera.main;
            if (cam == null) return;
            _menu.worldCamera = cam;
            var forward = cam.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();
            var position = cam.transform.position + forward * UIStyle.PanelDistance;
            position.y = cam.transform.position.y - UIStyle.PanelDrop;
            _menu.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, Vector3.up));
        }

        void LateUpdate()
        {
            // Captions follow the head lazily (never rigidly locked) so they stay readable without causing discomfort.
            var cam = Camera.main;
            if (cam == null || _captionCanvas == null) return;
            _captionCanvas.worldCamera = cam;
            var target = cam.transform.TransformPoint(new Vector3(0f, -UIStyle.CaptionDrop, UIStyle.CaptionDistance));
            float t = 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime);
            var ct = _captionCanvas.transform;
            ct.position = Vector3.Lerp(ct.position, target, t);
            var look = ct.position - cam.transform.position;
            if (look.sqrMagnitude > 0.0001f)
                ct.rotation = Quaternion.Slerp(ct.rotation, Quaternion.LookRotation(look.normalized, Vector3.up), t);
        }

        void OnSettingsChanged()
        {
            RestyleAll();
            RefreshAll();
        }

        void RestyleAll()
        {
            if (_menuBackground != null) _menuBackground.color = UIStyle.Background;
            if (_captionBackground != null) _captionBackground.color = UIStyle.CaptionBackground;
            foreach (var v in _visuals) if (v != null) v.Refresh();
            foreach (var t in _mutedTexts) if (t != null) t.color = UIStyle.TextMuted;
        }

        void RefreshAll()
        {
            foreach (var r in _refreshers) r();
        }

        void OpenSettings(MenuPanel from)
        {
            _settingsReturn = from;
            Show(MenuPanel.Settings);
        }

        static void QuitApp()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
