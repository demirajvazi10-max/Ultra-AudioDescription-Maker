using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace UltraAudioDescriptionMaker.Core
{
    /// <summary>
    /// Downloads FFmpeg when the computer does not have it: a Windows 64-bit build from GitHub
    /// (BtbN FFmpeg-Builds), or from gyan.dev when GitHub cannot be reached. Only ffmpeg.exe and
    /// ffprobe.exe are kept, in %LOCALAPPDATA%\UltraAudioDescriptionMaker\Ffmpeg — no
    /// administrator rights are needed, and the program finds them there from then on.
    /// </summary>
    public static class FfmpegInstaller
    {
        public static string Folder => Path.Combine(AppPaths.DataDir, "Ffmpeg");

        private static readonly string[] Sources =
        {
            "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip",
            "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip"
        };

        /// <summary>Downloads and unpacks FFmpeg. Step 1 is the download (percent, megabytes), step 2 the unpacking.</summary>
        public static async Task InstallAsync(WorkProgress wp, CancellationToken ct)
        {
            Exception last = null;
            foreach (string url in Sources)
            {
                string zip = Path.Combine(AppPaths.TempDir, $"ffmpeg_{Guid.NewGuid():N}.zip");
                try
                {
                    wp?.SetStep(1, 2, Strings.L("wp_ff_download"));
                    await DownloadAsync(url, zip, wp, ct);
                    wp?.SetStep(2, 2, Strings.L("wp_ff_unpack"));
                    await Task.Run(() => Unpack(zip), ct);
                    AppPaths.ResetTools();
                    if (File.Exists(Path.Combine(Folder, "ffmpeg.exe")) && File.Exists(Path.Combine(Folder, "ffprobe.exe")))
                    {
                        AppLog.Info("FFmpeg downloaded from " + url);
                        return;
                    }
                    last = new InvalidOperationException("ffmpeg.exe was not in the download.");
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    last = ex;
                    AppLog.Error("Downloading FFmpeg from " + url, ex);
                }
                finally
                {
                    try { File.Delete(zip); } catch { }
                }
            }
            throw new InvalidOperationException(last?.Message ?? "download failed");
        }

        private static async Task DownloadAsync(string url, string target, WorkProgress wp, CancellationToken ct)
        {
            using var http = new HttpClient { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
            http.DefaultRequestHeaders.Add("User-Agent", "UltraAudioDescriptionMaker");
            using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();
            long total = resp.Content.Headers.ContentLength ?? 0;
            await using var src = await resp.Content.ReadAsStreamAsync(ct);
            await using var dst = File.Create(target);
            var buffer = new byte[1 << 16];
            long got = 0;
            int read, lastMb = -1;
            while ((read = await src.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
            {
                await dst.WriteAsync(buffer.AsMemory(0, read), ct);
                got += read;
                int mb = (int)(got / 1048576);
                if (mb != lastMb)
                {
                    lastMb = mb;
                    if (total > 0) wp?.SetPercent(got * 100.0 / total);
                    wp?.SetDetail(total > 0 ? Strings.LF("wp_ff_mb", mb, (int)(total / 1048576)) : Strings.LF("wp_ff_mb_only", mb));
                }
            }
        }

        private static void Unpack(string zip)
        {
            Directory.CreateDirectory(Folder);
            using var archive = ZipFile.OpenRead(zip);
            foreach (string exe in new[] { "ffmpeg.exe", "ffprobe.exe" })
            {
                var entry = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith("/bin/" + exe, StringComparison.OrdinalIgnoreCase))
                         ?? archive.Entries.FirstOrDefault(e => e.Name.Equals(exe, StringComparison.OrdinalIgnoreCase));
                if (entry == null) continue;
                string target = Path.Combine(Folder, exe);
                string part = target + ".part";
                entry.ExtractToFile(part, true);
                if (File.Exists(target)) File.Delete(target);
                File.Move(part, target);
            }
        }
    }
}
