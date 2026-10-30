using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace UltraAudioDescriptionMaker.Core
{
    /// <summary>How a description fits its moment, after the voices are prepared (or estimated before).</summary>
    public enum FitStatus
    {
        /// <summary>Not checked yet.</summary>
        Unknown,
        /// <summary>Starts at its moment and ends before the next speech or description.</summary>
        Ok,
        /// <summary>Fits, but is spoken a little faster.</summary>
        SpedUp,
        /// <summary>Its moment is in the middle of dialogue or singing: heard over it (the sound goes down).</summary>
        OverDialogue,
        /// <summary>Starts in a pause but is still being spoken when someone starts talking.</summary>
        IntoDialogue,
        /// <summary>Still being spoken when the next description is due, so the next one starts late.</summary>
        IntoNext,
        /// <summary>Starts later than its moment, because the previous description was still being spoken.</summary>
        Late
    }

    /// <summary>One spoken description.</summary>
    public class AdLine
    {
        /// <summary>The moment the described thing appears or happens (seconds from the start). The description starts exactly here.</summary>
        public double Time;
        public string Text = "";

        // ── filled while the voices are prepared (not saved) ──
        [JsonIgnore] public string AudioPath;
        [JsonIgnore] public string AudioKey;
        [JsonIgnore] public double Duration;
        /// <summary>When it is actually heard (= Time, unless the previous one was still being spoken).</summary>
        [JsonIgnore] public double PlacedAt;
        /// <summary>The voice speed used (percent).</summary>
        [JsonIgnore] public int RatePercent;
        /// <summary>Free time from the moment to the next speech or description.</summary>
        [JsonIgnore] public double Window;
        /// <summary>Seconds it runs into the next speech/description, or how late it starts.</summary>
        [JsonIgnore] public double Overrun;
        [JsonIgnore] public FitStatus Status;
        /// <summary>True when the status comes from a word count, not from the real voice.</summary>
        [JsonIgnore] public bool Estimated;

        /// <summary>Clears the voice when the text or the time changed.</summary>
        public void Invalidate()
        {
            AudioPath = null; AudioKey = null; Duration = 0; PlacedAt = Time;
            Status = FitStatus.Unknown; Overrun = 0; Window = 0; RatePercent = 0;
        }

        public bool HasProblem => Status is FitStatus.OverDialogue or FitStatus.IntoDialogue or FitStatus.IntoNext or FitStatus.Late;
    }

    /// <summary>A stretch of time (dialogue or singing).</summary>
    public class Span
    {
        public double From;
        public double To;
    }

    /// <summary>A description project: the video and its descriptions (*.uad, JSON).</summary>
    public class AdProject
    {
        public const string Extension = ".uad";
        public int FormatVersion = 1;
        public string VideoPath;
        /// <summary>The web address the video came from (Open link), or null.</summary>
        public string SourceUrl;
        /// <summary>The video path relative to the project file, used when the folder moved.</summary>
        public string VideoRelative;
        public double Duration;
        public string Language = "sr";
        public string Voice;
        public List<AdLine> Lines = new();
        /// <summary>Where someone speaks or sings (from the AI); descriptions should end before these.</summary>
        public List<Span> Speech = new();
        /// <summary>Shot changes in the video (seconds).</summary>
        public List<double> Cuts = new();
        public string Notes;

        [JsonIgnore] public string FilePath;
        [JsonIgnore] public bool Dirty;

        public string Title => string.IsNullOrEmpty(VideoPath) ? "" : Path.GetFileNameWithoutExtension(VideoPath);

        public void Sort() => Lines = Lines.OrderBy(l => l.Time).ToList();

        public void Save(string path)
        {
            Sort();
            try { VideoRelative = Path.GetRelativePath(Path.GetDirectoryName(Path.GetFullPath(path)) ?? "", VideoPath ?? ""); }
            catch { VideoRelative = null; }
            File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
            FilePath = path;
            Dirty = false;
        }

        public static AdProject Load(string path)
        {
            var p = JsonConvert.DeserializeObject<AdProject>(File.ReadAllText(path)) ?? new AdProject();
            p.FilePath = path;
            p.Lines ??= new(); p.Speech ??= new(); p.Cuts ??= new();
            if (!File.Exists(p.VideoPath ?? "") && !string.IsNullOrEmpty(p.VideoRelative))
            {
                string guess = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path) ?? "", p.VideoRelative));
                if (File.Exists(guess)) p.VideoPath = guess;
            }
            foreach (var l in p.Lines) l.Invalidate();
            p.Sort();
            return p;
        }

        /// <summary>Is the moment inside dialogue or singing? Returns the stretch, or null.</summary>
        public Span SpeechAt(double t) => Speech.FirstOrDefault(s => t >= s.From && t < s.To);

        /// <summary>The next speech that begins after <paramref name="t"/>.</summary>
        public Span NextSpeechAfter(double t) => Speech.Where(s => s.From > t).OrderBy(s => s.From).FirstOrDefault();
    }

    /// <summary>Times as the program writes and reads them: "mm:ss.f" (minutes may pass 59: "95:20.4").</summary>
    public static class TimeText
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Format(double s)
        {
            s = Math.Max(0, s);
            int min = (int)(s / 60);
            double sec = Math.Round(s - min * 60, 1);
            if (sec >= 60) { min++; sec -= 60; }
            return $"{min:00}:{sec.ToString("00.0", Inv)}";
        }

        /// <summary>Spoken form for the screen reader: "1 min 5.4 s".</summary>
        public static string Spoken(double s, string lang)
        {
            s = Math.Max(0, s);
            int h = (int)(s / 3600), m = (int)(s % 3600 / 60);
            double sec = Math.Round(s % 60, 1);
            string ss = sec.ToString("0.#", lang == "en" ? Inv : CultureInfo.GetCultureInfo("sr-Latn-RS"));
            string hs = h > 0 ? $"{h} h " : "";
            return m > 0 || h > 0 ? $"{hs}{m} min {ss} s" : $"{ss} s";
        }

        /// <summary>Reads "mm:ss", "mm:ss.f", "h:mm:ss.f", or plain seconds ("75.5"). Null when it is not a time.</summary>
        public static double? Parse(string text)
        {
            text = (text ?? "").Trim().Replace(',', '.');
            if (text.Length == 0) return null;
            var m = Regex.Match(text, @"^(?:(\d{1,2}):)?(\d{1,3}):(\d{1,2}(?:\.\d+)?)$");
            if (m.Success)
            {
                double h = m.Groups[1].Success ? int.Parse(m.Groups[1].Value, Inv) : 0;
                return h * 3600 + int.Parse(m.Groups[2].Value, Inv) * 60 + double.Parse(m.Groups[3].Value, Inv);
            }
            if (double.TryParse(text, NumberStyles.Float, Inv, out double secs) && secs >= 0) return secs;
            return null;
        }

        public static string Vtt(double s) => TimeSpan.FromSeconds(Math.Max(0, s)).ToString(@"hh\:mm\:ss\.fff", Inv);
        public static string Srt(double s) => TimeSpan.FromSeconds(Math.Max(0, s)).ToString(@"hh\:mm\:ss\,fff", Inv);
    }

    /// <summary>The script as plain text — "mm:ss.f | description" per line — for editing all at once.</summary>
    public static class ScriptText
    {
        public static string Write(IEnumerable<AdLine> lines) =>
            string.Join(Environment.NewLine, lines.OrderBy(l => l.Time).Select(l => $"{TimeText.Format(l.Time)} | {l.Text}"));

        /// <summary>Reads the edited text back. A line without a time continues the previous description.</summary>
        public static List<AdLine> Read(string script)
        {
            var lines = new List<AdLine>();
            foreach (string raw in (script ?? "").Replace("\r", "").Split('\n'))
            {
                string s = raw.Trim();
                if (s.Length == 0) continue;
                var m = Regex.Match(s, @"^([\d:.,]+)\s*[|\-–]\s*(.*)$");
                double? t = m.Success ? TimeText.Parse(m.Groups[1].Value) : null;
                if (t != null)
                {
                    if (m.Groups[2].Value.Trim().Length > 0)
                        lines.Add(new AdLine { Time = t.Value, Text = m.Groups[2].Value.Trim() });
                }
                else if (lines.Count > 0) lines[^1].Text += " " + s;
            }
            return lines.OrderBy(l => l.Time).ToList();
        }
    }
}
