using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace UltraAudioDescriptionMaker.Core
{
    /// <summary>Reads a description script someone already has: our text format, WebVTT or SRT.</summary>
    public static class ScriptImport
    {
        public static List<AdLine> Read(string path)
        {
            string text = File.ReadAllText(path);
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext is ".vtt" or ".srt" || text.Contains("-->")) return Cues(text);
            return ScriptText.Read(text);
        }

        /// <summary>WebVTT / SRT cues: the start time of each cue is the moment of the description.</summary>
        private static List<AdLine> Cues(string text)
        {
            var lines = new List<AdLine>();
            var blocks = Regex.Split(text.Replace("\r", ""), @"\n\s*\n");
            foreach (string block in blocks)
            {
                var rows = block.Split('\n').Select(r => r.Trim()).Where(r => r.Length > 0).ToList();
                int arrow = rows.FindIndex(r => r.Contains("-->"));
                if (arrow < 0) continue;
                string start = rows[arrow].Split("-->")[0].Trim().Replace(',', '.');
                double? t = TimeText.Parse(start);
                if (t == null) continue;
                string body = string.Join(" ", rows.Skip(arrow + 1));
                body = Regex.Replace(body, "<[^>]+>", "").Trim();   // VTT tags
                if (body.Length > 0) lines.Add(new AdLine { Time = System.Math.Round(t.Value, 1), Text = body, PlacedAt = t.Value });
            }
            return lines.OrderBy(l => l.Time).ToList();
        }
    }
}
