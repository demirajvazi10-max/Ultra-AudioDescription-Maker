package com.demirajvazi.ultraad

import android.app.Application
import android.content.Context
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.launch

class App : Application() {
    val scope = CoroutineScope(SupervisorJob() + Dispatchers.Main)

    override fun onCreate() {
        super.onCreate()
        instance = this
    }

    companion object {
        lateinit var instance: App
            private set
    }
}

/**
 * Gemini writing a script runs in the app (not in one screen), so it goes on when the screen
 * changes. Progress is said when the work starts, at every quarter, and when it ends — never more
 * often — and the open screen also shows it.
 */
object Writer {
    var job: Job? = null
        private set
    val busy get() = job?.isActive == true
    var status: String = ""
        private set
    /** The screen that shows the progress (set by the open screen). */
    var listener: ((String) -> Unit)? = null
    /** Called when a script was saved (key). */
    var finished: ((String) -> Unit)? = null

    private fun report(ctx: Context, text: String, speak: Boolean) {
        status = text
        listener?.invoke(text)
        if (speak) Announce.say(ctx, text)
    }

    fun write(ctx: Context, url: String, duration: Double?) {
        if (busy) return
        val app = App.instance
        val prefs = Prefs(ctx)
        if (prefs.geminiKey.isBlank()) { report(ctx, ctx.getString(R.string.err_no_key), true); return }
        val id = Scripts.youtubeId(url)
        if (id == null) { report(ctx, ctx.getString(R.string.err_not_youtube), true); return }
        val clean = "https://www.youtube.com/watch?v=$id"
        job = app.scope.launch {
            val started = System.currentTimeMillis()
            report(ctx, ctx.getString(R.string.w_start), true)
            var lastQuarter = 0
            try {
                val title = Gemini.youtubeTitle(clean) ?: clean
                val (lines, speech) = Gemini.writeYouTube(prefs.geminiKey, clean, duration, prefs.lang, prefs.density,
                    onProgress = { fraction, count, reached ->
                        val text = if (fraction >= 0)
                            ctx.getString(R.string.w_progress, (fraction * 100).toInt(), count, Langs.clock(reached))
                        else ctx.getString(R.string.w_progress_count, count, Langs.clock(reached))
                        val quarter = if (fraction >= 0) (fraction * 4).toInt() else 0
                        val speak = quarter > lastQuarter && fraction < 1
                        if (speak) lastQuarter = quarter
                        report(ctx, text, speak)
                    },
                    onBusy = { seconds -> report(ctx, ctx.getString(R.string.w_busy, seconds), true) })
                if (lines.isEmpty()) { report(ctx, ctx.getString(R.string.w_nothing), true); return@launch }
                val key = "yt:$id"
                Scripts.save(ctx, Script(key, title, clean, lines, speech, "gemini"))
                val secs = ((System.currentTimeMillis() - started) / 1000).toInt()
                report(ctx, ctx.getString(R.string.w_done, lines.size, secs / 60, secs % 60), true)
                finished?.invoke(key)
            } catch (e: kotlinx.coroutines.CancellationException) {
                report(ctx, ctx.getString(R.string.w_stopped), true)
            } catch (e: Exception) {
                report(ctx, ctx.getString(R.string.w_failed, e.message ?: e.toString()), true)
            }
        }
    }

    fun cancel() { job?.cancel() }
}
