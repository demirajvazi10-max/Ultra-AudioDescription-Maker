package com.demirajvazi.ultraad

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.delay
import kotlinx.coroutines.ensureActive
import kotlinx.coroutines.withContext
import org.json.JSONArray
import org.json.JSONObject
import java.io.BufferedReader
import java.io.InputStreamReader
import java.net.HttpURLConnection
import java.net.URL
import java.net.URLEncoder
import kotlin.coroutines.coroutineContext

/**
 * Google Gemini with the user's own key. Gemini watches a public YouTube video directly from its
 * link and writes the synchronized description script — the same prompt as the desktop program:
 * every description starts at the exact moment of its action. The answer streams in while Gemini
 * writes, so the progress is real.
 */
object Gemini {
    private const val BASE = "https://generativelanguage.googleapis.com/v1beta/"
    private var model: String? = null
    private var modelFor: String? = null

    class GeminiException(message: String, val status: Int = 0) : Exception(message)

    private fun open(url: String, key: String, method: String = "GET"): HttpURLConnection =
        (URL(url).openConnection() as HttpURLConnection).apply {
            requestMethod = method
            setRequestProperty("x-goog-api-key", key)
            connectTimeout = 20000
            readTimeout = 5 * 60 * 1000   // nothing at all for 5 minutes: the connection is stuck
        }

    /** The newest general "flash" model this key can use (keeps working when Google renames models). */
    private suspend fun model(key: String): String = withContext(Dispatchers.IO) {
        model?.takeIf { modelFor == key }?.let { return@withContext it }
        var chosen = "gemini-flash-latest"
        try {
            val c = open(BASE + "models?pageSize=200", key)
            if (c.responseCode == 200) {
                val arr = JSONObject(c.inputStream.bufferedReader().readText()).optJSONArray("models") ?: JSONArray()
                val names = (0 until arr.length()).map { arr.getJSONObject(it) }
                    .filter { m -> (m.optJSONArray("supportedGenerationMethods")?.toString() ?: "").contains("generateContent") }
                    .map { it.optString("name").removePrefix("models/") }
                    .filter { Regex("^gemini-[\\d.]+-flash(-\\d+)?$").matches(it) }
                    .sortedByDescending { it.split('-')[1].toDoubleOrNull() ?: 0.0 }
                if (names.isNotEmpty()) chosen = names[0]
            }
        } catch (e: Exception) { }
        modelFor = key
        model = chosen
        chosen
    }

    private fun errorMessage(c: HttpURLConnection): String = try {
        val t = (c.errorStream ?: c.inputStream).bufferedReader().readText()
        val j = if (t.trim().startsWith("[")) JSONArray(t).getJSONObject(0) else JSONObject(t)
        j.optJSONObject("error")?.optString("message") ?: "HTTP ${c.responseCode}"
    } catch (e: Exception) { "HTTP ${c.responseCode}" }

    /** A quick test of a key: null when it works, otherwise the reason. */
    suspend fun testKey(key: String): String? = withContext(Dispatchers.IO) {
        try {
            model = null
            val m = model(key)
            val c = open("${BASE}models/$m:generateContent", key, "POST")
            c.doOutput = true
            c.setRequestProperty("content-type", "application/json")
            val body = JSONObject().put("contents", JSONArray().put(JSONObject().put("parts", JSONArray().put(JSONObject().put("text", "Reply with the single word: OK")))))
                .put("generationConfig", JSONObject().put("maxOutputTokens", 512))
            c.outputStream.use { it.write(body.toString().toByteArray()) }
            if (c.responseCode == 200) null else errorMessage(c)
        } catch (e: Exception) { e.message ?: "error" }
    }

    /** The title of a YouTube video (for the list, and to recognise it in the YouTube app). */
    suspend fun youtubeTitle(url: String): String? = withContext(Dispatchers.IO) {
        try {
            val c = URL("https://www.youtube.com/oembed?format=json&url=" + URLEncoder.encode(url, "UTF-8")).openConnection() as HttpURLConnection
            c.connectTimeout = 10000; c.readTimeout = 10000
            if (c.responseCode == 200) JSONObject(c.inputStream.bufferedReader().readText()).optString("title").takeIf { it.isNotBlank() } else null
        } catch (e: Exception) { null }
    }

    private fun prompt(lang: String, total: Double?, from: Double, to: Double?, before: String, density: String): String {
        val dens = when (density) {
            "Essential" -> "- Describe only what a blind viewer cannot follow without: who appears, where we are, important actions, and text on screen."
            "Detailed" -> "- Describe every visible change that matters: a new place, a new person, every important action, gestures and expressions — about every 5 to 8 seconds when the picture changes."
            else -> "- Describe the important changes: a new place, a new person, important actions and expressions. Do not leave the picture undescribed for more than about 15 seconds when something new is seen."
        }
        val part = if (total != null && to != null)
            "The whole video is ${Langs.clock(total)} long; describe only the part from ${Langs.clock(from)} to ${Langs.clock(to)}."
        else "Describe the whole video."
        return buildString {
            appendLine("You are a professional audio describer writing audio description like on Netflix, for blind and visually impaired viewers.")
            appendLine("Watch this video WITH its sound. $part")
            appendLine()
            appendLine("TIMING — the most important rule:")
            appendLine("- Each description starts at the EXACT moment the thing it describes appears or happens on screen. Never before it (that would spoil it), never after it (the viewer would hear it too late).")
            appendLine("- Give each time with tenths of a second (MM:SS.s), at the moment the action begins or the new picture appears.")
            appendLine("- Describe an action while it is happening, in the present tense.")
            appendLine("- Keep each description short enough to be spoken before the next thing happens and, wherever possible, before someone speaks: about 2.5 words per second of free time. When almost no time is free, use only the most important words (\"Ana runs out.\").")
            appendLine()
            appendLine("CONTENT:")
            appendLine("- Describe what a blind viewer needs to follow it: who is there, what they do, where, important changes, facial expressions and gestures that matter, and any text on screen (titles, signs, messages, credits) — read that text out.")
            appendLine(dens)
            appendLine("- Do not describe what can be heard (the music, what is said). Do not interpret or guess feelings that are not shown.")
            appendLine("- No camera words (shot, close-up, frame, camera). Do not repeat what is still true; describe only changes. Complete sentences only.")
            appendLine("- Once a character's name is clear from the dialogue or the screen, use it; until then use a short consistent description (\"the girl in the red coat\").")
            appendLine("- Write in ${Langs.promptName(lang)}.")
            appendLine()
            appendLine("DIALOGUE MAP: also list every stretch where someone speaks or sings, so the program knows where the dialogue is.")
            appendLine()
            appendLine("Times are measured from the START OF THE WHOLE VIDEO, in minutes:seconds.tenths (minutes may go past 59, for example 95:20.5).")
            if (before.isNotBlank()) {
                appendLine("The descriptions just before this part were (keep the same names, do not repeat them):")
                appendLine(before)
            }
            appendLine("Answer ONLY with lines in these two forms, nothing else:")
            appendLine("MM:SS.s | description")
            appendLine("SPEECH MM:SS.s-MM:SS.s")
        }
    }

    private fun time(s: String): Double? =
        Regex("(?:(\\d{1,2}):)?(\\d{1,3}):(\\d{1,2}(?:[.,]\\d+)?)").find(s)?.let { Scripts.parseTime(it.value) }

    /** Reads "MM:SS.s | text" and "SPEECH MM:SS.s-MM:SS.s" lines. */
    fun parse(answer: String, from: Double, to: Double?, lang: String): Pair<MutableList<Line>, MutableList<Pair<Double, Double>>> {
        var lines = mutableListOf<Line>()
        var speech = mutableListOf<Pair<Double, Double>>()
        for (raw0 in answer.replace("\r", "").split('\n')) {
            val raw = raw0.replace("*", "").replace("`", "").trim().trimStart('-', ' ', '•')
            if (raw.length < 4) continue
            val sp = Regex("^SPEECH\\s*:?\\s*\\[?([\\d:.,]+)\\]?\\s*[-–—]+\\s*\\[?([\\d:.,]+)\\]?", RegexOption.IGNORE_CASE).find(raw)
            if (sp != null) {
                val a = time(sp.groupValues[1]); val b = time(sp.groupValues[2])
                if (a != null && b != null && b > a) speech.add(a to b)
                continue
            }
            val m = Regex("^\\[?([\\d:.,]+)\\]?\\s*[|\\-–—:]\\s*(.+)$").find(raw) ?: continue
            val t = time(m.groupValues[1]) ?: continue
            var text = m.groupValues[2].trim().trim('"', '“', '”', '„')
            if (text.length < 2) continue
            if (lang == "sr") text = Langs.serbianLatin(text)
            lines.add(Line(Math.round(t * 10) / 10.0, text))
        }
        // times counted from the part instead of the whole video
        if (from > 0 && to != null && lines.isNotEmpty() && lines.all { it.t < to - from + 1 } && lines.any { it.t < from - 1 }) {
            lines = lines.map { it.copy(t = it.t + from) }.toMutableList()
            speech = speech.map { (it.first + from) to (it.second + from) }.toMutableList()
        }
        val hi = (to ?: Double.MAX_VALUE) + 1
        return lines.filter { it.t >= from - 1 && it.t <= hi }.sortedBy { it.t }.toMutableList() to speech
    }

    /** One streamed request; onText gets the whole text so far. Busy (429) is waited out and sent again. */
    private suspend fun stream(key: String, request: JSONObject, onText: (String) -> Unit, onBusy: (Int) -> Unit): String {
        var m = model(key)
        for (attempt in 0..4) {
            coroutineContext.ensureActive()
            val result = withContext(Dispatchers.IO) {
                val c = open("${BASE}models/$m:streamGenerateContent?alt=sse", key, "POST")
                c.doOutput = true
                c.setRequestProperty("content-type", "application/json")
                c.outputStream.use { it.write(request.toString().toByteArray()) }
                when {
                    c.responseCode == 429 -> "\u0000busy"
                    c.responseCode == 404 && m != "gemini-flash-latest" -> "\u0000model"
                    c.responseCode != 200 -> throw GeminiException(errorMessage(c), c.responseCode)
                    else -> {
                        val text = StringBuilder()
                        BufferedReader(InputStreamReader(c.inputStream, Charsets.UTF_8)).use { r ->
                            while (true) {
                                coroutineContext.ensureActive()
                                val line = r.readLine() ?: break
                                if (!line.startsWith("data:")) continue
                                val chunk = try { JSONObject(line.substring(5).trim()) } catch (e: Exception) { continue }
                                chunk.optJSONObject("error")?.let { throw GeminiException(it.optString("message")) }
                                val parts = chunk.optJSONArray("candidates")?.optJSONObject(0)?.optJSONObject("content")?.optJSONArray("parts") ?: continue
                                var grew = false
                                for (i in 0 until parts.length()) {
                                    val p = parts.getJSONObject(i)
                                    if (p.optBoolean("thought")) continue
                                    val t = p.optString("text")
                                    if (t.isNotEmpty()) { text.append(t); grew = true }
                                }
                                if (grew) { val s = text.toString(); withContext(Dispatchers.Main) { onText(s) } }
                            }
                        }
                        text.toString().trim()
                    }
                }
            }
            when (result) {
                "\u0000busy" -> { val wait = 15 * (attempt + 1); onBusy(wait); delay(wait * 1000L) }
                "\u0000model" -> { m = "gemini-flash-latest"; model = m }
                else -> return result
            }
        }
        throw GeminiException("Google is busy. Try again later.", 429)
    }

    /**
     * Writes the whole script for a public YouTube video. With a known length, long videos go in
     * 5-minute parts. onProgress(fraction 0–1 or −1 when unknown, descriptions so far, time reached).
     */
    suspend fun writeYouTube(key: String, url: String, duration: Double?, lang: String, density: String,
                             onProgress: (Double, Int, Double) -> Unit, onBusy: (Int) -> Unit): Pair<List<Line>, List<Pair<Double, Double>>> {
        val total = duration?.takeIf { it > 1 }
        val partLength = 300.0
        val parts = if (total == null) 1 else maxOf(1, Math.ceil(total / partLength).toInt())
        val lines = mutableListOf<Line>()
        val speech = mutableListOf<Pair<Double, Double>>()
        for (i in 0 until parts) {
            val from = i * partLength
            val to = total?.let { minOf(it, (i + 1) * partLength) }
            val before = lines.takeLast(12).joinToString("\n") { "${Langs.clock(it.t)} | ${it.text}" }
            val video = JSONObject().put("file_data", JSONObject().put("file_uri", url))
            if (total != null) video.put("video_metadata", JSONObject().put("start_offset", "${from.toInt()}s")
                .put("end_offset", "${Math.ceil(to!!).toInt()}s").put("fps", 2))
            val config = JSONObject().put("temperature", 0.3).put("maxOutputTokens", 16384)
            if (total != null && total > 1200) config.put("mediaResolution", "MEDIA_RESOLUTION_LOW")
            val request = JSONObject()
                .put("contents", JSONArray().put(JSONObject().put("parts", JSONArray().put(video).put(JSONObject().put("text", prompt(lang, total, from, to, before, density))))))
                .put("generationConfig", config)
            val answer = stream(key, request, { soFar ->
                val partial = parse(soFar, from, to, lang).first
                if (partial.isNotEmpty()) {
                    val last = partial.last().t
                    val fraction = if (total == null || to == null) -1.0 else (i + ((last - from) / maxOf(1.0, to - from)).coerceIn(0.0, 1.0)) / parts
                    onProgress(fraction, lines.size + partial.size, last)
                }
            }, onBusy)
            val (l, s) = parse(answer, from, to, lang)
            for (line in l) if (lines.isEmpty() || line.t > lines.last().t + 0.8) lines += line
            speech += s
        }
        return lines to speech.sortedBy { it.first }
    }
}
