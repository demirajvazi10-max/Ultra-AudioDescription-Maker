using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UltraAudioDescriptionMaker.Core;

namespace UltraAudioDescriptionMaker.Ai
{
    /// <summary>How many descriptions the AI writes.</summary>
    public enum Density { Essential, Normal, Detailed }

    /// <summary>What the AI wrote: the descriptions, and where people speak or sing.</summary>
    public class ScriptResult
    {
        public List<AdLine> Lines = new();
        public List<Span> Speech = new();
        public List<double> Cuts = new();
    }

    /// <summary>
    /// Writes the description script — synchronized, like audio description on Netflix: every
    /// description starts at the exact moment the thing it describes appears or happens, not
    /// before and not after.
    ///
    /// Two ways:
    ///  • Gemini watches the WHOLE video with its sound (2 pictures a second, so the times are
    ///    accurate to about half a second), writes each description at the moment of its action,
    ///    and also lists where people speak or sing. Long videos are done in 5-minute parts, each
    ///    part told what was described before, so names stay the same.
    ///  • Locally (Ollama), shot by shot: every description starts right at its shot change.
    ///
    /// Afterwards the times are sharpened on the video itself: a description less than a second
    /// from a shot change is moved onto that change, because that is where the new picture
    /// really begins.
    /// </summary>
    public static class ScriptWriter
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ── Gemini, the whole video ───────────────────────────────────────────

        /// <summary>
        /// Steps: 1 a small copy of the video, 2 sending it, 3 Google prepares it, 4 Gemini writes
        /// (part by part), 5 the descriptions are placed on the shot changes.
        /// A video that came from a public YouTube link is watched by Gemini directly on YouTube —
        /// nothing is copied or sent (steps: 1 Gemini writes, 2 shot changes). When YouTube refuses
        /// (a private or age-restricted video), the usual way with the upload is taken.
        /// </summary>
        public static async Task<ScriptResult> WithGeminiAsync(string video, double total, string lang, Density density,
                                                               bool snap, WorkProgress wp, CancellationToken ct, string sourceUrl = null)
        {
            if (!GeminiClient.HasKey) throw new InvalidOperationException("no_gemini_key");
            bool hasPicture = await Ffmpeg.HasVideoAsync(video, ct);
            if (!hasPicture) throw new InvalidOperationException("no_picture");
            var result = new ScriptResult();

            bool direct = false;
            if (GeminiClient.IsYouTube(sourceUrl))
            {
                int dsteps = 2 + wp.ExtraSteps;
                wp.SetStep(1, dsteps, Strings.L("wp_writing_youtube"));
                try
                {
                    var (yfps, ylow) = GeminiClient.Quality(AppSettings.Current.GeminiQuality, total);
                    await WriteAllPartsAsync(sourceUrl, null, total, yfps, ylow, lang, density, wp, result, ct);
                    direct = true;
                    if (result.Lines.Count > 0) await CutsAsync(video, total, snap, wp, 2, dsteps, result, ct);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    AppLog.Error("Gemini could not watch the YouTube link; uploading instead", ex);
                    result = new ScriptResult();
                    direct = false;
                }
                if (direct && result.Lines.Count > 0) return Finish(result, total);
            }

            result = new ScriptResult();
            var (fps, low) = GeminiClient.Quality(AppSettings.Current.GeminiQuality, total);
            // a short video goes inside the request itself: no upload, no waiting for Google to prepare it
            const long inlineLimit = 13L * 1024 * 1024;
            bool expectInline = total <= 300;
            int steps = (expectInline ? 3 : 5) + wp.ExtraSteps;
            wp.SetStep(1, steps, Strings.L("wp_prepare"));
            string small = Path.Combine(AppPaths.TempDir, $"gemini_{Guid.NewGuid():N}.mp4");
            string fileName = null;
            int cutsStep;
            try
            {
                // a small copy: 2 pictures a second (Gemini looks at 1 or 2), 360p for low detail or 480p, mono sound
                int height = low ? 360 : 480;
                string args = $"-nostdin -y -i \"{video}\" -map 0:v:0 -map 0:a:0? -sn " +
                              $"-vf \"scale=-2:'min({height},ih)',fps=2\" -c:v libx264 -preset veryfast -crf 30 " +
                              $"-c:a aac -ac 1 -b:a 48k -movflags +faststart \"{small}\"";
                if (!await Ffmpeg.RunAsync(args, ct, null, t => wp.SetPercent(t / Math.Max(1, total) * 100)) || !File.Exists(small))
                    throw new InvalidOperationException("prepare_failed");

                long size = new FileInfo(small).Length;
                if (size <= inlineLimit)
                {
                    if (!expectInline) steps = 3 + wp.ExtraSteps;
                    byte[] inline = await File.ReadAllBytesAsync(small, ct);
                    wp.SetStep(2, steps, Strings.L("wp_writing"));
                    await WriteAllPartsAsync(null, inline, total, fps, low, lang, density, wp, result, ct);
                    cutsStep = 3;
                }
                else
                {
                    if (expectInline) steps = 5 + wp.ExtraSteps;
                    wp.SetStep(2, steps, Strings.L("wp_upload"));
                    wp.SetDetail(Strings.LF("wp_size", Math.Max(1, size / 1048576)));
                    var (name, uri) = await GeminiClient.UploadVideoAsync(small, s =>
                    {
                        var m = Regex.Match(s ?? "", @"^(\d+)/(\d+)$");
                        if (m.Success) wp.SetPercent(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value));
                    }, ct, () => wp.SetStep(3, steps, Strings.L("wp_processing")));
                    fileName = name;

                    wp.SetStep(4, steps, Strings.L("wp_writing"));
                    await WriteAllPartsAsync(uri, null, total, fps, low, lang, density, wp, result, ct);
                    cutsStep = 5;
                }
            }
            finally
            {
                try { File.Delete(small); } catch { }
                if (fileName != null) await GeminiClient.DeleteFileAsync(fileName);
            }
            await CutsAsync(video, total, snap, wp, cutsStep, steps, result, ct);
            return Finish(result, total);
        }

        /// <summary>Gemini describes the video in 5-minute parts, each part told what was described before.</summary>
        private static async Task WriteAllPartsAsync(string uri, byte[] inline, double total, double fps, bool low, string lang, Density density,
                                                     WorkProgress wp, ScriptResult result, CancellationToken ct)
        {
            const double partLength = 300;
            int parts = Math.Max(1, (int)Math.Ceiling(total / partLength));
            for (int i = 0; i < parts; i++)
            {
                ct.ThrowIfCancellationRequested();
                double from = i * partLength, to = Math.Min(total, (i + 1) * partLength);
                wp.SetPercent(i, parts);
                wp.SetDetail(parts > 1 ? Strings.LF("wp_part", i + 1, parts, result.Lines.Count, Clock(from), Clock(to))
                                       : Strings.L("wp_whole"));
                string before = string.Join("\n", result.Lines.Skip(Math.Max(0, result.Lines.Count - 12))
                                                              .Select(l => $"{Clock(l.Time)} | {l.Text}"));
                // the answer arrives while Gemini writes it: the last time written so far shows how far it is
                int partIndex = i;
                wp.SetDetail(Strings.L("wp_gemini_thinking") + (parts > 1 ? " " + Strings.LF("wp_part_of", i + 1, parts) : ""));
                string answer = await GeminiClient.AskVideoStreamAsync(uri, from, to, low, MainPrompt(lang, total, from, to, before, density),
                    soFar =>
                    {
                        var (sofar, _) = Parse(soFar, from, to, lang);
                        if (sofar.Count == 0) return;
                        double reached = Math.Clamp((sofar[^1].Time - from) / Math.Max(1, to - from), 0, 1);
                        wp.SetPercent((partIndex + reached) * 100 / parts);
                        wp.SetDetail(Strings.LF("wp_gemini_writing", result.Lines.Count + sofar.Count, Clock(sofar[^1].Time), Clock(total)));
                    },
                    state =>
                    {
                        var st = state.Split('|');
                        wp.SetDetail(st[0] == "busy" ? Strings.LF("wp_gemini_busy", st[1]) : Strings.L("wp_gemini_retry"));
                    }, ct, fps, inline);
                var (lines, speech) = Parse(answer, from, to, lang);

                // long stretches without any description are asked for once more
                var gaps = Gaps(lines, from, to, MaxGap(density), lang);
                if (gaps.Count > 0)
                {
                    wp.SetDetail(Strings.LF("wp_gaps", gaps.Count, i + 1, parts));
                    string fill = await GeminiClient.AskVideoAsync(uri, from, to, low, GapPrompt(lang, from, to, lines, gaps, density), ct, fps, inline);
                    var (more, _) = Parse(fill, from, to, lang);
                    foreach (var l in more)
                        if (gaps.Any(g => l.Time >= g.From - 0.5 && l.Time <= g.To + 0.5)) lines.Add(l);
                }
                foreach (var l in lines.OrderBy(l => l.Time))
                    if (result.Lines.Count == 0 || l.Time > result.Lines[^1].Time + 0.8) result.Lines.Add(l);
                result.Speech.AddRange(speech);
                wp.SetPercent(i + 1, parts);
                wp.SetDetail(Strings.LF("wp_written_so_far", result.Lines.Count));
            }
            result.Speech = MergeSpans(result.Speech);
        }

        private static async Task CutsAsync(string video, double total, bool snap, WorkProgress wp, int step, int steps, ScriptResult result, CancellationToken ct)
        {
            wp.SetStep(step, steps, Strings.L("wp_cuts"));
            result.Cuts = await Ffmpeg.ShotChangesAsync(video, ct, 0.3, t => wp.SetPercent(t / Math.Max(1, total) * 100));
            int moved = snap ? SnapToCuts(result.Lines, result.Cuts) : 0;
            wp.SetDetail(Strings.LF("wp_cuts_done", result.Cuts.Count, moved));
        }

        private static ScriptResult Finish(ScriptResult result, double total)
        {
            result.Lines.RemoveAll(l => l.Time >= total - 0.3);
            foreach (var l in result.Lines) l.PlacedAt = l.Time;
            return result;
        }

        private static double MaxGap(Density d) => d switch { Density.Detailed => 8, Density.Normal => 14, _ => double.MaxValue };

        private static string DensityRule(Density d) => d switch
        {
            Density.Essential =>
                "- Describe only what a blind viewer cannot follow without: who appears, where we are, important actions, and text on screen.",
            Density.Detailed =>
                "- Describe every visible change that matters: a new place, a new person, every important action, gestures and expressions — about every 5 to 8 seconds when the picture changes.",
            _ =>
                "- Describe the important changes: a new place, a new person, important actions and expressions. Do not leave the picture undescribed for more than about 15 seconds when something new is seen."
        };

        private static string MainPrompt(string lang, double total, double from, double to, string before, Density density)
        {
            var sb = new StringBuilder();
            sb.AppendLine("You are a professional audio describer writing audio description like on Netflix, for blind and visually impaired viewers.");
            sb.AppendLine($"Watch this video WITH its sound. The whole video is {Clock(total)} long; describe only the part from {Clock(from)} to {Clock(to)}.");
            sb.AppendLine();
            sb.AppendLine("TIMING — the most important rule:");
            sb.AppendLine("- Each description starts at the EXACT moment the thing it describes appears or happens on screen. Never before it (that would spoil it), never after it (the viewer would hear it too late).");
            sb.AppendLine("- Give each time with tenths of a second (MM:SS.s), at the moment the action begins or the new picture appears.");
            sb.AppendLine("- Describe an action while it is happening, in the present tense.");
            sb.AppendLine("- Keep each description short enough to be spoken before the next thing happens and, wherever possible, before someone speaks: about 2.5 words per second of free time. When almost no time is free, use only the most important words (\"Ana runs out.\").");
            sb.AppendLine();
            sb.AppendLine("CONTENT:");
            sb.AppendLine("- Describe what a blind viewer needs to follow it: who is there, what they do, where, important changes, facial expressions and gestures that matter, and any text on screen (titles, signs, messages, credits) — read that text out.");
            sb.AppendLine(DensityRule(density));
            sb.AppendLine("- Do not describe what can be heard (the music, what is said). Do not interpret or guess feelings that are not shown.");
            sb.AppendLine("- No camera words (shot, close-up, frame, camera). Do not repeat what is still true; describe only changes. Complete sentences only.");
            sb.AppendLine("- Once a character's name is clear from the dialogue or the screen, use it; until then use a short consistent description (\"the girl in the red coat\").");
            sb.AppendLine($"- Write in {Languages.PromptName(lang)}.");
            sb.AppendLine();
            sb.AppendLine("DIALOGUE MAP: also list every stretch in this part where someone speaks or sings, so the program knows where the dialogue is.");
            sb.AppendLine();
            sb.AppendLine("Times are measured from the START OF THE WHOLE VIDEO, in minutes:seconds.tenths (minutes may go past 59, for example 95:20.5).");
            if (!string.IsNullOrWhiteSpace(before))
            {
                sb.AppendLine("The descriptions just before this part were (keep the same names, do not repeat them):");
                sb.AppendLine(before);
            }
            sb.AppendLine("Answer ONLY with lines in these two forms, nothing else:");
            sb.AppendLine("MM:SS.s | description");
            sb.AppendLine("SPEECH MM:SS.s-MM:SS.s");
            return sb.ToString();
        }

        private static string GapPrompt(string lang, double from, double to, List<AdLine> have, List<Span> gaps, Density density)
        {
            var sb = new StringBuilder();
            sb.AppendLine("You are a professional audio describer for blind viewers. Watch this video WITH its sound.");
            sb.AppendLine($"An audio description for the part from {Clock(from)} to {Clock(to)} already exists:");
            foreach (var l in have.OrderBy(l => l.Time)) sb.AppendLine($"{Clock(l.Time)} | {l.Text}");
            sb.AppendLine("These stretches still have NO description, although the picture goes on:");
            foreach (var g in gaps) sb.AppendLine($"{Clock(g.From)} – {Clock(g.To)}");
            sb.AppendLine("Write the missing descriptions, ONLY inside those stretches, about what is seen there that has not been described yet.");
            sb.AppendLine("- Each one starts at the exact moment its action begins (MM:SS.s, with tenths). Short, complete sentences in the present tense, about 2.5 words per second of free time.");
            sb.AppendLine(DensityRule(density));
            sb.AppendLine("- No camera words. Do not repeat the existing descriptions. Keep the same names.");
            sb.AppendLine($"- Write in {Languages.PromptName(lang)}.");
            sb.AppendLine("Times are measured from the START OF THE WHOLE VIDEO.");
            sb.AppendLine("Answer ONLY with the new lines, in this exact form, nothing else:");
            sb.AppendLine("MM:SS.s | description");
            return sb.ToString();
        }

        /// <summary>Stretches longer than <paramref name="maxGap"/> with no description (from a line's estimated end to the next line).</summary>
        private static List<Span> Gaps(List<AdLine> lines, double from, double to, double maxGap, string lang)
        {
            var gaps = new List<Span>();
            if (maxGap == double.MaxValue) return gaps;
            double cursor = from;
            foreach (var l in lines.OrderBy(l => l.Time))
            {
                if (l.Time - cursor > maxGap) gaps.Add(new Span { From = cursor, To = l.Time });
                cursor = Math.Max(cursor, l.Time + Timing.EstimateSeconds(l.Text, lang));
            }
            if (to - cursor > maxGap) gaps.Add(new Span { From = cursor, To = to });
            return gaps;
        }

        private static readonly Regex TimeRx = new(@"(?:(\d{1,2}):)?(\d{1,3}):(\d{1,2}(?:[.,]\d+)?)", RegexOptions.Compiled);

        private static double? ReadTime(string s)
        {
            var m = TimeRx.Match(s ?? "");
            return m.Success ? TimeText.Parse(m.Value.Replace(',', '.')) : null;
        }

        /// <summary>Reads Gemini's answer: "MM:SS.s | text" and "SPEECH MM:SS.s-MM:SS.s" lines.</summary>
        public static (List<AdLine>, List<Span>) Parse(string answer, double from, double to, string lang)
        {
            var lines = new List<AdLine>();
            var speech = new List<Span>();
            foreach (string raw0 in (answer ?? "").Replace("\r", "").Split('\n'))
            {
                string raw = raw0.Replace("*", "").Replace("`", "").Trim().TrimStart('-', ' ', '•');
                if (raw.Length < 4) continue;
                var sp = Regex.Match(raw, @"^SPEECH\s*:?\s*\[?([\d:.,]+)\]?\s*[-–—]+\s*\[?([\d:.,]+)\]?", RegexOptions.IgnoreCase);
                if (sp.Success)
                {
                    double? a = ReadTime(sp.Groups[1].Value), b = ReadTime(sp.Groups[2].Value);
                    if (a != null && b != null && b > a) speech.Add(new Span { From = a.Value, To = b.Value });
                    continue;
                }
                var m = Regex.Match(raw, @"^\[?([\d:.,]+)\]?\s*[|\-–—:]\s*(.+)$");
                if (!m.Success) continue;
                double? t = ReadTime(m.Groups[1].Value);
                if (t == null) continue;
                string text = m.Groups[2].Value.Trim().Trim('"', '“', '”', '„');
                if (text.Length < 2) continue;
                if (lang == "sr") text = SerbianLatin(text);
                lines.Add(new AdLine { Time = Math.Round(t.Value, 1), Text = text });
            }
            // times counted from the part instead of the whole video: shift them
            if (from > 0 && lines.Count > 0 && lines.All(l => l.Time < to - from + 1) && lines.Any(l => l.Time < from - 1))
            {
                foreach (var l in lines) l.Time += from;
                foreach (var s in speech) { s.From += from; s.To += from; }
            }
            lines = lines.Where(l => l.Time >= from - 1 && l.Time <= to + 1).OrderBy(l => l.Time).ToList();
            foreach (var l in lines) l.PlacedAt = l.Time;
            return (lines, speech);
        }

        private static List<Span> MergeSpans(List<Span> spans)
        {
            var result = new List<Span>();
            foreach (var s in spans.OrderBy(s => s.From))
            {
                if (result.Count > 0 && s.From <= result[^1].To + 0.3) result[^1].To = Math.Max(result[^1].To, s.To);
                else result.Add(new Span { From = s.From, To = s.To });
            }
            return result;
        }

        /// <summary>
        /// A description less than a second from a shot change starts on that change (a tenth after
        /// it, when the new picture is there). Gemini sees two pictures a second, the cut is exact.
        /// </summary>
        public static int SnapToCuts(List<AdLine> lines, List<double> cuts)
        {
            if (cuts == null || cuts.Count == 0) return 0;
            int moved = 0;
            foreach (var l in lines)
            {
                double best = double.NaN, dist = double.MaxValue;
                foreach (double c in cuts)
                {
                    double d = c - l.Time;
                    if (d < -1.0 || d > 0.8) continue;   // a cut a little later is allowed: the AI is often early
                    if (Math.Abs(d) < dist) { dist = Math.Abs(d); best = c; }
                }
                if (!double.IsNaN(best) && Math.Abs(best + 0.1 - l.Time) > 0.05)
                {
                    l.Time = Math.Round(best + 0.1, 1);
                    moved++;
                }
            }
            return moved;
        }

        // ── local, shot by shot ───────────────────────────────────────────────

        /// <summary>
        /// One description per shot, starting a tenth of a second after the cut. A shot longer than
        /// 10 seconds is looked at again every ~8 seconds and described again only when the picture
        /// changed. A shot that is over before the previous description could end is skipped.
        /// </summary>
        public static async Task<ScriptResult> LocalAsync(string video, double total, string lang, WorkProgress wp, CancellationToken ct)
        {
            if (!await LocalVision.AvailableAsync()) throw new InvalidOperationException("no_model");
            if (!await Ffmpeg.HasVideoAsync(video, ct)) throw new InvalidOperationException("no_picture");
            int steps = 2 + wp.ExtraSteps;
            var result = new ScriptResult();
            wp.SetStep(1, steps, Strings.L("wp_cuts_find"));
            result.Cuts = await Ffmpeg.ShotChangesAsync(video, ct, 0.3, t => wp.SetPercent(t / Math.Max(1, total) * 100));

            var bounds = new List<double> { 0 };
            foreach (double c in result.Cuts)
                if (c - bounds[^1] >= 1.2 && total - c >= 0.8) bounds.Add(c);
            bounds.Add(total);

            var shots = new List<(double Start, double End, int Part)>();
            for (int i = 0; i + 1 < bounds.Count; i++)
            {
                double a = bounds[i], b = bounds[i + 1];
                int pieces = Math.Max(1, (int)Math.Floor((b - a) / 8.0));
                for (int k = 0; k < pieces; k++) shots.Add((a + (b - a) * k / pieces, a + (b - a) * (k + 1) / pieces, k));
            }

            wp.SetStep(2, steps, Strings.L("wp_local"));
            double free = 0;
            for (int i = 0; i < shots.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var (s, e, part) = shots[i];
                wp.SetPercent(i, shots.Count);
                wp.SetDetail(Strings.LF("wp_shot", i + 1, shots.Count, result.Lines.Count));
                if (Math.Max(s, free) > e - 0.6) continue;   // the previous description is still running through this shot
                double look = Math.Min(e - 0.1, s + Math.Min(0.8, (e - s) / 3));
                string text = await LocalVision.DescribeAsync(video, look, lang, ct);
                if (string.IsNullOrWhiteSpace(text)) continue;
                if (result.Lines.Count > 0 && Similar(result.Lines[^1].Text, text) && (part > 0 || s - result.Lines[^1].Time < 12)) continue;
                double at = Math.Round(part == 0 && s > 0 ? s + 0.1 : s, 1);
                result.Lines.Add(new AdLine { Time = at, PlacedAt = at, Text = text });
                free = at + Timing.EstimateSeconds(text, lang) + 0.3;
            }
            wp.SetPercent(100);
            return result;
        }

        private static bool Similar(string a, string b)
        {
            var wa = Regex.Split((a ?? "").ToLowerInvariant(), @"\W+").Where(w => w.Length > 3).ToHashSet();
            var wb = Regex.Split((b ?? "").ToLowerInvariant(), @"\W+").Where(w => w.Length > 3).ToHashSet();
            if (wa.Count == 0 || wb.Count == 0) return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
            return wa.Intersect(wb).Count() >= 0.7 * Math.Min(wa.Count, wb.Count);
        }

        // ── helpers ───────────────────────────────────────────────────────────

        /// <summary>mm:ss.s (minutes past 59 allowed), as the prompts use.</summary>
        public static string Clock(double s)
        {
            s = Math.Max(0, s);
            int m = (int)(s / 60);
            return $"{m:00}:{(s - m * 60).ToString("00.0", Inv)}";
        }

        /// <summary>Serbian answers sometimes come in Cyrillic: the script is in Latin letters.</summary>
        public static string SerbianLatin(string s)
        {
            if (string.IsNullOrEmpty(s) || !s.Any(c => c >= 'Ѐ' && c <= 'ӿ')) return s;
            var map = new Dictionary<char, string>
            {
                ['а']="a",['б']="b",['в']="v",['г']="g",['д']="d",['ђ']="đ",['е']="e",['ж']="ž",['з']="z",['и']="i",
                ['ј']="j",['к']="k",['л']="l",['љ']="lj",['м']="m",['н']="n",['њ']="nj",['о']="o",['п']="p",['р']="r",
                ['с']="s",['т']="t",['ћ']="ć",['у']="u",['ф']="f",['х']="h",['ц']="c",['ч']="č",['џ']="dž",['ш']="š"
            };
            var sb = new StringBuilder();
            foreach (char c in s)
            {
                char lower = char.ToLowerInvariant(c);
                if (map.TryGetValue(lower, out string lat)) sb.Append(c == lower ? lat : char.ToUpperInvariant(lat[0]) + lat.Substring(1));
                else sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
