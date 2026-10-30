# Ultra Audio Description — browser extension

Audio description for videos in the browser (Microsoft Edge and Google Chrome): a voice tells what happens on screen at the moment it happens. A companion to the **Ultra Audio Description Maker** desktop program.

## What it does

- **YouTube** — Gemini watches the video directly from its link and writes the whole synchronized script (with a map of where people speak). You keep watching meanwhile; the page announces the progress.
- **Other sites** — live description: when the picture changes, one frame goes to the AI and the new picture is described. By default the video pauses for that moment, so nothing is missed.
- **Scripts from the desktop program** — WebVTT, SRT or text files exported by Ultra Audio Description Maker; scripts made from a link know which web video they belong to.
- **Timing like on Netflix** — each description starts at its moment; the video sound goes down while it is heard; a long description is spoken a little faster, and if it still needs time the video pauses briefly and continues by itself (extended audio description).
- **Heard your way** — a voice of the browser (in Edge, the free natural voices), or your screen reader (also on braille). The description can also be shown as text on the picture.
- **English and Serbian** interface (English by default; Options → Language of the extension).

Sites that protect their video with DRM (most paid streaming services) do not let any extension see the picture. The extension detects this and says so; scripts loaded from a file still work there.

## Keyboard

| Keys | Action |
|---|---|
| `Alt+Shift+A` | The menu |
| `Alt+Shift+D` | Audio description on / off on this page |
| `Alt+Shift+S` | Describe the picture now |
| `Alt+Shift+R` | Repeat the last description (or say the status) |

The shortcuts can be changed at `edge://extensions/shortcuts` or `chrome://extensions/shortcuts`.

## Install while developing

1. Open `edge://extensions` (or `chrome://extensions`) and turn on **Developer mode**.
2. Choose **Load unpacked** and select this `BrowserExtension` folder.
3. Open the options (the menu → *Options and keys*) and add a Gemini key; the steps are there.

## Privacy

Nothing runs on a page until you turn it on there. Settings, the key and the scripts stay in the browser. Only when you ask for descriptions is the YouTube address or a still picture sent to Gemini with your own key — or to Ollama on your own computer. See [PRIVACY.md](PRIVACY.md).

## Publishing

The texts for the Chrome Web Store and Microsoft Edge Add-ons (descriptions in English and Serbian, permission justifications, data usage) are in [STORE_LISTING.md](STORE_LISTING.md). The GitHub workflow packs this folder into `UltraAudioDescription-extension-<version>.zip`, ready to upload to both stores.

## Author

Made by [Demir Ajvazi](https://github.com/demirajvazi10-max). Support the project on [GitHub Sponsors](https://github.com/sponsors/demirajvazi10-max).

## License

GPL-3.0, like the rest of the Ultra suite.
