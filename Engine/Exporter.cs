using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UltraAudioDescriptionMaker.Core;

namespace UltraAudioDescriptionMaker
{
    /// <summary>What to export.</summary>
    public class ExportOptions
    {
        public string Folder;
        public string BaseName;
        /// <summary>The video with the description mixed into its sound.</summary>
        public bool VideoMixed = true;
        /// <summary>The video with two sound tracks: the original, and the original with the description (choosable in the player).</summary>
        public bool VideoExtraTrack;
        /// <summary>The whole sound with the description (no picture).</summary>
        public bool AudioMix;
        /// <summary>Only the voice of the description, on silence, as long as the video (for editing elsewhere).</summary>
        public bool AudioVoiceOnly;
        /// <summary>"mp3" or "wav" for the two sound files.</summary>
        public string AudioFormat = "mp3";
        public bool ScriptTxt = true;
        public bool ScriptVtt = true;
        public bool ScriptSrt;
    }

    /// <summary>Makes the finished files from a prepared project (voices placed by <see cref="Timing.FitAsync"/>).</summary>
    public static class Exporter
    {
        public static string Ratio(string ducking) => ducking switch { "Light" => "1.6", "Strong" => "4", _ => "2.2" };

        /// <summary>Exports what was chosen and returns the files made. Every file made is one step in <paramref name="wp"/>.</summary>
        public static async Task<List<string>> ExportAsync(AdProject p, ExportOptions o, WorkProgress wp, CancellationToken ct)
        {
            var made = new List<string>();
            var spoken = p.Lines.Where(l => l.Duration > 0.05 && File.Exists(l.AudioPath ?? "")).OrderBy(l => l.PlacedAt).ToList();
            if (spoken.Count == 0) throw new InvalidOperationException("nothing_spoken");
            Directory.CreateDirectory(o.Folder);
            string basePath = Path.Combine(o.Folder, o.BaseName);
            string work = Path.Combine(AppPaths.TempDir, "export_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(work);
            try
            {
                double total = p.Duration > 0 ? p.Duration : await Ffmpeg.DurationAsync(p.VideoPath, ct);
                bool needVoice = o.VideoMixed || o.VideoExtraTrack || o.AudioMix || o.AudioVoiceOnly;
                int steps = (needVoice ? 1 : 0) + (o.VideoMixed ? 1 : 0) + (o.VideoExtraTrack ? 1 : 0) + (o.AudioMix ? 1 : 0) +
                            (o.AudioVoiceOnly ? 1 : 0) + (o.ScriptTxt || o.ScriptVtt || o.ScriptSrt ? 1 : 0);
                int step = 0;
                void Stage(string key) => wp?.SetStep(++step, steps, Strings.L(key));
                Action<double> pct = t => wp?.SetPercent(t / Math.Max(1, total) * 100);
                string narration = Path.Combine(work, "narration.wav");
                if (needVoice)
                {
                    Stage("ex_stage_voice");
                    wp?.SetDetail(Strings.LF("wp_voices_count", spoken.Count));
                    await VoiceTrackAsync(spoken, total, narration, work, ct, wp);
                }
                bool hasAudio = needVoice && await Ffmpeg.HasAudioAsync(p.VideoPath, ct);
                string graph = Graph(hasAudio, AppSettings.Current.Ducking);
                string lang3 = Languages.Iso3(p.Language);
                string adTitle = Strings.L("track_title");

                if (o.VideoMixed)
                {
                    Stage("ex_stage_video");
                    string outMp4 = basePath + Strings.L("suffix_ad") + ".mp4";
                    string common = $"-nostdin -y -i \"{p.VideoPath}\" -i \"{narration}\" -filter_complex \"{graph}\" -map 0:v:0 -map \"[aout]\" ";
                    string audio = $"-c:a aac -b:a 256k -ar 48000 -metadata:s:a:0 title=\"{adTitle}\" -metadata:s:a:0 language={lang3} -movflags +faststart \"{outMp4}\"";
                    if (!await Ffmpeg.RunAsync(common + "-c:v copy " + audio, ct, null, pct))
                    {
                        // a picture format MP4 cannot hold as it is: encode it once
                        AppLog.Warn("Copying the picture failed, encoding it with H.264.");
                        wp?.SetDetail(Strings.L("wp_encoding"));
                        if (!await Ffmpeg.RunAsync(common + "-c:v libx264 -preset veryfast -crf 20 -pix_fmt yuv420p " + audio, ct, null, pct))
                            throw new InvalidOperationException("mix_failed");
                    }
                    made.Add(outMp4);
                }

                if (o.VideoExtraTrack)
                {
                    Stage("ex_stage_video2");
                    string outMp4 = basePath + Strings.L("suffix_two_tracks") + ".mp4";
                    string orig = hasAudio ? "-map 0:a:0 " : "";
                    int ad = hasAudio ? 1 : 0;
                    string common = $"-nostdin -y -i \"{p.VideoPath}\" -i \"{narration}\" -filter_complex \"{graph}\" -map 0:v:0 {orig}-map \"[aout]\" ";
                    string meta = (hasAudio ? $"-metadata:s:a:0 language={lang3} -disposition:a:0 default " : "") +
                                  $"-metadata:s:a:{ad} title=\"{adTitle}\" -metadata:s:a:{ad} language={lang3} -disposition:a:{ad} 0 ";
                    string audio = $"-c:a aac -b:a 192k -ar 48000 {meta}-movflags +faststart \"{outMp4}\"";
                    if (!await Ffmpeg.RunAsync(common + "-c:v copy " + audio, ct, null, pct) &&
                        !await Ffmpeg.RunAsync(common + "-c:v libx264 -preset veryfast -crf 20 -pix_fmt yuv420p " + audio, ct, null, pct))
                        throw new InvalidOperationException("mix_failed");
                    made.Add(outMp4);
                }

                string codec = o.AudioFormat == "wav" ? "-c:a pcm_s16le" : "-c:a libmp3lame -b:a 192k";
                string ext = o.AudioFormat == "wav" ? ".wav" : ".mp3";
                if (o.AudioMix)
                {
                    Stage("ex_stage_audio");
                    string outA = basePath + Strings.L("suffix_ad") + ext;
                    if (!await Ffmpeg.RunAsync($"-nostdin -y -i \"{p.VideoPath}\" -i \"{narration}\" -filter_complex \"{graph}\" -map \"[aout]\" -ar 48000 {codec} \"{outA}\"", ct, null, pct))
                        throw new InvalidOperationException("mix_failed");
                    made.Add(outA);
                }
                if (o.AudioVoiceOnly)
                {
                    Stage("ex_stage_voice_file");
                    string outV = basePath + Strings.L("suffix_voice") + ext;
                    if (!await Ffmpeg.RunAsync($"-nostdin -y -i \"{narration}\" -ar 48000 {codec} \"{outV}\"", ct, null, pct))
                        throw new InvalidOperationException("mix_failed");
                    made.Add(outV);
                }

                if (o.ScriptTxt || o.ScriptVtt || o.ScriptSrt) Stage("ex_stage_text");
                var utf8 = new UTF8Encoding(false);
                if (o.ScriptTxt)
                {
                    var txt = new StringBuilder();
                    txt.AppendLine($"{p.Title} — {Strings.L("track_title").ToLowerInvariant()}");
                    txt.AppendLine();
                    foreach (var l in p.Lines.OrderBy(l => l.Time)) txt.AppendLine($"{TimeText.Format(l.Time)}  {l.Text}");
                    await File.WriteAllTextAsync(basePath + Strings.L("suffix_ad") + ".txt", txt.ToString(), utf8, ct);
                    made.Add(basePath + Strings.L("suffix_ad") + ".txt");
                }
                if (o.ScriptVtt)
                {
                    // a WebVTT "descriptions" track: players that support text description read it with the screen reader
                    var vtt = new StringBuilder("WEBVTT\nKind: descriptions\nLanguage: " + p.Language + "\n\n");
                    // the browser extension finds the right script for a web video by this address
                    if (!string.IsNullOrEmpty(p.SourceUrl)) vtt.Append("NOTE source: " + p.SourceUrl + "\n\n");
                    int n = 1;
                    foreach (var l in spoken)
                    {
                        vtt.AppendLine((n++).ToString());
                        vtt.AppendLine($"{TimeText.Vtt(l.PlacedAt)} --> {TimeText.Vtt(l.PlacedAt + Math.Max(1, l.Duration))}");
                        vtt.AppendLine(l.Text);
                        vtt.AppendLine();
                    }
                    await File.WriteAllTextAsync(basePath + Strings.L("suffix_ad") + ".vtt", vtt.ToString(), utf8, ct);
                    made.Add(basePath + Strings.L("suffix_ad") + ".vtt");
                }
                if (o.ScriptSrt)
                {
                    var srt = new StringBuilder();
                    int n = 1;
                    foreach (var l in spoken)
                    {
                        srt.AppendLine((n++).ToString());
                        srt.AppendLine($"{TimeText.Srt(l.PlacedAt)} --> {TimeText.Srt(l.PlacedAt + Math.Max(1, l.Duration))}");
                        srt.AppendLine(l.Text);
                        srt.AppendLine();
                    }
                    await File.WriteAllTextAsync(basePath + Strings.L("suffix_ad") + ".srt", srt.ToString(), utf8, ct);
                    made.Add(basePath + Strings.L("suffix_ad") + ".srt");
                }
                return made;
            }
            finally
            {
                try { Directory.Delete(work, true); } catch { }
            }
        }

        /// <summary>
        /// The film sound goes down under the voice (side-chain compression: only while the voice
        /// is heard), the voice is a little louder, and a limiter keeps the result from clipping.
        /// </summary>
        private static string Graph(bool hasAudio, string ducking)
        {
            if (!hasAudio) return "[1:a]aformat=sample_rates=48000:channel_layouts=stereo,volume=1.5,alimiter=limit=0.95[aout]";
            return "[0:a]aformat=sample_rates=48000:channel_layouts=stereo[p];" +
                   "[1:a]aformat=sample_rates=48000:channel_layouts=stereo,asplit=2[sc][nv];" +
                   $"[p][sc]sidechaincompress=threshold=0.015:ratio={Ratio(ducking)}:attack=30:release=500:makeup=1[duck];" +
                   "[nv]volume=1.5[nv2];" +
                   "[duck][nv2]amix=inputs=2:duration=first:normalize=0,alimiter=limit=0.95[aout]";
        }

        /// <summary>The voice track alone: silence as long as the video, every description at its place (in passes of 32).</summary>
        private static async Task VoiceTrackAsync(List<AdLine> spoken, double total, string outPath, string work, CancellationToken ct, WorkProgress wp = null)
        {
            var current = spoken.Select(l => (Path: l.AudioPath, At: l.PlacedAt)).ToList();
            var temps = new List<string>();
            int groupsTotal = (spoken.Count + 31) / 32 + (spoken.Count > 32 ? 1 : 0), groupsDone = 0;
            try
            {
                while (true)
                {
                    bool last = current.Count <= 32;
                    var next = new List<(string Path, double At)>();
                    foreach (var group in current.Select((p, i) => (p, i)).GroupBy(x => x.i / 32))
                    {
                        string target = last ? outPath : Path.Combine(work, $"part_{Guid.NewGuid():N}.wav");
                        if (!last) temps.Add(target);
                        var args = new StringBuilder($"-nostdin -y -f lavfi -t {Ffmpeg.N(total)} -i anullsrc=r=48000:cl=stereo ");
                        var g = new StringBuilder();
                        int k = 1;
                        foreach (var (p, _) in group)
                        {
                            args.Append($"-i \"{p.Path}\" ");
                            long ms = (long)Math.Round(p.At * 1000);
                            g.Append($"[{k}:a]aformat=sample_rates=48000:channel_layouts=stereo,adelay={ms}:all=1[a{k}];");
                            k++;
                        }
                        g.Append("[0:a]");
                        for (int j = 1; j < k; j++) g.Append($"[a{j}]");
                        g.Append($"amix=inputs={k}:duration=first:normalize=0[out]");
                        args.Append($"-filter_complex \"{g}\" -map \"[out]\" -c:a pcm_f32le \"{target}\"");
                        if (!await Ffmpeg.RunAsync(args.ToString(), ct) || !File.Exists(target))
                            throw new InvalidOperationException("voice_track_failed");
                        next.Add((target, 0));
                        wp?.SetPercent(++groupsDone, Math.Max(groupsDone, groupsTotal));
                    }
                    if (last) return;
                    current = next;
                }
            }
            finally
            {
                foreach (var t in temps) try { File.Delete(t); } catch { }
            }
        }
    }
}
