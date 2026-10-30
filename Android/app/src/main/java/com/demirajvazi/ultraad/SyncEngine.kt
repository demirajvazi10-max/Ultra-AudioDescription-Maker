package com.demirajvazi.ultraad

import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch

/** A video player the descriptions follow: the app's own player, or the YouTube app. */
interface PlaybackTarget {
    /** Current position in seconds, or null when nothing is playing. */
    val position: Double?
    val isPlaying: Boolean
    val duration: Double?
    fun pause()
    fun play()
    /** Lower the video while a description speaks (the YouTube app is lowered by Android itself). */
    fun duck(on: Boolean) {}
}

/**
 * Follows the video's clock and speaks every description at its moment — like audio description on
 * Netflix. A description too long for the free time (until someone speaks or the next description)
 * is spoken a little faster; when it still needs time, the video pauses for that moment and goes on
 * by itself (extended description). The same rules as the desktop program and the browser extension.
 */
class SyncEngine(private val scope: CoroutineScope, private val target: PlaybackTarget,
                 private val speaker: Speaker, private val prefs: Prefs) {
    var script: Script? = null
        set(v) { field = v; queue.clear() }
    /** Called on the main thread when a description starts (to show it). */
    var onDescription: ((Line) -> Unit)? = null
    var lastText: String? = null
        private set

    private var job: Job? = null
    private var lastT = 0.0
    private var speaking = false
    private var pausedByUs = false
    private var boundary: Double? = null
    private val queue = ArrayDeque<Int>()

    val running get() = job?.isActive == true

    fun start() {
        if (running) return
        lastT = target.position ?: 0.0
        job = scope.launch {
            while (isActive) {
                tick()
                delay(100)
            }
        }
    }

    fun stop() {
        job?.cancel()
        job = null
        speaker.stop()
        target.duck(false)
        speaking = false
        queue.clear()
        if (pausedByUs) { pausedByUs = false; target.play() }
    }

    private fun boundaryAfter(index: Int): Double {
        val s = script ?: return Double.MAX_VALUE
        val t = s.lines[index].t
        var b = s.lines.getOrNull(index + 1)?.t ?: Double.MAX_VALUE
        val inSpeech = s.speech.any { t >= it.first && t < it.second }
        if (!inSpeech) s.speech.firstOrNull { it.first > t + 0.05 && it.first < b }?.let { b = it.first }
        target.duration?.let { b = minOf(b, it) }
        return b
    }

    private fun tick() {
        val t = target.position ?: return
        val s = script
        val jumped = t < lastT - 0.05 || t > lastT + 1.5
        if (jumped) {
            if (speaking && !pausedByUs) { speaker.stop() }
            queue.clear()
            lastT = t
            return
        }
        if (target.isPlaying && s != null) {
            for (i in s.lines.indices) {
                val l = s.lines[i]
                if (l.t > lastT && l.t <= t) {
                    if (speaking) {
                        queue.addLast(i)
                        if (prefs.extended && !pausedByUs) { pausedByUs = true; target.pause() }
                    } else say(i)
                }
            }
        }
        // extended description: someone is about to speak (or the next description is due) and the voice still talks
        val b = boundary
        if (speaking && prefs.extended && b != null && b < Double.MAX_VALUE && target.isPlaying && t >= b - 0.1) {
            pausedByUs = true
            target.pause()
        }
        lastT = t
    }

    private fun say(index: Int) {
        val s = script ?: return
        val line = s.lines[index]
        speaking = true
        val end = boundaryAfter(index)
        boundary = if (prefs.extended) end else null
        val now = target.position ?: line.t
        val free = (if (end < Double.MAX_VALUE) end else now + 30) - now - 0.15
        val need = Langs.estimateSeconds(line.text, prefs.lang) / prefs.rate
        val factor = if (free > 0.3 && need > free) minOf(need / free, prefs.maxSpeedUp.toDouble()) else 1.0
        lastText = line.text
        onDescription?.invoke(line)
        scope.launch {
            target.duck(true)
            speaker.speak(line.text, factor)
            target.duck(false)
            speaking = false
            boundary = null
            val next = queue.removeFirstOrNull()
            if (next != null) { say(next); return@launch }
            if (pausedByUs) { pausedByUs = false; target.play() }
        }
    }

    /** Says the last description again (or nothing). */
    fun repeat() {
        val text = lastText ?: return
        scope.launch {
            target.duck(true)
            speaker.speak(text)
            target.duck(false)
        }
    }
}
