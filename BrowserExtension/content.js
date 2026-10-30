/* Ultra Audio Description — runs in the page (only after the user turns it on, or on "always ready" sites).
 *
 * Follows the video's clock and speaks every description at its moment, like audio description
 * on Netflix: the video sound goes down while it speaks; a description that is too long for the
 * free time is spoken a little faster, and when it still needs more time the video pauses for a
 * moment (extended description) and continues by itself.
 * Without a script, the picture can be described live: when it changes, one frame goes to the AI.
 * Messages go to the screen reader through a live region; the description text can also be shown
 * on the picture for viewers who can see. */
(function () {
  'use strict';
  if (globalThis.__uadContent) return;
  globalThis.__uadContent = true;

  const U = globalThis.UAD;
  let S = Object.assign({}, U.DEFAULTS);

  let video = null;          // the video being described
  let active = false;        // audio description on
  let key = null;            // which video the script belongs to
  let script = null;         // { lines: [{t, text}], speech: [[from, to]] }
  let lastT = 0;
  let timer = null;
  let speaking = false, speakToken = 0, queue = [];
  let pausedByUs = false, savedVolume = null, boundary = null;
  let lastText = null;
  let noVoiceSaid = false, adSaid = false;
  const live = { on: false, busy: false, lastAt: -99, sig: null, dark: 0, lastSample: 0, previous: [], backoffUntil: 0, failed: false };

  // ── finding the video ──────────────────────────────────────────────────

  function findVideo() {
    let best = null, bestArea = 0;
    for (const v of document.querySelectorAll('video')) {
      const r = v.getBoundingClientRect();
      const area = r.width * r.height;
      if (area > bestArea && r.width > 120 && r.height > 70) { best = v; bestArea = area; }
    }
    return best;
  }

  // asked by the background to choose the frame with the video
  globalThis.__uadVideoArea = () => {
    const v = findVideo();
    if (!v) return 0;
    const r = v.getBoundingClientRect();
    return Math.round(r.width * r.height);
  };

  const isYouTubeAd = () => !!document.querySelector('#movie_player.ad-showing, .html5-video-player.ad-showing');

  // ── screen reader and visible text ─────────────────────────────────────

  let politeRegion = null, assertiveRegion = null, caption = null;

  function regions() {
    if (politeRegion && document.contains(politeRegion)) return;
    const hidden = 'position:absolute!important;width:1px!important;height:1px!important;overflow:hidden!important;clip:rect(0 0 0 0)!important;white-space:nowrap!important;';
    politeRegion = document.createElement('div');
    politeRegion.setAttribute('role', 'status');
    politeRegion.setAttribute('aria-live', 'polite');
    politeRegion.style.cssText = hidden;
    assertiveRegion = document.createElement('div');
    assertiveRegion.setAttribute('aria-live', 'assertive');
    assertiveRegion.setAttribute('aria-atomic', 'true');
    assertiveRegion.style.cssText = hidden;
    caption = document.createElement('div');
    caption.setAttribute('aria-hidden', 'true');   // the screen reader hears the description already
    caption.style.cssText = 'position:fixed;left:50%;bottom:12%;transform:translateX(-50%);max-width:80vw;z-index:2147483647;' +
      'background:rgba(0,0,0,.82);color:#fff;font:600 20px/1.35 system-ui,Segoe UI,sans-serif;padding:8px 14px;border-radius:6px;' +
      'border:2px solid #00E676;pointer-events:none;display:none;text-align:center;';
    document.documentElement.append(politeRegion, assertiveRegion, caption);
  }

  function placeCaption() {
    if (!caption) return;
    const host = document.fullscreenElement || document.documentElement;
    if (caption.parentNode !== host) host.appendChild(caption);
  }

  /** Says something through the screen reader (polite, does not cut off what is being read). */
  function announce(text) {
    regions();
    politeRegion.textContent = '';
    setTimeout(() => { politeRegion.textContent = text; }, 60);
  }

  function showCaption(text) {
    if (!S.showText || !caption) return;
    placeCaption();
    caption.textContent = text;
    caption.style.display = 'block';
  }

  function hideCaption() { if (caption) caption.style.display = 'none'; }

  // ── speaking ───────────────────────────────────────────────────────────

  let voices = [];
  function loadVoices() { voices = speechSynthesis.getVoices(); }
  if ('speechSynthesis' in globalThis) {
    loadVoices();
    speechSynthesis.addEventListener('voiceschanged', loadVoices);
  }

  function pickVoice() {
    if (S.voiceName) {
      const v = voices.find(v => v.name === S.voiceName);
      if (v) return v;
    }
    const prefix = (U.LOCALE[S.lang] || 'en-US').slice(0, 2).toLowerCase();
    const forLang = voices.filter(v => (v.lang || '').toLowerCase().startsWith(prefix));
    return forLang.find(v => /natural|online|neural/i.test(v.name)) || forLang[0] || null;
  }

  /** Speaks one text; resolves when it is over. rateFactor > 1 speaks faster. */
  function speak(text, rateFactor) {
    const token = ++speakToken;
    lastText = text;
    showCaption(text);
    if (S.speakWith === 'reader' || !('speechSynthesis' in globalThis)) {
      regions();
      assertiveRegion.textContent = '';
      setTimeout(() => { assertiveRegion.textContent = text; }, 40);
      const ms = U.estimateSeconds(text, S.lang) / (S.rate * rateFactor) * 1000 + 300;
      return new Promise(res => setTimeout(() => { if (token === speakToken) hideCaption(); res(); }, ms));
    }
    return new Promise(res => {
      const u = new SpeechSynthesisUtterance(text);
      const v = pickVoice();
      if (v) u.voice = v;
      else if (!noVoiceSaid) { noVoiceSaid = true; announce(U.t('no_voice')); }
      u.lang = U.LOCALE[S.lang] || 'en-US';
      u.rate = Math.min(2, Math.max(0.5, S.rate * rateFactor));
      let done = false;
      const finish = () => { if (done) return; done = true; if (token === speakToken) hideCaption(); res(); };
      u.onend = finish;
      u.onerror = finish;
      speechSynthesis.cancel();
      speechSynthesis.speak(u);
      // a safety net: some voices never report the end
      setTimeout(finish, (U.estimateSeconds(text, S.lang) / u.rate) * 1000 * 2.5 + 3000);
    });
  }

  function stopSpeaking() {
    speakToken++;
    queue = [];
    if ('speechSynthesis' in globalThis) speechSynthesis.cancel();
    hideCaption();
    restoreVolume();
    speaking = false;
    boundary = null;
  }

  function duck() {
    if (!video || savedVolume != null) return;
    savedVolume = video.volume;
    video.volume = Math.max(0, Math.min(1, savedVolume * S.duck));
  }

  function restoreVolume() {
    if (video && savedVolume != null) video.volume = savedVolume;
    savedVolume = null;
  }

  // ── the description script ─────────────────────────────────────────────

  async function loadScript() {
    key = U.keyFor(location.href);
    const got = await chrome.storage.local.get('script:' + key);
    script = got['script:' + key] || null;
    if (script && !Array.isArray(script.lines)) script = null;
    return script;
  }

  const lineCount = () => (script ? script.lines.length : 0);

  /** Where the free time after a moment ends: the next speech, or the next description. */
  function boundaryAfter(t, index) {
    let b = Infinity;
    const next = script && script.lines[index + 1];
    if (next) b = next.t;
    if (script && Array.isArray(script.speech)) {
      const inSpeech = script.speech.some(([a, z]) => t >= a && t < z);
      if (!inSpeech) for (const [a] of script.speech) if (a > t + 0.05 && a < b) { b = a; break; }
    }
    if (video && isFinite(video.duration)) b = Math.min(b, video.duration);
    return b;
  }

  async function sayLine(line, index) {
    speaking = true;
    const t = video ? video.currentTime : line.t;
    boundary = S.extended ? boundaryAfter(line.t, index) : null;
    const free = (isFinite(boundaryAfter(line.t, index)) ? boundaryAfter(line.t, index) : t + 30) - t - 0.15;
    const need = U.estimateSeconds(line.text, S.lang) / S.rate;
    const factor = free > 0.3 && need > free ? Math.min(need / free, S.maxSpeedUp) : 1;
    duck();
    await speak(line.text, factor);
    restoreVolume();
    speaking = false;
    boundary = null;
    if (queue.length) {
      const next = queue.shift();
      sayLine(next.line, next.index);
      return;
    }
    if (pausedByUs && video) { pausedByUs = false; video.play().catch(() => { }); }
  }

  function tick() {
    const v = findVideo() || video;
    if (v !== video) { stopSpeaking(); video = v; lastT = v ? v.currentTime : 0; }
    if (!video) return;
    const t = video.currentTime;

    if (isYouTubeAd()) {
      if (!adSaid && active) { adSaid = true; announce(U.t('ad_playing')); }
      lastT = t;
      return;
    }
    adSaid = false;

    const jumped = t < lastT - 0.05 || t > lastT + 1.5;
    if (jumped) {
      if (speaking && !pausedByUs) stopSpeaking();
      lastT = t;
      return;
    }

    if (!video.paused && script && script.lines.length) {
      const lines = script.lines;
      for (let i = 0; i < lines.length; i++) {
        const l = lines[i];
        if (l.t > lastT && l.t <= t) {
          if (speaking) {
            queue.push({ line: l, index: i });
            // extended description: wait for the previous one, the picture holds
            if (S.extended && !pausedByUs) { pausedByUs = true; video.pause(); }
          } else sayLine(l, i);
        }
      }
    }

    // extended description: someone is about to speak (or the next description is due) and the voice still talks
    if (speaking && S.extended && boundary != null && isFinite(boundary) && !video.paused && t >= boundary - 0.1) {
      pausedByUs = true;
      video.pause();
    }

    if (live.on && !video.paused) liveTick(t);
    lastT = t;
  }

  // ── live description ───────────────────────────────────────────────────

  const sampleCanvas = document.createElement('canvas');
  sampleCanvas.width = 32; sampleCanvas.height = 18;

  /** A tiny grey picture of the frame, to notice changes. Null when the page forbids looking. */
  function sample() {
    try {
      const c = sampleCanvas.getContext('2d', { willReadFrequently: true });
      c.drawImage(video, 0, 0, 32, 18);
      const d = c.getImageData(0, 0, 32, 18).data;
      const out = new Uint8Array(32 * 18);
      for (let i = 0; i < out.length; i++) out[i] = (d[i * 4] * 3 + d[i * 4 + 1] * 6 + d[i * 4 + 2]) / 10;
      return out;
    } catch (e) {
      return null;   // a video from another site without permission ("tainted")
    }
  }

  function frameJpeg() {
    const w = 512, h = Math.round(512 * (video.videoHeight || 9) / (video.videoWidth || 16));
    const c = document.createElement('canvas');
    c.width = w; c.height = h;
    c.getContext('2d').drawImage(video, 0, 0, w, h);
    return c.toDataURL('image/jpeg', 0.8).split(',')[1];
  }

  function liveTick(t) {
    const now = performance.now();
    if (now - live.lastSample < 500) return;
    live.lastSample = now;
    if (live.busy || speaking || now < live.backoffUntil) return;
    const sig = sample();
    if (!sig) { liveFail(U.t('live_blocked')); return; }
    const mean = sig.reduce((a, b) => a + b, 0) / sig.length;
    if (mean < 3 && t > 2) {
      // a protected picture (DRM) looks black to the extension
      if (++live.dark >= 6) { liveFail(U.t('live_protected')); return; }
    } else live.dark = 0;
    let diff = 0;
    if (live.sig) { for (let i = 0; i < sig.length; i++) diff += Math.abs(sig[i] - live.sig[i]); diff /= sig.length; }
    live.sig = sig;
    const since = t - live.lastAt;
    if (since < 0) live.lastAt = -99;   // went back in the video
    if ((live.lastAt < 0 && t > 0.5) || (since > 6 && diff > S.liveSensitivity) || since > 25) describeNow(false);
  }

  function liveFail(message) {
    if (live.failed) return;
    live.failed = true;
    live.on = false;
    announce(message);
  }

  /** Describes the picture that is on screen now. <forced>: asked with Alt+Shift+S (says "nothing new" too). */
  async function describeNow(forced) {
    if (!video || live.busy) return;
    live.busy = true;
    const t = video.currentTime;
    live.lastAt = t;
    let image;
    try { image = frameJpeg(); } catch (e) { live.busy = false; liveFail(U.t('live_blocked')); live.failed = false; return; }
    const pause = S.livePause && !video.paused;
    if (pause) { pausedByUs = true; video.pause(); }
    let answer = null;
    try { answer = await chrome.runtime.sendMessage({ type: 'describeFrame', image, previous: live.previous.slice(-3) }); }
    catch (e) { answer = { error: e.message }; }
    live.busy = false;
    if (answer && answer.text && answer.text !== 'SAME') {
      live.previous.push(answer.text);
      if (live.previous.length > 6) live.previous.shift();
      speaking = true;
      duck();
      await speak(answer.text, 1);
      restoreVolume();
      speaking = false;
    } else if (answer && answer.error) {
      if (answer.error === 'nokey') { live.on = false; announce(U.t('no_key')); }
      else if (answer.error === 'busy') { live.backoffUntil = performance.now() + 30000; announce(U.t('ai_busy')); }
      else if (answer.error === 'ollama') { live.on = false; announce(U.t('ollama_missing')); }
      else announce(U.t('ai_error', answer.error));
    } else if (forced && lastText) {
      await speak(lastText, 1);
    }
    if (pause && pausedByUs) { pausedByUs = false; video.play().catch(() => { }); }
  }

  // ── on / off ───────────────────────────────────────────────────────────

  async function start(quiet) {
    S = await U.loadSettings();
    video = findVideo();
    if (!video) { announce(U.t('no_video')); return; }
    regions();
    await loadScript();
    active = true;
    lastT = video.currentTime;
    live.on = !lineCount() && S.liveWhenNoScript;
    live.failed = false; live.lastAt = -99; live.sig = null; live.dark = 0;
    clearInterval(timer);
    timer = setInterval(tick, 100);
    if (!quiet) announce(lineCount() ? U.t('on', lineCount()) : live.on ? U.t('on_live') : U.t('on_empty'));
  }

  function stop() {
    active = false;
    live.on = false;
    clearInterval(timer);
    timer = null;
    stopSpeaking();
    if (pausedByUs && video) { pausedByUs = false; video.play().catch(() => { }); }
    announce(U.t('off'));
  }

  // a new video in the same page (YouTube moves from video to video without loading a page)
  let lastHref = location.href;
  setInterval(async () => {
    if (location.href === lastHref) return;
    lastHref = location.href;
    if (!active) return;
    if (U.keyFor(location.href) === key) return;
    stopSpeaking();
    live.previous = [];
    await loadScript();
    live.on = !lineCount() && S.liveWhenNoScript;
    live.failed = false; live.lastAt = -99; live.sig = null;
    lastT = 0;
    announce(lineCount() ? U.t('new_video', lineCount()) : U.t('new_video_none'));
  }, 1000);

  chrome.storage.onChanged.addListener((changes) => {
    if (changes.settings) S = Object.assign({}, U.DEFAULTS, changes.settings.newValue || {}), U.setUiLang(S.uiLang);
  });

  chrome.runtime.onMessage.addListener((msg, sender, sendResponse) => {
    // only the frame that holds the video answers (the background chooses it)
    switch (msg.type) {
      case 'toggle':
        (active ? Promise.resolve(stop()) : start()).then(() => sendResponse({ active }));
        return true;
      case 'start':
        (active ? Promise.resolve() : start()).then(() => sendResponse({ active }));
        return true;
      case 'stop':
        if (active) stop();
        sendResponse({ active });
        return false;
      case 'describeNow':
        (async () => {
          if (!video) { S = await U.loadSettings(); video = findVideo(); }
          if (!video) { announce(U.t('no_video')); sendResponse({}); return; }
          regions();
          await describeNow(true);
          sendResponse({});
        })();
        return true;
      case 'repeat':
        (async () => {
          S = await U.loadSettings();
          if (lastText) await speak(lastText, 1);
          else if (active) announce(lineCount() ? U.t('on', lineCount()) : U.t('nothing_to_repeat'));
          else announce(U.t('nothing_to_repeat'));
          sendResponse({});
        })();
        return true;
      case 'getState':
        (async () => {
          if (!active) await loadScript();
          const v = video || findVideo();
          sendResponse({
            active, live: live.on, lines: lineCount(), key: U.keyFor(location.href),
            youtubeId: U.youtubeId(location.href), duration: v && isFinite(v.duration) ? v.duration : 0,
            title: document.title, origin: location.origin
          });
        })();
        return true;
      case 'setLive':
        live.on = !!msg.on && active;
        live.failed = false; live.lastAt = -99;
        sendResponse({ live: live.on });
        return false;
      case 'reloadScript':
        (async () => {
          await loadScript();
          if (active) live.on = !lineCount() && S.liveWhenNoScript;
          else if (msg.start !== false) await start(true);
          if (msg.announce) announce(msg.announce);
          sendResponse({ lines: lineCount() });
        })();
        return true;
      case 'announce':
        regions();
        announce(msg.text);
        sendResponse({});
        return false;
      case 'noVideo':
        regions();
        U.loadSettings().then(() => announce(U.t('no_video')));
        sendResponse({});
        return false;
      default:
        return false;
    }
  });

  // "always ready" sites: the extension was put here by the browser, start by itself when a video appears
  (async () => {
    const { alwaysOn = [] } = await chrome.storage.local.get('alwaysOn');
    if (!alwaysOn.includes(location.origin)) return;
    S = await U.loadSettings();
    let tries = 0;
    const wait = setInterval(() => {
      if (findVideo()) { clearInterval(wait); if (!active) start(); }
      else if (++tries > 60) clearInterval(wait);
    }, 1000);
  })();
})();
