using System;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;

namespace UltraAudioDescriptionMaker.Core
{
    /// <summary>
    /// The program settings (%LOCALAPPDATA%\UltraAudioDescriptionMaker\settings.json).
    /// The description language and voice here are the defaults for new projects;
    /// a project keeps its own.
    /// </summary>
    public class AppSettings
    {
        public static AppSettings Current { get; private set; } = new AppSettings();

        /// <summary>"en" (default) or "sr" — the language of the program's menus and messages.</summary>
        public string UiLanguage;
        /// <summary>The language the descriptions are written and spoken in.</summary>
        public string DescLanguage;
        /// <summary>Voice tag: "ms:…", "gc:…", "google" or "win:…".</summary>
        public string Voice;
        /// <summary>Basic voice speed in percent (−30 … +50). 0 = natural.</summary>
        public int VoiceRate = 0;
        /// <summary>How much faster a description may be spoken to fit before the next speech (1.0 = never).</summary>
        public double MaxSpeedUp = 1.25;
        /// <summary>Light, Medium or Strong — how much the film sound goes down under a description.</summary>
        public string Ducking = "Medium";
        /// <summary>Move a description to a shot change that is less than a second away.</summary>
        public bool SnapToCuts = true;
        /// <summary>Dark, Light, Contrast or System.</summary>
        public string Theme = "System";
        public bool ShowVideo = true;
        public string FfmpegFolder;
        /// <summary>Where videos opened from a link are saved (Videos\Ultra Audio Description by default).</summary>
        public string DownloadFolder;
        public string LastFolder;
        /// <summary>"gemini" (watches the whole video) or "local" (Ollama, shot by shot).</summary>
        public string AiSource = "gemini";
        /// <summary>Fast, Balanced or Precise — how closely Gemini looks at the video (and so how fast it answers).</summary>
        public string GeminiQuality = "Balanced";
        /// <summary>Essential, Normal or Detailed.</summary>
        public string Density = "Normal";
        /// <summary>Seconds of video played before a description when it is previewed.</summary>
        public double PreviewLeadIn = 2.0;
        /// <summary>Film volume (0–100) while a description is heard in the preview.</summary>
        public int PreviewDuckVolume = 35;
        /// <summary>Speak the status of a description (too long, over dialogue) in the list.</summary>
        public bool ListReadsTiming = true;
        /// <summary>A sound when long work finishes or fails.</summary>
        public bool Sounds = true;

        private static string FilePath => Path.Combine(AppPaths.DataDir, "settings.json");

        public static void Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    Current = JsonConvert.DeserializeObject<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
            }
            catch (Exception ex)
            {
                AppLog.Error("Reading the settings", ex);
                Current = new AppSettings();
            }
            string sys = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            bool balkan = sys is "sr" or "hr" or "bs" or "sh" or "me";
            // the program speaks English unless Serbian is chosen (Tools > Language, or Settings)
            if (Current.UiLanguage is not ("en" or "sr")) Current.UiLanguage = "en";
            Current.DescLanguage ??= balkan ? "sr" : (Languages.IsKnown(sys) ? sys : "en");
            if (string.IsNullOrWhiteSpace(Current.DownloadFolder))
                Current.DownloadFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Ultra Audio Description");
            Current.MaxSpeedUp = Math.Clamp(Current.MaxSpeedUp, 1.0, 1.6);
            Current.VoiceRate = Math.Clamp(Current.VoiceRate, -30, 50);
        }

        public void Save()
        {
            try { File.WriteAllText(FilePath, JsonConvert.SerializeObject(this, Formatting.Indented)); }
            catch (Exception ex) { AppLog.Error("Saving the settings", ex); }
        }
    }

    /// <summary>The description languages the program offers.</summary>
    public static class Languages
    {
        public static readonly string[] Codes = { "sr", "en", "de", "hr", "bs", "es", "fr", "it" };

        public static bool IsKnown(string code) => Array.IndexOf(Codes, code) >= 0;

        /// <summary>The language name for the AI prompt.</summary>
        public static string PromptName(string lang) => lang switch
        {
            "sr" => "Serbian (ekavian, as spoken in Serbia), written in the Latin alphabet",
            "hr" => "Croatian",
            "bs" => "Bosnian, written in the Latin alphabet",
            "de" => "German",
            "es" => "Spanish",
            "fr" => "French",
            "it" => "Italian",
            _ => "English"
        };

        /// <summary>The language's own name, for the lists in the program.</summary>
        public static string Native(string lang) => lang switch
        {
            "sr" => "Srpski",
            "hr" => "Hrvatski",
            "bs" => "Bosanski",
            "de" => "Deutsch",
            "es" => "Español",
            "fr" => "Français",
            "it" => "Italiano",
            _ => "English"
        };

        /// <summary>ISO 639-2 code for the audio track language in the exported video.</summary>
        public static string Iso3(string lang) => lang switch
        {
            "sr" => "srp", "hr" => "hrv", "bs" => "bos", "de" => "deu",
            "es" => "spa", "fr" => "fra", "it" => "ita", _ => "eng"
        };

        /// <summary>Locale for the voices.</summary>
        public static string Locale(string lang) => lang switch
        {
            "sr" => "sr-RS", "hr" => "hr-HR", "bs" => "bs-BA", "de" => "de-DE",
            "es" => "es-ES", "fr" => "fr-FR", "it" => "it-IT", _ => "en-US"
        };

        /// <summary>About how many words a second a narrator says (longer words in the Slavic languages and German).</summary>
        public static double WordsPerSecond(string lang) => lang switch
        {
            "en" => 2.7, "es" => 2.8, "it" => 2.7, "fr" => 2.7, _ => 2.3
        };
    }
}
