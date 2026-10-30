package com.demirajvazi.ultraad

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.app.Service
import android.content.ComponentName
import android.content.Context
import android.content.Intent
import android.content.pm.ServiceInfo
import android.media.MediaMetadata
import android.media.session.MediaController
import android.media.session.MediaSessionManager
import android.media.session.PlaybackState
import android.os.IBinder
import android.os.SystemClock
import android.service.notification.NotificationListenerService
import androidx.core.app.NotificationCompat
import androidx.core.app.ServiceCompat
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.delay
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import java.util.Locale

/**
 * Only here so the user can allow the app to see which video the YouTube app plays and where it is
 * (Android shows this as "notification access"). It reads nothing from notifications.
 */
class MediaListener : NotificationListenerService()

/**
 * Watching in the YouTube app: this service follows the YouTube app's player (through Android's media
 * sessions) and speaks the descriptions at their moments. Android lowers YouTube while the voice
 * speaks; when a description needs more time, YouTube is paused for that moment and goes on by itself.
 * The script is chosen by the video's title (scripts written with Gemini know it), or the one the
 * user picked.
 */
class CompanionService : Service() {
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.Main)
    private lateinit var prefs: Prefs
    private lateinit var speaker: Speaker
    private lateinit var engine: SyncEngine
    private var controller: MediaController? = null
    private var forcedKey: String? = null
    private var currentTitle: String? = null
    private var mismatchSaid = false
    private var watching = false

    private val target = object : PlaybackTarget {
        override val position: Double?
            get() {
                val st = controller?.playbackState ?: return null
                if (st.position < 0) return null
                var ms = st.position.toDouble()
                if (st.state == PlaybackState.STATE_PLAYING) ms += (SystemClock.elapsedRealtime() - st.lastPositionUpdateTime) * st.playbackSpeed
                return ms / 1000.0
            }
        override val isPlaying get() = controller?.playbackState?.state == PlaybackState.STATE_PLAYING
        override val duration: Double?
            get() = controller?.metadata?.getLong(MediaMetadata.METADATA_KEY_DURATION)?.takeIf { it > 0 }?.let { it / 1000.0 }
        override fun pause() { controller?.transportControls?.pause() }
        override fun play() { controller?.transportControls?.play() }
    }

    private val callback = object : MediaController.Callback() {
        override fun onMetadataChanged(metadata: MediaMetadata?) { matchScript() }
        override fun onSessionDestroyed() { controller = null }
    }

    override fun onBind(intent: Intent?): IBinder? = null

    override fun onCreate() {
        super.onCreate()
        prefs = Prefs(this)
        speaker = Speaker(this, prefs)
        speaker.onNoVoice = { Announce.say(this, getString(R.string.err_no_voice)) }
        engine = SyncEngine(scope, target, speaker, prefs)
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        when (intent?.action) {
            ACTION_STOP -> { stopEverything(); return START_NOT_STICKY }
            ACTION_REPEAT -> { if (engine.lastText != null) engine.repeat() else Announce.say(this, getString(R.string.p_nothing_yet)); return START_NOT_STICKY }
        }
        forcedKey = intent?.getStringExtra(EXTRA_KEY)
        ServiceCompat.startForeground(this, NOTIFICATION_ID, notification(getString(R.string.c_waiting)),
            if (android.os.Build.VERSION.SDK_INT >= 29) ServiceInfo.FOREGROUND_SERVICE_TYPE_MEDIA_PLAYBACK else 0)
        engine.start()
        if (watching) return START_STICKY
        watching = true
        scope.launch {
            // find the YouTube app's player, and find it again when it is closed and opened
            while (isActive) {
                if (controller == null) findYouTube()
                delay(1500)
            }
        }
        return START_STICKY
    }

    private fun findYouTube() {
        try {
            val msm = getSystemService(Context.MEDIA_SESSION_SERVICE) as MediaSessionManager
            val sessions = msm.getActiveSessions(ComponentName(this, MediaListener::class.java))
            val yt = sessions.firstOrNull { it.packageName in YOUTUBE_APPS } ?: return
            controller = yt
            yt.registerCallback(callback)
            matchScript()
        } catch (e: SecurityException) {
            update(getString(R.string.c_no_permission))
        }
    }

    private fun norm(s: String?) = (s ?: "").lowercase(Locale.ROOT).replace(Regex("\\s+"), " ").trim()

    /** The script for the video YouTube plays now: the chosen one, or the one with the same title. */
    private fun matchScript() {
        val title = controller?.metadata?.getString(MediaMetadata.METADATA_KEY_TITLE)
        if (title == currentTitle && engine.script != null) return
        currentTitle = title
        val all = Scripts.all(this)
        val byTitle = all.firstOrNull { it.title.isNotBlank() && norm(it.title) == norm(title) }
        val forced = forcedKey?.let { Scripts.load(this, it) }
        val chosen = byTitle ?: forced
        engine.script = chosen
        when {
            chosen != null -> update(getString(R.string.c_following, chosen.title.ifBlank { title ?: "" }, chosen.lines.size), true)
            title != null -> {
                if (!mismatchSaid) { mismatchSaid = true; update(getString(R.string.c_no_script_for, title), true) }
                else update(getString(R.string.c_no_script_for, title))
            }
            else -> update(getString(R.string.c_waiting))
        }
    }

    private fun update(text: String, speak: Boolean = false) {
        (getSystemService(Context.NOTIFICATION_SERVICE) as NotificationManager).notify(NOTIFICATION_ID, notification(text))
        if (speak) Announce.say(this, text)
    }

    private fun notification(text: String): Notification {
        val nm = getSystemService(Context.NOTIFICATION_SERVICE) as NotificationManager
        if (nm.getNotificationChannel(CHANNEL) == null)
            nm.createNotificationChannel(NotificationChannel(CHANNEL, getString(R.string.c_channel), NotificationManager.IMPORTANCE_LOW))
        val flags = PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT
        val stop = PendingIntent.getService(this, 1, Intent(this, CompanionService::class.java).setAction(ACTION_STOP), flags)
        val repeat = PendingIntent.getService(this, 2, Intent(this, CompanionService::class.java).setAction(ACTION_REPEAT), flags)
        val open = PendingIntent.getActivity(this, 3, Intent(this, MainActivity::class.java), flags)
        return NotificationCompat.Builder(this, CHANNEL)
            .setSmallIcon(R.drawable.ic_stat)
            .setContentTitle(getString(R.string.app_name))
            .setContentText(text)
            .setStyle(NotificationCompat.BigTextStyle().bigText(text))
            .setOngoing(true)
            .setOnlyAlertOnce(true)
            .setContentIntent(open)
            .addAction(0, getString(R.string.c_repeat), repeat)
            .addAction(0, getString(R.string.c_stop), stop)
            .build()
    }

    private fun stopEverything() {
        engine.stop()
        Announce.say(this, getString(R.string.c_stopped))
        ServiceCompat.stopForeground(this, ServiceCompat.STOP_FOREGROUND_REMOVE)
        stopSelf()
    }

    override fun onDestroy() {
        try { controller?.unregisterCallback(callback) } catch (e: Exception) { }
        engine.stop()
        speaker.shutdown()
        scope.cancel()
        super.onDestroy()
    }

    companion object {
        const val ACTION_START = "start"
        const val ACTION_STOP = "stop"
        const val ACTION_REPEAT = "repeat"
        const val EXTRA_KEY = "key"
        private const val CHANNEL = "companion"
        private const val NOTIFICATION_ID = 7
        private val YOUTUBE_APPS = setOf("com.google.android.youtube", "com.google.android.apps.youtube.music", "app.revanced.android.youtube")
    }
}
