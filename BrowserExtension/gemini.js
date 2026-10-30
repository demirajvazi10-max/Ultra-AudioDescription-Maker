/* Ultra Audio Description — talking to the AI (used by the background service worker).
 * Gemini with the user's own key (Google AI Studio), or a local model in Ollama.
 * The prompts are the same as in the Ultra Audio Description Maker desktop program:
 * every description starts at the exact moment of its action, like audio description on Netflix. */
(function (g) {
  'use strict';

  const BASE = 'https://generativelanguage.googleapis.com/v1beta/';

  const PROMPT_NAMES = {
    sr: 'Serbian (ekavian, as spoken in Serbia), written in the Latin alphabet',
    hr: 'Croatian', bs: 'Bosnian, written in the Latin alphabet', de: 'German',
    es: 'Spanish', fr: 'French', it: 'Italian', en: 'English'
  };
  const promptName = (lang) => PROMPT_NAMES[lang] || 'English';

  function clock(s) {
    s = Math.max(0, s);
    const m = Math.floor(s / 60);
    const sec = (s - m * 60).toFixed(1).padStart(4, '0');
    return String(m).padStart(2, '0') + ':' + sec;
  }

  function parseTime(text) {
    const m = /(?:(\d{1,2}):)?(\d{1,3}):(\d{1,2}(?:[.,]\d+)?)/.exec(text || '');
    if (!m) return null;
    return (m[1] ? +m[1] * 3600 : 0) + (+m[2]) * 60 + parseFloat(m[3].replace(',', '.'));
  }

  const CYR = { 'а':'a','б':'b','в':'v','г':'g','д':'d','ђ':'đ','е':'e','ж':'ž','з':'z','и':'i','ј':'j','к':'k','л':'l','љ':'lj','м':'m',
    'н':'n','њ':'nj','о':'o','п':'p','р':'r','с':'s','т':'t','ћ':'ć','у':'u','ф':'f','х':'h','ц':'c','ч':'č','џ':'dž','ш':'š' };
  function serbianLatin(s) {
    if (!/[Ѐ-ӿ]/.test(s)) return s;
    let out = '';
    for (const c of s) {
      const lower = c.toLowerCase();
      const lat = CYR[lower];
      if (!lat) { out += c; continue; }
      out += c === lower ? lat : lat[0].toUpperCase() + lat.slice(1);
    }
    return out;
  }

  const DENSITY = {
    Essential: '- Describe only what a blind viewer cannot follow without: who appears, where we are, important actions, and text on screen.',
    Detailed: '- Describe every visible change that matters: a new place, a new person, every important action, gestures and expressions — about every 5 to 8 seconds when the picture changes.',
    Normal: '- Describe the important changes: a new place, a new person, important actions and expressions. Do not leave the picture undescribed for more than about 15 seconds when something new is seen.'
  };

  function mainPrompt(lang, total, from, to, before, density) {
    const L = [];
    L.push('You are a professional audio describer writing audio description like on Netflix, for blind and visually impaired viewers.');
    L.push(`Watch this video WITH its sound. The whole video is ${clock(total)} long; describe only the part from ${clock(from)} to ${clock(to)}.`);
    L.push('');
    L.push('TIMING — the most important rule:');
    L.push('- Each description starts at the EXACT moment the thing it describes appears or happens on screen. Never before it (that would spoil it), never after it (the viewer would hear it too late).');
    L.push('- Give each time with tenths of a second (MM:SS.s), at the moment the action begins or the new picture appears.');
    L.push('- Describe an action while it is happening, in the present tense.');
    L.push('- Keep each description short enough to be spoken before the next thing happens and, wherever possible, before someone speaks: about 2.5 words per second of free time. When almost no time is free, use only the most important words ("Ana runs out.").');
    L.push('');
    L.push('CONTENT:');
    L.push('- Describe what a blind viewer needs to follow it: who is there, what they do, where, important changes, facial expressions and gestures that matter, and any text on screen (titles, signs, messages, credits) — read that text out.');
    L.push(DENSITY[density] || DENSITY.Normal);
    L.push('- Do not describe what can be heard (the music, what is said). Do not interpret or guess feelings that are not shown.');
    L.push('- No camera words (shot, close-up, frame, camera). Do not repeat what is still true; describe only changes. Complete sentences only.');
    L.push('- Once a character\'s name is clear from the dialogue or the screen, use it; until then use a short consistent description ("the girl in the red coat").');
    L.push(`- Write in ${promptName(lang)}.`);
    L.push('');
    L.push('DIALOGUE MAP: also list every stretch in this part where someone speaks or sings, so the program knows where the dialogue is.');
    L.push('');
    L.push('Times are measured from the START OF THE WHOLE VIDEO, in minutes:seconds.tenths (minutes may go past 59, for example 95:20.5).');
    if (before) {
      L.push('The descriptions just before this part were (keep the same names, do not repeat them):');
      L.push(before);
    }
    L.push('Answer ONLY with lines in these two forms, nothing else:');
    L.push('MM:SS.s | description');
    L.push('SPEECH MM:SS.s-MM:SS.s');
    return L.join('\n');
  }

  /** Reads "MM:SS.s | text" and "SPEECH MM:SS.s-MM:SS.s" lines. */
  function parseScript(answer, from, to, lang) {
    let lines = [], speech = [];
    for (let raw of (answer || '').replace(/\r/g, '').split('\n')) {
      raw = raw.replace(/[*`]/g, '').trim().replace(/^[-•\s]+/, '');
      if (raw.length < 4) continue;
      const sp = /^SPEECH\s*:?\s*\[?([\d:.,]+)\]?\s*[-–—]+\s*\[?([\d:.,]+)\]?/i.exec(raw);
      if (sp) {
        const a = parseTime(sp[1]), b = parseTime(sp[2]);
        if (a != null && b != null && b > a) speech.push([a, b]);
        continue;
      }
      const m = /^\[?([\d:.,]+)\]?\s*[|\-–—:]\s*(.+)$/.exec(raw);
      if (!m) continue;
      const t = parseTime(m[1]);
      if (t == null) continue;
      let text = m[2].trim().replace(/^["“„]|["”]$/g, '');
      if (text.length < 2) continue;
      if (lang === 'sr') text = serbianLatin(text);
      lines.push({ t: Math.round(t * 10) / 10, text });
    }
    // times counted from the part instead of the whole video
    if (from > 0 && lines.length && lines.every(l => l.t < to - from + 1) && lines.some(l => l.t < from - 1)) {
      lines.forEach(l => { l.t += from; });
      speech = speech.map(([a, b]) => [a + from, b + from]);
    }
    lines = lines.filter(l => l.t >= from - 1 && l.t <= to + 1).sort((a, b) => a.t - b.t);
    return { lines, speech };
  }

  let cachedModel = null, cachedFor = null;

  /** The newest general "flash" model this key can use (keeps working when Google renames models). */
  async function model(key) {
    if (cachedModel && cachedFor === key) return cachedModel;
    let chosen = 'gemini-flash-latest';
    try {
      const r = await fetch(BASE + 'models?pageSize=200', { headers: { 'x-goog-api-key': key } });
      if (r.ok) {
        const list = ((await r.json()).models || [])
          .filter(m => (m.supportedGenerationMethods || []).includes('generateContent'))
          .map(m => (m.name || '').replace('models/', ''))
          .filter(n => /^gemini-[\d.]+-flash(-\d+)?$/.test(n))
          .sort((a, b) => parseFloat(b.split('-')[1]) - parseFloat(a.split('-')[1]));
        if (list.length) chosen = list[0];
      }
    } catch (e) { /* the alias works too */ }
    cachedFor = key;
    return cachedModel = chosen;
  }

  /** One request; "busy" (429) is retried a few times. */
  async function generate(key, parts, config, signal) {
    if (!key) throw new Error('no_key');
    let m = await model(key);
    const body = JSON.stringify({ contents: [{ parts }], generationConfig: config });
    for (let attempt = 0; ; attempt++) {
      const r = await fetch(`${BASE}models/${m}:generateContent`, {
        method: 'POST', signal,
        headers: { 'content-type': 'application/json', 'x-goog-api-key': key },
        body
      });
      const text = await r.text();
      if (r.status === 429 && attempt < 3) { await new Promise(res => setTimeout(res, 15000 * (attempt + 1))); continue; }
      if (r.status === 404 && attempt === 0 && m !== 'gemini-flash-latest') { m = cachedModel = 'gemini-flash-latest'; continue; }
      if (!r.ok) {
        let msg = r.status + '';
        try { msg = JSON.parse(text).error.message || msg; } catch (e) { }
        const err = new Error(msg);
        err.status = r.status;
        throw err;
      }
      const json = JSON.parse(text);
      const p = (((json.candidates || [])[0] || {}).content || {}).parts || [];
      return p.filter(x => !x.thought).map(x => x.text || '').join('').trim();
    }
  }

  /** The newer Flash models think before writing; describing needs only a little of it. */
  function thinkingFor(m) {
    return /^gemini-[3-9]/.test(m || '') ? { thinkingLevel: 'low' } : { thinkingBudget: 512 };
  }

  /** Like generate(), but the answer arrives while Gemini writes it; onText(textSoFar) after every piece. */
  async function generateStream(key, parts, config, onText, onBusy, signal) {
    if (!key) throw new Error('no_key');
    let m = await model(key);
    let body = JSON.stringify({ contents: [{ parts }], generationConfig: config });
    for (let attempt = 0; ; attempt++) {
      const r = await fetch(`${BASE}models/${m}:streamGenerateContent?alt=sse`, {
        method: 'POST', signal,
        headers: { 'content-type': 'application/json', 'x-goog-api-key': key },
        body
      });
      if (r.status === 429 && attempt < 4) { onBusy && onBusy(15 * (attempt + 1)); await new Promise(res => setTimeout(res, 15000 * (attempt + 1))); continue; }
      if (r.status === 404 && attempt === 0 && m !== 'gemini-flash-latest') { m = cachedModel = 'gemini-flash-latest'; continue; }
      if (!r.ok) {
        const t = await r.text();
        let msg = r.status + '';
        try { const j = JSON.parse(t); msg = (Array.isArray(j) ? j[0] : j).error.message || msg; } catch (e) { }
        if (r.status === 400 && config.thinkingConfig && /thinking/i.test(msg)) {
          // this model does not take the thinking setting: the same request without it
          delete config.thinkingConfig;
          body = JSON.stringify({ contents: [{ parts }], generationConfig: config });
          continue;
        }
        const err = new Error(msg); err.status = r.status; throw err;
      }
      const reader = r.body.getReader();
      const decoder = new TextDecoder();
      let buffer = '', text = '';
      for (;;) {
        const { value, done } = await reader.read();
        if (done) break;
        buffer += decoder.decode(value, { stream: true });
        let nl;
        while ((nl = buffer.indexOf('\n')) >= 0) {
          const line = buffer.slice(0, nl).trim();
          buffer = buffer.slice(nl + 1);
          if (!line.startsWith('data:')) continue;
          let chunk;
          try { chunk = JSON.parse(line.slice(5)); } catch (e) { continue; }
          if (chunk.error) throw new Error(chunk.error.message || 'Gemini error');
          const p = (((chunk.candidates || [])[0] || {}).content || {}).parts || [];
          const add = p.filter(x => !x.thought).map(x => x.text || '').join('');
          if (add) { text += add; onText && onText(text); }
        }
      }
      return text.trim();
    }
  }

  /**
   * Gemini watches a public YouTube video directly from its link and writes the whole
   * synchronized script, in 5-minute parts. onProgress(partIndex, parts, linesSoFar).
   */
  /** onProgress(fraction 0–1, descriptions so far, time reached); onBusy(seconds) while Google is busy. */
  async function writeYouTube(key, url, duration, lang, density, onProgress, onBusy, signal) {
    const total = duration > 0 ? duration : 600;
    const partLength = 300;
    const parts = Math.max(1, Math.ceil(total / partLength));
    let lines = [], speech = [];
    for (let i = 0; i < parts; i++) {
      const from = i * partLength, to = Math.min(total, (i + 1) * partLength);
      onProgress && onProgress(i / parts, lines.length, from);
      const before = lines.slice(-12).map(l => `${clock(l.t)} | ${l.text}`).join('\n');
      const request = [
        { file_data: { file_uri: url }, video_metadata: { start_offset: Math.floor(from) + 's', end_offset: Math.ceil(to) + 's', fps: 2 } },
        { text: mainPrompt(lang, total, from, to, before, density) }
      ];
      // low detail and little "thinking": several times faster, and still accurate for describing
      const config = { temperature: 0.3, maxOutputTokens: 16384, mediaResolution: 'MEDIA_RESOLUTION_LOW', thinkingConfig: thinkingFor(await model(key)) };
      const answer = await generateStream(key, request, config, (soFar) => {
        const partial = parseScript(soFar, from, to, lang).lines;
        if (!partial.length) return;
        const last = partial[partial.length - 1].t;
        const reached = Math.min(1, Math.max(0, (last - from) / Math.max(1, to - from)));
        onProgress && onProgress((i + reached) / parts, lines.length + partial.length, last);
      }, onBusy, signal);
      const r = parseScript(answer, from, to, lang);
      for (const l of r.lines) if (!lines.length || l.t > lines[lines.length - 1].t + 0.8) lines.push(l);
      speech = speech.concat(r.speech);
    }
    onProgress && onProgress(1, lines.length, total);
    speech.sort((a, b) => a[0] - b[0]);
    return { lines: lines.filter(l => l.t < total - 0.3), speech };
  }

  function framePrompt(lang, previous) {
    return 'You write live audio description for a blind viewer watching a video. Look at this picture from the video. ' +
      'In ONE short, complete sentence (4 to 12 words, present tense) say what is important that is NEW: who is there, what happens, where, ' +
      'and read any important text on screen. Do not mention the camera, the shot, the image or the picture. No introduction. ' +
      (previous && previous.length ? 'Already described just before: ' + previous.join(' / ') + '. If nothing important is new, answer exactly: SAME. ' : '') +
      `Answer only in ${promptName(lang)}.`;
  }

  function cleanSentence(text, lang) {
    if (!text) return null;
    text = text.replace(/\s+/g, ' ').trim().replace(/^["“„*]+|["”*]+$/g, '');
    if (/^SAME\.?$/i.test(text)) return 'SAME';
    text = text.replace(/^(in\s+)?(this|the)\s+(image|picture|frame|shot|scene)\s*(shows|depicts)?,?\s*/i, '');
    if (!text) return null;
    text = text[0].toUpperCase() + text.slice(1);
    if (lang === 'sr') text = serbianLatin(text);
    if (!/[.!?…]$/.test(text)) text += '.';
    return text;
  }

  /** One sentence about a picture (JPEG, base64) — Gemini. */
  async function describeFrame(key, jpegBase64, lang, previous, signal) {
    const answer = await generate(key, [
      { text: framePrompt(lang, previous) },
      { inline_data: { mime_type: 'image/jpeg', data: jpegBase64 } }
    ], { temperature: 0.2, maxOutputTokens: 1024 }, signal);
    return cleanSentence(answer, lang);
  }

  /** One sentence about a picture — a local vision model in Ollama (nothing leaves the computer). */
  async function describeFrameOllama(modelName, jpegBase64, lang, previous, signal) {
    let name = modelName;
    if (!name) {
      const tags = await (await fetch('http://localhost:11434/api/tags', { signal })).json();
      const names = (tags.models || []).map(m => m.name);
      for (const pref of ['qwen3-vl', 'qwen2.5vl', 'qwen2.5-vl', 'llava', 'minicpm-v', 'gemma3', 'moondream']) {
        name = names.find(n => n.toLowerCase().startsWith(pref));
        if (name) break;
      }
      if (!name) throw new Error('no_ollama_model');
    }
    const r = await fetch('http://localhost:11434/api/generate', {
      method: 'POST', signal, headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ model: name, prompt: framePrompt(lang, previous), images: [jpegBase64], stream: false, keep_alive: '30m',
        options: { temperature: 0.1, num_predict: 80 } })
    });
    if (!r.ok) throw new Error('Ollama ' + r.status);
    return cleanSentence((await r.json()).response, lang);
  }

  /** A quick test of a key: null when it works, otherwise the reason. */
  async function testKey(key) {
    try {
      cachedModel = null;
      const r = await generate(key, [{ text: 'Reply with the single word: OK' }], { temperature: 0, maxOutputTokens: 512 });
      return r ? null : 'no answer';
    } catch (e) { return e.message; }
  }

  g.UADGemini = { writeYouTube, describeFrame, describeFrameOllama, testKey, parseScript, parseTime, clock, serbianLatin };
})(globalThis);
