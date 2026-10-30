/* Ultra Audio Description — background service worker.
 * Puts the extension into a tab only when the user asks (shortcut or menu), finds the frame
 * that holds the video, talks to Gemini / Ollama, and keeps "always ready" sites. */
importScripts('common.js', 'gemini.js');

const FILES = ['common.js', 'content.js'];

/** Puts the content script into every frame of the tab (it ignores a second injection). */
async function inject(tabId) {
  try {
    await chrome.scripting.executeScript({ target: { tabId, allFrames: true }, files: FILES });
    return true;
  } catch (e) {
    return false;   // a browser page, the web store, or no permission
  }
}

/** The frame with the biggest video in the tab, or null. */
async function videoFrame(tabId) {
  try {
    const results = await chrome.scripting.executeScript({
      target: { tabId, allFrames: true },
      func: () => (globalThis.__uadVideoArea ? globalThis.__uadVideoArea() : 0)
    });
    let best = null;
    for (const r of results) if (r.result > 0 && (!best || r.result > best.result)) best = r;
    return best ? best.frameId : null;
  } catch (e) {
    return null;
  }
}

async function sendToFrame(tabId, frameId, msg) {
  try { return await chrome.tabs.sendMessage(tabId, msg, { frameId }); } catch (e) { return null; }
}

/** Sends a command to the video's frame; with no video, the top frame says so. */
async function command(tabId, msg) {
  if (!(await inject(tabId))) return { error: 'cannot' };
  // a moment for the video element to be found in a page that is still loading
  let frame = await videoFrame(tabId);
  if (frame == null) { await new Promise(r => setTimeout(r, 400)); frame = await videoFrame(tabId); }
  if (frame == null) {
    await sendToFrame(tabId, 0, { type: 'noVideo' });
    return { error: 'novideo' };
  }
  return (await sendToFrame(tabId, frame, msg)) || { error: 'noanswer' };
}

chrome.commands.onCommand.addListener(async (cmd, tab) => {
  if (!tab || tab.id == null) return;
  const type = { 'toggle-descriptions': 'toggle', 'describe-now': 'describeNow', 'repeat-last': 'repeat' }[cmd];
  if (type) await command(tab.id, { type });
});

// ── long work keeps the service worker awake ──
let keepAlive = null, workCount = 0;
function workStarted() { if (workCount++ === 0) keepAlive = setInterval(() => chrome.runtime.getPlatformInfo(() => { }), 20000); }
function workEnded() { if (--workCount <= 0) { workCount = 0; clearInterval(keepAlive); keepAlive = null; } }

async function geminiKey() { return (await chrome.storage.local.get('geminiKey')).geminiKey || ''; }

/** Gemini writes the whole script for a YouTube video; the page announces the progress. */
async function writeYouTube(tabId) {
  const state = await command(tabId, { type: 'getState' });
  if (!state || state.error) return state || { error: 'novideo' };
  const frame = await videoFrame(tabId);
  const say = (text) => sendToFrame(tabId, frame, { type: 'announce', text });
  const settings = await UAD.loadSettings();
  const key = await geminiKey();
  if (!key) { say(UAD.t('no_key')); return { error: 'nokey' }; }
  if (!state.youtubeId) return { error: 'notyoutube' };

  workStarted();
  say(UAD.t('yt_start'));
  (async () => {
    try {
      const url = 'https://www.youtube.com/watch?v=' + state.youtubeId;
      // the page hears the progress at every quarter, like the desktop program
      let lastQuarter = 0;
      const result = await UADGemini.writeYouTube(key, url, state.duration, settings.lang, settings.density, (fraction, count, reached) => {
        const quarter = Math.floor(fraction * 4);
        if (quarter > lastQuarter && fraction < 1) {
          lastQuarter = quarter;
          say(UAD.t('yt_progress', Math.round(fraction * 100), count, UADGemini.clock(reached)));
        }
      }, (seconds) => say(UAD.t('yt_busy', seconds)));
      await chrome.storage.local.set({ ['script:' + state.key]: { lines: result.lines, speech: result.speech, source: 'gemini', url, saved: Date.now() } });
      await sendToFrame(tabId, frame, { type: 'reloadScript', announce: UAD.t('yt_done', result.lines.length) });
    } catch (e) {
      say(UAD.t('yt_failed', e.message || e));
    } finally {
      workEnded();
    }
  })();
  return { ok: true };
}

/** One live description of a picture. */
async function describeFrame(msg) {
  const settings = await UAD.loadSettings();
  workStarted();
  try {
    if (settings.ai === 'ollama') {
      const text = await UADGemini.describeFrameOllama(settings.ollamaModel, msg.image, settings.lang, msg.previous || []);
      return { text };
    }
    const key = await geminiKey();
    if (!key) return { error: 'nokey' };
    const text = await UADGemini.describeFrame(key, msg.image, settings.lang, msg.previous || []);
    return { text };
  } catch (e) {
    if (e.status === 429) return { error: 'busy' };
    if (e.message === 'no_ollama_model' || /Failed to fetch/i.test(e.message || '')) return { error: settings.ai === 'ollama' ? 'ollama' : (e.message || 'failed') };
    return { error: e.message || 'failed' };
  } finally {
    workEnded();
  }
}

// ── "always ready" sites: the content script is registered for the site's address ──
function siteId(origin) { return 'site-' + origin.replace(/[^a-z0-9]/gi, '_'); }

async function setAlwaysOn(origin, on) {
  const { alwaysOn = [] } = await chrome.storage.local.get('alwaysOn');
  const list = alwaysOn.filter(o => o !== origin);
  try { await chrome.scripting.unregisterContentScripts({ ids: [siteId(origin)] }); } catch (e) { }
  if (on) {
    await chrome.scripting.registerContentScripts([{
      id: siteId(origin), matches: [origin + '/*'], js: FILES, allFrames: true, runAt: 'document_idle', persistAcrossSessions: true
    }]);
    list.push(origin);
  }
  await chrome.storage.local.set({ alwaysOn: list });
  return { ok: true, on };
}

chrome.runtime.onMessage.addListener((msg, sender, sendResponse) => {
  const reply = (p) => { p.then(sendResponse, (e) => sendResponse({ error: e.message || String(e) })); return true; };
  switch (msg.type) {
    case 'command': return reply(command(msg.tabId, msg.payload));
    case 'writeYouTube': return reply(writeYouTube(msg.tabId));
    case 'describeFrame': return reply(describeFrame(msg));
    case 'testKey': return reply(UADGemini.testKey(msg.key).then(problem => ({ problem })));
    case 'setAlwaysOn': return reply(setAlwaysOn(msg.origin, msg.on));
    case 'scriptSaved': return reply(command(msg.tabId, { type: 'reloadScript', announce: msg.announce }));
    default: return false;
  }
});
