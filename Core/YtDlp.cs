using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace UltraAudioDescriptionMaker.Core
{
    /// <summary>
    /// Videos from a link (YouTube and the many other sites yt-dlp knows). yt-dlp.exe is
    /// downloaded by the program itself from its official GitHub releases the first time a link
    /// is opened, into %LOCALAPPDATA%\UltraAudioDescriptionMaker\Tools, and updated from the
    /// Tools menu. An existing yt-dlp in PATH is used too.
    /// Sites with copy protection (DRM) cannot be downloaded — that is the site's choice, not a fault.
    /// </summary>
    public static class YtDlp
    {
        private const string Release = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string ToolsDir
        {
            get { string d = Path.Combine(AppPaths.DataDir, "Tools"); Directory.CreateDirectory(d); return d; }
        }

        public static string Exe
        {
            get
            {
                string own = Path.Combine(ToolsDir, "yt-dlp.exe");
                if (File.Exists(own)) return own;
                foreach (string dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
                {
                    try
                    {
                        string c = Path.Combine(dir.Trim(), "yt-dlp.exe");
                        if (dir.Trim().Length > 0 && File.Exists(c)) return c;
                    }
                    catch { }
                }
                return own;
            }
        }

        public static bool Available => File.Exists(Exe);

        /// <summary>Downloads (or replaces) yt-dlp.exe.</summary>
        public static async Task InstallAsync(WorkProgress wp, CancellationToken ct)
        {
            string target = Path.Combine(ToolsDir, "yt-dlp.exe");
            string part = target + ".part";
            using var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            http.DefaultRequestHeaders.Add("User-Agent", "UltraAudioDescriptionMaker");
            using var resp = await http.GetAsync(Release, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();
            long total = resp.Content.Headers.ContentLength ?? 0;
            await using (var src = await resp.Content.ReadAsStreamAsync(ct))
            await using (var dst = File.Create(part))
            {
                var buffer = new byte[1 << 16];
                long got = 0;
                int read;
                while ((read = await src.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
                {
                    await dst.WriteAsync(buffer.AsMemory(0, read), ct);
                    got += read;
                    if (total > 0) wp?.SetPercent(got * 100.0 / total);
                }
            }
            if (new FileInfo(part).Length < 100_000) throw new InvalidOperationException("yt-dlp download was incomplete.");
            if (File.Exists(target)) File.Delete(target);
            File.Move(part, target);
            AppLog.Info("yt-dlp downloaded");
        }

        /// <summary>Does the text look like a web address?</summary>
        public static bool LooksLikeUrl(string text) =>
            Uri.TryCreate((text ?? "").Trim(), UriKind.Absolute, out var u) && (u.Scheme == "http" || u.Scheme == "https");

        /// <summary>
        /// Downloads the video (up to 1080p, as MP4) into <paramref name="folder"/> and returns the file.
        /// <paramref name="wp"/> gets the percent and "12 of 80 MB"; the title is reported as the detail first.
        /// </summary>
        public static async Task<string> DownloadAsync(string url, string folder, WorkProgress wp, CancellationToken ct)
        {
            Directory.CreateDirectory(folder);
            string ffmpegDir = Path.GetDirectoryName(AppPaths.FFmpeg) ?? "";
            string args =
                "--newline --no-playlist --no-mtime --restrict-filenames --no-part " +
                $"--ffmpeg-location \"{ffmpegDir}\" " +
                "-f \"bv*[height<=1080][ext=mp4]+ba[ext=m4a]/bv*[height<=1080]+ba/b[height<=1080]/b\" --merge-output-format mp4 " +
                $"-o \"{Path.Combine(folder, "%(title).100B [%(id)s].%(ext)s")}\" " +
                "--print before_dl:\"TITLE:%(title)s\" --print after_move:\"FILE:%(filepath)s\" --no-simulate " +
                $"\"{url}\"";

            using var p = new Process
            {
                StartInfo = new ProcessStartInfo(Exe, args)
                {
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
                }
            };
            p.StartInfo.Environment["PYTHONIOENCODING"] = "utf-8";
            string file = null, title = null;
            var errors = new List<string>();
            var progress = new Regex(@"\[download\]\s+([\d.]+)%\s+of\s+~?\s*([\d.]+)(\w+)", RegexOptions.Compiled);
            p.OutputDataReceived += (_, e) =>
            {
                string line = e.Data;
                if (string.IsNullOrEmpty(line)) return;
                if (line.StartsWith("TITLE:")) { title = line.Substring(6).Trim(); wp?.SetDetail(title); return; }
                if (line.StartsWith("FILE:")) { file = line.Substring(5).Trim(); return; }
                var m = progress.Match(line);
                if (m.Success && double.TryParse(m.Groups[1].Value, NumberStyles.Float, Inv, out double pct))
                {
                    wp?.SetPercent(pct);
                    if (double.TryParse(m.Groups[2].Value, NumberStyles.Float, Inv, out double size))
                    {
                        double mb = m.Groups[3].Value.StartsWith("G") ? size * 1024 : m.Groups[3].Value.StartsWith("K") ? size / 1024 : size;
                        wp?.SetDetail((title != null ? title + ". " : "") + Strings.LF("wp_ff_mb", (int)(mb * pct / 100), (int)mb));
                    }
                }
            };
            p.ErrorDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) lock (errors) errors.Add(e.Data.Trim()); };
            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            await p.WaitForExitOrKillAsync(ct);

            if (p.ExitCode == 0 && file != null && File.Exists(file)) return file;
            string why;
            lock (errors) why = errors.LastOrDefault(l => l.StartsWith("ERROR")) ?? errors.LastOrDefault() ?? $"exit {p.ExitCode}";
            AppLog.Warn("yt-dlp failed: " + why);
            if (why.Contains("DRM", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("link_drm");
            if (why.Contains("Unsupported URL", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("link_unsupported");
            throw new InvalidOperationException(why.Replace("ERROR:", "").Trim());
        }
    }
}
