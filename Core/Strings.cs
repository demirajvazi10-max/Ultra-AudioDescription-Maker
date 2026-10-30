using System;
using System.Collections.Generic;

namespace UltraAudioDescriptionMaker.Core
{
    /// <summary>
    /// The program's texts in English and Serbian (Latin script). "_" before a letter marks the
    /// access key (Alt + that letter). A missing text shows its key, so nothing ever crashes.
    /// </summary>
    public static class Strings
    {
        public static string Language { get; set; } = "en";

        public static string L(string key)
        {
            if (key != null && T.TryGetValue(key, out var t)) return Language == "sr" ? t.Sr : t.En;
            AppLog.Warn("Missing text: " + key);
            return key ?? "";
        }

        public static string LF(string key, params object[] args)
        {
            try { return string.Format(L(key), args); }
            catch (FormatException) { return L(key); }
        }

        private static readonly Dictionary<string, (string En, string Sr)> T = new()
        {
            ["app_name"] = ("Ultra Audio Description Maker", "Ultra Audio Description Maker"),

            // ── menu ──
            ["m_file"] = ("_File", "_Datoteka"),
            ["m_open_video"] = ("_Open video…", "_Otvori video…"),
            ["m_open_project"] = ("Open _project…", "Otvori _projekat…"),
            ["m_save"] = ("_Save project", "_Sačuvaj projekat"),
            ["m_save_as"] = ("Save project _as…", "Sačuvaj projekat _kao…"),
            ["m_import"] = ("_Import a script (text, WebVTT, SRT)…", "_Uvezi skriptu (tekst, WebVTT, SRT)…"),
            ["m_export"] = ("_Export…", "_Izvezi…"),
            ["m_exit"] = ("E_xit", "_Zatvori program"),
            ["m_desc"] = ("_Descriptions", "_Opisi"),
            ["m_write_ai"] = ("_Write descriptions with AI…", "_Napiši opise pomoću AI…"),
            ["m_voices"] = ("_Prepare voices and check timing", "_Pripremi glasove i proveri usklađenost"),
            ["m_report"] = ("Timing _report…", "_Izveštaj o usklađenosti…"),
            ["m_next_problem"] = ("_Next description that needs attention", "_Sledeći opis koji treba proveriti"),
            ["m_prev_problem"] = ("P_revious description that needs attention", "P_rethodni opis koji treba proveriti"),
            ["m_add"] = ("_Add a description at the playback position", "_Dodaj opis na mesto reprodukcije"),
            ["m_delete"] = ("_Delete the description", "O_briši opis"),
            ["m_edit_text"] = ("Edit the _text", "Uredi _tekst"),
            ["m_edit_time"] = ("Edit the ti_me", "Uredi _vreme"),
            ["m_set_time"] = ("Set the time to the _playback position", "Postavi vreme na _mesto reprodukcije"),
            ["m_earlier"] = ("A tenth of a second earlier", "Desetinku ranije"),
            ["m_later"] = ("A tenth of a second later", "Desetinku kasnije"),
            ["m_earlier_1"] = ("One second earlier", "Sekundu ranije"),
            ["m_later_1"] = ("One second later", "Sekundu kasnije"),
            ["m_script_text"] = ("Edit all descriptions as _text…", "Uredi sve opise kao _tekst…"),
            ["m_undo"] = ("_Undo", "_Poništi"),
            ["m_redo"] = ("Re_do", "_Vrati poništeno"),
            ["m_play"] = ("_Playback", "_Reprodukcija"),
            ["m_preview"] = ("_Preview the description (with the video)", "_Preslušaj opis (sa videom)"),
            ["m_listen"] = ("_Listen to the voice only", "Samo _glas opisa"),
            ["m_play_pause"] = ("Play / pause _with descriptions", "Pusti / pauziraj _sa opisima"),
            ["m_stop"] = ("_Stop", "_Zaustavi"),
            ["m_back"] = ("5 seconds _back", "5 sekundi _nazad"),
            ["m_forward"] = ("5 seconds _forward", "5 sekundi _napred"),
            ["m_goto"] = ("_Go to time…", "_Idi na vreme…"),
            ["m_goto_line"] = ("Go to the selected _description", "Idi na izabrani _opis"),
            ["m_position"] = ("Say the p_osition", "Izgovori _poziciju"),
            ["m_tools"] = ("_Tools", "_Alati"),
            ["m_settings"] = ("_Settings…", "_Podešavanja…"),
            ["m_log"] = ("Open the l_og", "Otvori _dnevnik grešaka"),
            ["m_help"] = ("_Help", "_Pomoć"),
            ["m_keys"] = ("_Keyboard shortcuts", "_Prečice na tastaturi"),
            ["m_about"] = ("_About", "_O programu"),

            // ── buttons ──
            ["b_write"] = ("Write with AI (F5)", "Napiši pomoću AI (F5)"),
            ["b_preview"] = ("Preview (Space)", "Preslušaj (Razmak)"),
            ["b_play"] = ("Play with descriptions (F6)", "Pusti sa opisima (F6)"),
            ["b_pause"] = ("Pause (F6)", "Pauza (F6)"),
            ["b_add"] = ("Add (Insert)", "Dodaj (Insert)"),
            ["b_delete"] = ("Delete (Delete)", "Obriši (Delete)"),
            ["b_voices"] = ("Prepare voices (F8)", "Pripremi glasove (F8)"),
            ["b_export"] = ("Export (Ctrl+E)", "Izvezi (Ctrl+E)"),
            ["b_stop_work"] = ("Stop the work (Ctrl+Shift+X)", "Prekini posao (Ctrl+Shift+X)"),
            ["btn_ok"] = ("OK", "U redu"),
            ["btn_cancel"] = ("Cancel", "Otkaži"),
            ["btn_close"] = ("Close", "Zatvori"),
            ["btn_browse"] = ("Browse…", "Pretraži…"),
            ["btn_save_key"] = ("Test and save the key", "Proveri i sačuvaj ključ"),

            // ── main window ──
            ["lbl_list"] = ("_List of descriptions", "_Spisak opisa"),
            ["lbl_time"] = ("T_ime (mm:ss.s)", "_Vreme (mm:ss.s)"),
            ["lbl_text"] = ("Description te_xt", "_Tekst opisa"),
            ["lbl_timing"] = ("Ti_ming", "_Usklađenost"),
            ["lbl_video"] = ("Video", "Video"),
            ["lbl_goto"] = ("Time (mm:ss.s, or seconds)", "Vreme (mm:ss.s, ili sekunde)"),
            ["lbl_keys"] = ("Keyboard shortcuts", "Prečice na tastaturi"),
            ["help_time"] = ("The moment the description is heard. Enter saves, Escape cancels.", "Trenutak kada se opis čuje. Enter čuva, Escape poništava."),
            ["help_text"] = ("What the voice says. Enter saves and goes back to the list, Escape cancels.", "Šta glas izgovara. Enter čuva i vraća na spisak, Escape poništava."),
            ["no_video"] = ("No video is open. Press Ctrl+O to open a video.", "Nijedan video nije otvoren. Pritisni Ctrl+O da otvoriš video."),
            ["video_info"] = ("{0} — {1}, descriptions in {2}, {3} descriptions", "{0} — {1}, opisi na jeziku: {2}, broj opisa: {3}"),
            ["new_line_text"] = ("New description.", "Novi opis."),

            // ── statuses (list) ──
            ["st_spedup"] = ("a little faster", "malo brže"),
            ["st_over"] = ("over dialogue", "preko dijaloga"),
            ["st_into_dialogue"] = ("{0} s into dialogue", "{0} s ulazi u dijalog"),
            ["st_into_next"] = ("{0} s into the next description", "{0} s ulazi u sledeći opis"),
            ["st_late"] = ("starts {0} s late", "kasni {0} s"),
            ["st_estimate"] = ("(estimate)", "(procena)"),
            ["st_fits"] = ("fits", "staje"),

            // ── timing field ──
            ["tm_starts"] = ("Heard at {0}.", "Čuje se u {0}."),
            ["tm_length"] = ("Spoken in {0} s, {1} s free.", "Izgovara se {0} s, slobodno je {1} s."),
            ["tm_length_est"] = ("About {0} s to say, {1} s free.", "Oko {0} s za izgovor, slobodno je {1} s."),
            ["tm_rate"] = ("Spoken {0} % faster to fit.", "Izgovara se {0} % brže da bi stao."),
            ["tm_ok"] = ("It fits before the next speech.", "Staje pre sledećeg govora."),
            ["tm_over"] = ("Its moment is in the middle of dialogue: it is heard over the speech (the sound goes down). Shorten it or move it if the speech matters.", "Trenutak je usred dijaloga: čuje se preko govora (zvuk se utiša). Skrati ga ili pomeri ako je govor važan."),
            ["tm_into_dialogue"] = ("Still speaking {0} s after someone starts talking. Shorten the text.", "Još traje {0} s pošto neko počne da govori. Skrati tekst."),
            ["tm_into_next"] = ("Still speaking {0} s after the next description is due, so the next one starts late. Shorten the text.", "Još traje {0} s kada treba da počne sledeći opis, pa sledeći kasni. Skrati tekst."),
            ["tm_late"] = ("Starts {0} s after its moment, because the previous description is still speaking.", "Počinje {0} s posle svog trenutka, jer prethodni opis još traje."),
            ["tm_estimated"] = ("(Estimate from the word count — F8 prepares the real voices.)", "(Procena po broju reči — F8 priprema prave glasove.)"),

            // ── announcements ──
            ["st_welcome"] = ("Welcome. Ctrl+O opens a video, F1 lists the keyboard shortcuts.", "Dobro došao. Ctrl+O otvara video, F1 daje spisak prečica."),
            ["st_video_opened"] = ("Opened {0}, {1} long. F5 writes the descriptions with AI, Insert adds one by hand.", "Otvoren {0}, traje {1}. F5 piše opise pomoću AI, Insert dodaje opis ručno."),
            ["st_project_opened"] = ("Opened the project {0}, {1} descriptions.", "Otvoren projekat {0}, broj opisa: {1}."),
            ["st_saved"] = ("Saved: {0}.", "Sačuvano: {0}."),
            ["st_added"] = ("Description added at {0}. Type the text and press Enter.", "Opis dodat u {0}. Upiši tekst i pritisni Enter."),
            ["st_deleted"] = ("Description at {0} deleted. Ctrl+Z brings it back.", "Opis u {0} obrisan. Ctrl+Z ga vraća."),
            ["st_time_set"] = ("Time set to {0}.", "Vreme postavljeno na {0}."),
            ["st_text_saved"] = ("Saved, {0}.", "Sačuvano, {0}."),
            ["st_script_applied"] = ("The script is applied: {0} descriptions.", "Skripta je primenjena: broj opisa {0}."),
            ["st_imported"] = ("Imported {0} descriptions.", "Uvezeno opisa: {0}."),
            ["st_undone"] = ("Undone.", "Poništeno."),
            ["st_redone"] = ("Redone.", "Vraćeno."),
            ["st_nothing_undo"] = ("Nothing to undo.", "Nema šta da se poništi."),
            ["st_nothing_redo"] = ("Nothing to redo.", "Nema šta da se vrati."),
            ["st_writing_start"] = ("Writing the descriptions. F9 says how far it is; Ctrl+Shift+X stops the work.", "Pišem opise. F9 govori dokle je stiglo; Ctrl+Shift+X prekida posao."),
            ["st_written"] = ("Done: {0} descriptions written, {1} stretches of speech found.", "Gotovo: napisano {0} opisa, pronađeno {1} delova sa govorom."),
            ["st_voices_ok"] = ("The voices are ready. All {0} descriptions fit their moment; {1} are spoken a little faster.", "Glasovi su spremni. Svih {0} opisa staju u svoj trenutak; {1} se izgovaraju malo brže."),
            ["st_voices_problems"] = ("The voices are ready: {0} descriptions, {1} a little faster, {2} need attention. F4 goes to the next one, Ctrl+I shows the report.", "Glasovi su spremni: {0} opisa, {1} malo brže, {2} treba proveriti. F4 ide na sledeći, Ctrl+I prikazuje izveštaj."),
            ["st_preparing_one"] = ("Preparing the voice…", "Pripremam glas…"),
            ["st_previewing"] = ("Preview of {0}.", "Pregled opisa u {0}."),
            ["st_paused"] = ("Paused at {0}.", "Pauzirano u {0}."),
            ["st_stopped_at"] = ("Stopped at {0}.", "Zaustavljeno u {0}."),
            ["st_end_reached"] = ("The end of the video.", "Kraj videa."),
            ["st_went_to"] = ("At {0}.", "Na {0}."),
            ["st_position"] = ("{0} of {1}, {2}.", "{0} od {1}, {2}."),
            ["st_playing"] = ("playing", "reprodukuje se"),
            ["st_not_playing"] = ("paused", "pauzirano"),
            ["st_next_desc"] = ("Next description in {0}.", "Sledeći opis za {0}."),
            ["st_in_dialogue"] = ("Someone is speaking here.", "Ovde neko govori."),
            ["st_no_problems"] = ("No description needs attention.", "Nijedan opis ne treba proveravati."),
            ["st_stopped"] = ("Stopped.", "Prekinuto."),
            ["st_settings_saved"] = ("Settings saved.", "Podešavanja sačuvana."),
            ["st_exported"] = ("Exported {0} files to {1}.", "Izvezeno datoteka: {0}, u {1}."),
            ["st_exported_problems"] = ("{0} descriptions still need attention.", "Opisa koje treba proveriti: {0}."),
            ["ex_stage_voice"] = ("Building the description track…", "Pravim zvučni zapis opisa…"),
            ["ex_stage_video"] = ("Mixing the description into the video…", "Umiksavam opis u video…"),
            ["ex_stage_audio"] = ("Writing the sound files…", "Pravim zvučne datoteke…"),
            ["ex_stage_text"] = ("Writing the script files…", "Pravim datoteke sa skriptom…"),

            // ── progress of long work ──
            ["wp_step"] = ("Step {0} of {1}: {2}.", "Korak {0} od {1}: {2}."),
            ["wp_percent"] = ("{0} %.", "{0} %."),
            ["wp_percent_only"] = ("{0} %.", "{0} %."),
            ["wp_elapsed"] = ("Running for {0}.", "Traje {0}."),
            ["wp_took"] = ("It took {0}.", "Trajalo je {0}."),
            ["wp_title_gemini"] = ("Writing the descriptions with Gemini", "Pisanje opisa pomoću Geminija"),
            ["wp_title_local"] = ("Writing the descriptions with the local model", "Pisanje opisa lokalnim modelom"),
            ["wp_title_voices"] = ("Preparing the voices", "Priprema glasova"),
            ["wp_title_export"] = ("Export", "Izvoz"),
            ["wp_title_ffmpeg"] = ("Downloading FFmpeg", "Preuzimanje FFmpeg-a"),
            ["wp_prepare"] = ("a small copy of the video for Gemini", "mala kopija videa za Gemini"),
            ["wp_size"] = ("{0} MB.", "{0} MB."),
            ["wp_upload"] = ("sending the video to Gemini", "slanje videa Geminiju"),
            ["wp_processing"] = ("Google is preparing the video", "Google priprema video"),
            ["wp_writing"] = ("Gemini is watching and writing the descriptions", "Gemini gleda i piše opise"),
            ["wp_whole"] = ("Gemini watches the whole video and answers when it has seen all of it.", "Gemini gleda ceo video i odgovara kada ga pogleda do kraja."),
            ["wp_part"] = ("Part {0} of {1} ({3} to {4}), {2} descriptions so far.", "Deo {0} od {1} (od {3} do {4}), do sada {2} opisa."),
            ["wp_gaps"] = ("Filling {0} longer stretches without descriptions in part {1} of {2}.", "Popunjavam {0} dužih delova bez opisa u delu {1} od {2}."),
            ["wp_written_so_far"] = ("{0} descriptions so far.", "Do sada {0} opisa."),
            ["wp_cuts"] = ("placing the descriptions on the shot changes", "postavljanje opisa na promene kadra"),
            ["wp_cuts_done"] = ("{0} shot changes found, {1} descriptions moved onto them.", "Pronađeno {0} promena kadra, {1} opisa pomereno na njih."),
            ["wp_cuts_find"] = ("finding the shot changes", "traženje promena kadra"),
            ["wp_local"] = ("describing shot by shot", "opisivanje kadar po kadar"),
            ["wp_shot"] = ("Shot {0} of {1}, {2} descriptions so far.", "Kadar {0} od {1}, do sada {2} opisa."),
            ["wp_voices"] = ("preparing the voices and checking the timing", "priprema glasova i provera usklađenosti"),
            ["wp_voice_line"] = ("Description {0} of {1}.", "Opis {0} od {1}."),
            ["wp_voices_count"] = ("{0} descriptions.", "Opisa: {0}."),
            ["wp_encoding"] = ("The picture has to be encoded again, which takes longer.", "Slika mora ponovo da se kodira, pa traje duže."),
            ["wp_ff_download"] = ("downloading FFmpeg", "preuzimanje FFmpeg-a"),
            ["wp_ff_unpack"] = ("unpacking", "raspakivanje"),
            ["wp_ff_mb"] = ("{0} of {1} MB.", "{0} od {1} MB."),
            ["wp_ff_mb_only"] = ("{0} MB.", "{0} MB."),
            ["ex_stage_video2"] = ("the video with two sound tracks", "video sa dva zvučna zapisa"),
            ["ex_stage_voice_file"] = ("the description voice file", "datoteka sa glasom deskripcije"),
            ["m_ffmpeg"] = ("_Download FFmpeg", "Preuzmi _FFmpeg"),
            ["q_ffmpeg_again"] = ("FFmpeg is already here:\n{0}\n\nDownload the newest version anyway?", "FFmpeg već postoji:\n{0}\n\nDa ipak preuzmem najnoviju verziju?"),
            ["st_ffmpeg_auto"] = ("The program needs FFmpeg for video and sound, and it is not on this computer. Downloading it now, about 100 MB. Ctrl+Shift+X stops it.", "Programu treba FFmpeg za video i zvuk, a nema ga na ovom računaru. Preuzimam ga sada, oko 100 MB. Ctrl+Shift+X prekida."),
            ["st_ffmpeg_start"] = ("Downloading FFmpeg. Ctrl+Shift+X stops it.", "Preuzimam FFmpeg. Ctrl+Shift+X prekida."),
            ["st_ffmpeg_done"] = ("FFmpeg is ready ({0}). Ctrl+O opens a video.", "FFmpeg je spreman ({0}). Ctrl+O otvara video."),
            ["st_ffmpeg_stopped"] = ("The FFmpeg download was stopped. Tools, Download FFmpeg starts it again.", "Preuzimanje FFmpeg-a je prekinuto. Alati, Preuzmi FFmpeg ga pokreće ponovo."),
            ["err_ffmpeg_download"] = ("FFmpeg could not be downloaded ({0}). Check the internet connection and try Tools, Download FFmpeg.", "FFmpeg nije mogao da se preuzme ({0}). Proveri internet i probaj Alati, Preuzmi FFmpeg."),
            ["err_ffmpeg_downloading"] = ("FFmpeg is still downloading. F9 says how far it is.", "FFmpeg se još preuzima. F9 govori dokle je stiglo."),
            ["st_language"] = ("The program is now in English.", "Program je sada na srpskom."),

            ["m_open_link"] = ("Open a video from a _link…", "Otvori video sa _linka…"),
            ["m_ytdlp"] = ("_Update the link downloader (yt-dlp)", "_Ažuriraj alat za linkove (yt-dlp)"),
            ["lbl_link"] = ("Web address of the video (YouTube or another site)", "Adresa videa (YouTube ili drugi sajt)"),
            ["wp_title_link"] = ("Opening a video from a link", "Otvaranje videa sa linka"),
            ["wp_title_ytdlp"] = ("Updating yt-dlp", "Ažuriranje yt-dlp-a"),
            ["wp_ytdlp_get"] = ("getting the link downloader (yt-dlp, about 18 MB)", "preuzimanje alata za linkove (yt-dlp, oko 18 MB)"),
            ["wp_link_download"] = ("downloading the video", "preuzimanje videa"),
            ["wp_writing_youtube"] = ("Gemini is watching the video directly on YouTube and writing the descriptions", "Gemini gleda video direktno na YouTube-u i piše opise"),
            ["st_link_done"] = ("Downloaded: {0}.", "Preuzeto: {0}."),
            ["st_ytdlp_done"] = ("yt-dlp is up to date.", "yt-dlp je ažuran."),
            ["err_link_bad"] = ("That is not a web address. Paste the whole link, starting with https.", "To nije veb adresa. Nalepi ceo link, koji počinje sa https."),
            ["err_link_failed"] = ("The video could not be downloaded: {0}", "Video nije mogao da se preuzme: {0}"),
            ["err_link_drm"] = ("This site protects its videos against copying (DRM), so they cannot be downloaded.", "Ovaj sajt štiti svoje video snimke od kopiranja (DRM), pa ne mogu da se preuzmu."),
            ["err_link_unsupported"] = ("The downloader does not know this site.", "Alat za preuzimanje ne poznaje ovaj sajt."),
            ["set_lbl_downloads"] = ("Folder for videos from _links", "Folder za video sa _linkova"),
            ["set_help_downloads"] = ("Videos opened with Ctrl+L are saved here.", "Video snimci otvoreni sa Ctrl+L čuvaju se ovde."),
            ["wp_gemini_thinking"] = ("Gemini is watching the video; the descriptions start to arrive when it begins to write.", "Gemini gleda video; opisi počinju da stižu kada počne da piše."),
            ["wp_part_of"] = ("(Part {0} of {1}.)", "(Deo {0} od {1}.)"),
            ["wp_gemini_writing"] = ("{0} descriptions written, up to {1} of {2}.", "Napisano {0} opisa, do {1} od {2}."),
            ["wp_gemini_busy"] = ("Google is busy; trying again in {0} seconds.", "Google je zauzet; ponovo pokušavam za {0} sekundi."),
            ["wp_gemini_retry"] = ("The connection went quiet; sending the request again.", "Veza je utihnula; ponovo šaljem zahtev."),
            ["m_cancel_work"] = ("Stop the work in progress…", "Prekini posao koji traje…"),
            ["q_cancel_work"] = ("Stop this work?\n\n{0}\n\nWhat was done so far is not kept.", "Da prekinem ovaj posao?\n\n{0}\n\nOno što je do sada urađeno neće biti sačuvano."),
            ["st_no_work"] = ("No work is running.", "Nijedan posao ne traje."),
            ["q_close_busy"] = ("Work is still running:\n{0}\n\nClose the program anyway? The work stops and is not kept.", "Posao još traje:\n{0}\n\nIpak da zatvorim program? Posao se prekida i neće biti sačuvan."),
            // ── questions ──
            ["q_save_changes"] = ("Save the changes to the project first?", "Da prvo sačuvam izmene u projektu?"),
            ["q_open_existing"] = ("A description project for this video already exists ({0}). Open it?", "Za ovaj video već postoji projekat ({0}). Da ga otvorim?"),
            ["q_open_folder"] = ("Finished. Open the folder?\n\n{0}", "Gotovo. Da otvorim folder?\n\n{0}"),

            // ── errors ──
            ["err_unexpected"] = ("Something went wrong: {0}\n\nThe details are in the log (Tools menu).", "Nešto nije u redu: {0}\n\nDetalji su u dnevniku grešaka (meni Alati)."),
            ["err_failed"] = ("It did not work: {0}", "Nije uspelo: {0}"),
            ["err_no_ffmpeg"] = ("FFmpeg was not found. Tools, Download FFmpeg gets it, or choose its folder in Settings (Ctrl+Shift+P).", "FFmpeg nije pronađen. Alati, Preuzmi FFmpeg ga preuzima, ili izaberi njegov folder u Podešavanjima (Ctrl+Shift+P)."),
            ["err_ffmpeg_missing"] = ("FFmpeg was not found. Tools, Download FFmpeg gets it.", "FFmpeg nije pronađen. Alati, Preuzmi FFmpeg ga preuzima."),
            ["err_player"] = ("The video player could not start ({0}). Descriptions can still be written and exported.", "Plejer nije mogao da se pokrene ({0}). Opisi i dalje mogu da se pišu i izvezu."),
            ["err_player_missing"] = ("Open a video first.", "Prvo otvori video."),
            ["err_no_project"] = ("Open a video first (Ctrl+O).", "Prvo otvori video (Ctrl+O)."),
            ["err_no_lines"] = ("There are no descriptions yet. F5 writes them with AI, Insert adds one.", "Još nema opisa. F5 ih piše pomoću AI, Insert dodaje jedan."),
            ["err_bad_video"] = ("{0} could not be read as a video.", "{0} ne može da se pročita kao video."),
            ["err_video_missing"] = ("The project's video was not found:\n{0}\n\nChoose where it is now.", "Video iz projekta nije pronađen:\n{0}\n\nIzaberi gde se sada nalazi."),
            ["err_time_format"] = ("That is not a time. Write it like 01:23.4 or 83.4.", "To nije vreme. Napiši ga ovako: 01:23.4 ili 83.4."),
            ["err_empty_text"] = ("A description cannot be empty. Delete it with the Delete key instead.", "Opis ne može da bude prazan. Obriši ga tasterom Delete."),
            ["err_import_empty"] = ("No descriptions were found in that file.", "U toj datoteci nema opisa."),
            ["err_ai_nothing"] = ("The AI returned no descriptions. Try again, or choose another way in the dialog.", "AI nije vratio nijedan opis. Pokušaj ponovo, ili izaberi drugi način u dijalogu."),
            ["err_no_gemini_key"] = ("Gemini needs a key. Add it in the dialog or in Settings.", "Geminiju treba ključ. Dodaj ga u dijalogu ili u Podešavanjima."),
            ["err_no_model"] = ("No local vision model was found in Ollama (for example qwen2.5vl), and there is no Gemini key.", "U Ollami nije pronađen lokalni model za slike (na primer qwen2.5vl), a nema ni Gemini ključa."),
            ["err_no_picture"] = ("This file has no moving pictures to describe.", "Ova datoteka nema pokretnu sliku koja može da se opiše."),
            ["err_prepare_failed"] = ("FFmpeg could not prepare the video for Gemini.", "FFmpeg nije mogao da pripremi video za Gemini."),
            ["err_nothing_spoken"] = ("No description could be spoken. Check the internet connection or choose another voice.", "Nijedan opis nije mogao da se izgovori. Proveri internet ili izaberi drugi glas."),
            ["err_mix_failed"] = ("FFmpeg could not mix the description into the sound. Details are in the log.", "FFmpeg nije mogao da umiksa opis u zvuk. Detalji su u dnevniku grešaka."),
            ["err_voice_track_failed"] = ("FFmpeg could not build the description track. Details are in the log.", "FFmpeg nije mogao da napravi zvučni zapis opisa. Detalji su u dnevniku grešaka."),

            // ── file dialogs ──
            ["filter_video"] = ("Videos|*.mp4;*.mkv;*.mov;*.avi;*.webm;*.m4v;*.wmv;*.mpg;*.mpeg;*.ts|All files|*.*", "Video snimci|*.mp4;*.mkv;*.mov;*.avi;*.webm;*.m4v;*.wmv;*.mpg;*.mpeg;*.ts|Sve datoteke|*.*"),
            ["filter_project"] = ("Audio description project|*.uad", "Projekat audio deskripcije|*.uad"),
            ["filter_script"] = ("Scripts|*.txt;*.vtt;*.srt|All files|*.*", "Skripte|*.txt;*.vtt;*.srt|Sve datoteke|*.*"),

            // ── voices ──
            ["voice_gc"] = ("{0} — Google HD voice", "{0} — Google HD glas"),
            ["voice_ms"] = ("{0} — Microsoft natural voice (free)", "{0} — Microsoft prirodni glas (besplatno)"),
            ["voice_google"] = ("Google voice, plain (free)", "Google glas, običan (besplatno)"),
            ["voice_win"] = ("{0} ({1}) — Windows voice, works offline", "{0} ({1}) — Windows glas, radi bez interneta"),

            // ── export ──
            ["track_title"] = ("Audio description", "Audio deskripcija"),
            ["suffix_ad"] = (" (audio description)", " (audio deskripcija)"),
            ["suffix_two_tracks"] = (" (two sound tracks)", " (dva zvučna zapisa)"),
            ["suffix_voice"] = (" (description voice only)", " (samo glas deskripcije)"),
            ["ex_title"] = ("Export", "Izvoz"),
            ["ex_lbl_folder"] = ("_Folder", "_Folder"),
            ["ex_lbl_name"] = ("File _name", "_Ime datoteka"),
            ["ex_help_name"] = ("Each file gets this name plus what it is, for example \"(audio description)\".", "Svaka datoteka dobija ovo ime i oznaku šta je, na primer \"(audio deskripcija)\"."),
            ["ex_lbl_what"] = ("What to make", "Šta da napravim"),
            ["ex_mixed"] = ("Video with the description mixed in (MP4)", "Video sa umiksanom deskripcijom (MP4)"),
            ["ex_two_tracks"] = ("Video with two sound tracks: original, and with description (MP4)", "Video sa dva zvučna zapisa: originalni i sa deskripcijom (MP4)"),
            ["ex_two_tracks_help"] = ("Like on Netflix: the viewer chooses the audio description track in the player.", "Kao na Netflixu: gledalac u plejeru bira zvučni zapis sa deskripcijom."),
            ["ex_audio_mix"] = ("The whole sound with the description (no picture)", "Ceo zvuk sa deskripcijom (bez slike)"),
            ["ex_voice_only"] = ("Only the description voice, as long as the video", "Samo glas deskripcije, dužine kao video"),
            ["ex_voice_only_help"] = ("For mixing it yourself in an audio or video editor.", "Za sopstveno miksanje u audio ili video editoru."),
            ["ex_txt"] = ("Script as text (TXT)", "Skripta kao tekst (TXT)"),
            ["ex_vtt"] = ("Text description track (WebVTT)", "Tekstualni zapis deskripcije (WebVTT)"),
            ["ex_vtt_help"] = ("For web players that read text descriptions with the screen reader.", "Za veb plejere koji tekstualne opise čitaju čitačem ekrana."),
            ["ex_srt"] = ("Script with times (SRT)", "Skripta sa vremenima (SRT)"),
            ["ex_lbl_format"] = ("Sound _format", "_Format zvuka"),
            ["ex_warn_problems"] = ("{0} descriptions still need attention (Ctrl+I in the main window). They are exported as they are.", "Opisa koje treba proveriti: {0} (Ctrl+I u glavnom prozoru). Izvoze se takvi kakvi su."),
            ["ex_ok"] = ("Export", "Izvezi"),
            ["ex_err_folder"] = ("Choose a folder.", "Izaberi folder."),
            ["ex_err_name"] = ("Type a file name.", "Upiši ime datoteke."),
            ["ex_err_nothing"] = ("Choose at least one thing to make.", "Izaberi bar jednu stvar."),

            // ── write with AI ──
            ["gen_title"] = ("Write descriptions with AI", "Napiši opise pomoću AI"),
            ["gen_lbl_source"] = ("_How", "_Način"),
            ["gen_help_source"] = ("Gemini watches the whole video with its sound, like a human describer — the best descriptions and timing. Locally, a vision model in Ollama describes shot by shot, and nothing leaves the computer.", "Gemini gleda ceo video sa zvukom, kao čovek koji piše deskripciju — najbolji opisi i usklađenost. Lokalno, model za slike u Ollami opisuje kadar po kadar i ništa ne napušta računar."),
            ["gen_src_gemini"] = ("Gemini, the whole video (best)", "Gemini, ceo video (najbolje)"),
            ["gen_src_local"] = ("Local model in Ollama, shot by shot (private)", "Lokalni model u Ollami, kadar po kadar (privatno)"),
            ["gen_need_key"] = ("No Gemini key is saved. Get a free key in Google AI Studio and paste it here.", "Nema sačuvanog Gemini ključa. Besplatan ključ se dobija u Google AI Studiju; nalepi ga ovde."),
            ["gen_lbl_density"] = ("How _many descriptions", "_Koliko opisa"),
            ["gen_dens_essential"] = ("Essential — only what is needed to follow", "Osnovno — samo ono što je potrebno da se prati"),
            ["gen_dens_normal"] = ("Normal — the important changes", "Uobičajeno — važne promene"),
            ["gen_dens_detailed"] = ("Detailed — every visible change", "Detaljno — svaka vidljiva promena"),
            ["gen_lbl_language"] = ("_Language of the descriptions", "_Jezik opisa"),
            ["gen_snap"] = ("Place descriptions exactly on shot changes", "Postavi opise tačno na promene kadra"),
            ["gen_snap_help"] = ("A description less than a second from a cut starts on the cut, where the new picture really begins.", "Opis koji je manje od sekunde od reza počinje na rezu, tamo gde nova slika zaista počinje."),
            ["gen_lbl_existing"] = ("The descriptions already in the list", "Opisi koji su već na spisku"),
            ["gen_replace"] = ("Replace them", "Zameni ih"),
            ["gen_keep"] = ("Keep them, and add AI descriptions only where there are none", "Zadrži ih, a AI opise dodaj samo tamo gde ih nema"),
            ["gen_prepare"] = ("Prepare the voices right after, and check the timing", "Odmah zatim pripremi glasove i proveri usklađenost"),
            ["gen_privacy"] = ("With Gemini, a small copy of the video (480p) is sent to Google and deleted when the work is done.", "Sa Geminijem se mala kopija videa (480p) šalje Google-u i briše se kada posao završi."),
            ["gen_ok"] = ("Write", "Napiši"),

            ["gen_lbl_quality"] = ("_Speed and precision", "_Brzina i preciznost"),
            ["gen_help_quality"] = ("How closely Gemini looks at the picture. Balanced is fast and accurate enough for most videos; the times are also sharpened on the shot changes.", "Koliko pažljivo Gemini gleda sliku. Uravnoteženo je brzo i dovoljno precizno za većinu videa; vremena se dodatno izoštravaju na promenama kadra."),
            ["gen_q_fast"] = ("Fast — one picture a second", "Brzo — jedna slika u sekundi"),
            ["gen_q_balanced"] = ("Balanced — two pictures a second (recommended)", "Uravnoteženo — dve slike u sekundi (preporučeno)"),
            ["gen_q_precise"] = ("Precise — two pictures a second in full detail (slowest)", "Precizno — dve slike u sekundi u punoj rezoluciji (najsporije)"),
            // ── report ──
            ["rep_title"] = ("Timing report", "Izveštaj o usklađenosti"),
            ["rep_summary"] = ("{0} descriptions: {1} fit their moment ({2} of them a little faster), {3} need attention.", "Opisa: {0}. Staje u svoj trenutak: {1} (od toga malo brže: {2}). Treba proveriti: {3}."),
            ["rep_estimated"] = ("Some results are estimates; F8 checks them with the real voices.", "Neki rezultati su procena; F8 ih proverava pravim glasovima."),
            ["rep_lbl_list"] = ("_Descriptions that need attention", "_Opisi koje treba proveriti"),
            ["rep_help"] = ("Enter goes to the description in the main list.", "Enter vodi na taj opis u glavnom spisku."),
            ["rep_go"] = ("Go to it", "Idi na njega"),

            // ── script as text ──
            ["scr_title"] = ("All descriptions as text", "Svi opisi kao tekst"),
            ["scr_lbl"] = ("_Script", "_Skripta"),
            ["scr_help"] = ("One description per line: time, a vertical bar, the text. For example: 01:23.4 | Ana opens the door. A line without a time continues the line above.", "Jedan opis po redu: vreme, uspravna crta, tekst. Na primer: 01:23.4 | Ana otvara vrata. Red bez vremena nastavlja red iznad."),
            ["scr_err"] = ("No line has a time. Write each line like: 01:23.4 | text", "Nijedan red nema vreme. Piši svaki red ovako: 01:23.4 | tekst"),

            // ── settings ──
            ["set_title"] = ("Settings", "Podešavanja"),
            ["set_sec_program"] = ("Program", "Program"),
            ["set_lbl_ui"] = ("Program _language", "_Jezik programa"),
            ["set_lbl_theme"] = ("_Colours", "_Boje"),
            ["set_theme_system"] = ("Like Windows (high contrast when it is on)", "Kao Windows (visok kontrast kada je uključen)"),
            ["set_theme_dark"] = ("Dark", "Tamne"),
            ["set_theme_light"] = ("Light", "Svetle"),
            ["set_theme_contrast"] = ("High contrast", "Visok kontrast"),
            ["set_show_video"] = ("Show the video picture", "Prikaži sliku videa"),
            ["set_list_timing"] = ("Read the timing problems in the list", "Čitaj probleme sa usklađenošću u spisku"),
            ["set_sounds"] = ("A sound when work finishes or fails", "Zvuk kada se posao završi ili ne uspe"),
            ["set_sec_voice"] = ("Descriptions and voice", "Opisi i glas"),
            ["set_lbl_lang"] = ("Language of the _descriptions", "Jezik _opisa"),
            ["set_help_lang"] = ("Applies to this project and to new ones.", "Važi za ovaj projekat i za nove."),
            ["set_lbl_voice"] = ("_Voice", "_Glas"),
            ["set_test_voice"] = ("Test the voice", "Probaj glas"),
            ["set_testing_voice"] = ("Testing the voice…", "Probam glas…"),
            ["set_lbl_rate"] = ("Voice _speed", "_Brzina glasa"),
            ["set_rate_normal"] = ("Natural", "Prirodna"),
            ["set_lbl_maxup"] = ("Speed up to fit, at _most", "Ubrzaj da stane, _najviše"),
            ["set_help_maxup"] = ("A description that is too long for its moment is spoken this much faster at most, so it still starts exactly on time.", "Opis koji je predug za svoj trenutak izgovara se najviše ovoliko brže, da bi i dalje počeo tačno na vreme."),
            ["set_up_none"] = ("Never speed up", "Nikad ne ubrzavaj"),
            ["set_lbl_duck"] = ("Film sound under a description (export)", "Zvuk filma ispod opisa (izvoz)"),
            ["set_duck_light"] = ("A little lower", "Malo tiše"),
            ["set_duck_medium"] = ("Lower", "Tiše"),
            ["set_duck_strong"] = ("Much lower", "Mnogo tiše"),
            ["set_lbl_preview_duck"] = ("Film volume under a description (preview)", "Jačina filma ispod opisa (pregled)"),
            ["set_sec_keys"] = ("Keys (saved encrypted on this computer)", "Ključevi (čuvaju se šifrovano na ovom računaru)"),
            ["set_lbl_gemini"] = ("_Gemini key (Google AI Studio)", "_Gemini ključ (Google AI Studio)"),
            ["set_help_gemini"] = ("Gemini watches the video and writes the descriptions. The Flash models have a free tier.", "Gemini gleda video i piše opise. Flash modeli imaju besplatan nivo."),
            ["set_lbl_gc"] = ("Google _Cloud key (HD voices)", "Google _Cloud ključ (HD glasovi)"),
            ["set_help_gc"] = ("Optional. Google's Chirp 3 HD voices; the first million characters a month are free.", "Nije obavezno. Google Chirp 3 HD glasovi; prvi milion znakova mesečno je besplatan."),
            ["key_none"] = ("No key is saved.", "Ključ nije sačuvan."),
            ["key_saved"] = ("A key is saved.", "Ključ je sačuvan."),
            ["key_saved_gc"] = ("The key is saved. The Google HD voices are now in the voice list.", "Ključ je sačuvan. Google HD glasovi su sada na spisku glasova."),
            ["key_shared"] = ("The key saved in Ultra Video Editor is used.", "Koristi se ključ sačuvan u Ultra Video Editoru."),
            ["key_empty"] = ("Paste the key first.", "Prvo nalepi ključ."),
            ["key_testing"] = ("Testing the key…", "Proveravam ključ…"),
            ["key_bad"] = ("The key does not work: {0}", "Ključ ne radi: {0}"),
            ["set_sec_tools"] = ("Tools", "Alati"),
            ["set_lbl_ffmpeg"] = ("_FFmpeg folder", "_FFmpeg folder"),
            ["set_help_ffmpeg"] = ("Leave empty to find FFmpeg automatically (next to the program, in PATH, or in Ultra Video Editor).", "Ostavi prazno da se FFmpeg pronađe sam (pored programa, u PATH-u ili u Ultra Video Editoru)."),
            ["set_ffmpeg_found"] = ("Found: {0}", "Pronađen: {0}"),
            ["set_ffmpeg_missing"] = ("FFmpeg was not found.", "FFmpeg nije pronađen."),
            ["set_clear_cache"] = ("Clear the voice cache ({0} MB)", "Obriši keš glasova ({0} MB)"),
            ["set_cache_cleared"] = ("The voice cache is cleared.", "Keš glasova je obrisan."),
            ["set_test_sentence_sr"] = ("Ana ulazi u sobu i pali svetlo.", "Ana ulazi u sobu i pali svetlo."),
            ["set_test_sentence_hr"] = ("Ana ulazi u sobu i pali svjetlo.", "Ana ulazi u sobu i pali svjetlo."),
            ["set_test_sentence_bs"] = ("Ana ulazi u sobu i pali svjetlo.", "Ana ulazi u sobu i pali svjetlo."),
            ["set_test_sentence_en"] = ("Anna walks into the room and turns on the light.", "Anna walks into the room and turns on the light."),
            ["set_test_sentence_de"] = ("Anna betritt das Zimmer und macht das Licht an.", "Anna betritt das Zimmer und macht das Licht an."),
            ["set_test_sentence_es"] = ("Ana entra en la habitación y enciende la luz.", "Ana entra en la habitación y enciende la luz."),
            ["set_test_sentence_fr"] = ("Anna entre dans la pièce et allume la lumière.", "Anna entre dans la pièce et allume la lumière."),
            ["set_test_sentence_it"] = ("Anna entra nella stanza e accende la luce.", "Anna entra nella stanza e accende la luce."),

            ["m_sponsor"] = ("_Support the project (GitHub Sponsors)", "Po_drži projekat (GitHub Sponsors)"),
            ["about_lbl"] = ("About", "O programu"),
            ["about_sponsor"] = ("Support Demir Ajvazi on GitHub Sponsors", "Podrži Demira Ajvazija na GitHub Sponsors"),
            ["about_github"] = ("Demir Ajvazi on GitHub", "Demir Ajvazi na GitHubu"),
            ["set_lbl_how_gemini"] = ("How to get a Gemini key", "Kako dobiti Gemini ključ"),
            ["set_how_gemini"] = (
                "1. Press the button below, \"Open Google AI Studio\". The page aistudio.google.com/app/apikey opens in your browser.\n" +
                "2. Sign in with your Google account (the same as for Gmail or YouTube).\n" +
                "3. Press \"Create API key\". If Google asks for a project, choose the one it offers, or create a new one.\n" +
                "4. Copy the key (a long line of letters and numbers that starts with AIza).\n" +
                "5. Come back here, paste it into the Gemini key field above, and press \"Test and save the key\".\n" +
                "The Gemini Flash models have a free tier, and no payment card is needed. Keep the key to yourself, like a password.",
                "1. Pritisni dugme ispod, \"Otvori Google AI Studio\". U pregledaču se otvara stranica aistudio.google.com/app/apikey.\n" +
                "2. Prijavi se svojim Google nalogom (istim kao za Gmail ili YouTube).\n" +
                "3. Pritisni \"Create API key\". Ako Google traži projekat, izaberi onaj koji ponudi ili napravi novi.\n" +
                "4. Kopiraj ključ (dugačak red slova i brojeva koji počinje sa AIza).\n" +
                "5. Vrati se ovde, nalepi ga u polje za Gemini ključ iznad i pritisni \"Proveri i sačuvaj ključ\".\n" +
                "Gemini Flash modeli imaju besplatan nivo i ne treba platna kartica. Čuvaj ključ za sebe, kao lozinku."),
            ["set_link_gemini"] = ("Open Google AI Studio (Gemini key)", "Otvori Google AI Studio (Gemini ključ)"),
            ["set_lbl_how_gc"] = ("How to get a Google Cloud key", "Kako dobiti Google Cloud ključ"),
            ["set_how_gc"] = (
                "This key is only for the Google HD voices; everything else works without it.\n" +
                "1. Press \"1. Create a project\" below and sign in with your Google account. Give the project any name and press Create.\n" +
                "2. Google Cloud asks for a billing account (a payment card) even for the free amount. Add it when asked; Google's free monthly amount of characters for Text-to-Speech applies.\n" +
                "3. Press \"2. Turn on Text-to-Speech\", check that your new project is chosen at the top of the page, and press Enable.\n" +
                "4. Press \"3. Create the key\", then Create credentials, then API key. Copy the key.\n" +
                "5. Advised: on the same page choose Edit API key, and under API restrictions allow only the Cloud Text-to-Speech API.\n" +
                "6. Come back here, paste the key into the Google Cloud key field above, and press \"Test and save the key\". The Google HD voices then appear in the voice list.",
                "Ovaj ključ treba samo za Google HD glasove; sve ostalo radi i bez njega.\n" +
                "1. Pritisni \"1. Napravi projekat\" ispod i prijavi se svojim Google nalogom. Daj projektu bilo koje ime i pritisni Create.\n" +
                "2. Google Cloud traži nalog za plaćanje (platnu karticu) čak i za besplatnu količinu. Dodaj ga kada zatraži; važi Google-ova besplatna mesečna količina znakova za Text-to-Speech.\n" +
                "3. Pritisni \"2. Uključi Text-to-Speech\", proveri da je na vrhu stranice izabran tvoj novi projekat i pritisni Enable.\n" +
                "4. Pritisni \"3. Napravi ključ\", zatim Create credentials, pa API key. Kopiraj ključ.\n" +
                "5. Preporuka: na istoj stranici izaberi Edit API key i pod API restrictions dozvoli samo Cloud Text-to-Speech API.\n" +
                "6. Vrati se ovde, nalepi ključ u polje za Google Cloud ključ iznad i pritisni \"Proveri i sačuvaj ključ\". Google HD glasovi se tada pojavljuju na spisku glasova."),
            ["set_link_gc_console"] = ("1. Create a project (Google Cloud)", "1. Napravi projekat (Google Cloud)"),
            ["set_link_gc_api"] = ("2. Turn on Text-to-Speech", "2. Uključi Text-to-Speech"),
            ["set_link_gc_keys"] = ("3. Create the key", "3. Napravi ključ"),
            // ── about, keys ──
            ["about_text"] = (
                "Ultra Audio Description Maker\nVersion {0}\n\nCreative tools that start with the keyboard, not the mouse.\n\nMade by Demir Ajvazi.\nPart of the Ultra suite. License: GPL-3.0.\n\nIf the program helps you, you can support its development on GitHub Sponsors:\ngithub.com/sponsors/demirajvazi10-max",
                "Ultra Audio Description Maker\nVerzija {0}\n\nCreative tools that start with the keyboard, not the mouse.\n\nAutor: Demir Ajvazi.\nDeo Ultra paketa. Licenca: GPL-3.0.\n\nAko ti program pomaže, njegov razvoj možeš da podržiš na GitHub Sponsors:\ngithub.com/sponsors/demirajvazi10-max"),
            ["keys_text"] = (
@"WORK ORDER
Ctrl+O  open a video
Ctrl+L  open a video from a link (YouTube and other sites)
F5  write the descriptions with AI
F8  prepare the voices and check the timing
F4 / Shift+F4  next / previous description that needs attention
Ctrl+I  timing report
Ctrl+E  export

IN THE LIST OF DESCRIPTIONS
Up / Down  move through the descriptions
Space  preview: the video from 2 seconds before, with the description at its place
Shift+Space  hear only the voice of the description
Enter  move the video to the description
F2  edit the text (Enter saves and returns to the list, Escape cancels)
Ctrl+F2  edit the time
Insert  add a description at the playback position
Delete  delete the description
Ctrl+Left / Ctrl+Right  a tenth of a second earlier / later
Ctrl+Shift+Left / Ctrl+Shift+Right  one second earlier / later
Ctrl+T  set the time to the playback position

PLAYBACK
F6  play / pause with descriptions
Escape  stop the preview or the playback
Ctrl+Shift+X  stop long work (AI, voices, export, download), after a question
Alt+Left / Alt+Right  5 seconds back / forward
Ctrl+G  go to a time
F9  say the position, and how far the next description is; during long work: how far the work is

FILES AND EDITING
Ctrl+S  save the project, Ctrl+Shift+S  save as
Ctrl+Shift+O  open a project
Ctrl+Shift+E  edit all descriptions as text
Ctrl+Z / Ctrl+Y  undo / redo
Ctrl+Shift+P  settings
Alt+T, L  program language (English / Srpski)
F1  this list

TIMING, LIKE ON NETFLIX
Every description starts at the exact moment of its action. When it is too long for the free time after that moment, it is spoken a little faster. What still does not fit is marked in the list: over dialogue, into dialogue, into the next description, or late.",
@"REDOSLED RADA
Ctrl+O  otvori video
Ctrl+L  otvori video sa linka (YouTube i drugi sajtovi)
F5  napiši opise pomoću AI
F8  pripremi glasove i proveri usklađenost
F4 / Shift+F4  sledeći / prethodni opis koji treba proveriti
Ctrl+I  izveštaj o usklađenosti
Ctrl+E  izvoz

U SPISKU OPISA
Strelice gore / dole  kretanje kroz opise
Razmak  pregled: video od 2 sekunde ranije, sa opisom na njegovom mestu
Shift+Razmak  samo glas opisa
Enter  pomeri video na opis
F2  uredi tekst (Enter čuva i vraća na spisak, Escape poništava)
Ctrl+F2  uredi vreme
Insert  dodaj opis na mesto reprodukcije
Delete  obriši opis
Ctrl+Levo / Ctrl+Desno  desetinku ranije / kasnije
Ctrl+Shift+Levo / Ctrl+Shift+Desno  sekundu ranije / kasnije
Ctrl+T  postavi vreme na mesto reprodukcije

REPRODUKCIJA
F6  pusti / pauziraj sa opisima
Escape  zaustavi pregled ili reprodukciju
Ctrl+Shift+X  prekini dugi posao (AI, glasovi, izvoz, preuzimanje), uz pitanje
Alt+Levo / Alt+Desno  5 sekundi nazad / napred
Ctrl+G  idi na vreme
F9  izgovori poziciju i koliko je do sledećeg opisa; tokom dugog posla: dokle je posao stigao

DATOTEKE I UREĐIVANJE
Ctrl+S  sačuvaj projekat, Ctrl+Shift+S  sačuvaj kao
Ctrl+Shift+O  otvori projekat
Ctrl+Shift+E  uredi sve opise kao tekst
Ctrl+Z / Ctrl+Y  poništi / vrati
Ctrl+Shift+P  podešavanja
Alt+A, L  jezik programa (English / Srpski)
F1  ovaj spisak

USKLAĐENOST, KAO NA NETFLIXU
Svaki opis počinje tačno u trenutku svoje radnje. Kada je predug za slobodno vreme posle tog trenutka, izgovara se malo brže. Ono što i dalje ne staje označeno je u spisku: preko dijaloga, ulazi u dijalog, ulazi u sledeći opis, ili kasni."),
        };
    }
}
