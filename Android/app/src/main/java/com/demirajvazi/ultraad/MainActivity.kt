package com.demirajvazi.ultraad

import android.Manifest
import android.content.Intent
import android.content.pm.PackageManager
import android.net.Uri
import android.os.Build
import android.os.Bundle
import android.provider.OpenableColumns
import android.provider.Settings
import android.widget.Button
import android.widget.EditText
import android.widget.LinearLayout
import android.widget.TextView
import androidx.activity.result.contract.ActivityResultContracts
import androidx.appcompat.app.AlertDialog
import androidx.appcompat.app.AppCompatActivity
import androidx.core.app.NotificationManagerCompat
import androidx.core.content.ContextCompat

/**
 * The start screen: a YouTube link (typed, pasted, or shared from the YouTube app with "Share"),
 * then: write the descriptions with Gemini, watch here with descriptions, or watch in the YouTube app
 * while this app speaks the descriptions. Scripts made in the desktop program can be loaded too.
 */
class MainActivity : AppCompatActivity() {
    private lateinit var status: TextView
    private lateinit var link: EditText
    private lateinit var write: Button
    private lateinit var stopWriting: Button
    private lateinit var savedList: LinearLayout
    private lateinit var empty: TextView

    private val openFile = registerForActivityResult(ActivityResultContracts.OpenDocument()) { uri -> if (uri != null) loadScript(uri) }
    private val askNotifications = registerForActivityResult(ActivityResultContracts.RequestPermission()) { startCompanionNow() }
    private var pendingCompanionKey: String? = null

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_main)
        status = findViewById(R.id.status)
        link = findViewById(R.id.link)
        write = findViewById(R.id.write)
        stopWriting = findViewById(R.id.stopWriting)
        savedList = findViewById(R.id.savedList)
        empty = findViewById(R.id.empty)

        write.setOnClickListener {
            val url = link.text.toString().trim()
            if (Scripts.youtubeId(url) == null) { say(getString(R.string.err_not_youtube)); return@setOnClickListener }
            Writer.write(this, url, null)
            updateWriting()
        }
        stopWriting.setOnClickListener { Writer.cancel() }
        findViewById<Button>(R.id.watchHere).setOnClickListener {
            val url = link.text.toString().trim()
            if (Scripts.youtubeId(url) == null) { say(getString(R.string.err_not_youtube)); return@setOnClickListener }
            startActivity(Intent(this, PlayerActivity::class.java).putExtra(PlayerActivity.EXTRA_URL, url))
        }
        findViewById<Button>(R.id.watchYoutube).setOnClickListener {
            val url = link.text.toString().trim()
            startCompanion(if (Scripts.youtubeId(url) != null) Scripts.keyFor(url) else null)
        }
        findViewById<Button>(R.id.load).setOnClickListener { openFile.launch(arrayOf("text/*", "application/octet-stream")) }
        findViewById<Button>(R.id.settings).setOnClickListener { startActivity(Intent(this, SettingsActivity::class.java)) }
        findViewById<Button>(R.id.sponsor).setOnClickListener { openWeb(SPONSORS) }
        findViewById<Button>(R.id.about).setOnClickListener { about() }

        handleShare(intent)
    }

    override fun onNewIntent(intent: Intent) {
        super.onNewIntent(intent)
        handleShare(intent)
    }

    /** A link shared from the YouTube app (Share → Ultra Audio Description). */
    private fun handleShare(intent: Intent?) {
        if (intent?.action != Intent.ACTION_SEND) return
        val url = Scripts.findUrl(intent.getStringExtra(Intent.EXTRA_TEXT)) ?: return
        link.setText(url)
        val saved = Scripts.load(this, Scripts.keyFor(url))
        say(if (saved != null) getString(R.string.m_shared_has, saved.lines.size) else getString(R.string.m_shared_new))
    }

    override fun onResume() {
        super.onResume()
        Writer.listener = { text -> status.text = text; updateWriting() }
        Writer.finished = { key -> refreshList(); Scripts.load(this, key)?.url?.let { link.setText(it) } }
        if (Writer.busy) status.text = Writer.status
        updateWriting()
        refreshList()
    }

    override fun onPause() {
        super.onPause()
        Writer.listener = null
        Writer.finished = null
    }

    private fun say(text: String) {
        status.text = text
        Announce.say(this, text)
    }

    private fun updateWriting() {
        val busy = Writer.busy
        write.isEnabled = !busy
        stopWriting.visibility = if (busy) android.view.View.VISIBLE else android.view.View.GONE
    }

    private fun refreshList() {
        savedList.removeAllViews()
        val all = Scripts.all(this)
        empty.visibility = if (all.isEmpty()) android.view.View.VISIBLE else android.view.View.GONE
        for (s in all) {
            val b = com.google.android.material.button.MaterialButton(this, null, com.google.android.material.R.attr.materialButtonOutlinedStyle)
            b.text = getString(R.string.m_saved_item, s.title.ifBlank { s.key }, s.lines.size)
            b.isAllCaps = false
            b.layoutParams = LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT)
            b.setOnClickListener { savedActions(s) }
            savedList.addView(b)
        }
    }

    private fun savedActions(s: Script) {
        val actions = mutableListOf<Pair<String, () -> Unit>>()
        if (s.url != null && Scripts.youtubeId(s.url) != null) {
            actions += getString(R.string.m_watch_here) to {
                startActivity(Intent(this, PlayerActivity::class.java).putExtra(PlayerActivity.EXTRA_URL, s.url))
            }
        }
        actions += getString(R.string.m_watch_youtube) to { startCompanion(s.key) }
        if (s.url != null) actions += getString(R.string.m_write_again) to { link.setText(s.url); Writer.write(this, s.url, null); updateWriting() }
        actions += getString(R.string.m_delete) to {
            Scripts.delete(this, s.key)
            refreshList()
            say(getString(R.string.m_deleted))
        }
        AlertDialog.Builder(this)
            .setTitle(s.title.ifBlank { s.key })
            .setItems(actions.map { it.first }.toTypedArray()) { _, which -> actions[which].second() }
            .setNegativeButton(R.string.cancel, null)
            .show()
    }

    private fun loadScript(uri: Uri) {
        try {
            val text = contentResolver.openInputStream(uri)?.use { it.bufferedReader().readText() } ?: return
            val (lines, source) = Scripts.parseFile(text)
            if (lines.isEmpty()) { say(getString(R.string.m_load_empty)); return }
            var name = "script"
            contentResolver.query(uri, arrayOf(OpenableColumns.DISPLAY_NAME), null, null, null)?.use { c ->
                if (c.moveToFirst()) name = c.getString(0) ?: name
            }
            val typed = link.text.toString().trim()
            val url = source ?: typed.takeIf { Scripts.youtubeId(it) != null }
            val key = url?.let { Scripts.keyFor(it) } ?: "file:$name"
            Scripts.save(this, Script(key, name.substringBeforeLast('.'), url, lines, emptyList(), "file"))
            refreshList()
            say(getString(R.string.m_loaded, lines.size))
        } catch (e: Exception) {
            say(getString(R.string.err_failed, e.message ?: ""))
        }
    }

    // ── watching in the YouTube app ──

    private fun startCompanion(key: String?) {
        pendingCompanionKey = key
        if (!NotificationManagerCompat.getEnabledListenerPackages(this).contains(packageName)) {
            AlertDialog.Builder(this)
                .setTitle(R.string.c_permission_title)
                .setMessage(R.string.c_permission_text)
                .setPositiveButton(R.string.c_permission_open) { _, _ -> startActivity(Intent(Settings.ACTION_NOTIFICATION_LISTENER_SETTINGS)) }
                .setNegativeButton(R.string.cancel, null)
                .show()
            return
        }
        if (Build.VERSION.SDK_INT >= 33 &&
            ContextCompat.checkSelfPermission(this, Manifest.permission.POST_NOTIFICATIONS) != PackageManager.PERMISSION_GRANTED) {
            askNotifications.launch(Manifest.permission.POST_NOTIFICATIONS)
            return
        }
        startCompanionNow()
    }

    private fun startCompanionNow() {
        val i = Intent(this, CompanionService::class.java).setAction(CompanionService.ACTION_START)
        pendingCompanionKey?.let { i.putExtra(CompanionService.EXTRA_KEY, it) }
        ContextCompat.startForegroundService(this, i)
        say(getString(R.string.c_started))
        // open the YouTube app (at the shared video, when there is one)
        val url = link.text.toString().trim()
        val open = if (Scripts.youtubeId(url) != null) Intent(Intent.ACTION_VIEW, Uri.parse(url)).setPackage(YOUTUBE)
                   else packageManager.getLaunchIntentForPackage(YOUTUBE)
        try { if (open != null) startActivity(open) } catch (e: Exception) { }
    }

    private fun about() {
        val version = try { packageManager.getPackageInfo(packageName, 0).versionName } catch (e: Exception) { "" }
        AlertDialog.Builder(this)
            .setTitle(R.string.m_about)
            .setMessage(getString(R.string.about_text, version))
            .setPositiveButton(R.string.sponsor) { _, _ -> openWeb(SPONSORS) }
            .setNegativeButton(R.string.close, null)
            .show()
    }

    private fun openWeb(url: String) {
        try { startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(url))) } catch (e: Exception) { }
    }

    companion object {
        const val SPONSORS = "https://github.com/sponsors/demirajvazi10-max"
        const val YOUTUBE = "com.google.android.youtube"
    }
}
