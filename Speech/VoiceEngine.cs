using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UltraAudioDescriptionMaker.Core;

namespace UltraAudioDescriptionMaker.Speech
{
    /// <summary>A voice in the voice list.</summary>
    public class VoiceInfo
    {
        /// <summary>"ms:&lt;name&gt;" Microsoft natural, "gc:&lt;name&gt;" Google Cloud HD, "google" plain Google, "win:&lt;name&gt;" Windows voice.</summary>
        public string Tag;
        public string Display;
        public override string ToString() => Display;
    }

    /// <summary>
    /// Speaks descriptions into audio files, at a chosen speed. Every result is kept in the voice
    /// cache (same text + voice + speed = same file), so previewing, re-checking the timing and
    /// exporting never synthesize the same sentence twice.
    /// The Microsoft and Google Cloud voices speak faster themselves; the plain Google voice and
    /// the Windows voices are made faster afterwards with FFmpeg (atempo), without changing the pitch.
    /// </summary>
    public static class VoiceEngine
    {
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        private static readonly SemaphoreSlim _gate = new SemaphoreSlim(2);

        static VoiceEngine()
        {
            _http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
        }

        /// <summary>The voices for a language: Google HD (with a key), Microsoft natural, plain Google, then the installed Windows voices.</summary>
        public static List<VoiceInfo> Voices(string lang)
        {
            var list = new List<VoiceInfo>();
            if (!string.IsNullOrEmpty(NeuralTts.GoogleKey))
                foreach (var (tag, name) in NeuralTts.GoogleVoices(lang))
                    list.Add(new VoiceInfo { Tag = tag, Display = Strings.LF("voice_gc", name) });
            foreach (var (tag, name) in NeuralTts.MicrosoftVoices(lang))
                list.Add(new VoiceInfo { Tag = tag, Display = Strings.LF("voice_ms", name) });
            list.Add(new VoiceInfo { Tag = "google", Display = Strings.L("voice_google") });
            foreach (var (name, culture) in WindowsVoices())
                list.Add(new VoiceInfo { Tag = "win:" + name, Display = Strings.LF("voice_win", name, culture) });
            return list;
        }

        /// <summary>The voice to use for a language when none was chosen (or the chosen one is for another language).</summary>
        public static string DefaultVoice(string lang) => NeuralTts.MicrosoftVoices(lang)[0].Tag;

        /// <summary>Is this voice right for the language? (Windows voices and the plain Google voice always are.)</summary>
        public static bool Fits(string tag, string lang)
        {
            if (string.IsNullOrEmpty(tag)) return false;
            if (tag == "google" || tag.StartsWith("win:")) return true;
            return tag.Substring(3).StartsWith(Languages.Locale(lang).Substring(0, 2), StringComparison.OrdinalIgnoreCase);
        }

        private static List<(string Name, string Culture)> _windows;

        private static List<(string, string)> WindowsVoices()
        {
            if (_windows != null) return _windows;
            var list = new List<(string, string)>();
            try
            {
                using var s = new System.Speech.Synthesis.SpeechSynthesizer();
                foreach (var v in s.GetInstalledVoices().Where(v => v.Enabled))
                    list.Add((v.VoiceInfo.Name, v.VoiceInfo.Culture?.Name ?? ""));
            }
            catch (Exception ex) { AppLog.Error("Listing Windows voices", ex); }
            return _windows = list;
        }

        /// <summary>
        /// The spoken file for a text (from the cache when it was made before).
        /// <paramref name="ratePercent"/>: 0 = natural speed, +25 = a quarter faster.
        /// A natural voice that fails is replaced by the plain Google voice, so the work never stops on one line.
        /// </summary>
        public static async Task<string> SpeakAsync(string text, string voice, string lang, int ratePercent, CancellationToken ct)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0) return null;
            voice = string.IsNullOrEmpty(voice) ? DefaultVoice(lang) : voice;
            ratePercent = Math.Clamp(ratePercent, -50, 100);
            string key = Hash($"{voice}|{lang}|{ratePercent}|{text}");
            string ext = voice.StartsWith("win:") ? ".wav" : ".mp3";
            string path = Path.Combine(AppPaths.VoiceCacheDir, key + ext);
            if (File.Exists(path) && new FileInfo(path).Length > 200) return path;

            await _gate.WaitAsync(ct);
            try
            {
                if (File.Exists(path) && new FileInfo(path).Length > 200) return path;
                string tmp = path + ".part";
                bool tempoAfter = false;
                try
                {
                    if (voice.StartsWith("ms:") || voice.StartsWith("gc:"))
                    {
                        // Serbian is sent in Cyrillic letters: both services read Serbian Cyrillic most reliably
                        string say = lang == "sr" ? NeuralTts.SerbianToCyrillic(text) : text;
                        byte[] data = voice.StartsWith("ms:")
                            ? await NeuralTts.MicrosoftAsync(say, voice.Substring(3), ct, ratePercent)
                            : await NeuralTts.GoogleAsync(say, voice.Substring(3), NeuralTts.GoogleKey, ct, ratePercent);
                        await File.WriteAllBytesAsync(tmp, data, ct);
                    }
                    else if (voice.StartsWith("win:"))
                    {
                        await Task.Run(() => SpeakWindows(text, voice.Substring(4), tmp), ct);
                        tempoAfter = ratePercent != 0;
                    }
                    else
                    {
                        await File.WriteAllBytesAsync(tmp, await PlainGoogleAsync(text, lang, ct), ct);
                        tempoAfter = ratePercent != 0;
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex) when (voice != "google")
                {
                    AppLog.Error($"Voice '{voice}' failed, using the plain Google voice", ex);
                    await File.WriteAllBytesAsync(tmp, await PlainGoogleAsync(text, lang, ct), ct);
                    tempoAfter = ratePercent != 0;
                    if (ext == ".wav")
                    {
                        // the file is MP3 now
                        path = Path.ChangeExtension(path, ".mp3");
                    }
                }

                if (tempoAfter)
                {
                    string fast = path + ".tempo" + Path.GetExtension(path);
                    double tempo = Math.Clamp(1 + ratePercent / 100.0, 0.5, 2.0);
                    string codec = Path.GetExtension(path) == ".wav" ? "-c:a pcm_s16le" : "-c:a libmp3lame -q:a 3";
                    if (await Ffmpeg.RunAsync($"-nostdin -y -hide_banner -i \"{tmp}\" -filter:a \"atempo={Ffmpeg.N(tempo)}\" {codec} \"{fast}\"", ct) && File.Exists(fast))
                    {
                        File.Delete(tmp);
                        File.Move(fast, tmp);
                    }
                }
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
                return path;
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>A Windows (SAPI) voice speaking into a WAV file. Runs on a worker thread.</summary>
        private static void SpeakWindows(string text, string voiceName, string wavPath)
        {
            using var s = new System.Speech.Synthesis.SpeechSynthesizer();
            try { s.SelectVoice(voiceName); } catch (Exception ex) { AppLog.Error("Selecting the Windows voice " + voiceName, ex); }
            s.Rate = 0;
            s.SetOutputToWaveFile(wavPath);
            s.Speak(text);
            s.SetOutputToNull();
        }

        /// <summary>The plain (free) Google Translate voice.</summary>
        private static async Task<byte[]> PlainGoogleAsync(string text, string lang, CancellationToken ct)
        {
            using var ms = new MemoryStream();
            foreach (string chunk in Chunks(text, 190))
            {
                string url = $"https://translate.google.com/translate_tts?ie=UTF-8&q={Uri.EscapeDataString(chunk)}&tl={lang}&client=tw-ob";
                byte[] data = await _http.GetByteArrayAsync(url, ct);
                ms.Write(data, 0, data.Length);
            }
            return ms.ToArray();
        }

        private static IEnumerable<string> Chunks(string text, int max)
        {
            var sb = new StringBuilder();
            foreach (string w in (text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (sb.Length > 0 && sb.Length + 1 + w.Length > max) { yield return sb.ToString(); sb.Clear(); }
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(w);
            }
            if (sb.Length > 0) yield return sb.ToString();
        }

        /// <summary>Length of an audio file in seconds (NAudio; FFprobe when NAudio cannot read it).</summary>
        public static async Task<double> LengthAsync(string path, CancellationToken ct)
        {
            try
            {
                using var r = new NAudio.Wave.AudioFileReader(path);
                return r.TotalTime.TotalSeconds;
            }
            catch
            {
                return await Ffmpeg.DurationAsync(path, ct);
            }
        }

        private static string Hash(string s) => Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(s))).Substring(0, 24);

        /// <summary>Size of the voice cache in megabytes.</summary>
        public static double CacheMegabytes()
        {
            try { return new DirectoryInfo(AppPaths.VoiceCacheDir).GetFiles().Sum(f => f.Length) / 1048576.0; }
            catch { return 0; }
        }

        public static void ClearCache()
        {
            foreach (var f in new DirectoryInfo(AppPaths.VoiceCacheDir).GetFiles())
                try { f.Delete(); } catch { }
        }
    }
}
