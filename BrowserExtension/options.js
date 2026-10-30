/* Options: every change is saved at once and confirmed through the status line (read by the screen reader). */
(async function () {
  'use strict';
  const U = globalThis.UAD;
  const $ = (id) => document.getElementById(id);
  let S = await U.loadSettings();

  function fill(select, items, value) {
    select.innerHTML = '';
    for (const [v, text] of items) {
      const o = document.createElement('option');
      o.value = String(v); o.textContent = text;
      select.appendChild(o);
    }
    select.value = String(value);
    if (select.selectedIndex < 0) select.selectedIndex = 0;
  }

  function texts() {
    document.documentElement.lang = U.uiLang;
    document.title = $('title').textContent = U.t('o_title');
    const set = { secGeneral: 'o_sec_general', uiLangLabel: 'o_ui', langLabel: 'o_lang', secVoice: 'o_sec_voice', speakWithLabel: 'o_speak',
      voiceLabel: 'o_voice', test: 'o_test', rateLabel: 'o_rate', duckLabel: 'o_duck', extendedLabel: 'o_extended', maxLabel: 'o_maxup',
      showTextLabel: 'o_show', densityLabel: 'o_density', secLive: 'o_sec_live', liveAutoLabel: 'o_live_auto', livePauseLabel: 'o_live_pause',
      sensLabel: 'o_sens', secAi: 'o_sec_ai', aiLabel: 'o_ai', ollamaModelLabel: 'o_ollama_model', ollamaAllow: 'o_ollama_allow',
      keyLabel: 'o_key', keySave: 'o_key_save', keyHowLabel: 'o_key_how', keyLink: 'o_key_link', privacy: 'o_privacy', about: 'o_about', sponsor: 'p_sponsor' };
    for (const [id, k] of Object.entries(set)) $(id).textContent = U.t(k);
    $('keySteps').value = U.t('o_key_steps');
    const opt = (sel, keys) => [...$(sel).options].forEach((o, i) => { o.textContent = U.t(keys[i]); });
    opt('speakWith', ['o_speak_voice', 'o_speak_reader']);
    opt('density', ['o_dens_essential', 'o_dens_normal', 'o_dens_detailed']);
    opt('liveSensitivity', ['o_sens_low', 'o_sens_mid', 'o_sens_high']);
    opt('ai', ['o_ai_gemini', 'o_ai_ollama']);
    fill($('lang'), U.LANGS.map(l => [l, U.NATIVE[l]]), S.lang);
    fill($('rate'), [[0.8, '80 %'], [0.9, '90 %'], [1, '100 %'], [1.1, '110 %'], [1.2, '120 %'], [1.35, '135 %'], [1.5, '150 %']], S.rate);
    fill($('duck'), [[0.2, '20 %'], [0.35, '35 %'], [0.5, '50 %'], [0.7, '70 %'], [1, '100 %']], S.duck);
    fill($('maxSpeedUp'), [[1, U.t('o_up_none')], [1.1, '10 %'], [1.25, '25 %'], [1.35, '35 %'], [1.5, '50 %']], S.maxSpeedUp);
    voicesList();
  }

  function voicesList() {
    const prefix = (U.LOCALE[S.lang] || 'en-US').slice(0, 2).toLowerCase();
    const all = speechSynthesis.getVoices();
    const mine = all.filter(v => (v.lang || '').toLowerCase().startsWith(prefix));
    const others = all.filter(v => !mine.includes(v));
    fill($('voiceName'), [['', U.t('o_voice_auto')], ...mine.map(v => [v.name, `${v.name} (${v.lang})`]), ...others.map(v => [v.name, `${v.name} (${v.lang})`])], S.voiceName);
  }
  speechSynthesis.addEventListener('voiceschanged', voicesList);

  function showBoxes() {
    $('ollamaBox').hidden = S.ai !== 'ollama';
    $('geminiBox').hidden = S.ai !== 'gemini';
  }

  async function keyState() {
    const { geminiKey } = await chrome.storage.local.get('geminiKey');
    $('keyState').textContent = geminiKey ? U.t('o_key_state_saved') : U.t('o_key_state_none');
  }

  const say = (text) => { $('status').textContent = ''; setTimeout(() => { $('status').textContent = text; }, 50); };

  // values into the form
  $('uiLang').value = S.uiLang;
  $('speakWith').value = S.speakWith;
  $('density').value = S.density;
  $('liveSensitivity').value = String(S.liveSensitivity);
  $('ai').value = S.ai;
  $('ollamaModel').value = S.ollamaModel || '';
  for (const id of ['extended', 'showText', 'liveWhenNoScript', 'livePause']) $(id).checked = !!S[id];
  texts();
  showBoxes();
  keyState();

  async function save() {
    S.uiLang = $('uiLang').value;
    S.lang = $('lang').value;
    S.speakWith = $('speakWith').value;
    S.voiceName = $('voiceName').value;
    S.rate = parseFloat($('rate').value);
    S.duck = parseFloat($('duck').value);
    S.maxSpeedUp = parseFloat($('maxSpeedUp').value);
    S.density = $('density').value;
    S.liveSensitivity = parseInt($('liveSensitivity').value, 10);
    S.ai = $('ai').value;
    S.ollamaModel = $('ollamaModel').value.trim();
    for (const id of ['extended', 'showText', 'liveWhenNoScript', 'livePause']) S[id] = $(id).checked;
    await U.saveSettings(S);
    showBoxes();
    say(U.t('o_saved'));
  }

  for (const el of document.querySelectorAll('select, input[type=checkbox]')) el.addEventListener('change', save);
  $('ollamaModel').addEventListener('change', save);
  $('uiLang').addEventListener('change', () => { texts(); });
  $('lang').addEventListener('change', () => { S.voiceName = ''; voicesList(); $('voiceName').value = ''; save(); });

  $('test').onclick = () => {
    const v = speechSynthesis.getVoices().find(x => x.name === $('voiceName').value);
    const u = new SpeechSynthesisUtterance(S.lang === 'sr' || S.lang === 'hr' || S.lang === 'bs' ? 'Ana ulazi u sobu i pali svetlo.' : 'Anna walks into the room and turns on the light.');
    if (v) u.voice = v;
    u.lang = U.LOCALE[S.lang];
    u.rate = parseFloat($('rate').value);
    speechSynthesis.cancel();
    speechSynthesis.speak(u);
  };

  $('keySave').onclick = async () => {
    const key = $('key').value.trim();
    if (!key) { $('key').focus(); return; }
    say(U.t('o_key_testing'));
    const r = await chrome.runtime.sendMessage({ type: 'testKey', key });
    if (r && r.problem) { say(U.t('o_key_bad', r.problem)); $('key').focus(); return; }
    await chrome.storage.local.set({ geminiKey: key });
    $('key').value = '';
    await keyState();
    say(U.t('o_key_saved'));
  };

  $('keyLink').onclick = () => chrome.tabs.create({ url: U.LINKS.geminiKeys });
  $('sponsor').onclick = () => chrome.tabs.create({ url: U.LINKS.sponsors });
  $('ollamaAllow').onclick = async () => {
    const ok = await chrome.permissions.request({ origins: ['http://localhost:11434/*', 'http://127.0.0.1:11434/*'] });
    if (ok) say(U.t('o_ollama_allowed'));
  };
})();
