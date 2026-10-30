using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UltraAudioDescriptionMaker.Core;
using UltraAudioDescriptionMaker.Speech;

namespace UltraAudioDescriptionMaker
{
    /// <summary>
    /// Synchronized placement — the heart of the program.
    ///
    /// A description is heard at the exact moment of its action (its Time), never moved earlier or
    /// later to find a pause. To make it fit the free time after that moment (until someone speaks
    /// or the next description is due) the program first speaks it a little faster (up to the
    /// "maximum speed-up" in Settings). What still does not fit is not hidden: the line is marked
    /// (runs into dialogue, runs into the next description, over dialogue, starts late) so the
    /// describer can shorten the text or move it — F4 jumps to the next such line.
    ///
    /// Only one description is heard at a time: when one is still being spoken, the next one
    /// waits for it (and is marked "late").
    /// </summary>
    public static class Timing
    {
        /// <summary>Space left between a description and the speech or description after it.</summary>
        public const double Gap = 0.15;

        /// <summary>About how long a sentence takes to say, before the real voice is known.</summary>
        public static double EstimateSeconds(string text, string lang)
        {
            int words = (text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            return 0.3 + words / Languages.WordsPerSecond(lang);
        }

        /// <summary>The free time from a line's moment until the next speech or description.</summary>
        private static (double Window, bool InSpeech, bool LimitIsSpeech) FreeTime(AdProject p, int index, double start)
        {
            var lines = p.Lines;
            double nextLine = index + 1 < lines.Count ? lines[index + 1].Time : (p.Duration > 0 ? p.Duration : start + 30);
            bool inSpeech = p.SpeechAt(start + 0.05) != null;
            double limit = nextLine;
            bool isSpeech = false;
            if (!inSpeech)
            {
                var next = p.NextSpeechAfter(start + 0.05);
                if (next != null && next.From < limit) { limit = next.From; isSpeech = true; }
            }
            if (p.Duration > 0) limit = Math.Min(limit, p.Duration);
            return (limit - start - Gap, inSpeech, isSpeech);
        }

        /// <summary>
        /// Quick check without voices: uses the word count. Shown in the list until the voices are prepared.
        /// </summary>
        public static void Estimate(AdProject p)
        {
            p.Sort();
            double prevEnd = 0;
            for (int i = 0; i < p.Lines.Count; i++)
            {
                var l = p.Lines[i];
                if (l.AudioPath != null && l.Duration > 0 && !l.Estimated)
                {
                    // a real voice is known: keep its result, but the neighbours may have moved
                    Classify(p, i, l, l.Duration, ref prevEnd, l.RatePercent > AppSettings.Current.VoiceRate);
                    continue;
                }
                double d = EstimateSeconds(l.Text, p.Language) / (1 + AppSettings.Current.VoiceRate / 100.0);
                var (window, _, _) = FreeTime(p, i, l.Time);
                bool faster = false;
                if (d > window && window > 0.3)
                {
                    double factor = Math.Min(d / window, AppSettings.Current.MaxSpeedUp);
                    if (factor > 1.01) { d /= factor; faster = true; }
                }
                l.Estimated = true;
                Classify(p, i, l, d, ref prevEnd, faster);
            }
        }

        private static void Classify(AdProject p, int i, AdLine l, double duration, ref double prevEnd, bool faster)
        {
            double start = l.Time;
            double late = 0;
            if (start < prevEnd + Gap - 0.001) { late = prevEnd + Gap - start; start = prevEnd + Gap; }
            var (window, inSpeech, limitIsSpeech) = FreeTime(p, i, start);
            l.PlacedAt = start;
            l.Window = Math.Max(0, window);
            l.Overrun = 0;
            if (late > 0.3) { l.Status = FitStatus.Late; l.Overrun = late; }
            else if (inSpeech) l.Status = FitStatus.OverDialogue;
            else if (duration > window + 0.05)
            {
                l.Status = limitIsSpeech ? FitStatus.IntoDialogue : FitStatus.IntoNext;
                l.Overrun = duration - window;
            }
            else l.Status = faster ? FitStatus.SpedUp : FitStatus.Ok;
            prevEnd = start + duration;
        }

        /// <summary>
        /// Prepares the real voice for every line (from the cache when it was made before), speeds
        /// up the ones that are too long, and places them. <paramref name="progress"/> gets "done/total".
        /// </summary>
        public static async Task FitAsync(AdProject p, string voice, Action<string> progress, CancellationToken ct, IEnumerable<AdLine> only = null)
        {
            p.Sort();
            var set = only?.ToHashSet();
            int baseRate = AppSettings.Current.VoiceRate;
            double maxUp = AppSettings.Current.MaxSpeedUp;
            double prevEnd = 0;
            int count = set?.Count ?? p.Lines.Count, done = 0;
            for (int i = 0; i < p.Lines.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var l = p.Lines[i];
                if (set != null && !set.Contains(l))
                {
                    double d0 = l.Duration > 0 ? l.Duration : EstimateSeconds(l.Text, p.Language);
                    Classify(p, i, l, d0, ref prevEnd, l.RatePercent > baseRate);
                    continue;
                }
                progress?.Invoke($"{++done}/{count}");
                await FitLineAsync(p, i, voice, baseRate, maxUp, prevEnd, ct);
                Classify(p, i, l, l.Duration, ref prevEnd, l.RatePercent > baseRate);
            }
        }

        private static async Task FitLineAsync(AdProject p, int i, string voice, int baseRate, double maxUp, double prevEnd, CancellationToken ct)
        {
            var l = p.Lines[i];
            double start = Math.Max(l.Time, prevEnd + Gap);
            var (window, _, _) = FreeTime(p, i, start);
            string key = $"{voice}|{p.Language}|{l.Text}|{Math.Round(window, 1)}|{baseRate}|{maxUp}";
            if (l.AudioKey == key && l.AudioPath != null && System.IO.File.Exists(l.AudioPath)) return;

            int rate = baseRate;
            string path = await VoiceEngine.SpeakAsync(l.Text, voice, p.Language, rate, ct);
            double d = path == null ? 0 : await VoiceEngine.LengthAsync(path, ct);
            if (path != null && d > window && window > 0.3 && maxUp > 1.0)
            {
                // a little faster, so it ends before the next speech or description
                double factor = Math.Min(d / window, maxUp);
                int faster = (int)Math.Ceiling(((1 + baseRate / 100.0) * factor - 1) * 100);
                if (faster > rate)
                {
                    string p2 = await VoiceEngine.SpeakAsync(l.Text, voice, p.Language, faster, ct);
                    if (p2 != null)
                    {
                        path = p2;
                        rate = faster;
                        d = await VoiceEngine.LengthAsync(p2, ct);
                    }
                }
            }
            l.AudioPath = path;
            l.Duration = d;
            l.RatePercent = rate;
            l.AudioKey = key;
            l.Estimated = false;
        }

        /// <summary>The lines that need attention, in time order.</summary>
        public static List<AdLine> Problems(AdProject p) => p.Lines.Where(l => l.HasProblem).OrderBy(l => l.Time).ToList();
    }
}
