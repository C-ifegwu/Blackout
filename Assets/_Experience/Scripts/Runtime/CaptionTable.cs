using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Blackout.Experience
{
    public readonly struct CaptionEntry
    {
        public readonly string Id;
        public readonly string Speaker;
        public readonly string Text;
        public readonly float Seconds;

        public CaptionEntry(string id, string speaker, string text, float seconds)
        {
            Id = id; Speaker = speaker; Text = text; Seconds = seconds;
        }
    }

    /// <summary>
    /// Parses captions.csv: id,speaker,text,seconds. Quoted fields and "" escapes are supported.
    /// Lines starting with # and blank lines are ignored. Missing or invalid seconds are estimated from word count.
    /// </summary>
    public static class CaptionTable
    {
        public static Dictionary<string, CaptionEntry> Parse(string csv)
        {
            var table = new Dictionary<string, CaptionEntry>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(csv)) return table;

            foreach (var rawLine in csv.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line[0] == '#') continue;

                var cells = SplitLine(line);
                if (cells.Count < 3) continue;
                var id = cells[0].Trim();
                if (id.Length == 0 || id.Equals("id", StringComparison.OrdinalIgnoreCase)) continue;

                var speaker = cells[1].Trim();
                var text = cells[2].Trim();
                float seconds = 0f;
                if (cells.Count > 3) float.TryParse(cells[3].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out seconds);
                if (seconds <= 0f) seconds = EstimateSeconds(text);

                table[id] = new CaptionEntry(id, speaker, text, seconds);
            }
            return table;
        }

        public static float EstimateSeconds(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 2f;
            var words = text.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
            return Math.Max(2f, words * 0.4f + 1f);
        }

        static List<string> SplitLine(string line)
        {
            var cells = new List<string>();
            var sb = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (quoted)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                        else quoted = false;
                    }
                    else sb.Append(c);
                }
                else if (c == '"') quoted = true;
                else if (c == ',') { cells.Add(sb.ToString()); sb.Clear(); }
                else sb.Append(c);
            }
            cells.Add(sb.ToString());
            return cells;
        }
    }
}
