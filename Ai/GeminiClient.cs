using System;
using System.Collections.Generic;
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
    /// Google Gemini, with the user's own key from Google AI Studio (the Flash models have a free tier).
    /// Gemini watches the whole video, picture and sound together, and writes the descriptions.
    /// The model is chosen from the key's own model list (the newest general "flash" model), so
    /// the program keeps working when Google renames or retires models.
    /// Ported from Ultra Video Editor.
    /// </summary>
    public static class GeminiClient
    {
        public const string KeyName = "gemini";
        public static string Key => KeyStore.Load(KeyName);
        /// <summary>Forget the chosen model (after the key changed).</summary>
        public static void Reset() { _model = null; _modelForKey = null; }
        public static bool HasKey => !string.IsNullOrEmpty(Key);

        private const string Base = "https://generativelanguage.googleapis.com/v1beta/";
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        private static string _model;
        private static string _modelForKey;

        public static string LanguageName(string lang) => Languages.PromptName(lang);

        /// <summary>The newest general Flash model this key can use (not lite, live, TTS, image or preview when a stable one exists).</summary>
        private static async Task<string> ModelAsync(string key, CancellationToken ct)
        {
            if (_model != null && _modelForKey == key) return _model;
            string chosen = "gemini-flash-latest";
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, Base + "models?pageSize=200");
                req.Headers.Add("x-goog-api-key", key);
                using var resp = await _http.SendAsync(req, ct);
                if (resp.IsSuccessStatusCode)
                {
                    var models = (JObject.Parse(await resp.Content.ReadAsStringAsync(ct))["models"] as JArray) ?? new JArray();
                    var flash = models
                        .Where(m => (m["supportedGenerationMethods"] as JArray)?.Any(x => x.ToString() == "generateContent") == true)
                        .Select(m => m["name"]?.ToString()?.Replace("models/", ""))
                        .Where(n => n != null && Regex.IsMatch(n, @"^gemini-[\d.]+-flash(-\d+)?$"))
                        .Select(n => (Name: n, Version: ParseVersion(Regex.Match(n, @"gemini-([\d.]+)-").Groups[1].Value)))
                        .OrderByDescending(x => x.Version).ToList();
                    if (flash.Count > 0) chosen = flash[0].Name;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex) { AppLog.Error("Listing the Gemini models", ex); }
            _modelForKey = key;
            AppLog.Info($"Gemini model: {chosen}");
            return _model = chosen;
        }

        private static Version ParseVersion(string v)
        {
            v = (v ?? "").Trim('.');
            if (!v.Contains('.')) v += ".0";
            return Version.TryParse(v, out var parsed) ? parsed : new Version(0, 0);
        }

        /// <summary>Asks Gemini (text, and a JPEG picture when given). Busy (429) is retried a few times.</summary>
        public static Task<string> AskAsync(string prompt, string jpegBase64, CancellationToken ct, string key = null)
        {
            var parts = new List<object> { new { text = prompt } };
            if (jpegBase64 != null) parts.Add(new { inline_data = new { mime_type = "image/jpeg", data = jpegBase64 } });
            return SendAsync(parts, new { temperature = 0.2, maxOutputTokens = 1024 }, _http, ct, key);
        }

        private static async Task<string> SendAsync(List<object> parts, object generationConfig, HttpClient http, CancellationToken ct, string key = null)
        {
            key ??= Key;
            if (string.IsNullOrEmpty(key)) throw new InvalidOperationException("No Gemini key.");
            string model = await ModelAsync(key, ct);
            var body = new { contents = new[] { new { parts } }, generationConfig };
            string json = JsonConvert.SerializeObject(body);

            for (int attempt = 0; ; attempt++)
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, $"{Base}models/{model}:generateContent")
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                req.Headers.Add("x-goog-api-key", key);
                using var resp = await http.SendAsync(req, ct);
                string text = await resp.Content.ReadAsStringAsync(ct);
                if ((int)resp.StatusCode == 429 && attempt < 3)
                {
                    // the free tier allows a limited number of requests a minute: wait and try again
                    await Task.Delay(TimeSpan.FromSeconds(15 * (attempt + 1)), ct);
                    continue;
                }
                if ((int)resp.StatusCode == 404 && attempt == 0 && model != "gemini-flash-latest")
                {
                    model = _model = "gemini-flash-latest";   // the chosen model went away
                    continue;
                }
                if (!resp.IsSuccessStatusCode)
                {
                    string msg = null;
                    try { msg = JObject.Parse(text)["error"]?["message"]?.ToString(); } catch { }
                    throw new InvalidOperationException("Gemini: " + (msg ?? resp.StatusCode.ToString()));
                }
                var answer = JObject.Parse(text)["candidates"]?[0]?["content"]?["parts"] as JArray;
                string result = answer == null ? null
                    : string.Concat(answer.Where(p => p["thought"]?.Value<bool>() != true).Select(p => p["text"]?.ToString()));
                return result?.Trim();
            }
        }

        // ── whole video ───────────────────────────────────────────────

        /// <summary>For long requests (a whole video): no fixed time-out, the Stop button cancels.</summary>
        private static readonly HttpClient _longHttp = new HttpClient { Timeout = System.Threading.Timeout.InfiniteTimeSpan };

        /// <summary>
        /// Uploads a video to the Gemini File API (resumable upload) and waits until Google has
        /// processed it. Returns the file's name ("files/…", for deleting) and URI (for asking).
        /// <paramref name="progress"/> gets "sent/total" while uploading.
        /// </summary>
        public static async Task<(string Name, string Uri)> UploadVideoAsync(string path, Action<string> progress, CancellationToken ct, Action processing = null)
        {
            string key = Key;
            if (string.IsNullOrEmpty(key)) throw new InvalidOperationException("No Gemini key.");
            long size = new System.IO.FileInfo(path).Length;

            using var start = new HttpRequestMessage(HttpMethod.Post, "https://generativelanguage.googleapis.com/upload/v1beta/files")
            {
                Content = new StringContent(JsonConvert.SerializeObject(new { file = new { display_name = System.IO.Path.GetFileName(path) } }),
                                            Encoding.UTF8, "application/json")
            };
            start.Headers.Add("x-goog-api-key", key);
            start.Headers.Add("X-Goog-Upload-Protocol", "resumable");
            start.Headers.Add("X-Goog-Upload-Command", "start");
            start.Headers.Add("X-Goog-Upload-Header-Content-Length", size.ToString(System.Globalization.CultureInfo.InvariantCulture));
            start.Headers.Add("X-Goog-Upload-Header-Content-Type", "video/mp4");
            using var startResp = await _longHttp.SendAsync(start, ct);
            if (!startResp.IsSuccessStatusCode || !startResp.Headers.TryGetValues("X-Goog-Upload-URL", out var urls))
                throw new InvalidOperationException("Gemini: the upload could not start (" + (int)startResp.StatusCode + " " +
                                                    await startResp.Content.ReadAsStringAsync(ct) + ")");
            string uploadUrl = urls.First();

            await using var file = System.IO.File.OpenRead(path);
            var content = new ProgressContent(file, size, sent => progress?.Invoke($"{sent * 100 / Math.Max(1, size)}/100"));
            content.Headers.ContentLength = size;
            using var up = new HttpRequestMessage(HttpMethod.Post, uploadUrl) { Content = content };
            up.Headers.Add("X-Goog-Upload-Offset", "0");
            up.Headers.Add("X-Goog-Upload-Command", "upload, finalize");
            using var upResp = await _longHttp.SendAsync(up, ct);
            string upJson = await upResp.Content.ReadAsStringAsync(ct);
            if (!upResp.IsSuccessStatusCode) throw new InvalidOperationException("Gemini: the upload failed (" + upJson + ")");
            var f = JObject.Parse(upJson)["file"];
            string name = f?["name"]?.ToString(), uri = f?["uri"]?.ToString(), state = f?["state"]?.ToString();

            // Google prepares the video before it can be used (a few seconds to a few minutes)
            if (state != "ACTIVE") processing?.Invoke();
            for (int i = 0; state != "ACTIVE" && i < 360; i++)
            {
                if (state == "FAILED") throw new InvalidOperationException("Gemini could not process the video.");
                await Task.Delay(5000, ct);
                using var get = new HttpRequestMessage(HttpMethod.Get, Base + name);
                get.Headers.Add("x-goog-api-key", key);
                using var getResp = await _longHttp.SendAsync(get, ct);
                if (getResp.IsSuccessStatusCode) state = JObject.Parse(await getResp.Content.ReadAsStringAsync(ct))["state"]?.ToString();
            }
            if (state != "ACTIVE") throw new InvalidOperationException("Gemini did not finish processing the video in time.");
            return (name, uri);
        }

        /// <summary>Deletes an uploaded file (Google would delete it after 48 hours anyway).</summary>
        public static async Task DeleteFileAsync(string name)
        {
            try
            {
                using var del = new HttpRequestMessage(HttpMethod.Delete, Base + name);
                del.Headers.Add("x-goog-api-key", Key);
                using var _ = await _longHttp.SendAsync(del);
            }
            catch (Exception ex) { AppLog.Error("Deleting the uploaded video from Gemini", ex); }
        }

        /// <summary>
        /// How closely Gemini looks at the video, and so how fast it answers:
        /// Fast = 1 picture a second, low detail; Balanced = 2 a second, low detail (the default);
        /// Precise = 2 a second, full detail (slow; long films still use low detail).
        /// </summary>
        public static (double Fps, bool Low) Quality(string quality, double total) => quality switch
        {
            "Fast" => (1, true),
            "Precise" => (2, total > 1200),
            _ => (2, true)
        };

        /// <summary>The video part of a request: a YouTube link, an uploaded file, or (for a small video) the video itself.</summary>
        private static object VideoPart(string fileUri, byte[] inline, double from, double to, double fps)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var meta = new { start_offset = Math.Floor(from).ToString(inv) + "s", end_offset = Math.Ceiling(to).ToString(inv) + "s", fps };
            if (inline != null)
                return new { inline_data = new { mime_type = "video/mp4", data = Convert.ToBase64String(inline) }, video_metadata = meta };
            // a YouTube link is given as it is (Gemini watches it on YouTube); an uploaded file is an MP4
            return new
            {
                file_data = IsYouTube(fileUri) ? (object)new { file_uri = fileUri } : new { mime_type = "video/mp4", file_uri = fileUri },
                video_metadata = meta
            };
        }

        /// <summary>
        /// The newer Flash models "think" before they write, which with a video can take minutes.
        /// Describing needs only a little thinking: it is limited here. A model that does not know the
        /// setting answers with an error, and the request is then sent again without it.
        /// </summary>
        private static Dictionary<string, object> VideoConfig(string model, bool low, bool limitThinking)
        {
            var c = new Dictionary<string, object> { ["temperature"] = 0.3, ["maxOutputTokens"] = 16384 };
            if (low) c["mediaResolution"] = "MEDIA_RESOLUTION_LOW";
            if (limitThinking)
            {
                if (Regex.IsMatch(model ?? "", @"^gemini-[3-9]")) c["thinkingConfig"] = new { thinkingLevel = "low" };
                else c["thinkingConfig"] = new { thinkingBudget = 512 };
            }
            return c;
        }

        private static bool IsThinkingError(string message) =>
            message != null && message.IndexOf("thinking", StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>Asks about a part of a video (seconds from its start), without streaming.</summary>
        public static async Task<string> AskVideoAsync(string fileUri, double from, double to, bool lowResolution, string prompt,
                                                       CancellationToken ct, double fps = 2, byte[] inline = null)
        {
            var parts = new List<object> { VideoPart(fileUri, inline, from, to, fps), new { text = prompt } };
            string model = await ModelAsync(Key, ct);
            try { return await SendAsync(parts, VideoConfig(model, lowResolution, true), _longHttp, ct); }
            catch (InvalidOperationException ex) when (IsThinkingError(ex.Message))
            {
                return await SendAsync(parts, VideoConfig(model, lowResolution, false), _longHttp, ct);
            }
        }

        /// <summary>
        /// Like <see cref="AskVideoAsync"/>, but Gemini sends its answer while writing it (streaming),
        /// so the progress is real: <paramref name="onText"/> gets the whole text so far after every piece.
        /// <paramref name="onState"/> gets "busy|seconds" while Google is busy and the request waits to be
        /// sent again, and "retry" when the connection went quiet and the request is sent again.
        /// When nothing at all arrives for 5 minutes, the request is given up and sent once more.
        /// </summary>
        public static async Task<string> AskVideoStreamAsync(string fileUri, double from, double to, bool lowResolution, string prompt,
                                                             Action<string> onText, Action<string> onState, CancellationToken ct,
                                                             double fps = 2, byte[] inline = null)
        {
            var parts = new List<object> { VideoPart(fileUri, inline, from, to, fps), new { text = prompt } };
            string key = Key;
            if (string.IsNullOrEmpty(key)) throw new InvalidOperationException("No Gemini key.");
            string model = await ModelAsync(key, ct);
            bool limitThinking = true;
            string json = JsonConvert.SerializeObject(new { contents = new[] { new { parts } }, generationConfig = VideoConfig(model, lowResolution, limitThinking) });

            int quietRetries = 0;
            for (int attempt = 0; ; attempt++)
            {
                using var watchdog = CancellationTokenSource.CreateLinkedTokenSource(ct);
                watchdog.CancelAfter(TimeSpan.FromMinutes(5));
                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Post, $"{Base}models/{model}:streamGenerateContent?alt=sse")
                    {
                        Content = new StringContent(json, Encoding.UTF8, "application/json")
                    };
                    req.Headers.Add("x-goog-api-key", key);
                    using var resp = await _longHttp.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, watchdog.Token);
                    if ((int)resp.StatusCode == 429 && attempt < 4)
                    {
                        int wait = 15 * (attempt + 1);
                        onState?.Invoke("busy|" + wait);
                        await Task.Delay(TimeSpan.FromSeconds(wait), ct);
                        continue;
                    }
                    if ((int)resp.StatusCode == 404 && attempt == 0 && model != "gemini-flash-latest")
                    {
                        model = _model = "gemini-flash-latest";
                        continue;
                    }
                    if (!resp.IsSuccessStatusCode)
                    {
                        string body = await resp.Content.ReadAsStringAsync(ct);
                        string msg = null;
                        try { msg = JArray.Parse(body)[0]?["error"]?["message"]?.ToString(); } catch { }
                        try { msg ??= JObject.Parse(body)["error"]?["message"]?.ToString(); } catch { }
                        if (limitThinking && (int)resp.StatusCode == 400 && IsThinkingError(msg ?? body))
                        {
                            // this model does not take the thinking setting: the same request without it
                            limitThinking = false;
                            json = JsonConvert.SerializeObject(new { contents = new[] { new { parts } }, generationConfig = VideoConfig(model, lowResolution, false) });
                            continue;
                        }
                        throw new InvalidOperationException("Gemini: " + (msg ?? resp.StatusCode.ToString()));
                    }

                    var text = new StringBuilder();
                    using var stream = await resp.Content.ReadAsStreamAsync(watchdog.Token);
                    using var reader = new System.IO.StreamReader(stream, Encoding.UTF8);
                    while (true)
                    {
                        string line = await reader.ReadLineAsync(watchdog.Token);
                        if (line == null) break;
                        watchdog.CancelAfter(TimeSpan.FromMinutes(5));   // something arrived: the clock starts again
                        if (!line.StartsWith("data:")) continue;
                        JObject chunk;
                        try { chunk = JObject.Parse(line.Substring(5).Trim()); } catch { continue; }
                        if (chunk["error"] != null) throw new InvalidOperationException("Gemini: " + chunk["error"]?["message"]);
                        var pieces = chunk["candidates"]?[0]?["content"]?["parts"] as JArray;
                        if (pieces == null) continue;
                        bool grew = false;
                        foreach (var piece in pieces)
                        {
                            if (piece["thought"]?.Value<bool>() == true) continue;
                            string t = piece["text"]?.ToString();
                            if (!string.IsNullOrEmpty(t)) { text.Append(t); grew = true; }
                        }
                        if (grew) onText?.Invoke(text.ToString());
                    }
                    return text.ToString().Trim();
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested && quietRetries < 1)
                {
                    // 5 minutes without a single byte: the connection is stuck, ask once more
                    quietRetries++;
                    AppLog.Warn("Gemini went quiet for 5 minutes; sending the request again.");
                    onState?.Invoke("retry");
                }
            }
        }

        /// <summary>A public YouTube video that Gemini can watch directly from its link.</summary>
        public static bool IsYouTube(string url) =>
            !string.IsNullOrEmpty(url) && Regex.IsMatch(url, @"^https?://(www\.|m\.)?(youtube\.com/(watch|shorts|live)|youtu\.be/)", RegexOptions.IgnoreCase);

        /// <summary>Upload content that reports how much has been sent.</summary>
        private sealed class ProgressContent : HttpContent
        {
            private readonly System.IO.Stream _source;
            private readonly long _size;
            private readonly Action<long> _progress;
            public ProgressContent(System.IO.Stream source, long size, Action<long> progress) { _source = source; _size = size; _progress = progress; }

            protected override async Task SerializeToStreamAsync(System.IO.Stream stream, System.Net.TransportContext context)
            {
                var buffer = new byte[1 << 20];
                long sent = 0;
                int read;
                while ((read = await _source.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await stream.WriteAsync(buffer, 0, read);
                    sent += read;
                    _progress?.Invoke(sent);
                }
            }

            protected override bool TryComputeLength(out long length) { length = _size; return true; }
        }

        /// <summary>A quick test of a key. Returns null when it works, otherwise the reason.</summary>
        public static async Task<string> TestKeyAsync(string key, CancellationToken ct)
        {
            try
            {
                _model = null;
                string r = await AskAsync("Reply with the single word: OK", null, ct, key);
                return string.IsNullOrWhiteSpace(r) ? "no answer" : null;
            }
            catch (Exception ex) { return ex.Message; }
        }
    }
}
