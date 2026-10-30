# Ultra Audio Description — Android

Audio description on the phone: a voice tells what happens on screen, at the moment it happens, while you watch YouTube. The Android companion of the **Ultra Audio Description Maker** desktop program and the browser extension.

## What it does

- **Share from YouTube** — in the YouTube app choose *Share → Ultra Audio Description*. The link arrives in the app.
- **Write the descriptions with Gemini** — Gemini watches the video directly from its YouTube link and writes the whole synchronized script, with a map of where people speak. The answer streams in, so the progress is real; it is announced when it starts, at every quarter and at the end. You can leave the screen meanwhile.
- **Watch here with descriptions** — YouTube's own embedded player inside the app. The app lowers the video under each description, speaks a long description a little faster, and pauses the video for a moment when a description needs more time (extended description).
- **Watch in the YouTube app with descriptions** — the app follows the YouTube app's player (through Android's media sessions) and speaks each description at its moment; Android lowers YouTube while the voice speaks, and YouTube is paused for a moment when a description needs more time. The right script is found by the video's title. A notification has *Repeat* and *Stop*.
- **Scripts from the desktop program** — WebVTT, SRT or text files exported by Ultra Audio Description Maker can be loaded; scripts made from a link know which video they belong to.
- **Heard your way** — the phone's voice (Android text-to-speech), or TalkBack (also on braille).
- **English and Serbian** — English by default; Settings → Language of the app switches to Serbian at once. Descriptions in Serbian, English, German, Croatian, Bosnian, Spanish, French and Italian.

Videos whose owner does not allow embedding cannot play inside the app; use *Watch in the YouTube app* for those. Apps that protect their picture (most paid streaming services) cannot be described.

## Accessibility

- Every control is a standard Android control with a label; the controls come before the video in the reading order.
- Progress and results are announced to TalkBack once — never a constant stream of messages.
- Light and dark follow the phone; Android's high-contrast text works on top.

## Permissions

- **Internet** — Gemini and YouTube.
- **Notification access** (asked only for *Watch in the YouTube app*) — Android's way to let an app see which video another app plays and where it is. No notification is read.
- **Notifications** — the notification with *Repeat* and *Stop* while descriptions follow the YouTube app.
- **Foreground service (media playback)** — keeps the descriptions running while the YouTube app is in front.

## Building

From the `Android` folder:

```
gradlew assembleDebug
```

The APK is `app\build\outputs\apk\debug\UltraAudioDescriptionMaker-0.1.0.apk` (the number is the version). If Gradle cannot find the Android SDK, copy `local.properties.example` to `local.properties`. Release builds are signed when a `keystore.properties` file (storeFile, storePassword, keyAlias, keyPassword) is next to `settings.gradle.kts`; it is never committed.

The GitHub workflow builds the APK on every push and adds it to each release as `UltraAudioDescriptionMaker-v0.1.0-android.apk`.

## Privacy

The YouTube address of a video goes to Google's Gemini API, with the user's own key, only when descriptions are asked for. The key and the scripts stay on the phone. The app collects nothing.

## Author

Made by [Demir Ajvazi](https://github.com/demirajvazi10-max). Support the project on [GitHub Sponsors](https://github.com/sponsors/demirajvazi10-max).

## License

GPL-3.0, like the rest of the Ultra suite.
