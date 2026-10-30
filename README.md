# Ultra Audio Description Maker

*Creative tools that start with the keyboard, not the mouse.*

Ultra Audio Description Maker adds **audio description** to any video: a natural voice that tells blind and visually impaired viewers what is happening on screen — who is there, where we are, what they do, and any text that appears. Every description is heard **at the exact moment of its action**, the way it is done on Netflix.

It comes in three parts that work together:

| Part | What it is for | Get it |
|---|---|---|
| [**Windows program**](#the-windows-program) | Write, check, voice and export audio description for any video file or video link | [Installer or zip](../../releases/latest) |
| [**Browser extension**](#the-browser-extension) | Hear descriptions while you watch on YouTube and other sites, in Microsoft Edge and Google Chrome | [Extension zip](../../releases/latest) |
| [**Android app**](#the-android-app) | Share a video from the YouTube app and watch it with descriptions on the phone | [APK](../../releases/latest) |

It is part of the [Ultra suite](https://github.com/demirajvazi10-max) of accessible creative software, built on the audio description engine of Ultra Video Editor. It is made for anyone who describes or watches videos — independent filmmakers, YouTubers, schools, organisations, professional describers and viewers — and it works the same with a screen reader (JAWS, NVDA, Narrator, TalkBack) and with a mouse or touch.

## Contents

- [Download](#download)
- [The Windows program](#the-windows-program)
  - [How it works](#how-it-works)
  - [Synchronized, like on Netflix](#synchronized-like-on-netflix)
  - [Export](#export)
  - [Voices and languages](#voices-and-languages)
  - [Accessibility](#accessibility)
  - [Keyboard shortcuts](#keyboard-shortcuts)
  - [Projects](#projects)
  - [Requirements](#requirements)
- [The browser extension](#the-browser-extension)
- [The Android app](#the-android-app)
- [Getting the keys](#getting-the-keys)
- [Privacy](#privacy)
- [Building from source](#building-from-source)
- [Support](#support)
- [Author and license](#author-and-license)

## Download

Everything is on the [Releases](../../releases/latest) page. Each release has four files (the number is the version):

| File | What to do with it |
|---|---|
| `UltraAudioDescriptionMaker-Setup-….exe` | **Windows installer**, in English or Serbian. No administrator rights needed. |
| `UltraAudioDescriptionMaker-v…-win-x64.zip` | **Windows program without installing**: unpack it anywhere and start `UltraAudioDescriptionMaker.exe`. |
| `UltraAudioDescription-extension-….zip` | **Browser extension** for Edge and Chrome — see [installing the extension](#installing-the-extension). |
| `UltraAudioDescriptionMaker-v…-android.apk` | **Android app** — see [installing the app](#installing-the-app). |

On Windows nothing else has to be installed: the .NET runtime is included, and **FFmpeg is downloaded by the program itself** the first time it starts.

---

## The Windows program

### How it works

1. **Open a video** (`Ctrl+O`), or **a video from a link** (`Ctrl+L`) — YouTube and the many other sites yt-dlp supports. The program downloads yt-dlp by itself the first time, and keeps the link in the project.
2. **Write the descriptions with AI** (`F5`). Gemini watches the whole video, picture and sound together, the way a human describer does — or a local vision model in Ollama describes it shot by shot, without anything leaving the computer.
3. **Check and correct** every description in a simple list: edit the text, move it by a tenth of a second, add your own, delete what is not needed. Undo and redo work for everything.
4. **Hear it**: `Space` plays the video from two seconds before a description, with the description spoken at its place and the film sound lowered under it — exactly as it will sound in the finished video. `F6` plays the whole video with descriptions.
5. **Export** (`Ctrl+E`) the video with the description, a separate sound track, or the script as text.

### Synchronized, like on Netflix

Every description starts **at the exact moment of its action** — not before it, which would spoil it, and not after it, which would be too late.

- Gemini is asked for the moment each action begins, with tenths of a second, and it watches two pictures a second so the times are accurate. For a video opened from a public YouTube link, Gemini watches it directly on YouTube — nothing is copied or uploaded.
- The program then sharpens the times on the video itself: a description less than a second away from a shot change is moved onto that change, where the new picture really begins.
- Gemini also maps where people speak or sing. A description that is too long for the free time after its moment is spoken a little faster (up to a limit you choose) instead of being moved.
- What still does not fit is never hidden. The list says it plainly — *over dialogue*, *runs into dialogue*, *runs into the next description*, or *starts late* — and `F4` jumps to the next description that needs attention. `Ctrl+I` shows a full timing report.

### Export

Choose any combination:

- **Video with the description mixed in** (MP4) — the film sound goes down only while the voice speaks.
- **Video with two sound tracks** (MP4) — the original sound and the described sound, so the viewer picks the audio description track in the player, as on streaming services.
- **The whole sound with the description** (MP3 or WAV).
- **Only the description voice**, on silence, as long as the video — for mixing it yourself in another editor.
- **The script** as plain text (TXT), as a text description track (WebVTT, `Kind: descriptions`) and with times (SRT).

The picture is copied as it is, without re-encoding, whenever the format allows it. For a video that came from a link, the WebVTT file remembers the address, so the [browser extension](#the-browser-extension) and the [Android app](#the-android-app) know which web video it belongs to.

### Voices and languages

Descriptions can be written and spoken in Serbian, English, German, Croatian, Bosnian, Spanish, French and Italian.

- **Microsoft natural voices** — free, no account (for Serbian: Sofija and Nikola).
- **Google Chirp 3 HD voices** — with your own [Google Cloud key](#getting-the-keys).
- **Plain Google voice** — free.
- **Windows voices** installed on the computer — they work without the internet.

Every spoken sentence is kept in a voice cache, so previewing, re-checking and exporting never synthesize the same sentence twice. Serbian descriptions are always written in Latin script.

### Accessibility

- Every control has a name and, where useful, a help text for the screen reader. Labels have access keys (`Alt` + letter).
- The keyboard order is the work order: the list of descriptions, the selected description's time and text, its timing, then the buttons.
- The program speaks only when you asked for something or when long work finishes — it never chatters on its own.
- **Speed and precision** (in the *Write with AI* dialog): *Balanced* (the default) shows Gemini two pictures a second in low detail and lets it think only briefly — several times faster than full detail, and the times are still sharpened on the shot changes. *Fast* uses one picture a second; *Precise* uses full detail. A short video (up to about five minutes) goes inside the request itself, so there is no upload and no waiting for Google to prepare it.
- While Gemini writes, its answer streams in, so the percent is real: it shows how far into the video the descriptions have reached ("12 descriptions written, up to 01:40 of 04:12"). When Google is busy, the program says so and when it tries again; a connection that goes quiet for five minutes is retried by itself.
- Long work shows clear progress: the step ("Step 4 of 6: Gemini is watching and writing the descriptions"), a progress bar, and details such as "part 2 of 3, 14 descriptions so far" or "description 31 of 58", with the running time. The step is announced once when it begins and again at every quarter; **`F9` says the whole state at any moment**. The progress bar can also be reached with `Tab`.
- Messages are announced with UI Automation notifications, which JAWS, NVDA and Narrator read without moving the focus.
- The focus is never lost: after a dialog closes, the focus returns to the list of descriptions.
- Dark, light and high-contrast colours, with a clear focus frame for keyboard users who can see. "Like Windows" switches to high contrast automatically.
- The video picture can be hidden in Settings for a pure audio workflow.
- `Ctrl+Shift+E` opens all descriptions as plain text (`mm:ss.s | text` per line), easy to read and edit line by line with a screen reader.
- The interface is in English (the default) and Serbian: **Tools → Language / Jezik** switches at once, without restarting.

### Keyboard shortcuts

| Keys | Action |
|---|---|
| `Ctrl+O` | Open a video |
| `Ctrl+L` | Open a video from a link |
| `Ctrl+Shift+O` | Open a project |
| `Ctrl+S` / `Ctrl+Shift+S` | Save the project / save as |
| `F5` | Write the descriptions with AI |
| `F8` | Prepare the voices and check the timing |
| `F4` / `Shift+F4` | Next / previous description that needs attention |
| `Ctrl+I` | Timing report |
| `Ctrl+E` | Export |
| `Space` | Preview the selected description with the video |
| `Shift+Space` | Hear only the description's voice |
| `Enter` | Move the video to the selected description |
| `F2` / `Ctrl+F2` | Edit the text / the time |
| `Insert` / `Delete` | Add a description at the playback position / delete it |
| `Ctrl+Left` / `Ctrl+Right` | A tenth of a second earlier / later |
| `Ctrl+Shift+Left` / `Ctrl+Shift+Right` | One second earlier / later |
| `Ctrl+T` | Set the time to the playback position |
| `F6` | Play / pause with descriptions |
| `Escape` | Stop the preview or the playback (never stops long work, and does nothing while a menu is open) |
| `Ctrl+Shift+X` | Stop long work (AI, voices, export, download) — asks first |
| `Alt+Left` / `Alt+Right` | 5 seconds back / forward |
| `Ctrl+G` | Go to a time |
| `F9` | Say the position and how far the next description is; during long work, how far the work is |
| `Ctrl+Shift+E` | Edit all descriptions as text |
| `Ctrl+Z` / `Ctrl+Y` | Undo / redo |
| `Ctrl+Shift+P` | Settings |
| `F1` | List of keyboard shortcuts |

### Projects

A project (`.uad`) keeps the video path, the descriptions, the language and voice, and the dialogue map. When a project is saved next to its video, opening the video offers to open the project too. Scripts can also be imported from text, WebVTT and SRT files.

### Requirements

- Windows 10 or 11 (64-bit). The release builds include the .NET runtime.
- **FFmpeg** (`ffmpeg.exe` and `ffprobe.exe`). When the computer does not have it, the program **downloads it automatically** on the first start (a Windows build from GitHub, or from gyan.dev as a fallback; about 100 MB) into `%LOCALAPPDATA%\UltraAudioDescriptionMaker\Ffmpeg` — no administrator rights needed. **Tools → Download FFmpeg** fetches the newest version at any time. An existing FFmpeg is used instead when it is found next to the program, in `PATH`, in the usual install folders, or in an installed Ultra Video Editor; a different folder can be chosen in Settings.
- For videos from links: **yt-dlp**, downloaded by the program itself into `%LOCALAPPDATA%\UltraAudioDescriptionMaker\Tools` the first time a link is opened (**Tools → Update the link downloader** keeps it current).
- For Gemini: a free key from Google AI Studio — see [Getting the keys](#getting-the-keys). A key already saved in Ultra Video Editor is used automatically.
- For local descriptions: [Ollama](https://ollama.com) with a vision model, for example `qwen2.5vl` or `llava`.

---

## The browser extension

Audio description while you watch in **Microsoft Edge** or **Google Chrome**. Full details: [BrowserExtension/README.md](BrowserExtension/README.md).

- **YouTube** — Gemini watches the video directly from its link and writes the whole synchronized script. You keep watching meanwhile; the page announces the progress.
- **Other sites** — live description: when the picture changes, one frame goes to the AI and the new picture is described. By default the video pauses for that moment, so nothing is missed.
- **Scripts from the Windows program** — WebVTT, SRT or text files [exported](#export) by Ultra Audio Description Maker.
- **Timing like on Netflix** — the video sound goes down under each description; a long description is spoken a little faster, and if it still needs time the video pauses briefly and continues by itself (extended audio description).
- **Heard your way** — a browser voice (in Edge, the free natural voices), or your screen reader, also on braille. The description can also be shown as text on the picture.
- English and Serbian interface.

Sites that protect their video with DRM (most paid streaming services) do not let any extension see the picture; the extension detects this and says so. Scripts loaded from a file still work there.

| Keys | Action |
|---|---|
| `Alt+Shift+A` | The menu |
| `Alt+Shift+D` | Audio description on / off on this page |
| `Alt+Shift+S` | Describe the picture now |
| `Alt+Shift+R` | Repeat the last description (or say the status) |

### Installing the extension

Until the extension is in the stores:

1. Download `UltraAudioDescription-extension-….zip` from [Releases](../../releases/latest) and unpack it into a folder you keep.
2. Open `edge://extensions` (or `chrome://extensions`) and turn on **Developer mode**.
3. Choose **Load unpacked** and select the unpacked folder.
4. Open the extension's menu (`Alt+Shift+A`) → **Options and keys**, and add a [Gemini key](#getting-the-keys).

Privacy policy: [BrowserExtension/PRIVACY.md](BrowserExtension/PRIVACY.md). Store texts, the store logo and the Edge Add-ons publishing steps: [BrowserExtension/STORE_LISTING.md](BrowserExtension/STORE_LISTING.md).

---

## The Android app

Audio description on the phone while you watch YouTube. Full details: [Android/README.md](Android/README.md).

- **Share from YouTube** — in the YouTube app choose *Share → Ultra Audio Description Maker*.
- **Gemini writes the descriptions** from the video's YouTube link, with real progress announced to TalkBack.
- **Watch in the app** — YouTube's own player inside the app, with the video lowered under each description and paused briefly when a description needs more time.
- **Watch in the YouTube app** — the app follows the YouTube app's player and speaks each description at its moment; a notification has *Repeat* and *Stop*.
- **Scripts from the Windows program** can be loaded too.
- The phone's voice or TalkBack (also on braille); English and Serbian interface.

### Installing the app

1. Download `UltraAudioDescriptionMaker-v…-android.apk` from [Releases](../../releases/latest) on the phone.
2. Open it and allow installing from this source when Android asks.
3. Open the app → **Settings and key** and add a [Gemini key](#getting-the-keys).

Android 8.0 or newer is needed.

---

## Getting the keys

The Windows program's Settings (`Ctrl+Shift+P`), the extension's options and the Android app's settings all explain step by step how to get each key, with buttons that open the right pages directly.

- **Gemini** (needed for AI descriptions) — [Google AI Studio → API keys](https://aistudio.google.com/app/apikey): sign in with a Google account, press *Create API key*, copy the key, paste it into the settings and test it. The Flash models have a free tier; no payment card is needed.
- **Google Cloud** (optional, only for the HD voices in the Windows program) — [create a project](https://console.cloud.google.com/projectcreate), [turn on the Text-to-Speech API](https://console.cloud.google.com/apis/library/texttospeech.googleapis.com), then [create an API key](https://console.cloud.google.com/apis/credentials) (best restricted to the Cloud Text-to-Speech API). Google Cloud asks for a billing account even for its free monthly amount.

## Privacy

- **Windows program** — with Gemini, a small 480p copy of the video is sent to Google and deleted as soon as the descriptions are written (a public YouTube video is watched by Gemini directly from its link). With the local model, nothing leaves the computer. Keys are stored encrypted for the current Windows user.
- **Browser extension** — nothing runs on a page until you turn it on there. Only when you ask for descriptions is the YouTube address or a still picture sent to Gemini with your own key, or to Ollama on your own computer. See [PRIVACY.md](BrowserExtension/PRIVACY.md).
- **Android app** — only the YouTube address goes to Gemini, with your own key, when you ask for descriptions. The key and the scripts stay on the phone.

None of the three collects anything about you.

## Building from source

### Windows program

Open `UltraAudioDescriptionMaker.slnx` in Visual Studio 2022 or newer and build (.NET 8). FFmpeg does not have to be in the repository; the program downloads it. To ship a particular FFmpeg with a build anyway, put `ffmpeg.exe` and `ffprobe.exe` in a folder named `Ffmpeg` next to the project file — they are copied to the output.

### Installer

[`Installer/UltraAudioDescriptionMaker.iss`](Installer/UltraAudioDescriptionMaker.iss) is the Inno Setup script (Inno Setup 6.5 or newer). The Serbian translation of the installer, [`Installer/SerbianLatin.isl`](Installer/SerbianLatin.isl), is kept next to it, because Inno Setup does not ship it.

1. Publish the program into the `publish` folder: in Visual Studio choose **Build → Publish** with the profile **Installer** (`Properties/PublishProfiles/Installer.pubxml`).
2. Open the `.iss` file in Inno Setup and press **Compile** (`Ctrl+F9`). The setup file appears in `installer-output`.

The installer speaks English or Serbian (a Serbian installation also starts the program in Serbian), installs for the current user without administrator rights (or for all users, if chosen), can create a desktop shortcut, can open `.uad` projects with the program, and adds a Start menu link to GitHub Sponsors. When uninstalling, it asks whether to delete the settings, the saved keys, the voice cache and the downloaded FFmpeg as well. The version number comes from the program itself.

### Browser extension

The [`BrowserExtension`](BrowserExtension) folder is the extension itself (Manifest V3); load it unpacked as described in [Installing the extension](#installing-the-extension). Nothing has to be built.

### Android app

From the [`Android`](Android) folder run `gradlew assembleDebug`. The APK appears in `Android\app\build\outputs\apk\debug\UltraAudioDescriptionMaker-<version>.apk`. More in [Android/README.md](Android/README.md).

### GitHub Actions

[`.github/workflows/build.yml`](.github/workflows/build.yml) builds the Windows program and the Android app on every push and pull request to `main`, and keeps the results with the run. Pushing a tag that starts with `v` (for example `v0.1.2`, made in GitHub Desktop) also creates a GitHub release with all four [downloads](#download); the version number in the program, the installer, the extension and the app is taken from the tag.

## Support

If the program helps you, you can support its development on [GitHub Sponsors](https://github.com/sponsors/demirajvazi10-max). The link is always in the program too: **Help → About** and **Help → Support the project**.

## Author and license

Made by [Demir Ajvazi](https://github.com/demirajvazi10-max).

GPL-3.0 — the same as the rest of the Ultra suite. See [LICENSE](LICENSE) and [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
