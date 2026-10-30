# Ultra Audio Description Maker

*Creative tools that start with the keyboard, not the mouse.*

Ultra Audio Description Maker turns any video into a video with **audio description**: a natural voice that tells blind and visually impaired viewers what is happening on screen — who is there, where we are, what they do, and any text that appears.

It is a standalone program from the [Ultra suite](https://github.com/demirajvazi10-max), built on the audio description engine of Ultra Video Editor. It is made for anyone who describes videos: independent filmmakers, YouTubers, schools, organisations, and describers who want a fast, fully keyboard-driven workflow. It works the same with a screen reader (JAWS, NVDA, Narrator) and with a mouse.

## Download

Ready-to-run builds are on the [Releases](../../releases) page: the **installer** (`UltraAudioDescriptionMaker-Setup-….exe`, in English or Serbian, no administrator rights needed), or a zip to unpack and start `UltraAudioDescriptionMaker.exe`. Nothing else has to be installed — the .NET runtime is included, and **FFmpeg is downloaded by the program itself** the first time it starts.

## Two parts

- **Ultra Audio Description Maker** — the Windows program described below: write, check and export audio description for any video file, or for a video from a link.
- **Ultra Audio Description for the browser** — an extension for Microsoft Edge and Google Chrome that speaks descriptions while you watch on YouTube and other sites. See [BrowserExtension/README.md](BrowserExtension/README.md).
- **Ultra Audio Description for Android** — share a video from the YouTube app, let Gemini write the descriptions, and watch with them in the app or in the YouTube app itself. See [Android/README.md](Android/README.md).

## How it works

1. **Open a video** (`Ctrl+O`), or **a video from a link** (`Ctrl+L`) — YouTube and the many other sites yt-dlp supports. The program downloads yt-dlp by itself the first time, and keeps the link in the project.
2. **Write the descriptions with AI** (`F5`). Gemini watches the whole video, picture and sound together, the way a human describer does — or a local vision model in Ollama describes it shot by shot, without anything leaving the computer.
3. **Check and correct** every description in a simple list: edit the text, move it by a tenth of a second, add your own, delete what is not needed. Undo and redo work for everything.
4. **Hear it**: `Space` plays the video from two seconds before a description, with the description spoken at its place and the film sound lowered under it — exactly as it will sound in the finished video. `F6` plays the whole video with descriptions.
5. **Export** (`Ctrl+E`) the video with the description, a separate sound track, or the script as text.

## Synchronized, like on Netflix

Every description starts **at the exact moment of its action** — not before it, which would spoil it, and not after it, which would be too late.

- Gemini is asked for the moment each action begins, with tenths of a second, and it watches two pictures a second so the times are accurate. For a video opened from a public YouTube link, Gemini watches it directly on YouTube — nothing is copied or uploaded.
- The program then sharpens the times on the video itself: a description less than a second away from a shot change is moved onto that change, where the new picture really begins.
- Gemini also maps where people speak or sing. A description that is too long for the free time after its moment is spoken a little faster (up to a limit you choose) instead of being moved.
- What still does not fit is never hidden. The list says it plainly — *over dialogue*, *runs into dialogue*, *runs into the next description*, or *starts late* — and `F4` jumps to the next description that needs attention. `Ctrl+I` shows a full timing report.

## Export

Choose any combination:

- **Video with the description mixed in** (MP4) — the film sound goes down only while the voice speaks.
- **Video with two sound tracks** (MP4) — the original sound and the described sound, so the viewer picks the audio description track in the player, as on streaming services.
- **The whole sound with the description** (MP3 or WAV).
- **Only the description voice**, on silence, as long as the video — for mixing it yourself in another editor.
- **The script** as plain text (TXT), as a text description track (WebVTT, `Kind: descriptions`) and with times (SRT).

The picture is copied as it is, without re-encoding, whenever the format allows it. For a video that came from a link, the WebVTT file remembers the address, so the browser extension knows which web video it belongs to.

## Voices and languages

Descriptions can be written and spoken in Serbian, English, German, Croatian, Bosnian, Spanish, French and Italian.

- **Microsoft natural voices** — free, no account (for Serbian: Sofija and Nikola).
- **Google Chirp 3 HD voices** — with your own Google Cloud key.
- **Plain Google voice** — free.
- **Windows voices** installed on the computer — they work without the internet.

Every spoken sentence is kept in a voice cache, so previewing, re-checking and exporting never synthesize the same sentence twice. Serbian descriptions are always written in Latin script.

## Accessibility

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

## Keyboard shortcuts

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

## Projects

A project (`.uad`) keeps the video path, the descriptions, the language and voice, and the dialogue map. When a project is saved next to its video, opening the video offers to open the project too. Scripts can also be imported from text, WebVTT and SRT files.

## Requirements

- Windows 10 or 11, .NET 8 Desktop Runtime.
- **FFmpeg** (`ffmpeg.exe` and `ffprobe.exe`). When the computer does not have it, the program **downloads it automatically** on the first start (a Windows build from GitHub, or from gyan.dev as a fallback; about 100 MB) into `%LOCALAPPDATA%\UltraAudioDescriptionMaker\Ffmpeg` — no administrator rights needed. **Tools → Download FFmpeg** fetches the newest version at any time. An existing FFmpeg is used instead when it is found next to the program, in `PATH`, in the usual install folders, or in an installed Ultra Video Editor; a different folder can be chosen in Settings.
- For videos from links: **yt-dlp**, downloaded by the program itself into `%LOCALAPPDATA%\UltraAudioDescriptionMaker\Tools` the first time a link is opened (Tools → Update the link downloader keeps it current).
- For Gemini: a free key from Google AI Studio. A key already saved in Ultra Video Editor is used automatically.
- For local descriptions: [Ollama](https://ollama.com) with a vision model, for example `qwen2.5vl` or `llava`.

## Building

Open `UltraAudioDescriptionMaker.slnx` in Visual Studio 2022 or newer and build. FFmpeg does not have to be in the repository; the program downloads it. To ship a particular FFmpeg with a build anyway, put `ffmpeg.exe` and `ffprobe.exe` in a folder named `Ffmpeg` next to the project file — they are copied to the output.

### Installer

`Installer/UltraAudioDescriptionMaker.iss` is the Inno Setup script (Inno Setup 6.2 or newer):

1. Publish the program into the `publish` folder: in Visual Studio choose **Build → Publish** with the profile **Installer** (`Properties/PublishProfiles/Installer.pubxml`).
2. Open the `.iss` file in Inno Setup and press **Compile** (`Ctrl+F9`). The setup file appears in `installer-output`.

The installer speaks English or Serbian (a Serbian installation also starts the program in Serbian), installs for the current user without administrator rights (or for all users, if chosen), can create a desktop shortcut, can open `.uad` projects with the program, and adds a Start menu link to GitHub Sponsors. When uninstalling, it asks whether to delete the settings, the saved keys, the voice cache and the downloaded FFmpeg as well. The version number comes from the program itself.

### GitHub Actions

`.github/workflows/build.yml` builds the program on every push and pull request to `main`, and keeps the zip with the run. Pushing a tag that starts with `v` (for example `v0.1.0`, made in GitHub Desktop) also creates a GitHub release with the installer, the Android APK, the browser extension zip (ready for the Chrome Web Store and Edge Add-ons) and a self-contained `UltraAudioDescriptionMaker-v0.1.0-win-x64.zip`; the version number in the program is taken from the tag.

## Privacy

With Gemini, a small 480p copy of the video is sent to Google and deleted as soon as the descriptions are written. With the local model, nothing leaves the computer. Keys are stored encrypted for the current Windows user.

## Getting the keys

Settings (`Ctrl+Shift+P`) explain step by step how to get each key, with buttons that open the right pages directly:

- **Gemini** — [Google AI Studio → API keys](https://aistudio.google.com/app/apikey): sign in with a Google account, press *Create API key*, copy the key, paste it into Settings and press *Test and save the key*. The Flash models have a free tier; no payment card is needed.
- **Google Cloud (HD voices, optional)** — [create a project](https://console.cloud.google.com/projectcreate), [turn on the Text-to-Speech API](https://console.cloud.google.com/apis/library/texttospeech.googleapis.com), then [create an API key](https://console.cloud.google.com/apis/credentials) (best restricted to the Cloud Text-to-Speech API). Google Cloud asks for a billing account even for its free monthly amount.

## Support

If the program helps you, you can support its development on [GitHub Sponsors](https://github.com/sponsors/demirajvazi10-max). The link is always in the program too: **Help → About** and **Help → Support the project**.

## Author

Made by [Demir Ajvazi](https://github.com/demirajvazi10-max).

## License

GPL-3.0 — the same as the rest of the Ultra suite. See `LICENSE` and `THIRD-PARTY-NOTICES.md`.
