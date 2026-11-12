using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

public class HudController : MonoBehaviour
{
    public static readonly List<Button> Buttons = new List<Button>();
    public static Material Overlay;
    public static Material OverlayFont;
    public static Color Panel => GameSettings.HighContrast ? new Color(0f, 0f, 0f, 0.98f) : new Color(0.03f, 0.05f, 0.08f, 0.92f);
    public static Color Ink => GameSettings.HighContrast ? Color.white : new Color(0.9f, 0.94f, 1f, 1f);
    public static Color Accent => GameSettings.HighContrast ? new Color(1f, 0.86f, 0f, 1f) : new Color(0.3f, 0.8f, 1f, 1f);
    private static HudController instance;

    [SerializeField] private Transform head;
    [SerializeField] private Material overlay;
    [SerializeField] private Material overlayFont;
    [SerializeField] private AudioSource ui;
    [SerializeField] private AudioClip click;
    [SerializeField] private AudioClip hover;

    private Image captionBack, promptBack;
    private TextMeshProUGUI caption, prompt;
    private string speaker = "", words = "";
    private Transform speakerAt;
    private float hideAt;

    private void Awake()
    {
        instance = this;
        Overlay = overlay;
        OverlayFont = overlayFont;
        ui.ignoreListenerPause = true;
        RectTransform root = Canvas(transform, "Hud", new Vector2(1100f, 500f), 10, false);
        captionBack = Box(root, new Vector2(0f, -110f), new Vector2(1060f, 190f), Panel);
        caption = Label(captionBack.rectTransform, "", 38, Vector2.zero, new Vector2(1010f, 170f), TextAlignmentOptions.Center, Ink);
        promptBack = Box(root, new Vector2(0f, 150f), new Vector2(1000f, 90f), Panel);
        prompt = Label(promptBack.rectTransform, "", 34, Vector2.zero, new Vector2(960f, 80f), TextAlignmentOptions.Center, new Color(1f, 0.66f, 0.1f));
        captionBack.gameObject.SetActive(false);
        promptBack.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        GameFlow.CaptionShown += OnCaption;
        GameFlow.PromptChanged += OnPrompt;
    }

    private void OnDisable()
    {
        GameFlow.CaptionShown -= OnCaption;
        GameFlow.PromptChanged -= OnPrompt;
    }

    private void LateUpdate()
    {
        Vector3 target = head.position + head.forward * 1.6f - head.up * 0.42f;
        bool far = Vector3.Distance(transform.position, target) > 2.5f;
        transform.position = far ? target : Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-4f * Time.unscaledDeltaTime));
        transform.rotation = Quaternion.LookRotation(transform.position - head.position, Vector3.up);
        if (captionBack.gameObject.activeSelf)
        {
            captionBack.gameObject.SetActive(Time.unscaledTime < hideAt && GameSettings.CaptionsOn);
            string where = speakerAt != null ? "  <color=#9FB3C8>(" + GameFlow.Describe(head, speakerAt.position) + ")</color>" : "";
            caption.text = speaker.Length > 0 ? "<color=#4CC9F0><b>" + speaker + "</b></color>" + where + "\n" + words : words + where;
        }
    }

    private void OnCaption(string who, string text, Transform from, float duration)
    {
        speaker = who;
        words = text;
        speakerAt = from;
        hideAt = Time.unscaledTime + duration + 0.6f;
        captionBack.gameObject.SetActive(GameSettings.CaptionsOn);
    }

    private void OnPrompt(string text)
    {
        promptBack.gameObject.SetActive(!string.IsNullOrEmpty(text));
        prompt.text = text;
    }

    public static RectTransform Canvas(Transform parent, string name, Vector2 size, int order, bool background)
    {
        GameObject root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(TrackedDeviceGraphicRaycaster));
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.sizeDelta = size;
        rect.localScale = Vector3.one * 0.001f;
        root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        root.GetComponent<Canvas>().sortingOrder = order;
        if (background)
        {
            Box(rect, Vector2.zero, size, Panel).gameObject.AddComponent<Outline>().effectColor = Accent;
        }
        return rect;
    }

    public static Image Box(RectTransform parent, Vector2 position, Vector2 size, Color color)
    {
        Image image = new GameObject("Box", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        image.rectTransform.SetParent(parent, false);
        image.rectTransform.anchoredPosition = position;
        image.rectTransform.sizeDelta = size;
        image.color = color;
        image.material = Overlay;
        return image;
    }

    public static TextMeshProUGUI Label(RectTransform parent, string text, float size, Vector2 position, Vector2 box, TextAlignmentOptions align, Color color)
    {
        TextMeshProUGUI label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        label.rectTransform.SetParent(parent, false);
        label.rectTransform.anchoredPosition = position;
        label.rectTransform.sizeDelta = box;
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = align;
        label.fontSharedMaterial = OverlayFont;
        label.raycastTarget = false;
        return label;
    }

    public static void Button(RectTransform parent, string text, Vector2 position, Vector2 size, UnityAction onClick)
    {
        Image back = Box(parent, position, size, Color.white);
        back.gameObject.AddComponent<Outline>().effectColor = Accent;
        Button button = back.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = GameSettings.HighContrast ? new Color(0.12f, 0.12f, 0.12f) : new Color(0.1f, 0.16f, 0.24f);
        colors.highlightedColor = Accent;
        colors.selectedColor = colors.normalColor;
        button.colors = colors;
        Label(back.rectTransform, "<b>" + text + "</b>", 40, Vector2.zero, size, TextAlignmentOptions.Center, Ink);
        button.onClick.AddListener(() => instance.ui.PlayOneShot(instance.click, 0.6f));
        button.onClick.AddListener(onClick);
        EventTrigger trigger = back.gameObject.AddComponent<EventTrigger>();
        EventTrigger.Entry enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(data => instance.ui.PlayOneShot(instance.hover, 0.25f));
        trigger.triggers.Add(enter);
        Buttons.Add(button);
    }
}
