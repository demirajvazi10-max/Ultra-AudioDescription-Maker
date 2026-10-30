# Third-party notices

Ultra Audio Description Maker is licensed under the GNU General Public License v3.0 (see `LICENSE`).
It uses the following components:

| Component | License | Use |
|---|---|---|
| [LibVLCSharp](https://code.videolan.org/videolan/LibVLCSharp) and [LibVLC](https://www.videolan.org/vlc/libvlc.html) | LGPL-2.1 or later | Video playback in the program |
| [NAudio](https://github.com/naudio/NAudio) | MIT | Playing the descriptions while previewing |
| [Newtonsoft.Json](https://www.newtonsoft.com/json) | MIT | Project and settings files |
| System.Speech, System.Security.Cryptography.ProtectedData | MIT (.NET) | Windows voices, encrypted keys |
| [FFmpeg](https://ffmpeg.org) (separate program, not included in the source) | LGPL-2.1 / GPL-2.0 or later, depending on the build | Reading, analysing and mixing the video and sound |
| [yt-dlp](https://github.com/yt-dlp/yt-dlp) (separate program, downloaded on first use) | Unlicense | Videos from links |
| Edge read-aloud token code, ported from [edge-tts](https://github.com/rany2/edge-tts) | LGPL-3.0 / GPL-3.0 | Microsoft natural voices |

Online services (used only when chosen, with the user's own keys where needed):
Google Gemini API, Google Cloud Text-to-Speech, Microsoft Edge read-aloud voices, Google Translate.
