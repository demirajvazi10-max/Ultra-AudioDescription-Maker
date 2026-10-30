package com.demirajvazi.ultraad

import android.content.Context
import android.media.AudioAttributes
import android.media.AudioFocusRequest
import android.media.AudioManager
import android.os.Bundle
import android.speech.tts.TextToSpeech
import android.speech.tts.UtteranceProgressListener
import android.view.accessibility.AccessibilityEvent
import android.view.accessibility.AccessibilityManager
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.delay
import kotlinx.coroutines.withTimeoutOrNull
import java.util.UUID

/**
 * Speaks descriptions. With the phone's voice (Android text-to-speech), other apps — the YouTube app —
 * are lowered by Android itself while the voice speaks (audio focus "may duck"). With TalkBack, the
 * description is announced to the screen reader (and shown on braille) instead.
 */
class Speaker(private val ctx: Context, private val prefs: Prefs) {
    private var tts: TextToSpeech? = null
    private val ready = CompletableDeferred<Boolean>()
    private var waiting: CompletableDeferred<Unit>? = null
    private val audio = ctx.getSystemService(Context.AUDIO_SERVICE) as AudioManager
    private val attributes = AudioAttributes.Builder()
        .setUsage(AudioAttributes.USAGE_MEDIA)
        .setContentType(AudioAttributes.CONTENT_TYPE_SPEECH)
        .build()
    private val focus = AudioFocusRequest.Builder(AudioManager.AUDIOFOCUS_GAIN_TRANSIENT_MAY_DUCK)
        .setAudioAttributes(attributes)
        .setOnAudioFocusChangeListener { }
        .build()

    /** Called when the chosen language has no voice on this phone (once). */
    var onNoVoice: (() -> Unit)? = null
    private var noVoiceSaid = false

    init {
        tts = TextToSpeech(ctx.applicationContext) { status ->
            if (status == TextToSpeech.SUCCESS) {
                tts?.setAudioAttributes(attributes)
                tts?.setOnUtteranceProgressListener(object : UtteranceProgressListener() {
                    override fun onStart(id: String?) {}
                    override fun onDone(id: String?) { waiting?.complete(Unit) }
                    @Deprecated("Deprecated in Java")
                    override fun onError(id: String?) { waiting?.complete(Unit) }
                    override fun onError(id: String?, code: Int) { waiting?.complete(Unit) }
                    override fun onStop(id: String?, interrupted: Boolean) { waiting?.complete(Unit) }
                })
                ready.complete(true)
            } else ready.complete(false)
        }
    }

    /** Speaks a text and returns when it is over. rateFactor > 1 speaks faster (to fit before someone speaks). */
    suspend fun speak(text: String, rateFactor: Double = 1.0) {
        val lang = prefs.lang
        if (prefs.speakWith == "talkback") {
            Announce.say(ctx, text)
            delay(((Langs.estimateSeconds(text, lang) / (prefs.rate * rateFactor)) * 1000 + 300).toLong())
            return
        }
        val engine = tts
        if (withTimeoutOrNull(5000) { ready.await() } != true || engine == null) {
            Announce.say(ctx, text)
            return
        }
        val result = engine.setLanguage(Langs.locale(lang))
        if (result == TextToSpeech.LANG_MISSING_DATA || result == TextToSpeech.LANG_NOT_SUPPORTED) {
            if (!noVoiceSaid) { noVoiceSaid = true; onNoVoice?.invoke() }
        }
        engine.setSpeechRate((prefs.rate * rateFactor).toFloat().coerceIn(0.5f, 2.5f))
        val say = if (lang == "sr") Langs.serbianCyrillic(text) else text
        val done = CompletableDeferred<Unit>()
        waiting = done
        audio.requestAudioFocus(focus)
        engine.speak(say, TextToSpeech.QUEUE_FLUSH, Bundle(), UUID.randomUUID().toString())
        // a safety net: some engines never report the end
        withTimeoutOrNull(((Langs.estimateSeconds(text, lang) * 3 + 5) * 1000).toLong()) { done.await() }
        audio.abandonAudioFocusRequest(focus)
    }

    fun stop() {
        tts?.stop()
        waiting?.complete(Unit)
        audio.abandonAudioFocusRequest(focus)
    }

    fun shutdown() {
        stop()
        tts?.shutdown()
        tts = null
    }
}

/** Says something through TalkBack (or any screen reader), from a screen or from the background service. */
object Announce {
    fun say(ctx: Context, text: String) {
        val am = ctx.getSystemService(Context.ACCESSIBILITY_SERVICE) as AccessibilityManager
        if (!am.isEnabled) return
        @Suppress("DEPRECATION")
        val event = if (android.os.Build.VERSION.SDK_INT >= 30) AccessibilityEvent(AccessibilityEvent.TYPE_ANNOUNCEMENT)
                    else AccessibilityEvent.obtain(AccessibilityEvent.TYPE_ANNOUNCEMENT)
        event.packageName = ctx.packageName
        event.className = Announce::class.java.name
        event.text.add(text)
        try { am.sendAccessibilityEvent(event) } catch (e: Exception) { }
    }
}
