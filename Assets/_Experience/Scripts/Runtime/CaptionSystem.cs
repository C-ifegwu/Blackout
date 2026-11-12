using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Blackout.Experience
{
    /// <summary>
    /// Shows a caption for every spoken or important sound. Track 3 owns captions.csv (Resources/captions)
    /// and calls CaptionSystem.Instance.Show("vendor_01"). Captions use unscaled time so they work while paused.
    /// </summary>
    public sealed class CaptionSystem : MonoBehaviour
    {
        public const float BaseFontSize = 38f;

        public static CaptionSystem Instance { get; private set; }

        TMP_Text _label;
        GameObject _root;
        Dictionary<string, CaptionEntry> _table = new Dictionary<string, CaptionEntry>();
        Coroutine _routine;
        string _currentText = string.Empty;

        public bool IsShowing => _routine != null;
        public string CurrentText => _currentText;
        public int EntryCount => _table.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        void Awake()
        {
            Instance = this;
            Reload();
            GameSettings.Changed += OnSettingsChanged;
        }

        void OnDestroy()
        {
            GameSettings.Changed -= OnSettingsChanged;
            if (Instance == this) Instance = null;
        }

        /// <summary>Connects the on-screen label. Called by ExperienceUI.</summary>
        public void Bind(TMP_Text label, GameObject root)
        {
            _label = label;
            _root = root;
            if (_root != null) _root.SetActive(false);
            ApplyScale();
        }

        public void Reload()
        {
            var asset = Resources.Load<TextAsset>("captions");
            _table = asset != null ? CaptionTable.Parse(asset.text) : new Dictionary<string, CaptionEntry>();
            if (asset == null) Debug.LogWarning("[Blackout] captions.csv not found in a Resources folder. Captions by id will not show.");
        }

        public bool Show(string id)
        {
            if (!_table.TryGetValue(id, out var entry))
            {
                Debug.LogWarning($"[Blackout] Caption id '{id}' not found in captions.csv.");
                return false;
            }
            ShowText(entry.Speaker, entry.Text, entry.Seconds);
            return true;
        }

        public void ShowText(string speaker, string text, float seconds)
        {
            if (!GameSettings.CaptionsOn || _label == null) return;
            // Long enough to read: about two words a second, plus a little extra time.
            int words = string.IsNullOrEmpty(text) ? 0 : text.Split(' ').Length;
            seconds = Mathf.Max(seconds * 1.5f + 1f, 2.5f + words * 0.5f);
            if (_routine != null) StopCoroutine(_routine);
            _currentText = string.IsNullOrEmpty(speaker) ? text : $"{speaker}: {text}";
            _routine = StartCoroutine(Display(_currentText, Mathf.Max(1f, seconds)));
        }

        public void Clear()
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = null;
            _currentText = string.Empty;
            if (_root != null) _root.SetActive(false);
        }

        IEnumerator Display(string text, float seconds)
        {
            ApplyScale();
            _label.text = text;
            if (_root != null) _root.SetActive(true);
            yield return new WaitForSecondsRealtime(seconds);
            if (_root != null) _root.SetActive(false);
            _currentText = string.Empty;
            _routine = null;
        }

        void OnSettingsChanged()
        {
            if (!GameSettings.CaptionsOn) Clear();
            ApplyScale();
        }

        void ApplyScale()
        {
            if (_label != null) _label.fontSize = BaseFontSize * GameSettings.CaptionScale;
        }
    }
}
