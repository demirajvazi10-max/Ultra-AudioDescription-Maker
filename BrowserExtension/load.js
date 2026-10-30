/* Loading a script file for the video in another tab (a page of its own, because a file dialog closes the small menu). */
(async function () {
  'use strict';
  const U = globalThis.UAD;
  const $ = (id) => document.getElementById(id);
  await U.loadSettings();
  document.documentElement.lang = U.uiLang;
  const params = new URLSearchParams(location.search);
  const tabId = +params.get('tab');
  const key = params.get('key');

  document.title = $('title').textContent = U.t('l_title');
  $('fileLabel').textContent = U.t('l_label');
  $('help').textContent = U.t('l_help');
  $('close').textContent = U.t('l_close');
  $('close').onclick = () => window.close();
  $('file').focus();

  $('file').onchange = async () => {
    const f = $('file').files[0];
    if (!f) return;
    const { lines, source } = U.parseScriptFile(await f.text());
    if (!lines.length) { $('status').textContent = U.t('l_empty'); return; }
    // a script exported by the desktop program says which video it is for
    const target = source ? U.keyFor(source) : key;
    await chrome.storage.local.set({ ['script:' + target]: { lines, speech: [], source: 'file', file: f.name, url: source || null, saved: Date.now() } });
    if (tabId) await chrome.runtime.sendMessage({ type: 'scriptSaved', tabId, announce: U.t('script_loaded', lines.length) });
    $('status').textContent = U.t('l_done', lines.length);
    $('close').focus();
  };
})();
