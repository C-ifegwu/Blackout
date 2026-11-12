using System.Collections;
using UnityEngine;
using Blackout.Experience;

namespace Blackout.World
{
    /// <summary>The key, three plaque clues, three braziers and the seal. The brazier order is random every run.</summary>
    public sealed class PuzzleChain : MonoBehaviour
    {
        public Transform key;
        public Plaque[] plaques = new Plaque[0];
        public Brazier[] braziers = new Brazier[0];
        public SealDoor seal;

        public bool HasKey { get; private set; }
        public bool BraziersDone { get; private set; }
        public bool Solved => HasKey && BraziersDone;

        int[] _order = { 0, 1, 2 };
        int _lit, _wrong;
        bool _sealTold;

        void OnEnable() { GameFlow.RunStarted += ResetRun; }
        void OnDisable() { GameFlow.RunStarted -= ResetRun; }
        void Start() { ResetRun(); }

        public Vector3 BrazierCentre
        {
            get
            {
                if (braziers.Length == 0) return transform.position;
                Vector3 s = Vector3.zero;
                foreach (var b in braziers) s += b.transform.position;
                return s / braziers.Length;
            }
        }

        public Vector3 NextTarget(Vector3 fallback)
        {
            if (!HasKey && key != null) return key.position;
            if (!BraziersDone) return BrazierCentre;
            return fallback;
        }

        public string NextName()
        {
            if (!HasKey) return "the brass key";
            if (!BraziersDone) return "the three braziers";
            return "the tower seal";
        }

        public string SealHint()
        {
            if (!HasKey && !BraziersDone) return "The seal is shut. It needs a brass key and three flames lit in order.";
            if (!HasKey) return "The seal is shut. It still needs the brass key.";
            return "The seal is shut. The three braziers must be lit in the right order.";
        }

        public void ResetRun()
        {
            // random order, every run
            var o = new[] { 0, 1, 2 };
            for (int i = 2; i > 0; i--) { int j = Random.Range(0, i + 1); int t = o[i]; o[i] = o[j]; o[j] = t; }
            _order = o;
            HasKey = false; BraziersDone = false; _lit = 0; _wrong = 0; _sealTold = false;
            if (key != null) SetVisible(key, true);
            foreach (var b in braziers) { if (b != null) { b.Set(false); b.inside = false; } }
            if (seal != null) seal.SetOpen(false);
            string[] words = { "first", "second", "last" };
            for (int i = 0; i < plaques.Length && i < 3; i++)
            {
                string c = ColourOf(_order[i]);
                plaques[i].text = i == 0 ? "Carved in the stone: the " + c + " flame wakes " + words[i] + "."
                                : i == 1 ? "Carved in the stone: then the " + c + " flame, " + words[i] + "."
                                : "Carved in the stone: the " + c + " flame comes " + words[i] + ".";
                plaques[i].lastShown = -99f;
            }
        }

        string ColourOf(int brazier) => braziers.Length > brazier && braziers[brazier] != null ? braziers[brazier].colorName : "?";

        static void SetVisible(Transform t, bool v)
        {
            foreach (var r in t.GetComponentsInChildren<Renderer>(true)) r.enabled = v;
            foreach (var l in t.GetComponentsInChildren<Light>(true)) l.enabled = v;
        }

        void Update()
        {
            if (GameFlow.State != GameState.Playing || !PlayerRig.HasPlayer) return;

            // plaques
            for (int i = 0; i < plaques.Length; i++)
            {
                var p = plaques[i];
                if (p == null || string.IsNullOrEmpty(p.text)) continue;
                if (PlayerRig.FlatDistance(p.transform.position) < 3.2f && Time.time > p.lastShown + 30f)
                {
                    p.lastShown = Time.time;
                    PlayerRig.Say("Plaque " + (i + 1), p.text, 6f);
                }
            }

            // key
            if (!HasKey && key != null && PlayerRig.FlatDistance(key.position) < 1.4f)
            {
                HasKey = true;
                SetVisible(key, false);
                PlayerRig.Say("Key", "A brass key. The seal at the tower will need it.", 4f);
            }

            // braziers
            if (!BraziersDone)
            {
                for (int i = 0; i < braziers.Length; i++)
                {
                    var b = braziers[i];
                    if (b == null) continue;
                    bool near = PlayerRig.FlatDistance(b.transform.position) < 1.5f;
                    if (near && !b.inside && !b.Lit)
                    {
                        if (i == _order[_lit]) { b.Set(true); _lit++; PlayerRig.Say("Flame", b.colorName + " flame lit.", 2.5f); }
                        else Wrong();
                    }
                    b.inside = near;
                }
                if (_lit >= 3)
                {
                    BraziersDone = true;
                    PlayerRig.Say("Flame", "All three flames burn.", 3f);
                }
            }

            if (Solved && !_sealTold)
            {
                _sealTold = true;
                if (seal != null) seal.SetOpen(true);
                PlayerRig.Say("Seal", "The seal unlocks. Reach the tower and send your message!", 5f);
            }
        }

        void Wrong()
        {
            _wrong++;
            _lit = 0;
            foreach (var b in braziers) if (b != null) b.Set(false);
            if (MonsterDirector.Instance != null && braziers.Length > 0)
                MonsterDirector.Instance.NoiseAt(BrazierCentre, 26f);
            PlayerRig.Say("Flame", "The flames gutter out. Wrong order. Something heard that.", 4f);
            if (_wrong >= 2)
                PlayerRig.Say("You recall", "The order was " + ColourOf(_order[0]) + ", " + ColourOf(_order[1]) + ", then " + ColourOf(_order[2]) + ".", 7f);
        }
    }
}
