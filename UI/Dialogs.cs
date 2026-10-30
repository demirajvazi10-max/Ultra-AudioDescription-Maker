using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using UltraAudioDescriptionMaker.Ai;
using UltraAudioDescriptionMaker.Core;
using UltraAudioDescriptionMaker.Speech;

namespace UltraAudioDescriptionMaker.UI
{
    // ── Write with AI (F5) ───────────────────────────────────────────────────

    /// <summary>How the AI writes the descriptions. Nothing is sent anywhere before OK.</summary>
    public class GenerateWindow : DialogWindow
    {
        private static string L(string k) => Strings.L(k);

        private readonly ComboBox _source, _density, _language, _existing, _quality;
        private readonly CheckBox _snap, _prepare;
        private readonly PasswordBox _key;
        private readonly TextBlock _keyState;

        public new string Language => (string)Ui.Tag(_language);
        public bool KeepExisting => _existing != null && (string)Ui.Tag(_existing) == "keep";
        public bool PrepareVoices => _prepare.IsChecked == true;

        public GenerateWindow(Window owner, bool hasLines, string projectLanguage) : base(owner, L("gen_title"))
        {
            var s = AppSettings.Current;
            _source = Ui.Combo(new (string, object)[] { (L("gen_src_gemini"), "gemini"), (L("gen_src_local"), "local") }, s.AiSource);
            Body.Children.Add(Ui.Labelled(L("gen_lbl_source"), _source, L("gen_help_source")));

            _key = new PasswordBox();
            var keyPanel = Ui.Labelled(L("set_lbl_gemini"), _key);
            _keyState = new TextBlock { TextWrapping = TextWrapping.Wrap, Text = GeminiClient.HasKey ? L("key_saved") : L("gen_need_key") };
            keyPanel.Children.Add(_keyState);
            keyPanel.Children.Add(Ui.Btn(L("btn_save_key"), async (o, e) => await SaveKeyAsync()));
            keyPanel.Children.Add(Ui.LinkButton(L("set_link_gemini"), Links.GeminiKeys));
            keyPanel.Children.Add(Ui.Instructions(L("set_lbl_how_gemini"), L("set_how_gemini")));
            Body.Children.Add(keyPanel);

            _density = Ui.Combo(new (string, object)[]
            {
                (L("gen_dens_essential"), "Essential"), (L("gen_dens_normal"), "Normal"), (L("gen_dens_detailed"), "Detailed")
            }, s.Density);
            Body.Children.Add(Ui.Labelled(L("gen_lbl_density"), _density));

            _quality = Ui.Combo(new (string, object)[]
            {
                (L("gen_q_fast"), "Fast"), (L("gen_q_balanced"), "Balanced"), (L("gen_q_precise"), "Precise")
            }, s.GeminiQuality);
            Body.Children.Add(Ui.Labelled(L("gen_lbl_quality"), _quality, L("gen_help_quality")));

            _language = Ui.Combo(Languages.Codes.Select(c => (Languages.Native(c), (object)c)), projectLanguage ?? s.DescLanguage);
            Body.Children.Add(Ui.Labelled(L("gen_lbl_language"), _language));

            _snap = Ui.Check(L("gen_snap"), s.SnapToCuts);
            AutomationProperties.SetHelpText(_snap, L("gen_snap_help"));
            Body.Children.Add(_snap);

            if (hasLines)
            {
                _existing = Ui.Combo(new (string, object)[] { (L("gen_replace"), "replace"), (L("gen_keep"), "keep") }, "replace");
                Body.Children.Add(Ui.Labelled(L("gen_lbl_existing"), _existing));
            }
            _prepare = Ui.Check(L("gen_prepare"), true);
            Body.Children.Add(_prepare);

            var note = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0), Text = L("gen_privacy") };
            note.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondary");
            Body.Children.Add(note);

            void Sync()
            {
                bool gemini = (string)Ui.Tag(_source) == "gemini";
                keyPanel.Visibility = gemini && !GeminiClient.HasKey ? Visibility.Visible : Visibility.Collapsed;
                _density.IsEnabled = gemini;
                _quality.IsEnabled = gemini;
                _snap.IsEnabled = gemini;
            }
            _source.SelectionChanged += (o, e) => Sync();
            Sync();

            AddOkCancel(Ok, L("gen_ok"));
            FocusFirst(_source);
        }

        private async Task SaveKeyAsync()
        {
            string key = _key.Password.Trim();
            if (key.Length == 0) { Say(L("key_empty")); _key.Focus(); return; }
            Say(L("key_testing"));
            string problem = await GeminiClient.TestKeyAsync(key, CancellationToken.None);
            if (problem != null) { Ui.Sound(false); Say(Strings.LF("key_bad", problem)); _key.Focus(); return; }
            KeyStore.Save(GeminiClient.KeyName, key);
            GeminiClient.Reset();
            _key.Clear();
            _keyState.Text = L("key_saved");
            Ui.Sound(true);
            Say(L("key_saved"));
            _density.Focus();
        }

        private bool Ok()
        {
            var s = AppSettings.Current;
            s.AiSource = (string)Ui.Tag(_source);
            if (s.AiSource == "gemini" && !GeminiClient.HasKey) { Say(L("gen_need_key")); _key.Focus(); return false; }
            s.Density = (string)Ui.Tag(_density);
            s.GeminiQuality = (string)Ui.Tag(_quality) ?? "Balanced";
            s.SnapToCuts = _snap.IsChecked == true;
            s.DescLanguage = Language;
            s.Save();
            return true;
        }
    }

    // ── Export (Ctrl+E) ──────────────────────────────────────────────────────

    public class ExportWindow : DialogWindow
    {
        private static string L(string k) => Strings.L(k);
        private static ExportOptions _last;

        private readonly TextBox _folder, _name;
        private readonly CheckBox _mixed, _twoTracks, _audioMix, _voiceOnly, _txt, _vtt, _srt;
        private readonly ComboBox _format;

        public ExportOptions Options { get; private set; }

        public ExportWindow(Window owner, AdProject p) : base(owner, L("ex_title"))
        {
            var last = _last ?? new ExportOptions();
            _folder = new TextBox { Text = Path.GetDirectoryName(p.VideoPath) ?? "" };
            var folderPanel = Ui.Labelled(L("ex_lbl_folder"), _folder);
            folderPanel.Children.Add(Ui.Btn(L("btn_browse"), (o, e) =>
            {
                var dlg = new Microsoft.Win32.OpenFolderDialog { Title = L("ex_lbl_folder").Replace("_", "") };
                if (Directory.Exists(_folder.Text)) dlg.InitialDirectory = _folder.Text;
                if (dlg.ShowDialog(this) == true) { _folder.Text = dlg.FolderName; Say(dlg.FolderName); }
                Activate();
                _folder.Focus();
            }));
            Body.Children.Add(folderPanel);

            _name = new TextBox { Text = p.Title };
            Body.Children.Add(Ui.Labelled(L("ex_lbl_name"), _name, L("ex_help_name")));

            var head = new Label { Content = L("ex_lbl_what"), FontWeight = FontWeights.SemiBold };
            Body.Children.Add(head);
            _mixed = Ui.Check(L("ex_mixed"), last.VideoMixed);
            _twoTracks = Ui.Check(L("ex_two_tracks"), last.VideoExtraTrack);
            AutomationProperties.SetHelpText(_twoTracks, L("ex_two_tracks_help"));
            _audioMix = Ui.Check(L("ex_audio_mix"), last.AudioMix);
            _voiceOnly = Ui.Check(L("ex_voice_only"), last.AudioVoiceOnly);
            AutomationProperties.SetHelpText(_voiceOnly, L("ex_voice_only_help"));
            _txt = Ui.Check(L("ex_txt"), last.ScriptTxt);
            _vtt = Ui.Check(L("ex_vtt"), last.ScriptVtt);
            AutomationProperties.SetHelpText(_vtt, L("ex_vtt_help"));
            _srt = Ui.Check(L("ex_srt"), last.ScriptSrt);
            foreach (var c in new[] { _mixed, _twoTracks, _audioMix, _voiceOnly, _txt, _vtt, _srt }) Body.Children.Add(c);

            _format = Ui.Combo(new (string, object)[] { ("MP3", "mp3"), ("WAV", "wav") }, last.AudioFormat);
            Body.Children.Add(Ui.Labelled(L("ex_lbl_format"), _format));

            int problems = p.Lines.Count(l => l.HasProblem);
            if (problems > 0)
            {
                var warn = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0), Text = Strings.LF("ex_warn_problems", problems) };
                warn.SetResourceReference(TextBlock.ForegroundProperty, "WarningBrush");
                Body.Children.Add(warn);
            }
            AddOkCancel(Ok, L("ex_ok"));
            FocusFirst(_mixed);
        }

        private bool Ok()
        {
            string folder = _folder.Text.Trim().Trim('"');
            string name = string.Concat((_name.Text ?? "").Trim().Split(Path.GetInvalidFileNameChars()));
            if (folder.Length == 0) { Say(L("ex_err_folder")); _folder.Focus(); return false; }
            if (name.Length == 0) { Say(L("ex_err_name")); _name.Focus(); return false; }
            var o = new ExportOptions
            {
                Folder = folder, BaseName = name,
                VideoMixed = _mixed.IsChecked == true, VideoExtraTrack = _twoTracks.IsChecked == true,
                AudioMix = _audioMix.IsChecked == true, AudioVoiceOnly = _voiceOnly.IsChecked == true,
                ScriptTxt = _txt.IsChecked == true, ScriptVtt = _vtt.IsChecked == true, ScriptSrt = _srt.IsChecked == true,
                AudioFormat = (string)Ui.Tag(_format) ?? "mp3"
            };
            if (!(o.VideoMixed || o.VideoExtraTrack || o.AudioMix || o.AudioVoiceOnly || o.ScriptTxt || o.ScriptVtt || o.ScriptSrt))
            {
                Say(L("ex_err_nothing"));
                _mixed.Focus();
                return false;
            }
            try { Directory.CreateDirectory(folder); }
            catch (Exception ex) { Say(Strings.LF("err_failed", ex.Message)); _folder.Focus(); return false; }
            Options = _last = o;
            return true;
        }
    }

    // ── Timing report (Ctrl+I) ───────────────────────────────────────────────

    /// <summary>The descriptions that need attention. Enter goes to one in the main list.</summary>
    public class ReportWindow : DialogWindow
    {
        public AdLine Chosen { get; private set; }

        public ReportWindow(Window owner, AdProject p) : base(owner, Strings.L("rep_title"), 760)
        {
            var problems = Timing.Problems(p);
            int faster = p.Lines.Count(l => l.Status == FitStatus.SpedUp);
            bool estimated = p.Lines.Any(l => l.Estimated);
            var summary = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8),
                Text = Strings.LF("rep_summary", p.Lines.Count, p.Lines.Count - problems.Count, faster, problems.Count) +
                       (estimated ? " " + Strings.L("rep_estimated") : "")
            };
            Body.Children.Add(summary);

            if (problems.Count == 0)
            {
                var box = new TextBox { Text = summary.Text, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, IsReadOnlyCaretVisible = true };
                Body.Children.Add(Ui.Labelled(Strings.L("rep_lbl_list"), box));
                AddClose();
                FocusFirst(box);
                return;
            }
            var list = new ListBox { Height = 380 };
            foreach (var l in problems)
            {
                string text = $"{TimeText.Format(l.Time)}  {l.Text} — {MainWindow.ShortStatus(l)}";
                var item = new ListBoxItem { Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap }, Tag = l };
                AutomationProperties.SetName(item, text);
                list.Items.Add(item);
            }
            list.SelectedIndex = 0;
            void Choose()
            {
                if (Chosen != null) return;   // Enter can reach both the list and the default button
                if ((list.SelectedItem as ListBoxItem)?.Tag is AdLine l) { Chosen = l; DialogResult = true; }
            }
            list.KeyDown += (o, e) => { if (e.Key == Key.Enter) { e.Handled = true; Choose(); } };
            list.MouseDoubleClick += (o, e) => Choose();
            Body.Children.Add(Ui.Labelled(Strings.L("rep_lbl_list"), list, Strings.L("rep_help")));
            var go = Ui.Btn(Strings.L("rep_go"), (o, e) => Choose());
            go.IsDefault = true;
            Buttons.Children.Add(go);
            AddClose();
            Loaded += (o, e) =>
            {
                list.UpdateLayout();
                (list.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem)?.Focus();
            };
        }
    }

    // ── Script as text (Ctrl+Shift+E) ────────────────────────────────────────

    /// <summary>All descriptions as plain text, "mm:ss.f | description" per line — easy to edit with a screen reader.</summary>
    public class ScriptTextWindow : DialogWindow
    {
        private readonly TextBox _box;
        public string ScriptValue => _box.Text;

        public ScriptTextWindow(Window owner, string text) : base(owner, Strings.L("scr_title"), 820)
        {
            _box = new TextBox
            {
                Text = text, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 460,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, FontFamily = new System.Windows.Media.FontFamily("Consolas")
            };
            Body.Children.Add(Ui.Labelled(Strings.L("scr_lbl"), _box, Strings.L("scr_help")));
            AddOkCancel(() =>
            {
                if (ScriptText.Read(_box.Text).Count == 0 && _box.Text.Trim().Length > 0) { Say(Strings.L("scr_err")); _box.Focus(); return false; }
                return true;
            });
            FocusFirst(_box);
        }
    }

    // ── Settings (Ctrl+Shift+P) ──────────────────────────────────────────────

    public class SettingsWindow : DialogWindow
    {
        private static string L(string k) => Strings.L(k);
        private static string LF(string k, params object[] a) => Strings.LF(k, a);

        private readonly AdProject _project;
        private readonly ComboBox _ui, _theme, _lang, _voice, _rate, _maxUp, _duck, _previewDuck;
        private readonly CheckBox _snap, _showVideo, _listTiming, _sounds;
        private readonly TextBox _ffmpeg, _downloads;
        private readonly TextBlock _ffmpegState, _gemState, _gcState;
        private readonly PasswordBox _gemKey, _gcKey;
        private NAudio.Wave.WaveOutEvent _test;
        private NAudio.Wave.AudioFileReader _testReader;

        /// <summary>True when the language, voice or speed changed (the voices must be prepared again).</summary>
        public bool ProjectChanged { get; private set; }

        public SettingsWindow(Window owner, AdProject project) : base(owner, L("set_title"), 680)
        {
            _project = project;
            var s = AppSettings.Current;

            Section(L("set_sec_program"));
            _ui = Ui.Combo(new (string, object)[] { ("Srpski", "sr"), ("English", "en") }, s.UiLanguage);
            Body.Children.Add(Ui.Labelled(L("set_lbl_ui"), _ui));
            _theme = Ui.Combo(new (string, object)[]
            {
                (L("set_theme_system"), "System"), (L("set_theme_dark"), "Dark"), (L("set_theme_light"), "Light"), (L("set_theme_contrast"), "Contrast")
            }, s.Theme);
            Body.Children.Add(Ui.Labelled(L("set_lbl_theme"), _theme));
            _showVideo = Ui.Check(L("set_show_video"), s.ShowVideo);
            _listTiming = Ui.Check(L("set_list_timing"), s.ListReadsTiming);
            _sounds = Ui.Check(L("set_sounds"), s.Sounds);
            Body.Children.Add(_showVideo); Body.Children.Add(_listTiming); Body.Children.Add(_sounds);

            Section(L("set_sec_voice"));
            string lang = project?.Language ?? s.DescLanguage;
            _lang = Ui.Combo(Languages.Codes.Select(c => (Languages.Native(c), (object)c)), lang);
            Body.Children.Add(Ui.Labelled(L("set_lbl_lang"), _lang, project != null ? L("set_help_lang") : null));
            _voice = new ComboBox { MinWidth = 320, HorizontalAlignment = HorizontalAlignment.Left };
            FillVoices(lang, project?.Voice ?? s.Voice);
            var voicePanel = Ui.Labelled(L("set_lbl_voice"), _voice);
            voicePanel.Children.Add(Ui.Btn(L("set_test_voice"), async (o, e) => await TestVoiceAsync()));
            Body.Children.Add(voicePanel);
            _lang.SelectionChanged += (o, e) => FillVoices((string)Ui.Tag(_lang), null);

            _rate = Ui.Combo(new[] { -20, -10, 0, 10, 20, 30 }.Select(r => (r == 0 ? L("set_rate_normal") : (r > 0 ? "+" : "") + r + " %", (object)r)), s.VoiceRate);
            Body.Children.Add(Ui.Labelled(L("set_lbl_rate"), _rate));
            _maxUp = Ui.Combo(new (string, object)[]
            {
                (L("set_up_none"), 1.0), ("10 %", 1.1), ("20 %", 1.2), ("25 %", 1.25), ("35 %", 1.35), ("50 %", 1.5)
            }, Nearest(s.MaxSpeedUp, new[] { 1.0, 1.1, 1.2, 1.25, 1.35, 1.5 }));
            Body.Children.Add(Ui.Labelled(L("set_lbl_maxup"), _maxUp, L("set_help_maxup")));
            _duck = Ui.Combo(new (string, object)[] { (L("set_duck_light"), "Light"), (L("set_duck_medium"), "Medium"), (L("set_duck_strong"), "Strong") }, s.Ducking);
            Body.Children.Add(Ui.Labelled(L("set_lbl_duck"), _duck));
            _previewDuck = Ui.Combo(new (string, object)[] { ("20 %", 20), ("35 %", 35), ("50 %", 50), ("70 %", 70) },
                                    (int)Nearest(s.PreviewDuckVolume, new double[] { 20, 35, 50, 70 }));
            Body.Children.Add(Ui.Labelled(L("set_lbl_preview_duck"), _previewDuck));
            _snap = Ui.Check(L("gen_snap"), s.SnapToCuts);
            Body.Children.Add(_snap);

            Section(L("set_sec_keys"));
            _gemKey = new PasswordBox();
            var gem = Ui.Labelled(L("set_lbl_gemini"), _gemKey, L("set_help_gemini"));
            _gemState = new TextBlock { TextWrapping = TextWrapping.Wrap, Text = KeyState(GeminiClient.KeyName) };
            gem.Children.Add(_gemState);
            gem.Children.Add(Ui.Btn(L("btn_save_key"), async (o, e) => await SaveGeminiAsync()));
            gem.Children.Add(Ui.Instructions(L("set_lbl_how_gemini"), L("set_how_gemini")));
            gem.Children.Add(Ui.LinkButton(L("set_link_gemini"), Links.GeminiKeys));
            Body.Children.Add(gem);
            _gcKey = new PasswordBox();
            var gc = Ui.Labelled(L("set_lbl_gc"), _gcKey, L("set_help_gc"));
            _gcState = new TextBlock { TextWrapping = TextWrapping.Wrap, Text = KeyState(NeuralTts.GoogleKeyName) };
            gc.Children.Add(_gcState);
            gc.Children.Add(Ui.Btn(L("btn_save_key"), async (o, e) => await SaveGoogleAsync()));
            gc.Children.Add(Ui.Instructions(L("set_lbl_how_gc"), L("set_how_gc")));
            var gcLinks = new WrapPanel();
            gcLinks.Children.Add(Ui.LinkButton(L("set_link_gc_console"), Links.CloudConsole));
            gcLinks.Children.Add(Ui.LinkButton(L("set_link_gc_api"), Links.TtsApi));
            gcLinks.Children.Add(Ui.LinkButton(L("set_link_gc_keys"), Links.CloudKeys));
            gc.Children.Add(gcLinks);
            Body.Children.Add(gc);

            Section(L("set_sec_tools"));
            _ffmpeg = new TextBox { Text = s.FfmpegFolder ?? "" };
            var ff = Ui.Labelled(L("set_lbl_ffmpeg"), _ffmpeg, L("set_help_ffmpeg"));
            _ffmpegState = new TextBlock { TextWrapping = TextWrapping.Wrap, Text = FfmpegState() };
            ff.Children.Add(_ffmpegState);
            ff.Children.Add(Ui.Btn(L("btn_browse"), (o, e) =>
            {
                var dlg = new Microsoft.Win32.OpenFolderDialog { Title = L("set_lbl_ffmpeg").Replace("_", "") };
                if (dlg.ShowDialog(this) == true)
                {
                    _ffmpeg.Text = dlg.FolderName;
                    s.FfmpegFolder = dlg.FolderName;
                    AppPaths.ResetTools();
                    _ffmpegState.Text = FfmpegState();
                    Say(_ffmpegState.Text);
                }
                Activate();
                _ffmpeg.Focus();
            }));
            Body.Children.Add(ff);
            _downloads = new TextBox { Text = s.DownloadFolder ?? "" };
            var dl = Ui.Labelled(L("set_lbl_downloads"), _downloads, L("set_help_downloads"));
            dl.Children.Add(Ui.Btn(L("btn_browse"), (o, e) =>
            {
                var dlg = new Microsoft.Win32.OpenFolderDialog { Title = L("set_lbl_downloads").Replace("_", "") };
                if (Directory.Exists(_downloads.Text)) dlg.InitialDirectory = _downloads.Text;
                if (dlg.ShowDialog(this) == true) { _downloads.Text = dlg.FolderName; Say(dlg.FolderName); }
                Activate();
                _downloads.Focus();
            }));
            Body.Children.Add(dl);
            var clear = Ui.Btn(LF("set_clear_cache", VoiceEngine.CacheMegabytes().ToString("0.0")), null);
            clear.Click += (o, e) =>
            {
                StopTest();
                VoiceEngine.ClearCache();
                if (_project != null) foreach (var l in _project.Lines) l.Invalidate();
                ProjectChanged = _project != null;
                clear.Content = LF("set_clear_cache", VoiceEngine.CacheMegabytes().ToString("0.0"));
                Say(L("set_cache_cleared"));
            };
            Body.Children.Add(clear);

            AddOkCancel(Ok);
            FocusFirst(_ui);
            Closed += (o, e) => StopTest();
        }

        private void Section(string title)
        {
            var h = new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 12, 0, 2) };
            AutomationProperties.SetHeadingLevel(h, AutomationHeadingLevel.Level2);
            Body.Children.Add(h);
        }

        private static double Nearest(double v, double[] options) => options.OrderBy(o => Math.Abs(o - v)).First();

        private static string KeyState(string name)
        {
            if (KeyStore.IsShared(name)) return L("key_shared");
            return KeyStore.Load(name) != null ? L("key_saved") : L("key_none");
        }

        private static string FfmpegState() =>
            AppPaths.FFmpegAvailable ? LF("set_ffmpeg_found", AppPaths.FFmpeg) : L("set_ffmpeg_missing");

        private void FillVoices(string lang, string selected)
        {
            _voice.Items.Clear();
            int pick = 0, i = 0;
            foreach (var v in VoiceEngine.Voices(lang))
            {
                _voice.Items.Add(new ComboBoxItem { Content = v.Display, Tag = v.Tag });
                if (v.Tag == selected) pick = i;
                i++;
            }
            _voice.SelectedIndex = _voice.Items.Count > 0 ? pick : -1;
        }

        private async Task TestVoiceAsync()
        {
            string tag = (string)Ui.Tag(_voice);
            string lang = (string)Ui.Tag(_lang);
            int rate = (int)Ui.Tag(_rate);
            Say(L("set_testing_voice"));
            try
            {
                string path = await VoiceEngine.SpeakAsync(L("set_test_sentence_" + lang), tag, lang, rate, CancellationToken.None);
                StopTest();
                if (path == null) return;
                _testReader = new NAudio.Wave.AudioFileReader(path);
                _test = new NAudio.Wave.WaveOutEvent();
                _test.Init(_testReader);
                _test.Play();
            }
            catch (Exception ex) { AppLog.Error("Testing a voice", ex); Ui.Sound(false); Say(LF("err_failed", ex.Message)); }
        }

        private void StopTest()
        {
            try { _test?.Stop(); _test?.Dispose(); _testReader?.Dispose(); } catch { }
            _test = null; _testReader = null;
        }

        private async Task SaveGeminiAsync()
        {
            string key = _gemKey.Password.Trim();
            if (key.Length == 0) { Say(L("key_empty")); _gemKey.Focus(); return; }
            Say(L("key_testing"));
            string problem = await GeminiClient.TestKeyAsync(key, CancellationToken.None);
            if (problem != null) { Ui.Sound(false); Say(LF("key_bad", problem)); _gemKey.Focus(); return; }
            KeyStore.Save(GeminiClient.KeyName, key);
            GeminiClient.Reset();
            _gemKey.Clear();
            _gemState.Text = KeyState(GeminiClient.KeyName);
            Ui.Sound(true);
            Say(L("key_saved"));
        }

        private async Task SaveGoogleAsync()
        {
            string key = _gcKey.Password.Trim();
            if (key.Length == 0) { Say(L("key_empty")); _gcKey.Focus(); return; }
            Say(L("key_testing"));
            string problem = await NeuralTts.TestGoogleKeyAsync(key, (string)Ui.Tag(_lang), CancellationToken.None);
            if (problem != null) { Ui.Sound(false); Say(LF("key_bad", problem)); _gcKey.Focus(); return; }
            KeyStore.Save(NeuralTts.GoogleKeyName, key);
            _gcKey.Clear();
            _gcState.Text = KeyState(NeuralTts.GoogleKeyName);
            FillVoices((string)Ui.Tag(_lang), (string)Ui.Tag(_voice));
            Ui.Sound(true);
            Say(L("key_saved_gc"));
        }

        private bool Ok()
        {
            var s = AppSettings.Current;
            string lang = (string)Ui.Tag(_lang);
            string voice = (string)Ui.Tag(_voice);
            int rate = (int)Ui.Tag(_rate);
            double maxUp = (double)Ui.Tag(_maxUp);
            if (_project != null && (lang != _project.Language || voice != _project.Voice || rate != s.VoiceRate || Math.Abs(maxUp - s.MaxSpeedUp) > 0.001))
                ProjectChanged = true;
            if (_project != null) { _project.Language = lang; _project.Voice = voice; }
            s.UiLanguage = (string)Ui.Tag(_ui);
            s.Theme = (string)Ui.Tag(_theme);
            s.ShowVideo = _showVideo.IsChecked == true;
            s.ListReadsTiming = _listTiming.IsChecked == true;
            s.Sounds = _sounds.IsChecked == true;
            s.DescLanguage = lang;
            s.Voice = voice;
            s.VoiceRate = rate;
            s.MaxSpeedUp = maxUp;
            s.Ducking = (string)Ui.Tag(_duck);
            s.PreviewDuckVolume = (int)Ui.Tag(_previewDuck);
            s.SnapToCuts = _snap.IsChecked == true;
            string ff = _ffmpeg.Text.Trim().Trim('"');
            s.FfmpegFolder = ff.Length == 0 ? null : ff;
            string dlf = _downloads.Text.Trim().Trim('"');
            if (dlf.Length > 0) s.DownloadFolder = dlf;
            AppPaths.ResetTools();
            s.Save();
            return true;
        }
    }
}

namespace UltraAudioDescriptionMaker.UI
{
    /// <summary>Web addresses used in the program, in one place.</summary>
    public static class Links
    {
        public const string Sponsors = "https://github.com/sponsors/demirajvazi10-max";
        public const string GitHub = "https://github.com/demirajvazi10-max";
        public const string GeminiKeys = "https://aistudio.google.com/app/apikey";
        public const string CloudConsole = "https://console.cloud.google.com/projectcreate";
        public const string TtsApi = "https://console.cloud.google.com/apis/library/texttospeech.googleapis.com";
        public const string CloudKeys = "https://console.cloud.google.com/apis/credentials";
    }

    /// <summary>About the program — always with the link to support the author on GitHub Sponsors.</summary>
    public class AboutWindow : DialogWindow
    {
        public AboutWindow(Window owner) : base(owner, Strings.L("m_about").Replace("_", ""), 560)
        {
            var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            string version = v == null ? "" : $"{v.Major}.{v.Minor}.{v.Build}";
            var box = new TextBox
            {
                Text = Strings.LF("about_text", version), IsReadOnly = true, IsReadOnlyCaretVisible = true,
                TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, BorderThickness = new Thickness(0)
            };
            Body.Children.Add(Ui.Labelled(Strings.L("about_lbl"), box));
            var sponsor = Ui.LinkButton(Strings.L("about_sponsor"), Links.Sponsors);
            sponsor.FontWeight = FontWeights.SemiBold;
            Body.Children.Add(sponsor);
            Body.Children.Add(Ui.LinkButton(Strings.L("about_github"), Links.GitHub));
            AddClose();
            FocusFirst(box);
        }
    }
}
