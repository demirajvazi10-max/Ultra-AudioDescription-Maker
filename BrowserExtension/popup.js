/* The menu (Alt+Shift+A). Opening it gives the extension access to this tab; nothing runs on a page before that. */
(async function () {
  'use strict';
  const U = globalThis.UAD;
  const $ = (id) => document.getElementById(id);
  const S = await U.loadSettings();
  document.documentElement.lang = U.uiLang;

  $('title').textContent = U.t('p_title');
  $('liveLabel').textContent = U.t('p_live');
  $('alwaysLabel').textContent = U.t('p_always');
  $('load').textContent = U.t('p_load');
  $('now').textContent = U.t('p_now');
  $('remove').textContent = U.t('p_remove');
  $('keys').textContent = U.t('p_keys_help');
  $('options').textContent = U.t('p_options');
  $('sponsor').textContent = U.t('p_sponsor');
  $('write').textContent = U.t('p_write');
  $('live').checked = S.liveWhenNoScript;

  const say = (text) => { $('status').textContent = text; };
  say(U.t('p_checking'));

  const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
  const cmd = (payload) => chrome.runtime.sendMessage({ type: 'command', tabId: tab.id, payload });

  $('options').onclick = () => chrome.runtime.openOptionsPage();
  $('sponsor').onclick = () => chrome.tabs.create({ url: U.LINKS.sponsors });

  let state = await cmd({ type: 'getState' });
  if (!state || state.error === 'cannot') { say(U.t('p_cannot')); $('options').focus(); return; }
  if (state.error) { say(U.t('no_video')); $('options').focus(); return; }

  function render() {
    const yt = state.youtubeId ? ' (YouTube)' : '';
    say((state.active ? U.t('p_state_on', yt, state.lines) : U.t('p_state_off', yt, state.lines)) + (state.live ? U.t('p_live_note') : ''));
    $('toggle').textContent = state.active ? U.t('p_off') : U.t('p_on');
    $('write').disabled = !state.youtubeId;
    $('writeNote').textContent = state.youtubeId ? U.t('p_write_note') : U.t('p_write_only_yt');
    for (const id of ['toggle', 'load', 'live', 'now', 'always']) $(id).disabled = false;
    $('remove').disabled = !state.lines;
  }

  const { alwaysOn = [] } = await chrome.storage.local.get('alwaysOn');
  $('always').checked = alwaysOn.includes(state.origin);
  render();
  $('toggle').focus();

  $('toggle').onclick = async () => {
    const r = await cmd({ type: 'toggle' });
    state = await cmd({ type: 'getState' });
    render();
    if (r && r.active) window.close();   // the page says the rest; the menu gets out of the way
  };

  $('write').onclick = async () => {
    const r = await chrome.runtime.sendMessage({ type: 'writeYouTube', tabId: tab.id });
    if (r && r.error === 'nokey') { say(U.t('no_key')); $('options').focus(); return; }
    say(U.t('p_writing'));
    setTimeout(() => window.close(), 1500);
  };

  // the file dialog would close this small menu, so the file is chosen on a page of its own
  $('load').onclick = () => chrome.tabs.create({ url: `load.html?tab=${tab.id}&key=${encodeURIComponent(state.key)}` });

  $('now').onclick = async () => { window.close(); await cmd({ type: 'describeNow' }); };

  $('live').onchange = async () => {
    const s = await U.loadSettings();
    s.liveWhenNoScript = $('live').checked;
    await U.saveSettings(s);
    const r = await cmd({ type: 'setLive', on: $('live').checked && !state.lines });
    state.live = r && r.live;
    render();
  };

  $('always').onchange = async () => {
    const on = $('always').checked;
    if (on) {
      const granted = await chrome.permissions.request({ origins: [state.origin + '/*'] });
      if (!granted) { $('always').checked = false; return; }
    }
    await chrome.runtime.sendMessage({ type: 'setAlwaysOn', origin: state.origin, on });
    say(on ? U.t('p_always_on') : U.t('p_always_off'));
  };

  $('remove').onclick = async () => {
    await chrome.storage.local.remove('script:' + state.key);
    await cmd({ type: 'reloadScript', start: false, announce: U.t('p_removed') });
    state = await cmd({ type: 'getState' });
    render();
    say(U.t('p_removed'));
  };
})();
