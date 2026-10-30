using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace UltraAudioDescriptionMaker.Core
{
    /// <summary>Running FFmpeg and FFprobe.</summary>
    public static class Ffmpeg
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string N(double v) => v.ToString("0.###", Inv);

        private static readonly Regex TimeRx = new(@"time=(\d+):(\d+):(\d+(?:\.\d+)?)", RegexOptions.Compiled);

        /// <summary>Runs FFmpeg. True when it finished without an error. <paramref name="log"/> receives everything FFmpeg wrote.</summary>
        /// <param name="onTime">Called with the position FFmpeg has reached (seconds), for a progress bar.</param>
        public static async Task<bool> RunAsync(string args, CancellationToken ct, StringBuilder log = null, Action<double> onTime = null)
        {
            if (!File.Exists(AppPaths.FFmpeg)) throw new InvalidOperationException("ffmpeg_missing");
            using var p = new Process
            {
                StartInfo = new ProcessStartInfo(AppPaths.FFmpeg, args)
                {
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardError = true, RedirectStandardOutput = true,
                    StandardErrorEncoding = Encoding.UTF8
                }
            };
            var err = new StringBuilder();
            p.ErrorDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                lock (err) err.AppendLine(e.Data);
                if (onTime != null && e.Data.Contains("time="))
                {
                    var m = TimeRx.Match(e.Data);
                    if (m.Success)
                        onTime(int.Parse(m.Groups[1].Value, Inv) * 3600 + int.Parse(m.Groups[2].Value, Inv) * 60 +
                               double.Parse(m.Groups[3].Value, Inv));
                }
            };
            p.Start();
            p.BeginErrorReadLine();
            p.DrainStdout();
            await p.WaitForExitOrKillAsync(ct);
            string text;
            lock (err) text = err.ToString();
            log?.Append(text);
            if (p.ExitCode != 0)
            {
                var last = text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).TakeLast(3);
                AppLog.Warn($"FFmpeg exit {p.ExitCode}: {args}\n    {string.Join("\n    ", last)}");
            }
            return p.ExitCode == 0;
        }

        private static async Task<string> ProbeAsync(string args, CancellationToken ct)
        {
            string probe = AppPaths.FFprobe;
            if (!File.Exists(probe)) throw new InvalidOperationException("ffmpeg_missing");
            using var p = new Process
            {
                StartInfo = new ProcessStartInfo(probe, args)
                { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true }
            };
            p.Start();
            var o = p.StandardOutput.ReadToEndAsync();
            p.DrainStderr();
            await p.WaitForExitOrKillAsync(ct);
            return (await o).Trim();
        }

        /// <summary>Length of a media file in seconds (0 when it cannot be read).</summary>
        public static async Task<double> DurationAsync(string path, CancellationToken ct)
        {
            try
            {
                string o = await ProbeAsync($"-v error -show_entries format=duration -of csv=p=0 \"{path}\"", ct);
                return double.TryParse(o, NumberStyles.Float, Inv, out double d) ? d : 0;
            }
            catch (OperationCanceledException) { throw; }
            catch (InvalidOperationException) { throw; }
            catch (Exception ex) { AppLog.Error("Reading the length of " + path, ex); return 0; }
        }

        /// <summary>Does the file have a sound track?</summary>
        public static async Task<bool> HasAudioAsync(string path, CancellationToken ct)
        {
            string o = await ProbeAsync($"-v error -select_streams a -show_entries stream=index -of csv=p=0 \"{path}\"", ct);
            return o.Length > 0;
        }

        /// <summary>Does the file have moving pictures (not only a cover image)?</summary>
        public static async Task<bool> HasVideoAsync(string path, CancellationToken ct)
        {
            string o = await ProbeAsync($"-v error -select_streams v -show_entries stream=codec_name:stream_disposition=attached_pic -of csv=p=0 \"{path}\"", ct);
            var lines = o.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
            return lines.Any(l => !l.EndsWith(",1"));
        }

        /// <summary>Shot changes in seconds, found with FFmpeg's scene score (a small copy of the picture is enough).</summary>
        public static async Task<List<double>> ShotChangesAsync(string path, CancellationToken ct, double threshold = 0.3, Action<double> onTime = null)
        {
            var times = new List<double>();
            var sb = new StringBuilder();
            await RunAsync($"-nostdin -hide_banner -stats -i \"{path}\" -an -sn " +
                           $"-vf \"scale=160:-2,select='gt(scene,{N(threshold)})',showinfo\" -f null -", ct, sb, onTime);
            foreach (Match m in Regex.Matches(sb.ToString(), @"pts_time:([0-9.]+)"))
                if (double.TryParse(m.Groups[1].Value, NumberStyles.Float, Inv, out double t)) times.Add(t);
            return times.OrderBy(t => t).ToList();
        }

        /// <summary>Saves the frame at <paramref name="time"/> as a small JPEG. Null when no frame could be taken.</summary>
        public static async Task<string> FrameAsync(string path, double time, CancellationToken ct, int width = 512)
        {
            string frame = Path.Combine(AppPaths.TempDir, $"frame_{Guid.NewGuid():N}.jpg");
            string ss = time > 0.01 ? $"-ss {N(time)} " : "";
            string tail = $"-map 0:v:0 -frames:v 1 -vf \"scale={width}:-2\" -q:v 3 -update 1 \"{frame}\"";
            await RunAsync($"-nostdin -y -hide_banner -loglevel error {ss}-i \"{path}\" {tail}", ct);
            if (!File.Exists(frame) && ss.Length > 0)
                await RunAsync($"-nostdin -y -hide_banner -loglevel error -i \"{path}\" {tail}", ct);
            return File.Exists(frame) && new FileInfo(frame).Length > 100 ? frame : null;
        }
    }
}
