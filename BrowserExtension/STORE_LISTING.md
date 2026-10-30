# Store listing — Chrome Web Store and Microsoft Edge Add-ons

Ready-to-paste texts for publishing the extension.

## Name
Ultra Audio Description

## Short description (up to 132 characters)
**English:** Audio description for web videos: a voice says what happens on screen, at the moment it happens. YouTube and more.
**Srpski:** Audio deskripcija za video na vebu: glas kaže šta se dešava na ekranu, u trenutku kada se dešava. YouTube i drugi.

## Category
Accessibility

## Detailed description — English
Ultra Audio Description adds audio description to the videos you watch in the browser: a natural voice tells who is on screen, where we are, what happens, and reads the text that appears — at the exact moment it happens, like audio description on Netflix.

• YouTube: Gemini watches the video directly from its link and writes the whole description script, timed to the tenth of a second, with a map of where people speak. You keep watching while it works.
• Any other site that allows it: live description — when the picture changes, the extension describes the new picture.
• Scripts made in the Ultra Audio Description Maker desktop program (WebVTT, SRT or text) can be loaded and are heard at their moments.
• Smart timing: the video sound goes down while a description is heard; a description that is too long is spoken a little faster, and when it needs more time the video pauses for a moment and continues by itself (extended audio description).
• Heard through a voice of the browser (the free natural voices in Microsoft Edge are excellent), or read by your screen reader and shown on braille.
• The description can also be shown as text on the picture.
• Fully usable from the keyboard: Alt+Shift+D on/off, Alt+Shift+S describe now, Alt+Shift+R repeat, Alt+Shift+A menu.
• English and Serbian interface; descriptions in Serbian, English, German, Croatian, Bosnian, Spanish, French and Italian.
• Private: it works only on the tab where you turn it on. It uses your own free Gemini key, or a local model in Ollama so nothing leaves your computer.

Sites that protect their video with DRM (most paid streaming services) do not let any extension see the picture; the extension tells you so.

Part of the Ultra suite by Demir Ajvazi. Free and open source (GPL-3.0).

## Detaljan opis — Srpski
Ultra Audio Description dodaje audio deskripciju video snimcima koje gledaš u pregledaču: prirodan glas kaže ko je na ekranu, gde smo, šta se dešava i čita tekst koji se pojavi — tačno u trenutku kada se to dešava, kao audio deskripcija na Netflixu.

• YouTube: Gemini gleda video direktno sa linka i piše celu skriptu opisa, usklađenu na desetinku sekunde, sa mapom mesta na kojima ljudi govore. Ti za to vreme gledaš dalje.
• Svaki drugi sajt koji to dozvoljava: opis uživo — kada se slika promeni, dodatak opiše novu sliku.
• Skripte napravljene u programu Ultra Audio Description Maker (WebVTT, SRT ili tekst) mogu da se učitaju i čuju se u svom trenutku.
• Pametno usklađivanje: zvuk videa se utiša dok se čuje opis; predug opis se izgovara malo brže, a kada mu treba više vremena, video se na trenutak pauzira i sam nastavi (produžena audio deskripcija).
• Čuje se glasom pregledača (besplatni prirodni glasovi u Microsoft Edge-u su odlični) ili ga čita tvoj čitač ekrana, i na brajevom redu.
• Opis može da se prikaže i kao tekst na slici.
• Potpuno sa tastature: Alt+Shift+D uključi/isključi, Alt+Shift+S opiši sada, Alt+Shift+R ponovi, Alt+Shift+A meni.
• Interfejs na engleskom i srpskom; opisi na srpskom, engleskom, nemačkom, hrvatskom, bosanskom, španskom, francuskom i italijanskom.
• Privatno: radi samo na kartici na kojoj ga uključiš. Koristi tvoj besplatni Gemini ključ, ili lokalni model u Ollami, pa ništa ne napušta računar.

Sajtovi koji štite video DRM-om (većina plaćenih servisa) ne dozvoljavaju nijednom dodatku da vidi sliku; dodatak to jasno kaže.

Deo Ultra paketa, autor Demir Ajvazi. Besplatan i otvorenog koda (GPL-3.0).

## Single purpose (Chrome Web Store)
Adds audio description (spoken descriptions of what is on screen) to videos the user watches in the browser.

## Permission justifications (Chrome Web Store)
- **activeTab**: to read the video's time and picture only on the tab where the user turns audio description on.
- **scripting**: to put the audio description player into that tab when the user asks for it, and on sites the user chose "Always ready".
- **storage**: to keep the user's settings, Gemini key and description scripts on the user's computer.
- **Host permission generativelanguage.googleapis.com**: to send the YouTube address or a still picture to the Gemini API, with the user's own key, so it can be described.
- **Optional host permissions**: requested only when the user turns on "Always ready on this site" for a site, or chooses a local Ollama model (localhost:11434).
- **Remote code**: none. All code is in the package.

## Data usage (Chrome Web Store form)
- Website content: yes — still pictures of the video, or the YouTube address, are sent to the Gemini API only when the user asks for descriptions; not sold, not used for anything else.
- Personally identifiable information, health, financial, authentication, personal communications, location, web history, user activity: no.

## Privacy policy URL
Link to `BrowserExtension/PRIVACY.md` in the GitHub repository.

## Screenshots
1280×800 or 640×400 PNG/JPEG, at least one: the menu (Alt+Shift+A) on a YouTube video; a description shown as text on the picture; the options page with the Gemini key steps.

## Fees
- Chrome Web Store: a one-time developer registration fee.
- Microsoft Edge Add-ons (Partner Center): free.
