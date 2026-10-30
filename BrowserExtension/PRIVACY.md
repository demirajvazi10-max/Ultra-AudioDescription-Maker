# Privacy policy — Ultra Audio Description (browser extension)

*Last updated: October 29, 2026*

Ultra Audio Description is made by Demir Ajvazi. It adds audio description to videos in the browser.

## What the extension does not do

- It does not collect, sell or share any personal data.
- It has no server of its own, no analytics and no advertising.
- It does nothing on a page until you turn it on there (with its shortcut or its menu), or until you choose "Always ready on this site" for that site.

## What stays in your browser

Saved with the browser's own extension storage, on your computer only:

- your settings;
- your Gemini API key, if you add one;
- the description scripts for videos (written by Gemini or loaded from a file);
- the list of sites where you chose "Always ready on this site".

Removing the extension deletes all of this.

## What is sent, and only when you ask for it

- **Gemini (Google)** — when you choose to write descriptions for a YouTube video, the video's YouTube address is sent to Google's Gemini API with your own key. When live description is on, still pictures from the video you are watching are sent to the Gemini API to be described. Google processes these under its own terms: https://ai.google.dev/gemini-api/terms
- **Ollama (local)** — if you choose Ollama instead, pictures go only to the Ollama program on your own computer (localhost) and never leave it.

Nothing else leaves your computer.

## Permissions

- **activeTab, scripting** — to work on the tab where you turn it on, and nowhere else.
- **storage** — to keep the settings, the key and the scripts listed above.
- **generativelanguage.googleapis.com** — to reach the Gemini API.
- **Optional: a site you choose** — only if you turn on "Always ready on this site" for it.
- **Optional: localhost:11434** — only if you choose Ollama.

## Contact

https://github.com/demirajvazi10-max
