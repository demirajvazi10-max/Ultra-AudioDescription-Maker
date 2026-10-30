using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace UltraAudioDescriptionMaker.Core
{
    /// <summary>
    /// One place for the external tools and the program's own folders.
    /// FFmpeg is looked for in this order: the folder chosen in Settings, next to the program
    /// (Ffmpeg\, ffmpeg\, tools\), PATH, the usual install folders, and finally the folder of an
    /// installed Ultra Video Editor, so a computer that already has the editor needs nothing more.
    /// </summary>
    public static class AppPaths
    {
        public static string BaseDir => AppDomain.CurrentDomain.BaseDirectory;

        private static string _ffmpeg;

        /// <summary>Forget the found FFmpeg (after the folder in Settings changed).</summary>
        public static void ResetTools() => _ffmpeg = null;

        public static string FFmpeg
        {
            get
            {
                if (_ffmpeg != null && File.Exists(_ffmpeg)) return _ffmpeg;
                _ffmpeg = FindTool("ffmpeg.exe");
                return _ffmpeg;
            }
        }

        /// <summary>ffprobe.exe, preferably from the same folder as ffmpeg.exe.</summary>
        public static string FFprobe
        {
            get
            {
                string sibling = Path.Combine(Path.GetDirectoryName(FFmpeg) ?? BaseDir, "ffprobe.exe");
                return File.Exists(sibling) ? sibling : FindTool("ffprobe.exe");
            }
        }

        public static bool FFmpegAvailable => File.Exists(FFmpeg) && File.Exists(FFprobe);

        private static string FindTool(string exe)
        {
            var candidates = new List<string>();
            string chosen = AppSettings.Current?.FfmpegFolder;
            if (!string.IsNullOrWhiteSpace(chosen))
            {
                candidates.Add(Path.Combine(chosen, exe));
                candidates.Add(Path.Combine(chosen, "bin", exe));
            }
            // downloaded by the program itself (Tools > Download FFmpeg, or automatically on the first start)
            candidates.Add(Path.Combine(DataDir, "Ffmpeg", exe));
            candidates.Add(Path.Combine(BaseDir, "Ffmpeg", exe));
            candidates.Add(Path.Combine(BaseDir, "ffmpeg", exe));
            candidates.Add(Path.Combine(BaseDir, "tools", exe));
            candidates.Add(Path.Combine(BaseDir, exe));

            string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (string dir in pathEnv.Split(';'))
            {
                if (string.IsNullOrWhiteSpace(dir)) continue;
                try { candidates.Add(Path.Combine(dir.Trim(), exe)); } catch { /* invalid PATH entry */ }
            }

            candidates.Add(Path.Combine(@"C:\ffmpeg\bin", exe));
            candidates.Add(Path.Combine(@"C:\Program Files\ffmpeg\bin", exe));
            candidates.Add(Path.Combine(@"C:\Program Files (x86)\ffmpeg\bin", exe));
            candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ffmpeg", "bin", exe));
            // a sibling Ultra program that already ships FFmpeg
            foreach (string pf in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                                          Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) })
            {
                if (string.IsNullOrEmpty(pf)) continue;
                candidates.Add(Path.Combine(pf, "Ultra Video Editor", "Ffmpeg", exe));
                candidates.Add(Path.Combine(pf, "UltraVideoEditor", "Ffmpeg", exe));
                candidates.Add(Path.Combine(pf, "Ultra Creative Suite", "Ffmpeg", exe));
            }

            foreach (string c in candidates)
                if (File.Exists(c)) return c;

            return Path.Combine(BaseDir, "Ffmpeg", exe);
        }

        /// <summary>%LOCALAPPDATA%\UltraAudioDescriptionMaker — settings, keys, logs, voice cache.</summary>
        public static string DataDir => Ensure(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UltraAudioDescriptionMaker"));

        public static string LogsDir => Ensure(Path.Combine(DataDir, "Logs"));

        /// <summary>Spoken descriptions are kept here, so the same sentence is never synthesized twice.</summary>
        public static string VoiceCacheDir => Ensure(Path.Combine(DataDir, "VoiceCache"));

        public static string TempDir => Ensure(Path.Combine(Path.GetTempPath(), "UltraAudioDescriptionMaker"));

        /// <summary>The data folder of Ultra Video Editor (its saved keys can be reused).</summary>
        public static string VideoEditorDataDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UltraVideoEditor");

        private static string Ensure(string dir)
        {
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    /// <summary>
    /// API keys, encrypted with Windows DPAPI for the current user ("&lt;name&gt;_key.bin").
    /// When this program has no key of its own, the key saved by Ultra Video Editor is used,
    /// so the Gemini and Google Cloud keys do not have to be typed in twice.
    /// </summary>
    public static class KeyStore
    {
        private static string FileFor(string dir, string name) => Path.Combine(dir, $"{name}_key.bin");

        public static string Load(string name) => LoadFrom(AppPaths.DataDir, name) ?? LoadFrom(AppPaths.VideoEditorDataDir, name);

        /// <summary>True when the key comes from Ultra Video Editor and not from this program.</summary>
        public static bool IsShared(string name) => LoadFrom(AppPaths.DataDir, name) == null && LoadFrom(AppPaths.VideoEditorDataDir, name) != null;

        private static string LoadFrom(string dir, string name)
        {
            try
            {
                string file = FileFor(dir, name);
                if (!File.Exists(file)) return null;
                byte[] dec = ProtectedData.Unprotect(File.ReadAllBytes(file), null, DataProtectionScope.CurrentUser);
                string key = Encoding.UTF8.GetString(dec).Trim();
                return key.Length == 0 ? null : key;
            }
            catch (Exception ex)
            {
                AppLog.Error($"Reading the saved key '{name}'", ex);
                return null;
            }
        }

        public static bool Save(string name, string key)
        {
            try
            {
                byte[] enc = ProtectedData.Protect(Encoding.UTF8.GetBytes((key ?? "").Trim()), null, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(FileFor(AppPaths.DataDir, name), enc);
                return true;
            }
            catch (Exception ex)
            {
                AppLog.Error($"Saving the key '{name}'", ex);
                return false;
            }
        }

        public static void Delete(string name)
        {
            try { File.Delete(FileFor(AppPaths.DataDir, name)); } catch { }
        }
    }

    /// <summary>
    /// A log file (%LOCALAPPDATA%\UltraAudioDescriptionMaker\Logs\uadm_yyyy-MM-dd.log).
    /// Nothing here is ever spoken: the log is for finding problems, not for interrupting the work.
    /// </summary>
    public static class AppLog
    {
        private static readonly object _lock = new();
        private static readonly Dictionary<string, DateTime> _lastSeen = new();

        public static string TodayFile => Path.Combine(AppPaths.LogsDir, $"uadm_{DateTime.Now:yyyy-MM-dd}.log");

        public static void Error(string context, Exception ex) =>
            Write("ERROR", $"{context}: {ex?.GetType().Name}: {ex?.Message}", ex?.ToString());

        public static void Warn(string message) => Write("WARN", message, null);

        public static void Info(string message) => Write("INFO", message, null);

        private static void Write(string level, string message, string details)
        {
            lock (_lock)
            {
                // the same message within 30 seconds is written once (some happen in loops)
                if (_lastSeen.TryGetValue(message, out var last) && (DateTime.Now - last).TotalSeconds < 30) return;
                _lastSeen[message] = DateTime.Now;
                if (_lastSeen.Count > 500) _lastSeen.Clear();
                try
                {
                    var sb = new StringBuilder();
                    sb.Append($"[{DateTime.Now:HH:mm:ss}] {level} {message}").AppendLine();
                    if (!string.IsNullOrEmpty(details) && level == "ERROR") sb.AppendLine(details);
                    File.AppendAllText(TodayFile, sb.ToString(), Encoding.UTF8);
                }
                catch { /* logging must never break the program */ }
            }
        }
    }
}
