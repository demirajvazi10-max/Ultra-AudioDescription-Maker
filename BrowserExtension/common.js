/* Ultra Audio Description — shared by every part of the extension:
 * settings, texts in English and Serbian, which video a script belongs to, and reading script files. */
(function (g) {
  'use strict';
  if (g.UAD) return;

  const DEFAULTS = {
    uiLang: 'en',            // the extension's own language: 'en' (default) or 'sr'
    lang: (() => { const n = ((g.navigator && g.navigator.language) || 'en').slice(0, 2).toLowerCase();
                   return ['sr', 'en', 'de', 'hr', 'bs', 'es', 'fr', 'it'].includes(n) ? n : 'en'; })(),   // the language of the descriptions (browser language)
    speakWith: 'voice',      // 'voice' = a voice of the browser; 'reader' = the screen reader reads them (also braille)
    voiceName: '',           // empty = the best voice for the language
    rate: 1.0,
    duck: 0.35,              // the video's volume while a description is heard (35 %)
    extended: true,          // pause the video when a description needs more time (extended audio description)
    maxSpeedUp: 1.25,        // speak up to 25 % faster to fit before the next speech
    showText: true,          // the description as text on the picture, for viewers who can see
    liveWhenNoScript: true,  // describe live when there is no script for the video
    livePause: true,         // pause the video for a moment while a live description is written
    liveSensitivity: 18,     // how big a picture change starts a new live description
    ai: 'gemini',            // 'gemini' or 'ollama'
    ollamaModel: '',
    density: 'Normal'
  };

  const LANGS = ['sr', 'en', 'de', 'hr', 'bs', 'es', 'fr', 'it'];
  const NATIVE = { sr: 'Srpski', en: 'English', de: 'Deutsch', hr: 'Hrvatski', bs: 'Bosanski', es: 'Español', fr: 'Français', it: 'Italiano' };
  const LOCALE = { sr: 'sr-RS', en: 'en-US', de: 'de-DE', hr: 'hr-HR', bs: 'bs-BA', es: 'es-ES', fr: 'fr-FR', it: 'it-IT' };
  const WPS = { en: 2.7, es: 2.8, it: 2.7, fr: 2.7 };

  const T = {
    app: ['Ultra Audio Description', 'Ultra Audio Description'],
    // states and announcements in the page
    on: ['Audio description on. {0} descriptions for this video.', 'Audio deskripcija uključena. Za ovaj video ima {0} opisa.'],
    on_live: ['Audio description on. There is no script for this video, so the picture is described live.', 'Audio deskripcija uključena. Za ovaj video nema skripte, pa se slika opisuje uživo.'],
    on_empty: ['Audio description on, but there are no descriptions for this video yet. Alt+Shift+A opens the menu: write them with Gemini, load a script, or turn on live description.', 'Audio deskripcija je uključena, ali za ovaj video još nema opisa. Alt+Shift+A otvara meni: napiši ih pomoću Geminija, učitaj skriptu ili uključi opis uživo.'],
    off: ['Audio description off.', 'Audio deskripcija isključena.'],
    no_video: ['No video was found on this page.', 'Na ovoj stranici nije pronađen video.'],
    new_video: ['New video. {0} descriptions.', 'Novi video. Opisa: {0}.'],
    new_video_none: ['New video, with no descriptions yet.', 'Novi video, još bez opisa.'],
    nothing_to_repeat: ['Nothing has been described yet.', 'Još ništa nije opisano.'],
    live_protected: ['This site protects its picture (DRM), so the picture cannot be seen and described. Scripts made in Ultra Audio Description Maker still work here.', 'Ovaj sajt štiti sliku (DRM), pa ne može da se vidi i opiše. Skripte napravljene u Ultra Audio Description Makeru ovde i dalje rade.'],
    live_blocked: ['The page does not allow the extension to look at this video picture.', 'Stranica ne dozvoljava dodatku da gleda sliku ovog videa.'],
    no_key: ['There is no Gemini key yet. Open the extension options to add one; the steps are there.', 'Još nema Gemini ključa. Otvori opcije dodatka i dodaj ga; koraci su tamo.'],
    ai_busy: ['The AI is busy (too many requests). Live description waits half a minute.', 'AI je zauzet (previše zahteva). Opis uživo čeka pola minuta.'],
    ai_error: ['The AI did not answer: {0}', 'AI nije odgovorio: {0}'],
    ollama_missing: ['No vision model was found in Ollama (for example qwen2.5vl), or Ollama is not running.', 'U Ollami nije pronađen model za slike (na primer qwen2.5vl), ili Ollama nije pokrenuta.'],
    no_voice: ['The browser has no voice for this language; the default voice is used. In Microsoft Edge the natural voices are free; or choose screen reader mode in the options.', 'Pregledač nema glas za ovaj jezik, pa se koristi podrazumevani. U Microsoft Edge-u su prirodni glasovi besplatni; ili u opcijama izaberi čitač ekrana.'],
    yt_start: ['Gemini is watching this YouTube video and writing the descriptions. This takes a minute or more; you can keep watching.', 'Gemini gleda ovaj YouTube video i piše opise. To traje minut ili duže; možeš da nastaviš da gledaš.'],
    yt_progress: ['Gemini: {0} %, {1} descriptions so far, up to {2}.', 'Gemini: {0} %, do sada {1} opisa, do {2}.'],
    yt_busy: ['Google is busy; Gemini tries again in {0} seconds.', 'Google je zauzet; Gemini pokušava ponovo za {0} sekundi.'],
    yt_done: ['Done: {0} descriptions are ready and will be heard at their moments.', 'Gotovo: {0} opisa je spremno i čuće se u svom trenutku.'],
    yt_failed: ['Gemini could not describe this video: {0}', 'Gemini nije mogao da opiše ovaj video: {0}'],
    script_loaded: ['{0} descriptions loaded for this video.', 'Za ovaj video učitano je {0} opisa.'],
    ad_playing: ['An advertisement is playing; descriptions wait.', 'Ide reklama; opisi čekaju.'],
    // popup
    p_title: ['Ultra Audio Description', 'Ultra Audio Description'],
    p_checking: ['Looking for a video on this page…', 'Tražim video na ovoj stranici…'],
    p_cannot: ['The extension cannot work on this page (a browser page, or the store).', 'Dodatak ne može da radi na ovoj stranici (stranica pregledača ili prodavnice).'],
    p_state_off: ['Video found{0}. Audio description is off. {1} descriptions saved for it.', 'Pronađen video{0}. Audio deskripcija je isključena. Sačuvano opisa: {1}.'],
    p_state_on: ['Video found{0}. Audio description is on. {1} descriptions.', 'Pronađen video{0}. Audio deskripcija je uključena. Opisa: {1}.'],
    p_live_note: [' Live description is on.', ' Opis uživo je uključen.'],
    p_on: ['Turn audio description on (Alt+Shift+D)', 'Uključi audio deskripciju (Alt+Shift+D)'],
    p_off: ['Turn audio description off (Alt+Shift+D)', 'Isključi audio deskripciju (Alt+Shift+D)'],
    p_write: ['Write descriptions with Gemini (YouTube)', 'Napiši opise pomoću Geminija (YouTube)'],
    p_write_note: ['Gemini watches the YouTube video directly from its link.', 'Gemini gleda YouTube video direktno sa linka.'],
    p_write_only_yt: ['Writing the whole script works for YouTube videos; on other sites use live description or a script file.', 'Pisanje cele skripte radi za YouTube; na drugim sajtovima koristi opis uživo ili datoteku sa skriptom.'],
    p_writing: ['Gemini is writing; the page announces the progress.', 'Gemini piše; stranica javlja napredak.'],
    p_load: ['Load a script file (WebVTT, SRT or text)…', 'Učitaj datoteku sa skriptom (WebVTT, SRT ili tekst)…'],
    p_live: ['Describe live when there is no script', 'Opisuj uživo kada nema skripte'],
    p_now: ['Describe the picture now (Alt+Shift+S)', 'Opiši sliku sada (Alt+Shift+S)'],
    p_always: ['Always ready on this site', 'Uvek spreman na ovom sajtu'],
    p_always_on: ['Always ready on this site: on. Audio description starts by itself here.', 'Uvek spreman na ovom sajtu: uključeno. Audio deskripcija se ovde pokreće sama.'],
    p_always_off: ['Always ready on this site: off.', 'Uvek spreman na ovom sajtu: isključeno.'],
    p_remove: ['Delete the descriptions saved for this video', 'Obriši opise sačuvane za ovaj video'],
    p_removed: ['The descriptions for this video are deleted.', 'Opisi za ovaj video su obrisani.'],
    p_options: ['Options and keys', 'Opcije i ključevi'],
    p_sponsor: ['Support the project (GitHub Sponsors)', 'Podrži projekat (GitHub Sponsors)'],
    p_keys_help: ['Shortcuts: Alt+Shift+D on/off, Alt+Shift+S describe now, Alt+Shift+R repeat. They can be changed in the browser\'s extension shortcuts.', 'Prečice: Alt+Shift+D uključi/isključi, Alt+Shift+S opiši sada, Alt+Shift+R ponovi. Mogu da se promene u prečicama za dodatke u pregledaču.'],
    // load page
    l_title: ['Load a description script', 'Učitavanje skripte sa opisima'],
    l_label: ['Script file (WebVTT, SRT, or text with "mm:ss.s | description" lines)', 'Datoteka sa skriptom (WebVTT, SRT ili tekst sa redovima „mm:ss.s | opis”)'],
    l_help: ['Scripts exported from Ultra Audio Description Maker know which video they belong to.', 'Skripte izvezene iz Ultra Audio Description Makera znaju kom videu pripadaju.'],
    l_empty: ['No descriptions were found in that file.', 'U toj datoteci nema opisa.'],
    l_done: ['{0} descriptions loaded. You can close this tab; the video tab uses them right away.', 'Učitano {0} opisa. Možeš da zatvoriš ovu karticu; kartica sa videom ih odmah koristi.'],
    l_close: ['Close this tab', 'Zatvori ovu karticu'],
    // options
    o_title: ['Ultra Audio Description — options', 'Ultra Audio Description — opcije'],
    o_saved: ['Saved.', 'Sačuvano.'],
    o_sec_general: ['General', 'Opšte'],
    o_ui: ['Language of the extension', 'Jezik dodatka'],
    o_lang: ['Language of the descriptions', 'Jezik opisa'],
    o_sec_voice: ['How descriptions are heard', 'Kako se opisi čuju'],
    o_speak: ['Speak with', 'Izgovara'],
    o_speak_voice: ['A voice of the browser (its own sound, over the video)', 'Glas pregledača (poseban zvuk, preko videa)'],
    o_speak_reader: ['My screen reader (also on braille)', 'Moj čitač ekrana (i na brajevom redu)'],
    o_voice: ['Voice', 'Glas'],
    o_voice_auto: ['The best voice for the language', 'Najbolji glas za jezik'],
    o_test: ['Test the voice', 'Probaj glas'],
    o_test_sentence: ['Ana walks into the room and turns on the light.', 'Ana ulazi u sobu i pali svetlo.'],
    o_rate: ['Voice speed', 'Brzina glasa'],
    o_duck: ['Video volume while a description is heard', 'Jačina videa dok se čuje opis'],
    o_extended: ['Pause the video when a description needs more time (extended description)', 'Pauziraj video kada opisu treba više vremena (produžena deskripcija)'],
    o_maxup: ['Speak faster to fit, at most', 'Ubrzaj da stane, najviše'],
    o_up_none: ['Never faster', 'Nikad brže'],
    o_show: ['Show the description as text on the picture', 'Prikaži opis kao tekst na slici'],
    o_sec_live: ['Live description (sites without a script)', 'Opis uživo (sajtovi bez skripte)'],
    o_live_auto: ['Describe live when there is no script', 'Opisuj uživo kada nema skripte'],
    o_live_pause: ['Pause the video for a moment while a live description is written', 'Pauziraj video na trenutak dok se piše opis uživo'],
    o_sens: ['How big a change of picture starts a new description', 'Kolika promena slike pokreće novi opis'],
    o_sens_low: ['Only big changes (fewer descriptions)', 'Samo velike promene (manje opisa)'],
    o_sens_mid: ['Normal', 'Uobičajeno'],
    o_sens_high: ['Smaller changes too (more descriptions)', 'I manje promene (više opisa)'],
    o_density: ['How many descriptions Gemini writes for YouTube', 'Koliko opisa Gemini piše za YouTube'],
    o_dens_essential: ['Essential', 'Osnovno'],
    o_dens_normal: ['Normal', 'Uobičajeno'],
    o_dens_detailed: ['Detailed', 'Detaljno'],
    o_sec_ai: ['AI', 'AI'],
    o_ai: ['Who looks at the picture', 'Ko gleda sliku'],
    o_ai_gemini: ['Gemini (Google, best)', 'Gemini (Google, najbolje)'],
    o_ai_ollama: ['Local model in Ollama (private, on this computer)', 'Lokalni model u Ollami (privatno, na ovom računaru)'],
    o_ollama_model: ['Ollama model (empty = found automatically)', 'Ollama model (prazno = pronalazi se sam)'],
    o_ollama_allow: ['Allow the extension to reach Ollama on this computer', 'Dozvoli dodatku da pristupi Ollami na ovom računaru'],
    o_ollama_allowed: ['Ollama can be reached.', 'Ollama je dostupna.'],
    o_key: ['Gemini key', 'Gemini ključ'],
    o_key_save: ['Test and save the key', 'Proveri i sačuvaj ključ'],
    o_key_testing: ['Testing the key…', 'Proveravam ključ…'],
    o_key_saved: ['The key works and is saved.', 'Ključ radi i sačuvan je.'],
    o_key_bad: ['The key does not work: {0}', 'Ključ ne radi: {0}'],
    o_key_state_none: ['No key is saved.', 'Ključ nije sačuvan.'],
    o_key_state_saved: ['A key is saved.', 'Ključ je sačuvan.'],
    o_key_how: ['How to get a Gemini key', 'Kako dobiti Gemini ključ'],
    o_key_steps: [
      '1. Press "Open Google AI Studio" below. The page aistudio.google.com/app/apikey opens.\n2. Sign in with your Google account (the same as for Gmail or YouTube).\n3. Press "Create API key". If Google asks for a project, choose the one it offers, or create a new one.\n4. Copy the key (a long line that starts with AIza).\n5. Come back here, paste it into the Gemini key field, and press "Test and save the key".\nThe Gemini Flash models have a free tier, and no payment card is needed. The key is kept only in this browser; keep it to yourself, like a password.',
      '1. Pritisni „Otvori Google AI Studio” ispod. Otvara se stranica aistudio.google.com/app/apikey.\n2. Prijavi se svojim Google nalogom (istim kao za Gmail ili YouTube).\n3. Pritisni „Create API key”. Ako Google traži projekat, izaberi onaj koji ponudi ili napravi novi.\n4. Kopiraj ključ (dugačak red koji počinje sa AIza).\n5. Vrati se ovde, nalepi ga u polje za Gemini ključ i pritisni „Proveri i sačuvaj ključ”.\nGemini Flash modeli imaju besplatan nivo i ne treba platna kartica. Ključ se čuva samo u ovom pregledaču; čuvaj ga za sebe, kao lozinku.'],
    o_key_link: ['Open Google AI Studio (Gemini key)', 'Otvori Google AI Studio (Gemini ključ)'],
    o_privacy: ['Privacy: with Gemini, pictures from the video (or the YouTube link) are sent to Google to be described. With Ollama nothing leaves the computer. The extension collects nothing itself.', 'Privatnost: sa Geminijem se slike iz videa (ili YouTube link) šalju Google-u da bi se opisale. Sa Ollamom ništa ne napušta računar. Dodatak sam ništa ne prikuplja.'],
    o_about: ['Made by Demir Ajvazi, part of the Ultra suite. License: GPL-3.0.', 'Autor: Demir Ajvazi, deo Ultra paketa. Licenca: GPL-3.0.']
  };

  let uiLang = 'en';

  function t(key, ...args) {
    const pair = T[key];
    let s = pair ? (uiLang === 'sr' ? pair[1] : pair[0]) : key;
    args.forEach((a, i) => { s = s.split('{' + i + '}').join(String(a)); });
    return s;
  }

  async function loadSettings() {
    const got = await chrome.storage.local.get('settings');
    const s = Object.assign({}, DEFAULTS, got.settings || {});
    uiLang = s.uiLang === 'sr' ? 'sr' : 'en';
    return s;
  }

  async function saveSettings(s) {
    uiLang = s.uiLang === 'sr' ? 'sr' : 'en';
    await chrome.storage.local.set({ settings: s });
  }

  function youtubeId(url) {
    try {
      const u = new URL(url);
      if (/(^|\.)youtube\.com$/.test(u.hostname)) {
        if (u.searchParams.get('v')) return u.searchParams.get('v');
        const m = /^\/(shorts|live|embed)\/([\w-]{6,})/.exec(u.pathname);
        if (m) return m[2];
      }
      if (u.hostname === 'youtu.be') return u.pathname.slice(1).split('/')[0] || null;
    } catch (e) { }
    return null;
  }

  /** Which video a script belongs to: "yt:ID" for YouTube, otherwise the address without its query. */
  function keyFor(url) {
    const id = youtubeId(url);
    if (id) return 'yt:' + id;
    try { const u = new URL(url); return u.origin + u.pathname; } catch (e) { return url; }
  }

  function parseTime(text) {
    const m = /^(?:(\d{1,2}):)?(\d{1,3}):(\d{1,2}(?:[.,]\d+)?)$/.exec((text || '').trim());
    if (m) return (m[1] ? +m[1] * 3600 : 0) + (+m[2]) * 60 + parseFloat(m[3].replace(',', '.'));
    const n = parseFloat((text || '').replace(',', '.'));
    return isNaN(n) ? null : n;
  }

  /** Reads WebVTT, SRT, or our text format ("mm:ss.s | description"). Returns { lines, source }. */
  function parseScriptFile(text) {
    text = (text || '').replace(/\r/g, '').replace(/^﻿/, '');
    let source = null;
    const note = /^NOTE source:\s*(\S+)/m.exec(text);
    if (note) source = note[1];
    const lines = [];
    if (text.includes('-->')) {
      for (const block of text.split(/\n\s*\n/)) {
        const rows = block.split('\n').map(r => r.trim()).filter(Boolean);
        const i = rows.findIndex(r => r.includes('-->'));
        if (i < 0) continue;
        const t0 = parseTime(rows[i].split('-->')[0].trim());
        const body = rows.slice(i + 1).join(' ').replace(/<[^>]+>/g, '').trim();
        if (t0 != null && body) lines.push({ t: Math.round(t0 * 10) / 10, text: body });
      }
    } else {
      for (const raw of text.split('\n')) {
        const s = raw.trim();
        if (!s) continue;
        const m = /^([\d:.,]+)\s*[|\-–]\s*(.*)$/.exec(s);
        const t0 = m ? parseTime(m[1]) : null;
        if (t0 != null) { if (m[2].trim()) lines.push({ t: t0, text: m[2].trim() }); }
        else if (lines.length) lines[lines.length - 1].text += ' ' + s;
      }
    }
    lines.sort((a, b) => a.t - b.t);
    return { lines, source };
  }

  function estimateSeconds(text, lang) {
    const words = (text || '').split(/\s+/).filter(Boolean).length;
    return 0.3 + words / (WPS[lang] || 2.3);
  }

  const LINKS = {
    sponsors: 'https://github.com/sponsors/demirajvazi10-max',
    geminiKeys: 'https://aistudio.google.com/app/apikey'
  };

  g.UAD = { DEFAULTS, LANGS, NATIVE, LOCALE, LINKS, t, loadSettings, saveSettings, youtubeId, keyFor, parseScriptFile, estimateSeconds,
    setUiLang: (l) => { uiLang = l === 'sr' ? 'sr' : 'en'; }, get uiLang() { return uiLang; } };
})(globalThis);
