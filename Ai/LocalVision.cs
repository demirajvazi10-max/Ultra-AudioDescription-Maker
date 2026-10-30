using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UltraAudioDescriptionMaker.Core;

namespace UltraAudioDescriptionMaker.Ai
{
    /// <summary>
    /// Describing single pictures without sending the video anywhere: a local vision model in
    /// Ollama (Qwen-VL, LLaVA, Moondream), as in the rest of the Ultra suite. The small models
    /// describe best in English, so the sentence is translated afterwards (Gemini when a key is
    /// saved, otherwise the free Google Translate service). With no Ollama model but a Gemini key,
    /// Gemini looks at the picture instead.
    /// </summary>
    public static class LocalVision
    {
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(180) };
        private static readonly string[] ModelPreference = { "qwen3-vl", "qwen2.5vl", "qwen2.5-vl", "qwen2-vl", "llava", "minicpm-v", "moondream" };
        private static string _model;

        /// <summary>The installed vision model, or null when none is installed / Ollama is not running.</summary>
        public static async Task<string> FindModelAsync()
        {
            if (_model != null) return _model;
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                string body = await client.GetStringAsync("http://localhost:11434/api/tags");
                var names = (JObject.Parse(body)["models"] as JArray)?
                            .Select(m => m["name"]?.ToString() ?? "").ToList() ?? new List<string>();
                foreach (string pref in ModelPreference)
                {
                    var hits = names.Where(n => n.StartsWith(pref, StringComparison.OrdinalIgnoreCase)).ToList();
                    string hit = hits.FirstOrDefault(n => !n.Contains("thinking", StringComparison.OrdinalIgnoreCase)) ?? hits.FirstOrDefault();
                    if (hit != null) return _model = hit;
                }
            }
            catch { }
            return null;
        }

        public static async Task<bool> AvailableAsync() => await FindModelAsync() != null || GeminiClient.HasKey;

        private static string Prompt(string language) =>
            "You write audio description for blind viewers of a film or video. Write ONE short, complete sentence " +
            "(5 to 12 words, present tense) that says who is there and what happens. Always finish the sentence. " +
            "If the picture is mostly text (a title, a sign, credits), just read the text. Do not mention the camera, " +
            $"the shot, the image or the frame. No introduction. Answer only in {language}.";

        /// <summary>One short sentence for viewers about the frame at <paramref name="time"/>, in <paramref name="lang"/>. Null on failure.</summary>
        public static async Task<string> DescribeAsync(string video, double time, string lang, CancellationToken ct)
        {
            string frame = await Ffmpeg.FrameAsync(video, time, ct, 448);
            if (frame == null) return null;
            try
            {
                string b64 = Convert.ToBase64String(await File.ReadAllBytesAsync(frame, ct));
                string model = await FindModelAsync();
                if (model == null)
                {
                    if (!GeminiClient.HasKey) throw new InvalidOperationException("no_model");
                    string g = await GeminiClient.AskAsync(Prompt(Languages.PromptName(lang)), b64, ct);
                    return Clean(g, lang);
                }
                var request = new
                {
                    model,
                    prompt = Prompt("English"),
                    images = new[] { b64 },
                    stream = false,
                    keep_alive = "30m",
                    options = new { temperature = 0.1, num_predict = 120, num_ctx = 4096 }
                };
                using var resp = await _http.PostAsync("http://localhost:11434/api/generate",
                    new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json"), ct);
                if (!resp.IsSuccessStatusCode) return null;
                string english = Clean(JObject.Parse(await resp.Content.ReadAsStringAsync(ct))["response"]?.ToString(), "en");
                if (string.IsNullOrWhiteSpace(english) || lang == "en") return english;
                return Clean(await TranslateAsync(english, lang, ct), lang) ?? english;
            }
            finally
            {
                try { File.Delete(frame); } catch { }
            }
        }

        /// <summary>Translates an English description: Gemini when there is a key, otherwise free Google Translate.</summary>
        public static async Task<string> TranslateAsync(string english, string lang, CancellationToken ct)
        {
            if (GeminiClient.HasKey)
            {
                try
                {
                    string g = await GeminiClient.AskAsync(
                        $"Translate this audio description sentence into {Languages.PromptName(lang)}. Keep it short, natural and " +
                        $"in the present tense. Reply with the translation only.\n\n{english}", null, ct);
                    if (!string.IsNullOrWhiteSpace(g)) return g;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex) { AppLog.Error("Gemini translation", ex); }
            }
            try
            {
                string tl = lang == "sr" ? "sr-Latn" : lang;
                string url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl=en&tl={tl}&dt=t&q={Uri.EscapeDataString(english)}";
                string json = await _http.GetStringAsync(url, ct);
                var arr = JArray.Parse(json)[0] as JArray;
                string t = arr == null ? null : string.Concat(arr.Select(x => x[0]?.ToString()));
                return string.IsNullOrWhiteSpace(t) ? english : t;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex) { AppLog.Error("Google Translate", ex); return english; }
        }

        /// <summary>One clean sentence: no quotes, no "The image shows", no camera words; Serbian in Latin letters.</summary>
        public static string Clean(string text, string lang)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            text = Regex.Replace(text, @"\s+", " ").Trim().Trim('"', '“', '”', '„', '*');
            text = Regex.Replace(text, @"^(in\s+)?(this|the)\s+(image|picture|frame|shot|scene)\s*(shows|depicts|we see)?,?\s*", "", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"^(an?\s+)?(close[- ]?up|wide|medium|long)\s+(shot\s+)?(of|shows|showing)\s+", "", RegexOptions.IgnoreCase);
            if (text.Length > 0) text = char.ToUpper(text[0]) + text.Substring(1);
            if (lang == "sr") text = ScriptWriter.SerbianLatin(text);
            if (text.Length > 0 && !".!?…".Contains(text[^1])) text += ".";
            return text;
        }
    }
}
