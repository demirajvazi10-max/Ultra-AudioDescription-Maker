package com.demirajvazi.ultraad

import android.content.Intent
import android.net.Uri
import android.os.Bundle
import android.view.View
import android.widget.AdapterView
import android.widget.ArrayAdapter
import android.widget.Button
import android.widget.CheckBox
import android.widget.EditText
import android.widget.Spinner
import android.widget.TextView
import androidx.appcompat.app.AppCompatActivity
import androidx.appcompat.app.AppCompatDelegate
import androidx.core.os.LocaleListCompat
import androidx.lifecycle.lifecycleScope
import kotlinx.coroutines.launch

/** Settings: every change is saved at once and confirmed in the status line (read by TalkBack). */
class SettingsActivity : AppCompatActivity() {
    private lateinit var prefs: Prefs
    private lateinit var status: TextView
    private var speaker: Speaker? = null
    private var loading = true

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_settings)
        setTitle(R.string.m_settings)
        prefs = Prefs(this)
        status = findViewById(R.id.status)

        val current = AppCompatDelegate.getApplicationLocales().toLanguageTags().ifBlank { "en" }
        choice(R.id.uiLang, listOf("en" to "English", "sr" to "Srpski"), if (current.startsWith("sr")) "sr" else "en") { v ->
            AppCompatDelegate.setApplicationLocales(LocaleListCompat.forLanguageTags(v))   // the screens are shown again in that language
        }
        choice(R.id.lang, Langs.codes.map { it to (Langs.native[it] ?: it) }, prefs.lang) { prefs.lang = it }
        choice(R.id.speakWith, listOf("voice" to getString(R.string.s_speak_voice), "talkback" to getString(R.string.s_speak_talkback)), prefs.speakWith) { prefs.speakWith = it }
        choice(R.id.rate, listOf(0.8f, 0.9f, 1.0f, 1.1f, 1.2f, 1.35f, 1.5f).map { it.toString() to "${(it * 100).toInt()} %" }, prefs.rate.toString()) { prefs.rate = it.toFloat() }
        choice(R.id.duck, listOf(20, 35, 50, 70, 100).map { it.toString() to "$it %" }, prefs.duck.toString()) { prefs.duck = it.toInt() }
        choice(R.id.maxSpeedUp, listOf("1.0" to getString(R.string.s_up_none), "1.1" to "10 %", "1.25" to "25 %", "1.35" to "35 %", "1.5" to "50 %"),
            prefs.maxSpeedUp.toString()) { prefs.maxSpeedUp = it.toFloat() }
        choice(R.id.density, listOf("Essential" to getString(R.string.s_dens_essential), "Normal" to getString(R.string.s_dens_normal),
            "Detailed" to getString(R.string.s_dens_detailed)), prefs.density) { prefs.density = it }

        findViewById<CheckBox>(R.id.extended).apply {
            isChecked = prefs.extended
            setOnCheckedChangeListener { _, on -> prefs.extended = on; saved() }
        }
        findViewById<Button>(R.id.test).setOnClickListener {
            val sp = speaker ?: Speaker(this, prefs).also { speaker = it }
            sp.onNoVoice = { say(getString(R.string.err_no_voice)) }
            lifecycleScope.launch { sp.speak(getString(R.string.s_test_sentence)) }
        }
        findViewById<Button>(R.id.ttsSettings).setOnClickListener {
            try { startActivity(Intent("com.android.settings.TTS_SETTINGS")) } catch (e: Exception) { }
        }

        val keyState = findViewById<TextView>(R.id.keyState)
        keyState.setText(if (prefs.geminiKey.isBlank()) R.string.s_key_none else R.string.s_key_saved)
        val keyBox = findViewById<EditText>(R.id.key)
        findViewById<Button>(R.id.keySave).setOnClickListener {
            val key = keyBox.text.toString().trim()
            if (key.isEmpty()) { keyBox.requestFocus(); return@setOnClickListener }
            say(getString(R.string.s_key_testing))
            lifecycleScope.launch {
                val problem = Gemini.testKey(key)
                if (problem != null) { say(getString(R.string.s_key_bad, problem)); return@launch }
                prefs.geminiKey = key
                keyBox.setText("")
                keyState.setText(R.string.s_key_saved)
                say(getString(R.string.s_key_ok))
            }
        }
        findViewById<Button>(R.id.keyLink).setOnClickListener {
            try { startActivity(Intent(Intent.ACTION_VIEW, Uri.parse("https://aistudio.google.com/app/apikey"))) } catch (e: Exception) { }
        }
        loading = false
    }

    override fun onDestroy() {
        speaker?.shutdown()
        super.onDestroy()
    }

    private fun say(text: String) {
        status.text = text
        Announce.say(this, text)
    }

    private fun saved() { if (!loading) say(getString(R.string.s_saved)) }

    /** A list to choose from; saves the choice as soon as it changes. */
    private fun choice(id: Int, items: List<Pair<String, String>>, value: String, save: (String) -> Unit) {
        val spinner = findViewById<Spinner>(id)
        spinner.adapter = ArrayAdapter(this, android.R.layout.simple_spinner_dropdown_item, items.map { it.second })
        val index = items.indexOfFirst { it.first == value }.let { if (it < 0) 0 else it }
        spinner.setSelection(index, false)
        var last = index
        spinner.onItemSelectedListener = object : AdapterView.OnItemSelectedListener {
            override fun onItemSelected(parent: AdapterView<*>?, view: View?, position: Int, idValue: Long) {
                if (loading || position == last) return
                last = position
                save(items[position].first)
                saved()
            }
            override fun onNothingSelected(parent: AdapterView<*>?) {}
        }
    }
}
