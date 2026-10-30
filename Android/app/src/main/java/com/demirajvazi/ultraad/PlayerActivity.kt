package com.demirajvazi.ultraad

import android.annotation.SuppressLint
import android.os.Bundle
import android.os.Handler
import android.os.Looper
import android.webkit.JavascriptInterface
import android.webkit.WebChromeClient
import android.webkit.WebView
import android.widget.Button
import android.widget.TextView
import androidx.appcompat.app.AppCompatActivity
import androidx.lifecycle.lifecycleScope
import java.util.Locale

/**
 * Watching a YouTube video inside the app (YouTube's own embedded player), with the descriptions
 * spoken at their moments. Here the app controls the player completely: it lowers the video under a
 * description and pauses it for a moment when a description needs more time.
 */
class PlayerActivity : AppCompatActivity() {
    private lateinit var web: WebView
    private lateinit var status: TextView
    private lateinit var caption: TextView
    private lateinit var playPause: Button
    private lateinit var toggle: Button
    private lateinit var write: Button
    private lateinit var prefs: Prefs
    private lateinit var speaker: Speaker
    private lateinit var engine: SyncEngine
    private lateinit var key: String
    private lateinit var url: String
    private val main = Handler(Looper.getMainLooper())

    // what the web player last reported
    @Volatile private var pos: Double? = null
    @Volatile private var state = -1
    @Volatile private var length: Double? = null

    private val target = object : PlaybackTarget {
        override val position get() = pos
        override val isPlaying get() = state == 1
        override val duration get() = length
        override fun pause() = js("player.pauseVideo()")
        override fun play() = js("player.playVideo()")
        override fun duck(on: Boolean) = js("player.setVolume(${if (on) prefs.duck else 100})")
    }

    @SuppressLint("SetJavaScriptEnabled")
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_player)
        prefs = Prefs(this)
        url = intent.getStringExtra(EXTRA_URL) ?: run { finish(); return }
        val id = Scripts.youtubeId(url) ?: run { finish(); return }
        key = "yt:$id"

        web = findViewById(R.id.web)
        status = findViewById(R.id.status)
        caption = findViewById(R.id.caption)
        playPause = findViewById(R.id.playPause)
        toggle = findViewById(R.id.toggle)
        write = findViewById(R.id.write)

        speaker = Speaker(this, prefs)
        speaker.onNoVoice = { main.post { say(getString(R.string.err_no_voice)) } }
        engine = SyncEngine(lifecycleScope, target, speaker, prefs)
        engine.onDescription = { line -> caption.text = line.text }

        playPause.setOnClickListener { if (state == 1) target.pause() else target.play() }
        findViewById<Button>(R.id.back).setOnClickListener { seekBy(-10.0) }
        findViewById<Button>(R.id.forward).setOnClickListener { seekBy(10.0) }
        findViewById<Button>(R.id.repeat).setOnClickListener {
            if (engine.lastText != null) engine.repeat() else say(getString(R.string.p_nothing_yet))
        }
        toggle.setOnClickListener {
            if (engine.running) { engine.stop(); toggle.setText(R.string.p_desc_on); say(getString(R.string.p_off)) }
            else { engine.start(); toggle.setText(R.string.p_desc_off); say(getString(R.string.p_on)) }
        }
        findViewById<Button>(R.id.position).setOnClickListener { sayPosition() }
        write.setOnClickListener { Writer.write(this, url, length) }

        web.settings.javaScriptEnabled = true
        web.settings.mediaPlaybackRequiresUserGesture = false
        web.settings.domStorageEnabled = true
        web.webChromeClient = WebChromeClient()
        web.addJavascriptInterface(Bridge(), "Android")
        web.loadDataWithBaseURL(BASE_URL, html(id), "text/html", "utf-8", null)

        loadScript(announce = true)
        engine.start()
    }

    override fun onResume() {
        super.onResume()
        if (!::engine.isInitialized) return
        Writer.listener = { text -> status.text = text }
        Writer.finished = { k -> if (k == key) loadScript(announce = false) }
    }

    override fun onPause() {
        super.onPause()
        Writer.listener = null
        Writer.finished = null
        if (::web.isInitialized) target.pause()
    }

    override fun onDestroy() {
        if (::engine.isInitialized) {
            engine.stop()
            speaker.shutdown()
            web.destroy()
        }
        super.onDestroy()
    }

    private fun loadScript(announce: Boolean) {
        val s = Scripts.load(this, key)
        engine.script = s
        write.visibility = if (s == null) android.view.View.VISIBLE else android.view.View.GONE
        val text = if (s == null) getString(R.string.p_no_script) else getString(R.string.p_ready, s.lines.size)
        if (announce) say(text) else status.text = text
    }

    private fun say(text: String) {
        status.text = text
        Announce.say(this, text)
    }

    private fun js(code: String) {
        main.post { web.evaluateJavascript("if (window.player && player.getPlayerState) { $code }", null) }
    }

    private fun seekBy(by: Double) {
        val to = maxOf(0.0, (pos ?: 0.0) + by)
        js(String.format(Locale.ROOT, "player.seekTo(%.1f, true)", to))
    }

    private fun sayPosition() {
        val p = pos ?: 0.0
        val next = engine.script?.lines?.firstOrNull { it.t > p + 0.05 }
        var text = getString(R.string.p_position_text, Langs.clock(p), Langs.clock(length ?: 0.0),
            getString(if (state == 1) R.string.p_playing else R.string.p_paused))
        if (next != null) text += " " + getString(R.string.p_next_in, String.format(Locale.ROOT, "%.0f", next.t - p))
        say(text)
    }

    private fun updatePlayButton() = playPause.setText(if (state == 1) R.string.p_pause else R.string.p_play)

    /** Messages from YouTube's player in the page (they arrive on a background thread). */
    inner class Bridge {
        @JavascriptInterface fun onTime(t: Double, s: Int) {
            pos = t
            if (s != state) { state = s; main.post { updatePlayButton() } }
        }
        @JavascriptInterface fun onReady(d: Double) { length = if (d > 0) d else null }
        @JavascriptInterface fun onError(code: Int) {
            main.post { say(getString(if (code == 101 || code == 150 || code == 153) R.string.p_err_embed else R.string.p_err_play, code)) }
        }
    }

    private fun html(id: String) = """
        <!doctype html><html><head><meta name="viewport" content="width=device-width,initial-scale=1">
        <style>html,body{margin:0;height:100%;background:#000}#p{width:100%;height:100%}</style></head>
        <body><div id="p"></div>
        <script src="https://www.youtube.com/iframe_api"></script>
        <script>
          var player;
          function onYouTubeIframeAPIReady() {
            player = new YT.Player('p', {
              videoId: '$id',
              playerVars: { playsinline: 1, rel: 0, origin: '$BASE_URL' },
              events: {
                onReady: function () { Android.onReady(player.getDuration() || 0); },
                onError: function (e) { Android.onError(e.data); }
              }
            });
            setInterval(function () {
              if (player && player.getCurrentTime) Android.onTime(player.getCurrentTime() || 0, player.getPlayerState());
            }, 100);
          }
        </script></body></html>
    """.trimIndent()

    companion object {
        const val EXTRA_URL = "url"
        /** The embedded player needs a real web address as its origin. */
        private const val BASE_URL = "https://demirajvazi10-max.github.io"
    }
}
