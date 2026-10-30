package com.demirajvazi.ultraad

import android.content.Context
import android.net.Uri
import org.json.JSONArray
import org.json.JSONObject
import java.io.File
import java.util.Locale

/** One spoken description: the moment it belongs to (seconds) and what is said. */
data class Line(val t: Double, val text: String)

/** The descriptions for one video. */
data class Script(
    val key: String,
    val title: String,
    val url: String?,
    val lines: List<Line>,
    val speech: List<Pair<Double, Double>>,
    val source: String,
    val saved: Long = System.currentTimeMillis()
) {
    fun toJson(): JSONObject = JSONObject().apply {
        put("key", key); put("title", title); put("url", url ?: JSONObject.NULL); put("source", source); put("saved", saved)
        put("lines", JSONArray().apply { lines.forEach { put(JSONObject().put("t", it.t).put("text", it.text)) } })
        put("speech", JSONArray().apply { speech.forEach { put(JSONArray().put(it.first).put(it.second)) } })
    }

    companion object {
        fun fromJson(o: JSONObject): Script {
            val lines = mutableListOf<Line>()
            val la = o.optJSONArray("lines") ?: JSONArray()
            for (i in 0 until la.length()) la.getJSONObject(i).let { lines += Line(it.getDouble("t"), it.getString("text")) }
            val speech = mutableListOf<Pair<Double, Double>>()
            val sa = o.optJSONArray("speech") ?: JSONArray()
            for (i in 0 until sa.length()) sa.getJSONArray(i).let { speech += it.getDouble(0) to it.getDouble(1) }
            return Script(o.getString("key"), o.optString("title", ""), if (o.isNull("url")) null else o.optString("url"),
                lines.sortedBy { it.t }, speech, o.optString("source", "file"), o.optLong("saved"))
        }
    }
}

/** Saved scripts (app storage, one JSON file per video), web addresses, and reading script files. */
object Scripts {
    private fun dir(ctx: Context) = File(ctx.filesDir, "scripts").apply { mkdirs() }
    private fun fileFor(ctx: Context, key: String) = File(dir(ctx), key.replace(Regex("[^A-Za-z0-9_-]"), "_") + ".json")

    fun save(ctx: Context, s: Script) = fileFor(ctx, s.key).writeText(s.toJson().toString())

    fun load(ctx: Context, key: String): Script? = try {
        val f = fileFor(ctx, key)
        if (f.exists()) Script.fromJson(JSONObject(f.readText())) else null
    } catch (e: Exception) { null }

    fun delete(ctx: Context, key: String) { fileFor(ctx, key).delete() }

    fun all(ctx: Context): List<Script> =
        (dir(ctx).listFiles() ?: emptyArray()).mapNotNull { f ->
            try { Script.fromJson(JSONObject(f.readText())) } catch (e: Exception) { null }
        }.sortedByDescending { it.saved }

    /** The YouTube video id of a link, or null. */
    fun youtubeId(url: String?): String? {
        if (url.isNullOrBlank()) return null
        return try {
            val u = Uri.parse(url.trim())
            val host = (u.host ?: "").lowercase(Locale.ROOT)
            when {
                host == "youtu.be" -> u.pathSegments.firstOrNull()
                host.endsWith("youtube.com") -> u.getQueryParameter("v")
                    ?: u.pathSegments.let { p -> if (p.size >= 2 && p[0] in listOf("shorts", "live", "embed")) p[1] else null }
                else -> null
            }?.takeIf { it.length >= 6 }
        } catch (e: Exception) { null }
    }

    /** Which video a script belongs to: "yt:ID" for YouTube, otherwise the address without its query. */
    fun keyFor(url: String): String {
        youtubeId(url)?.let { return "yt:$it" }
        return try { Uri.parse(url).let { "${it.scheme}://${it.host}${it.path}" } } catch (e: Exception) { url }
    }

    /** The first web address in a shared text ("Look at this: https://youtu.be/…"). */
    fun findUrl(text: String?): String? = text?.let { Regex("https?://\\S+").find(it)?.value?.trimEnd('.', ',', ')') }

    fun parseTime(text: String): Double? {
        val t = text.trim().replace(',', '.')
        Regex("^(?:(\\d{1,2}):)?(\\d{1,3}):(\\d{1,2}(?:\\.\\d+)?)$").find(t)?.let { m ->
            val h = m.groupValues[1].toIntOrNull() ?: 0
            return h * 3600.0 + m.groupValues[2].toInt() * 60 + m.groupValues[3].toDouble()
        }
        return t.toDoubleOrNull()
    }

    /** WebVTT, SRT, or "mm:ss.s | description" text. Returns the lines and the video address a script names ("NOTE source:"). */
    fun parseFile(raw: String): Pair<List<Line>, String?> {
        val text = raw.replace("\r", "").removePrefix("﻿")
        val source = Regex("^NOTE source:\\s*(\\S+)", RegexOption.MULTILINE).find(text)?.groupValues?.get(1)
        val lines = mutableListOf<Line>()
        if (text.contains("-->")) {
            for (block in text.split(Regex("\\n\\s*\\n"))) {
                val rows = block.split('\n').map { it.trim() }.filter { it.isNotEmpty() }
                val i = rows.indexOfFirst { it.contains("-->") }
                if (i < 0) continue
                val t = parseTime(rows[i].substringBefore("-->").trim()) ?: continue
                val body = rows.drop(i + 1).joinToString(" ").replace(Regex("<[^>]+>"), "").trim()
                if (body.isNotEmpty()) lines += Line(Math.round(t * 10) / 10.0, body)
            }
        } else {
            for (row in text.split('\n')) {
                val s = row.trim()
                if (s.isEmpty()) continue
                val m = Regex("^([\\d:.,]+)\\s*[|\\-–]\\s*(.*)$").find(s)
                val t = m?.let { parseTime(it.groupValues[1]) }
                if (t != null && m != null) { if (m.groupValues[2].isNotBlank()) lines += Line(t, m.groupValues[2].trim()) }
                else if (lines.isNotEmpty()) lines[lines.size - 1] = lines.last().copy(text = lines.last().text + " " + s)
            }
        }
        return lines.sortedBy { it.t } to source
    }
}

/** Languages of the descriptions. */
object Langs {
    val codes = listOf("sr", "en", "de", "hr", "bs", "es", "fr", "it")
    val native = mapOf("sr" to "Srpski", "en" to "English", "de" to "Deutsch", "hr" to "Hrvatski", "bs" to "Bosanski",
        "es" to "Español", "fr" to "Français", "it" to "Italiano")
    fun locale(lang: String): Locale = when (lang) {
        "sr" -> Locale("sr", "RS"); "hr" -> Locale("hr", "HR"); "bs" -> Locale("bs", "BA"); "de" -> Locale.GERMANY
        "es" -> Locale("es", "ES"); "fr" -> Locale.FRANCE; "it" -> Locale.ITALY; else -> Locale.US
    }
    fun promptName(lang: String) = when (lang) {
        "sr" -> "Serbian (ekavian, as spoken in Serbia), written in the Latin alphabet"
        "hr" -> "Croatian"; "bs" -> "Bosnian, written in the Latin alphabet"; "de" -> "German"
        "es" -> "Spanish"; "fr" -> "French"; "it" -> "Italian"; else -> "English"
    }
    fun wordsPerSecond(lang: String) = when (lang) { "en", "it", "fr" -> 2.7; "es" -> 2.8; else -> 2.3 }
    fun estimateSeconds(text: String, lang: String) = 0.3 + text.split(Regex("\\s+")).count { it.isNotBlank() } / wordsPerSecond(lang)

    private val cyr = mapOf('а' to "a", 'б' to "b", 'в' to "v", 'г' to "g", 'д' to "d", 'ђ' to "đ", 'е' to "e", 'ж' to "ž", 'з' to "z",
        'и' to "i", 'ј' to "j", 'к' to "k", 'л' to "l", 'љ' to "lj", 'м' to "m", 'н' to "n", 'њ' to "nj", 'о' to "o", 'п' to "p",
        'р' to "r", 'с' to "s", 'т' to "t", 'ћ' to "ć", 'у' to "u", 'ф' to "f", 'х' to "h", 'ц' to "c", 'ч' to "č", 'џ' to "dž", 'ш' to "š")

    /** Serbian answers sometimes come in Cyrillic: the descriptions are kept in Latin letters. */
    fun serbianLatin(s: String): String {
        if (s.none { it in 'Ѐ'..'ӿ' }) return s
        val sb = StringBuilder()
        for (c in s) {
            val lower = c.lowercaseChar()
            val lat = cyr[lower]
            if (lat == null) sb.append(c) else sb.append(if (c == lower) lat else lat.replaceFirstChar { it.uppercaseChar() })
        }
        return sb.toString()
    }

    private val toCyr = mapOf("dž" to "џ", "lj" to "љ", "nj" to "њ")
    private val single = mapOf('a' to 'а', 'b' to 'б', 'c' to 'ц', 'č' to 'ч', 'ć' to 'ћ', 'd' to 'д', 'đ' to 'ђ', 'e' to 'е', 'f' to 'ф',
        'g' to 'г', 'h' to 'х', 'i' to 'и', 'j' to 'ј', 'k' to 'к', 'l' to 'л', 'm' to 'м', 'n' to 'н', 'o' to 'о', 'p' to 'п',
        'r' to 'р', 's' to 'с', 'š' to 'ш', 't' to 'т', 'u' to 'у', 'v' to 'в', 'z' to 'з', 'ž' to 'ж')

    /** Serbian Latin → Cyrillic for the voice (Serbian voices read Cyrillic most reliably). Foreign words with q, w, x, y stay. */
    fun serbianCyrillic(text: String): String {
        val out = StringBuilder()
        for (word in Regex("[\\p{L}\\p{N}_]+|[^\\p{L}\\p{N}_]+").findAll(text).map { it.value }) {
            if (word.isEmpty() || !word[0].isLetter() || word.any { it in "qwxyQWXY" }) { out.append(word); continue }
            var i = 0
            while (i < word.length) {
                val c = word[i]
                val upper = c.isUpperCase()
                val two = if (i + 1 < word.length) word.substring(i, i + 2).lowercase() else null
                val dg = two?.let { toCyr[it] }
                if (dg != null) { out.append(if (upper) dg.uppercase() else dg); i += 2; continue }
                val cy = single[c.lowercaseChar()]
                out.append(if (cy == null) c else if (upper) cy.uppercaseChar() else cy)
                i++
            }
        }
        return out.toString()
    }

    fun clock(s: Double): String {
        val v = maxOf(0.0, s)
        val m = (v / 60).toInt()
        return String.format(Locale.ROOT, "%02d:%04.1f", m, v - m * 60)
    }
}

/** Settings (app storage). */
class Prefs(ctx: Context) {
    private val p = ctx.getSharedPreferences("settings", Context.MODE_PRIVATE)

    var geminiKey: String
        get() = p.getString("geminiKey", "") ?: ""
        set(v) = p.edit().putString("geminiKey", v).apply()
    var lang: String
        get() = p.getString("lang", null) ?: Locale.getDefault().language.let { if (it in Langs.codes) it else "en" }
        set(v) = p.edit().putString("lang", v).apply()
    /** "voice" = the phone's voice (its own sound); "talkback" = TalkBack reads it (also on braille). */
    var speakWith: String
        get() = p.getString("speakWith", "voice") ?: "voice"
        set(v) = p.edit().putString("speakWith", v).apply()
    var rate: Float
        get() = p.getFloat("rate", 1.0f)
        set(v) = p.edit().putFloat("rate", v).apply()
    /** Video volume (percent) under a description, in the app's own player. */
    var duck: Int
        get() = p.getInt("duck", 35)
        set(v) = p.edit().putInt("duck", v).apply()
    /** Pause the video when a description needs more time. */
    var extended: Boolean
        get() = p.getBoolean("extended", true)
        set(v) = p.edit().putBoolean("extended", v).apply()
    var maxSpeedUp: Float
        get() = p.getFloat("maxSpeedUp", 1.25f)
        set(v) = p.edit().putFloat("maxSpeedUp", v).apply()
    var density: String
        get() = p.getString("density", "Normal") ?: "Normal"
        set(v) = p.edit().putString("density", v).apply()
}
