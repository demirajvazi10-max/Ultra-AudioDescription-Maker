using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using UltraAudioDescriptionMaker.Ai;
using UltraAudioDescriptionMaker.Core;
using UltraAudioDescriptionMaker.Speech;

namespace UltraAudioDescriptionMaker.UI
{
    /// <summary>One description in the list.</summary>
    public class LineItem : INotifyPropertyChanged
    {
        public AdLine Line { get; }
        private readonly Func<string> _lang;
        public LineItem(AdLine line, Func<string> lang) { Line = line; _lang = lang; }

        public string TimeLabel => TimeText.Format(Line.Time);
        public string Text => Line.Text;
        public string StatusLabel => MainWindow.ShortStatus(Line);
        public Visibility StatusVisibility => string.IsNullOrEmpty(StatusLabel) ? Visibility.Collapsed : Visibility.Visible;

        /// <summary>What the screen reader says: the time, the text, and the timing only when it needs attention.</summary>
        public string Spoken
        {
            get
            {
                string s = $"{TimeLabel}  {Line.Text}";
                if (AppSettings.Current.ListReadsTiming && Line.HasProblem) s += ". " + StatusLabel;
                return s;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void Refresh() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    /// <summary>
    /// The main window. The keyboard order is the work order: the list of descriptions, the
    /// selected description's time and text, its timing, and the buttons. Every command is in the
    /// menu with its shortcut key. The program speaks only when something was asked for or
    /// finished — it never talks on its own.
    /// </summary>
    public partial class MainWindow : Window
    {
        private static string L(string k) => Strings.L(k);
        private static string LF(string k, params object[] a) => Strings.LF(k, a);

        private AdProject _project;
        private readonly ObservableCollection<LineItem> _items = new();
        private Player _player;
        private CancellationTokenSource _work;
        private bool _loadingEditor;
        private readonly HashSet<AdLine> _voicing = new();
        private readonly Stack<List<(double, string)>> _undo = new(), _redo = new();
        private readonly string _startFile;
        private Button _btnPreview, _btnPlay, _btnWrite, _btnVoices, _btnExport, _btnStop;

        public MainWindow(string startFile = null)
        {
            _startFile = startFile;
            InitializeComponent();
            LineList.ItemsSource = _items;
            BuildMenu();
            BuildButtons();
            ApplyTexts();

            LineList.SelectionChanged += (s, e) => LoadEditor();
            LineList.PreviewKeyDown += List_PreviewKeyDown;
            LineList.MouseDoubleClick += (s, e) => { if (Selected != null) _ = PreviewAsync(Selected); };
            TimeBox.PreviewKeyDown += Editor_PreviewKeyDown;
            TextEdit.PreviewKeyDown += Editor_PreviewKeyDown;
            TimeBox.LostKeyboardFocus += (s, e) => ApplyTime(false);
            TextEdit.LostKeyboardFocus += (s, e) => ApplyText(false);
            PreviewKeyDown += Window_PreviewKeyDown;
            Drop += Window_Drop;
            Closing += Window_Closing;
            Loaded += Window_Loaded;
            ShowProject();
        }

        // ── start ────────────────────────────────────────────────────────────

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _player = new Player(Dispatcher) { Lines = () => _project?.Lines };
                Video.MediaPlayer = _player.MediaPlayer;
                _player.Tick += pos => PositionText.Text = $"{TimeText.Format(pos)} / {TimeText.Format(_project?.Duration ?? 0)}";
                _player.PreviewEnded += () => { NowSpeaking.Text = ""; };
                _player.EndReached += () => { NowSpeaking.Text = ""; UpdatePlayButton(); Say(L("st_end_reached")); };
                _player.NeedsVoice += line => _ = VoiceAheadAsync(line);
                _player.DescriptionStarted += line => NowSpeaking.Text = line.Text;
            }
            catch (Exception ex)
            {
                AppLog.Error("Starting the video player", ex);
                Say(LF("err_player", ex.Message));
            }
            ApplyVideoVisibility();

            // FFmpeg is needed for everything with video: fetched by itself the first time
            if (!AppPaths.FFmpegAvailable) await DownloadFfmpegAsync(true);
            if (_startFile != null)
            {
                if (_startFile.EndsWith(AdProject.Extension, StringComparison.OrdinalIgnoreCase)) await OpenProjectAsync(_startFile);
                else await OpenVideoAsync(_startFile);
            }
            else if (AppPaths.FFmpegAvailable)
            {
                Say(L("st_welcome"));
            }
            FocusList();
        }

        /// <summary>Switches the program's language at once (menus, buttons, messages).</summary>
        private void SetUiLanguage(string code)
        {
            AppSettings.Current.UiLanguage = code;
            AppSettings.Current.Save();
            Strings.Language = code;
            BuildMenu();
            BuildButtons();
            ApplyTexts();
            ShowProject();
            Say(L("st_language"));
            FocusList();
        }

        private void ApplyTexts()
        {
            ListLabel.Content = L("lbl_list");
            TimeLabel.Content = L("lbl_time");
            TextLabel.Content = L("lbl_text");
            TimingLabel.Content = L("lbl_timing");
            AutomationProperties.SetName(LineList, L("lbl_list").Replace("_", ""));
            AutomationProperties.SetName(TimeBox, L("lbl_time").Replace("_", ""));
            AutomationProperties.SetHelpText(TimeBox, L("help_time"));
            AutomationProperties.SetName(TextEdit, L("lbl_text").Replace("_", ""));
            AutomationProperties.SetHelpText(TextEdit, L("help_text"));
            AutomationProperties.SetName(TimingBox, L("lbl_timing").Replace("_", ""));
            AutomationProperties.SetName(Video, L("lbl_video"));
        }

        private void ApplyVideoVisibility()
        {
            // the video view stays in the window (its native window must exist, or VLC opens its
            // own); "hidden" only makes its column zero wide
            VideoColumn.Width = AppSettings.Current.ShowVideo ? new GridLength(4, GridUnitType.Star) : new GridLength(0);
        }

        // ── menu and buttons ─────────────────────────────────────────────────

        private MenuItem Item(string key, string gesture, Action action)
        {
            var mi = new MenuItem { Header = L(key), InputGestureText = gesture ?? "" };
            mi.Click += (s, e) => action();
            return mi;
        }

        private MenuItem Top(string key, params object[] children)
        {
            var mi = new MenuItem { Header = L(key) };
            foreach (var c in children) mi.Items.Add(c is string ? new Separator() : c);
            return mi;
        }

        private void BuildMenu()
        {
            MainMenu.Items.Clear();
            MainMenu.Items.Add(Top("m_file",
                Item("m_open_video", "Ctrl+O", () => _ = OpenVideoDialogAsync()),
                Item("m_open_link", "Ctrl+L", () => _ = OpenLinkAsync()),
                Item("m_open_project", "Ctrl+Shift+O", () => _ = OpenProjectDialogAsync()),
                Item("m_save", "Ctrl+S", () => SaveProject(false)),
                Item("m_save_as", "Ctrl+Shift+S", () => SaveProject(true)),
                "-",
                Item("m_import", "", () => ImportScript()),
                Item("m_export", "Ctrl+E", () => _ = ExportAsync()),
                "-",
                Item("m_exit", "Alt+F4", Close)));
            MainMenu.Items.Add(Top("m_desc",
                Item("m_write_ai", "F5", () => _ = WriteWithAiAsync()),
                Item("m_voices", "F8", () => _ = PrepareVoicesAsync(true)),
                Item("m_report", "Ctrl+I", ShowReport),
                Item("m_next_problem", "F4", () => GoProblem(+1)),
                Item("m_prev_problem", "Shift+F4", () => GoProblem(-1)),
                "-",
                Item("m_add", "Insert", AddLine),
                Item("m_delete", "Delete", DeleteLine),
                Item("m_edit_text", "F2", () => { if (Selected != null) { TextEdit.Focus(); TextEdit.SelectAll(); } }),
                Item("m_edit_time", "Ctrl+F2", () => { if (Selected != null) { TimeBox.Focus(); TimeBox.SelectAll(); } }),
                Item("m_set_time", "Ctrl+T", SetTimeToPosition),
                Item("m_earlier", "Ctrl+Left", () => Nudge(-0.1)),
                Item("m_later", "Ctrl+Right", () => Nudge(+0.1)),
                Item("m_earlier_1", "Ctrl+Shift+Left", () => Nudge(-1)),
                Item("m_later_1", "Ctrl+Shift+Right", () => Nudge(+1)),
                "-",
                Item("m_script_text", "Ctrl+Shift+E", EditScriptAsText),
                Item("m_undo", "Ctrl+Z", Undo),
                Item("m_redo", "Ctrl+Y", Redo),
                "-",
                Item("m_cancel_work", "Ctrl+Shift+X", CancelWork)));
            MainMenu.Items.Add(Top("m_play",
                Item("m_preview", "Space", () => { if (Selected != null) _ = PreviewAsync(Selected); }),
                Item("m_listen", "Shift+Space", () => _ = ListenAsync()),
                Item("m_play_pause", "F6", PlayPause),
                Item("m_stop", "Escape", () => StopPlayback()),
                Item("m_back", "Alt+Left", () => SeekBy(-5)),
                Item("m_forward", "Alt+Right", () => SeekBy(+5)),
                Item("m_goto", "Ctrl+G", GoToTime),
                Item("m_goto_line", "Enter", GoToSelected),
                Item("m_position", "F9", SayPosition)));
            var language = new MenuItem { Header = "_Language / Jezik" };
            foreach (var (code, name) in new[] { ("en", "English"), ("sr", "Srpski") })
            {
                var mi = new MenuItem { Header = name, IsCheckable = true, IsChecked = AppSettings.Current.UiLanguage == code };
                mi.Click += (s, e) => SetUiLanguage(code);
                language.Items.Add(mi);
            }
            MainMenu.Items.Add(Top("m_tools",
                language,
                Item("m_settings", "Ctrl+Shift+P", OpenSettings),
                Item("m_ffmpeg", "", () => _ = DownloadFfmpegAsync(false)),
                Item("m_ytdlp", "", () => _ = UpdateYtDlpAsync()),
                Item("m_log", "", OpenLog)));
            MainMenu.Items.Add(Top("m_help",
                Item("m_keys", "F1", ShowKeys),
                Item("m_sponsor", "", () => Ui.OpenLink(Links.Sponsors)),
                Item("m_about", "", ShowAbout)));
        }

        private void BuildButtons()
        {
            ButtonRow.Children.Clear();
            _btnWrite = Ui.Btn(L("b_write"), (s, e) => _ = WriteWithAiAsync());
            _btnPreview = Ui.Btn(L("b_preview"), (s, e) => { if (Selected != null) _ = PreviewAsync(Selected); });
            _btnPlay = Ui.Btn(L("b_play"), (s, e) => PlayPause());
            var add = Ui.Btn(L("b_add"), (s, e) => AddLine());
            var del = Ui.Btn(L("b_delete"), (s, e) => DeleteLine());
            _btnVoices = Ui.Btn(L("b_voices"), (s, e) => _ = PrepareVoicesAsync(true));
            _btnExport = Ui.Btn(L("b_export"), (s, e) => _ = ExportAsync());
            _btnStop = Ui.Btn(L("b_stop_work"), (s, e) => CancelWork());
            _btnStop.Visibility = Visibility.Collapsed;
            foreach (var b in new[] { _btnWrite, _btnPreview, _btnPlay, add, del, _btnVoices, _btnExport, _btnStop })
                ButtonRow.Children.Add(b);
        }

        private void UpdatePlayButton() => _btnPlay.Content = _player?.IsPlaying == true ? L("b_pause") : L("b_play");

        // ── keyboard ─────────────────────────────────────────────────────────

        private bool InTextBox => Keyboard.FocusedElement is TextBox || Keyboard.FocusedElement is PasswordBox;

        /// <summary>A menu is open or has the focus: Escape belongs to it (it only closes the menu).</summary>
        private bool InMenu => Keyboard.FocusedElement is MenuItem || MainMenu.IsKeyboardFocusWithin ||
                               MainMenu.Items.OfType<MenuItem>().Any(m => m.IsSubmenuOpen);

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var mods = Keyboard.Modifiers;
            bool ctrl = mods.HasFlag(ModifierKeys.Control), shift = mods.HasFlag(ModifierKeys.Shift), alt = mods.HasFlag(ModifierKeys.Alt);
            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            bool handled = true;

            if (alt && !ctrl && key == Key.Left) SeekBy(-5);
            else if (alt && !ctrl && key == Key.Right) SeekBy(+5);
            else if (key == Key.F1 && mods == ModifierKeys.None) ShowKeys();
            else if (key == Key.F2 && ctrl) { if (Selected != null) { TimeBox.Focus(); TimeBox.SelectAll(); } }
            else if (key == Key.F2 && mods == ModifierKeys.None) { if (Selected != null) { TextEdit.Focus(); TextEdit.SelectAll(); } }
            else if (key == Key.F4 && !alt) GoProblem(shift ? -1 : +1);
            else if (key == Key.F5 && mods == ModifierKeys.None) _ = WriteWithAiAsync();
            else if (key == Key.F6 && mods == ModifierKeys.None) PlayPause();
            else if (key == Key.F8 && mods == ModifierKeys.None) _ = PrepareVoicesAsync(true);
            else if (key == Key.F9 && mods == ModifierKeys.None) SayPosition();
            // Escape only stops playback, and only when no menu or text field has it; long work is
            // stopped on purpose with Ctrl+Shift+X (with a question), never by a stray Escape
            else if (key == Key.Escape && mods == ModifierKeys.None && !InTextBox && !InMenu) { if (!StopPlayback()) handled = false; }
            else if (ctrl && shift && !alt && key == Key.X) CancelWork();
            else if (ctrl && !alt && key == Key.O) { if (shift) _ = OpenProjectDialogAsync(); else _ = OpenVideoDialogAsync(); }
            else if (ctrl && !alt && key == Key.S) SaveProject(shift);
            else if (ctrl && !alt && key == Key.E) { if (shift) EditScriptAsText(); else _ = ExportAsync(); }
            else if (ctrl && !alt && !shift && key == Key.I) ShowReport();
            else if (ctrl && !alt && !shift && key == Key.G) GoToTime();
            else if (ctrl && !alt && !shift && key == Key.L) _ = OpenLinkAsync();
            else if (ctrl && !alt && !shift && key == Key.T) SetTimeToPosition();
            else if (ctrl && !alt && !shift && key == Key.P) { if (Selected != null) _ = PreviewAsync(Selected); }
            else if (ctrl && !alt && shift && key == Key.P) OpenSettings();
            else if (ctrl && !alt && !shift && key == Key.Z && !InTextBox) Undo();
            else if (ctrl && !alt && !shift && key == Key.Y && !InTextBox) Redo();
            else handled = false;
            if (handled) e.Handled = true;
        }

        private void List_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var mods = Keyboard.Modifiers;
            bool ctrl = mods.HasFlag(ModifierKeys.Control), shift = mods.HasFlag(ModifierKeys.Shift);
            bool handled = true;
            if (e.Key == Key.Space && mods == ModifierKeys.None) { if (Selected != null) _ = PreviewAsync(Selected); }
            else if (e.Key == Key.Space && shift && !ctrl) _ = ListenAsync();
            else if (e.Key == Key.Enter && mods == ModifierKeys.None) GoToSelected();
            else if (e.Key == Key.Delete && mods == ModifierKeys.None) DeleteLine();
            else if (e.Key == Key.Insert && mods == ModifierKeys.None) AddLine();
            else if (ctrl && e.Key == Key.Left) Nudge(shift ? -1 : -0.1);
            else if (ctrl && e.Key == Key.Right) Nudge(shift ? +1 : +0.1);
            else handled = false;
            if (handled) e.Handled = true;
        }

        private void Editor_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
            {
                e.Handled = true;
                if (sender == TimeBox) ApplyTime(true); else ApplyText(true);
                FocusList();
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                LoadEditor();   // back to what was saved
                FocusList();
            }
        }

        // ── the list ─────────────────────────────────────────────────────────

        private AdLine Selected => (LineList.SelectedItem as LineItem)?.Line;

        private void FocusList()
        {
            if (LineList.SelectedIndex < 0 && _items.Count > 0) LineList.SelectedIndex = 0;
            LineList.UpdateLayout();
            if (LineList.ItemContainerGenerator.ContainerFromIndex(Math.Max(0, LineList.SelectedIndex)) is ListBoxItem item) item.Focus();
            else LineList.Focus();
        }

        /// <summary>Rebuilds the list in time order and selects <paramref name="select"/>.</summary>
        private void RefreshList(AdLine select = null)
        {
            bool listHadFocus = LineList.IsKeyboardFocusWithin;
            select ??= Selected;
            _project?.Sort();
            if (_project != null) Timing.Estimate(_project);
            _items.Clear();
            if (_project != null)
                foreach (var l in _project.Lines) _items.Add(new LineItem(l, () => _project?.Language));
            var pick = _items.FirstOrDefault(i => i.Line == select);
            if (pick != null) LineList.SelectedItem = pick;
            else if (_items.Count > 0) LineList.SelectedIndex = 0;
            if (LineList.SelectedItem != null) LineList.ScrollIntoView(LineList.SelectedItem);
            if (listHadFocus) FocusList();
            UpdateTitle();
            LoadEditor();
        }

        /// <summary>The timing changed but not the order: only the texts in the list are refreshed.</summary>
        private void RefreshStatuses()
        {
            if (_project != null) Timing.Estimate(_project);
            foreach (var i in _items) i.Refresh();
            LoadEditor();
        }

        private void LoadEditor()
        {
            _loadingEditor = true;
            var l = Selected;
            TimeBox.Text = l == null ? "" : TimeText.Format(l.Time);
            TextEdit.Text = l?.Text ?? "";
            TimingBox.Text = l == null ? "" : LongStatus(l);
            TimeBox.IsEnabled = TextEdit.IsEnabled = l != null;
            _loadingEditor = false;
        }

        // ── status texts ─────────────────────────────────────────────────────

        private static string Sec(double s) => s.ToString("0.0", Strings.Language == "en" ? System.Globalization.CultureInfo.InvariantCulture : System.Globalization.CultureInfo.GetCultureInfo("sr-Latn-RS"));

        /// <summary>Short status for the list (empty when it simply fits).</summary>
        public static string ShortStatus(AdLine l)
        {
            string s = l.Status switch
            {
                FitStatus.SpedUp => L("st_spedup"),
                FitStatus.OverDialogue => L("st_over"),
                FitStatus.IntoDialogue => LF("st_into_dialogue", Sec(l.Overrun)),
                FitStatus.IntoNext => LF("st_into_next", Sec(l.Overrun)),
                FitStatus.Late => LF("st_late", Sec(l.Overrun)),
                _ => ""
            };
            if (s.Length > 0 && l.Estimated && l.HasProblem) s += " " + L("st_estimate");
            return s;
        }

        /// <summary>The full timing of a description, for the read-only field under the text.</summary>
        private string LongStatus(AdLine l)
        {
            var parts = new List<string> { LF("tm_starts", TimeText.Format(l.PlacedAt > 0 ? l.PlacedAt : l.Time)) };
            if (l.Duration > 0 && !l.Estimated) parts.Add(LF("tm_length", Sec(l.Duration), Sec(l.Window)));
            else parts.Add(LF("tm_length_est", Sec(Timing.EstimateSeconds(l.Text, _project?.Language ?? "en")), Sec(l.Window)));
            if (l.RatePercent > AppSettings.Current.VoiceRate) parts.Add(LF("tm_rate", l.RatePercent - AppSettings.Current.VoiceRate));
            string status = l.Status switch
            {
                FitStatus.Ok => L("tm_ok"),
                FitStatus.SpedUp => L("tm_ok"),
                FitStatus.OverDialogue => L("tm_over"),
                FitStatus.IntoDialogue => LF("tm_into_dialogue", Sec(l.Overrun)),
                FitStatus.IntoNext => LF("tm_into_next", Sec(l.Overrun)),
                FitStatus.Late => LF("tm_late", Sec(l.Overrun)),
                _ => ""
            };
            if (status.Length > 0) parts.Add(status);
            if (l.Estimated) parts.Add(L("tm_estimated"));
            return string.Join(" ", parts);
        }

        // ── announcements ────────────────────────────────────────────────────

        private void Say(string text) => Ui.Announce(StatusText, text);

        private void UpdateTitle()
        {
            string name = _project == null ? "" : (_project.FilePath != null ? Path.GetFileNameWithoutExtension(_project.FilePath) : _project.Title);
            Title = (_project == null ? "" : name + (_project.Dirty ? " *" : "") + " — ") + L("app_name");
        }

        private void ShowProject()
        {
            if (_project == null)
            {
                VideoInfo.Text = L("no_video");
                PositionText.Text = "";
            }
            else
            {
                VideoInfo.Text = LF("video_info", Path.GetFileName(_project.VideoPath), TimeText.Format(_project.Duration),
                                    Languages.Native(_project.Language), _project.Lines.Count);
                PositionText.Text = $"{TimeText.Format(0)} / {TimeText.Format(_project.Duration)}";
            }
            RefreshList();
        }

        private void Changed()
        {
            if (_project == null) return;
            _project.Dirty = true;
            VideoInfo.Text = LF("video_info", Path.GetFileName(_project.VideoPath), TimeText.Format(_project.Duration),
                                Languages.Native(_project.Language), _project.Lines.Count);
            UpdateTitle();
        }

        // ── undo ─────────────────────────────────────────────────────────────

        private List<(double, string)> Snapshot() => _project.Lines.Select(l => (l.Time, l.Text)).ToList();

        private void PushUndo()
        {
            if (_project == null) return;
            _undo.Push(Snapshot());
            _redo.Clear();
            if (_undo.Count > 200)
            {
                var keep = _undo.Reverse().Skip(_undo.Count - 200).ToList();
                _undo.Clear();
                foreach (var s in keep) _undo.Push(s);
            }
        }

        private void Restore(List<(double Time, string Text)> snap)
        {
            // lines that did not change keep their prepared voices
            var old = _project.Lines.ToList();
            var lines = new List<AdLine>();
            foreach (var (t, x) in snap)
            {
                var same = old.FirstOrDefault(o => Math.Abs(o.Time - t) < 0.001 && o.Text == x);
                if (same != null) { old.Remove(same); lines.Add(same); }
                else lines.Add(new AdLine { Time = t, Text = x, PlacedAt = t });
            }
            _project.Lines = lines;
            Changed();
            RefreshList();
        }

        private void Undo()
        {
            if (_project == null || _undo.Count == 0) { Say(L("st_nothing_undo")); return; }
            _redo.Push(Snapshot());
            Restore(_undo.Pop());
            Say(L("st_undone"));
        }

        private void Redo()
        {
            if (_project == null || _redo.Count == 0) { Say(L("st_nothing_redo")); return; }
            _undo.Push(Snapshot());
            Restore(_redo.Pop());
            Say(L("st_redone"));
        }

        // ── editing ──────────────────────────────────────────────────────────

        private void ApplyTime(bool announce)
        {
            if (_loadingEditor || Selected == null) return;
            var l = Selected;
            double? t = TimeText.Parse(TimeBox.Text);
            if (t == null)
            {
                if (announce) Say(L("err_time_format"));
                TimeBox.Text = TimeText.Format(l.Time);
                return;
            }
            double v = Math.Round(Math.Clamp(t.Value, 0, Math.Max(0, _project.Duration - 0.1)), 1);
            if (Math.Abs(v - l.Time) < 0.001) return;
            PushUndo();
            l.Time = v;
            l.Invalidate();
            Changed();
            RefreshList(l);
            if (announce) Say(LF("st_time_set", TimeText.Format(v)));
        }

        private void ApplyText(bool announce)
        {
            if (_loadingEditor || Selected == null) return;
            var l = Selected;
            string text = (TextEdit.Text ?? "").Trim();
            if (text == l.Text) return;
            if (text.Length == 0)
            {
                if (announce) Say(L("err_empty_text"));
                TextEdit.Text = l.Text;
                return;
            }
            PushUndo();
            l.Text = text;
            l.Invalidate();
            Changed();
            RefreshStatuses();
            if (announce) Say(LF("st_text_saved", ShortStatusOrFits(l)));
        }

        private string ShortStatusOrFits(AdLine l) => l.HasProblem ? ShortStatus(l) : L("st_fits");

        private void AddLine()
        {
            if (!RequireProject()) return;
            double at = Math.Round(_player?.Position ?? 0, 1);
            // nothing played yet: a little after the selected description
            if ((_player == null || (!_player.IsPlaying && at < 0.05)) && Selected != null) at = Math.Round(Selected.Time + 2, 1);
            at = Math.Clamp(at, 0, Math.Max(0, _project.Duration - 0.1));
            PushUndo();
            var l = new AdLine { Time = at, Text = L("new_line_text"), PlacedAt = at };
            _project.Lines.Add(l);
            Changed();
            RefreshList(l);
            TextEdit.Focus();
            TextEdit.SelectAll();
            Say(LF("st_added", TimeText.Format(at)));
        }

        private void DeleteLine()
        {
            var l = Selected;
            if (l == null) return;
            int index = LineList.SelectedIndex;
            PushUndo();
            _project.Lines.Remove(l);
            Changed();
            var next = _project.Lines.Count == 0 ? null : _project.Lines[Math.Min(index, _project.Lines.Count - 1)];
            RefreshList(next);
            FocusList();
            Say(LF("st_deleted", TimeText.Format(l.Time)));
        }

        private void Nudge(double by)
        {
            var l = Selected;
            if (l == null) return;
            PushUndo();
            l.Time = Math.Round(Math.Clamp(l.Time + by, 0, Math.Max(0, _project.Duration - 0.1)), 1);
            l.Invalidate();
            Changed();
            RefreshList(l);
            Say(TimeText.Format(l.Time) + (l.HasProblem ? ". " + ShortStatus(l) : ""));
        }

        private void SetTimeToPosition()
        {
            var l = Selected;
            if (l == null || _player == null) return;
            PushUndo();
            l.Time = Math.Round(_player.Position, 1);
            l.Invalidate();
            Changed();
            RefreshList(l);
            Say(LF("st_time_set", TimeText.Format(l.Time)));
        }

        private void EditScriptAsText()
        {
            if (!RequireProject()) return;
            var w = new ScriptTextWindow(this, ScriptText.Write(_project.Lines));
            if (w.ShowDialog() != true) return;
            var lines = ScriptText.Read(w.ScriptValue);
            PushUndo();
            Restore(lines.Select(l => (l.Time, l.Text)).ToList());
            Say(LF("st_script_applied", lines.Count));
            FocusList();
        }

        private void ImportScript()
        {
            if (!RequireProject()) return;
            var dlg = new OpenFileDialog { Filter = L("filter_script"), Title = L("m_import").Replace("_", "") };
            if (dlg.ShowDialog(this) != true) { FocusList(); return; }
            try
            {
                var lines = ScriptImport.Read(dlg.FileName);
                if (lines.Count == 0) { Say(L("err_import_empty")); return; }
                PushUndo();
                Restore(lines.Select(l => (l.Time, l.Text)).ToList());
                Say(LF("st_imported", lines.Count));
            }
            catch (Exception ex) { AppLog.Error("Importing a script", ex); Say(LF("err_failed", ex.Message)); }
            FocusList();
        }

        // ── files ────────────────────────────────────────────────────────────

        private bool RequireProject()
        {
            if (_project != null) return true;
            Say(L("err_no_project"));
            return false;
        }

        /// <summary>Asks to save changes. False when the user cancelled.</summary>
        private bool ConfirmDiscard()
        {
            if (_project == null || !_project.Dirty) return true;
            var r = MessageBox.Show(this, L("q_save_changes"), L("app_name"), MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (r == MessageBoxResult.Cancel) return false;
            if (r == MessageBoxResult.Yes) return SaveProject(false);
            return true;
        }

        private async Task OpenVideoDialogAsync()
        {
            if (!ConfirmDiscard()) return;
            var dlg = new OpenFileDialog { Filter = L("filter_video"), Title = L("m_open_video").Replace("_", "") };
            if (Directory.Exists(AppSettings.Current.LastFolder ?? "")) dlg.InitialDirectory = AppSettings.Current.LastFolder;
            bool ok = dlg.ShowDialog(this) == true;
            Activate();
            if (ok) await OpenVideoAsync(dlg.FileName, confirmed: true);
            else FocusList();
        }

        private async Task OpenVideoAsync(string path, bool confirmed = false)
        {
            if (!confirmed && !ConfirmDiscard()) return;
            if (!AppPaths.FFmpegAvailable) { Say(L(Busy ? "err_ffmpeg_downloading" : "err_no_ffmpeg")); return; }
            try
            {
                double length = await Ffmpeg.DurationAsync(path, CancellationToken.None);
                if (length < 0.5) { Ui.Sound(false); Say(LF("err_bad_video", Path.GetFileName(path))); return; }
                var s = AppSettings.Current;
                string lang = s.DescLanguage;
                _project = new AdProject
                {
                    VideoPath = path, Duration = length, Language = lang,
                    Voice = VoiceEngine.Fits(s.Voice, lang) ? s.Voice : VoiceEngine.DefaultVoice(lang)
                };
                // a project saved next to the video before? open that instead
                string beside = Path.ChangeExtension(path, AdProject.Extension);
                if (File.Exists(beside) &&
                    MessageBox.Show(this, LF("q_open_existing", Path.GetFileName(beside)), L("app_name"), MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    await OpenProjectAsync(beside);
                    return;
                }
                s.LastFolder = Path.GetDirectoryName(path);
                s.Save();
                _undo.Clear(); _redo.Clear();
                _player?.Open(path, length);
                ShowProject();
                Ui.Sound(true);
                Say(LF("st_video_opened", Path.GetFileName(path), TimeText.Spoken(length, Strings.Language)));
                FocusList();
            }
            catch (InvalidOperationException ex) when (ex.Message == "ffmpeg_missing") { Say(L("err_no_ffmpeg")); }
            catch (Exception ex) { AppLog.Error("Opening the video", ex); Ui.Sound(false); Say(LF("err_failed", ex.Message)); }
        }

        private async Task OpenProjectDialogAsync()
        {
            if (!ConfirmDiscard()) return;
            var dlg = new OpenFileDialog { Filter = L("filter_project"), Title = L("m_open_project").Replace("_", "") };
            if (Directory.Exists(AppSettings.Current.LastFolder ?? "")) dlg.InitialDirectory = AppSettings.Current.LastFolder;
            bool ok = dlg.ShowDialog(this) == true;
            Activate();
            if (ok) await OpenProjectAsync(dlg.FileName);
            else FocusList();
        }

        private async Task OpenProjectAsync(string path)
        {
            try
            {
                var p = AdProject.Load(path);
                if (!File.Exists(p.VideoPath ?? ""))
                {
                    MessageBox.Show(this, LF("err_video_missing", p.VideoPath), L("app_name"), MessageBoxButton.OK, MessageBoxImage.Warning);
                    var dlg = new OpenFileDialog { Filter = L("filter_video"), Title = L("m_open_video").Replace("_", "") };
                    if (dlg.ShowDialog(this) != true) return;
                    p.VideoPath = dlg.FileName;
                    p.Dirty = true;
                }
                if (p.Duration < 0.5) p.Duration = await Ffmpeg.DurationAsync(p.VideoPath, CancellationToken.None);
                if (!Languages.IsKnown(p.Language)) p.Language = AppSettings.Current.DescLanguage;
                if (!VoiceEngine.Fits(p.Voice, p.Language)) p.Voice = VoiceEngine.DefaultVoice(p.Language);
                _project = p;
                _undo.Clear(); _redo.Clear();
                _player?.Open(p.VideoPath, p.Duration);
                AppSettings.Current.LastFolder = Path.GetDirectoryName(path);
                AppSettings.Current.Save();
                ShowProject();
                Ui.Sound(true);
                Say(LF("st_project_opened", Path.GetFileNameWithoutExtension(path), p.Lines.Count));
                FocusList();
            }
            catch (Exception ex) { AppLog.Error("Opening the project", ex); Ui.Sound(false); Say(LF("err_failed", ex.Message)); }
        }

        private bool SaveProject(bool saveAs)
        {
            if (!RequireProject()) return false;
            ApplyTime(false);
            ApplyText(false);
            string path = _project.FilePath;
            if (saveAs || path == null)
            {
                var dlg = new SaveFileDialog
                {
                    Filter = L("filter_project"), DefaultExt = AdProject.Extension, AddExtension = true,
                    FileName = Path.GetFileNameWithoutExtension(_project.VideoPath) + AdProject.Extension,
                    InitialDirectory = Path.GetDirectoryName(_project.VideoPath)
                };
                if (dlg.ShowDialog(this) != true) { FocusList(); return false; }
                path = dlg.FileName;
            }
            try
            {
                _project.Save(path);
                UpdateTitle();
                Say(LF("st_saved", Path.GetFileName(path)));
                return true;
            }
            catch (Exception ex)
            {
                AppLog.Error("Saving the project", ex);
                Ui.Sound(false);
                Say(LF("err_failed", ex.Message));
                return false;
            }
        }

        private async void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            {
                string f = files[0];
                if (f.EndsWith(AdProject.Extension, StringComparison.OrdinalIgnoreCase)) { if (ConfirmDiscard()) await OpenProjectAsync(f); }
                else await OpenVideoAsync(f);
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // closing while work runs would throw that work away: ask first
            if (Busy && MessageBox.Show(this, LF("q_close_busy", _wp?.Title ?? ""), L("app_name"), MessageBoxButton.YesNo,
                                        MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes)
            { e.Cancel = true; return; }
            if (!ConfirmDiscard()) { e.Cancel = true; return; }
            _work?.Cancel();
            try { _player?.Dispose(); } catch { }
        }

        // ── long work (AI, voices, export, FFmpeg) ───────────────────────────

        private WorkProgress _wp;
        private int _lastQuarter;
        private System.Windows.Threading.DispatcherTimer _workClock;

        private bool Busy => _work != null;

        /// <summary>Starts long work: the Stop button, the progress panel, and one announcement per step.</summary>
        private CancellationToken BeginWork(WorkProgress wp)
        {
            _work = new CancellationTokenSource();
            _btnStop.Visibility = Visibility.Visible;
            _btnWrite.IsEnabled = _btnVoices.IsEnabled = _btnExport.IsEnabled = false;
            AttachProgress(wp);
            WorkPanel.Visibility = Visibility.Visible;
            _workClock ??= new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _workClock.Tick -= WorkClock_Tick;
            _workClock.Tick += WorkClock_Tick;
            _workClock.Start();
            return _work.Token;
        }

        /// <summary>The panel follows another progress (voices after the AI work keep the same panel).</summary>
        private void AttachProgress(WorkProgress wp)
        {
            if (_wp != null) _wp.Changed -= Progress_Changed;
            _wp = wp;
            _lastQuarter = 0;
            if (wp == null) return;
            wp.Changed += Progress_Changed;
            WorkTitle.Text = wp.Title;
            AutomationProperties.SetName(WorkBar, wp.Title);
            ShowProgress(false);
        }

        private void Progress_Changed(bool stepChanged) =>
            Dispatcher.BeginInvoke(new Action(() => ShowProgress(stepChanged)));

        private void WorkClock_Tick(object sender, EventArgs e)
        {
            if (_wp != null) WorkText.Text = _wp.Describe(true);
        }

        /// <summary>
        /// Shows the progress; speaks only when a new step begins and at every quarter of a step
        /// (F9 says everything whenever it is wanted).
        /// </summary>
        private void ShowProgress(bool stepChanged)
        {
            var wp = _wp;
            if (wp == null) return;
            WorkText.Text = wp.Describe(true);
            WorkBar.IsIndeterminate = wp.Percent < 0;
            if (wp.Percent >= 0) WorkBar.Value = wp.Percent;
            if (stepChanged)
            {
                // the bar's name is the step; its value is the percent (renamed only when the step changes, so a focused bar stays quiet)
                AutomationProperties.SetName(WorkBar, Strings.LF("wp_step", wp.Step, wp.Steps, wp.StepText));
                _lastQuarter = 0;
                Say(Strings.LF("wp_step", wp.Step, wp.Steps, wp.StepText));
            }
            else if (wp.Percent >= 0)
            {
                int quarter = (int)wp.Percent / 25;
                if (quarter > _lastQuarter && wp.Percent < 100)
                {
                    _lastQuarter = quarter;
                    Say(Strings.LF("wp_percent_only", (int)wp.Percent) + (string.IsNullOrEmpty(wp.Detail) ? "" : " " + wp.Detail));
                }
            }
        }

        private void EndWork()
        {
            bool stopHadFocus = _btnStop.IsKeyboardFocused || WorkBar.IsKeyboardFocused;
            _work?.Dispose();
            _work = null;
            _workClock?.Stop();
            AttachProgress(null);
            WorkPanel.Visibility = Visibility.Collapsed;
            _btnStop.Visibility = Visibility.Collapsed;
            _btnWrite.IsEnabled = _btnVoices.IsEnabled = _btnExport.IsEnabled = true;
            if (stopHadFocus) FocusList();
        }

        private static string Took(WorkProgress wp) => Strings.LF("wp_took", WorkProgress.Clock(wp.Elapsed));

        private async Task WriteWithAiAsync()
        {
            if (!RequireProject() || Busy) return;
            StopAll(quiet: true);
            var dlg = new GenerateWindow(this, _project.Lines.Count > 0, _project.Language);
            if (dlg.ShowDialog() != true) { FocusList(); return; }
            var s = AppSettings.Current;
            if (dlg.Language != _project.Language)
            {
                _project.Language = dlg.Language;
                if (!VoiceEngine.Fits(_project.Voice, _project.Language)) _project.Voice = VoiceEngine.DefaultVoice(_project.Language);
            }
            var wp = new WorkProgress(L(s.AiSource == "local" ? "wp_title_local" : "wp_title_gemini")) { ExtraSteps = dlg.PrepareVoices ? 1 : 0 };
            var ct = BeginWork(wp);
            Say(L("st_writing_start"));
            bool chain = false;
            try
            {
                ScriptResult r = s.AiSource == "local"
                    ? await ScriptWriter.LocalAsync(_project.VideoPath, _project.Duration, _project.Language, wp, ct)
                    : await ScriptWriter.WithGeminiAsync(_project.VideoPath, _project.Duration, _project.Language,
                        Enum.TryParse(s.Density, out Density d) ? d : Density.Normal, s.SnapToCuts, wp, ct, _project.SourceUrl);

                if (r.Lines.Count == 0) { Ui.Sound(false); Say(L("err_ai_nothing")); return; }
                PushUndo();
                if (dlg.KeepExisting && _project.Lines.Count > 0)
                {
                    // the describer's own lines stay; AI lines are added only where there is room
                    foreach (var l in r.Lines)
                        if (!_project.Lines.Any(x => Math.Abs(x.Time - l.Time) < 3)) _project.Lines.Add(l);
                }
                else _project.Lines = r.Lines;
                if (r.Speech.Count > 0 || s.AiSource != "local") _project.Speech = r.Speech;
                if (r.Cuts.Count > 0) _project.Cuts = r.Cuts;
                Changed();
                RefreshList(_project.Lines.FirstOrDefault());
                if (dlg.PrepareVoices)
                {
                    chain = true;
                    Say(LF("st_written", r.Lines.Count, r.Speech.Count));
                }
                else
                {
                    Ui.Sound(true);
                    Say(LF("st_written", r.Lines.Count, r.Speech.Count) + " " + Took(wp));
                }
            }
            catch (OperationCanceledException) { Say(L("st_stopped")); }
            catch (InvalidOperationException ex) when (ex.Message is "no_gemini_key" or "no_model" or "no_picture" or "prepare_failed" or "ffmpeg_missing")
            {
                Ui.Sound(false);
                Say(L("err_" + ex.Message));
            }
            catch (Exception ex) { AppLog.Error("Writing descriptions with AI", ex); Ui.Sound(false); Say(LF("err_failed", ex.Message)); }
            finally { if (!chain) EndWork(); }

            if (chain)
            {
                // the same panel goes on: the last step is the voices
                await VoicesAsync(wp, wp.Steps, true);
                EndWork();
            }
            FocusList();
        }

        /// <summary>Prepares every voice and checks the real timing (F8).</summary>
        private async Task<bool> PrepareVoicesAsync(bool report)
        {
            if (!RequireProject() || Busy) return false;
            if (_project.Lines.Count == 0) { Say(L("err_no_lines")); return false; }
            var wp = new WorkProgress(L("wp_title_voices"));
            BeginWork(wp);
            try { return await VoicesAsync(wp, 1, report); }
            finally { EndWork(); }
        }

        /// <summary>The voices as step <paramref name="step"/> of <paramref name="wp"/> (the work must already be running).</summary>
        private async Task<bool> VoicesAsync(WorkProgress wp, int step, bool report)
        {
            var ct = _work.Token;
            wp.SetStep(step, Math.Max(step, wp.Steps), L("wp_voices"));
            try
            {
                int count = _project.Lines.Count;
                await Timing.FitAsync(_project, _project.Voice, prog =>
                {
                    var m = System.Text.RegularExpressions.Regex.Match(prog ?? "", @"^(\d+)/(\d+)$");
                    if (!m.Success) return;
                    int done = int.Parse(m.Groups[1].Value), all = int.Parse(m.Groups[2].Value);
                    wp.SetPercent(done - 1, all);
                    wp.SetDetail(LF("wp_voice_line", done, all));
                }, ct);
                wp.SetPercent(100);
                RefreshStatuses();
                if (report)
                {
                    int problems = _project.Lines.Count(l => l.HasProblem), faster = _project.Lines.Count(l => l.Status == FitStatus.SpedUp);
                    Ui.Sound(problems == 0);
                    Say((problems == 0 ? LF("st_voices_ok", count, faster) : LF("st_voices_problems", count, faster, problems)) + " " + Took(wp));
                }
                return true;
            }
            catch (OperationCanceledException) { Say(L("st_stopped")); return false; }
            catch (Exception ex) { AppLog.Error("Preparing the voices", ex); Ui.Sound(false); Say(LF("err_failed", ex.Message)); return false; }
        }

        /// <summary>A voice for one line, while watching (a little ahead of its moment).</summary>
        private async Task VoiceAheadAsync(AdLine line)
        {
            if (_project == null || !_voicing.Add(line)) return;
            try
            {
                await Timing.FitAsync(_project, _project.Voice, null, CancellationToken.None, new[] { line });
                foreach (var i in _items.Where(i => i.Line == line)) i.Refresh();
                if (Selected == line) LoadEditor();
            }
            catch (Exception ex) { AppLog.Error("Preparing a voice while playing", ex); }
            finally { _voicing.Remove(line); }
        }

        private async Task ExportAsync()
        {
            if (!RequireProject() || Busy) return;
            if (_project.Lines.Count == 0) { Say(L("err_no_lines")); return; }
            StopAll(quiet: true);
            var dlg = new ExportWindow(this, _project);
            if (dlg.ShowDialog() != true) { FocusList(); return; }
            var options = dlg.Options;

            // the voices first (from the cache when they are ready), then the files — one panel
            var voices = new WorkProgress(L("wp_title_export"));
            BeginWork(voices);
            bool ok;
            try { ok = await VoicesAsync(voices, 1, false); }
            finally { if (_work != null && _work.IsCancellationRequested) EndWork(); }
            if (!ok) { if (Busy) EndWork(); FocusList(); return; }

            int problems = _project.Lines.Count(l => l.HasProblem);
            var wp = new WorkProgress(L("wp_title_export"));
            AttachProgress(wp);
            var ct = _work.Token;
            try
            {
                var files = await Exporter.ExportAsync(_project, options, wp, ct);
                Ui.Sound(true);
                Say(LF("st_exported", files.Count, options.Folder) + (problems > 0 ? " " + LF("st_exported_problems", problems) : "") +
                    " " + Strings.LF("wp_took", WorkProgress.Clock(voices.Elapsed)));
                EndWork();
                if (MessageBox.Show(this, LF("q_open_folder", options.Folder), L("app_name"), MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                {
                    string first = files.FirstOrDefault();
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe",
                        first != null ? $"/select,\"{first}\"" : $"\"{options.Folder}\"") { UseShellExecute = true });
                }
            }
            catch (OperationCanceledException) { Say(L("st_stopped")); }
            catch (InvalidOperationException ex) when (ex.Message is "nothing_spoken" or "mix_failed" or "voice_track_failed" or "ffmpeg_missing")
            {
                Ui.Sound(false);
                Say(L("err_" + ex.Message));
            }
            catch (Exception ex) { AppLog.Error("Exporting", ex); Ui.Sound(false); Say(LF("err_failed", ex.Message)); }
            finally { if (Busy) EndWork(); }
            FocusList();
        }

        /// <summary>
        /// Opens a video from a web address (YouTube and many other sites): yt-dlp downloads it
        /// into the download folder, then it opens like any video. The address is kept in the
        /// project, so Gemini can watch a YouTube video directly from it.
        /// </summary>
        private async Task OpenLinkAsync()
        {
            if (Busy) return;
            if (!ConfirmDiscard()) return;
            string clip = "";
            try { if (Clipboard.ContainsText() && YtDlp.LooksLikeUrl(Clipboard.GetText())) clip = Clipboard.GetText().Trim(); } catch { }
            string url = InputDialog.Ask(this, L("m_open_link").Replace("_", ""), L("lbl_link"), clip);
            FocusList();
            if (url == null) return;
            url = url.Trim();
            if (!YtDlp.LooksLikeUrl(url)) { Say(L("err_link_bad")); return; }
            if (!AppPaths.FFmpegAvailable) { Say(L("err_no_ffmpeg")); return; }

            var wp = new WorkProgress(L("wp_title_link"));
            var ct = BeginWork(wp);
            string file = null;
            try
            {
                if (!YtDlp.Available)
                {
                    wp.SetStep(1, 2, L("wp_ytdlp_get"));
                    await YtDlp.InstallAsync(wp, ct);
                    wp.SetStep(2, 2, L("wp_link_download"));
                }
                else wp.SetStep(1, 1, L("wp_link_download"));
                file = await YtDlp.DownloadAsync(url, AppSettings.Current.DownloadFolder, wp, ct);
                Ui.Sound(true);
                Say(LF("st_link_done", Path.GetFileName(file)) + " " + Took(wp));
            }
            catch (OperationCanceledException) { Say(L("st_stopped")); }
            catch (InvalidOperationException ex) when (ex.Message is "link_drm" or "link_unsupported") { Ui.Sound(false); Say(L("err_" + ex.Message)); }
            catch (Exception ex) { AppLog.Error("Opening a link", ex); Ui.Sound(false); Say(LF("err_link_failed", ex.Message)); }
            finally { EndWork(); }

            if (file == null) { FocusList(); return; }
            await OpenVideoAsync(file, confirmed: true);
            if (_project != null && string.Equals(_project.VideoPath, file, StringComparison.OrdinalIgnoreCase))
            {
                _project.SourceUrl = url;
                _project.Dirty = true;
                UpdateTitle();
            }
        }

        private async Task UpdateYtDlpAsync()
        {
            if (Busy) return;
            var wp = new WorkProgress(L("wp_title_ytdlp"));
            var ct = BeginWork(wp);
            try
            {
                wp.SetStep(1, 1, L("wp_ytdlp_get"));
                await YtDlp.InstallAsync(wp, ct);
                Ui.Sound(true);
                Say(L("st_ytdlp_done"));
            }
            catch (OperationCanceledException) { Say(L("st_stopped")); }
            catch (Exception ex) { Ui.Sound(false); Say(LF("err_link_failed", ex.Message)); }
            finally { EndWork(); }
            FocusList();
        }

        /// <summary>Downloads FFmpeg (automatically on the first start when it is missing, or from the Tools menu).</summary>
        private async Task DownloadFfmpegAsync(bool automatic)
        {
            if (Busy) return;
            if (!automatic && AppPaths.FFmpegAvailable &&
                MessageBox.Show(this, LF("q_ffmpeg_again", AppPaths.FFmpeg), L("app_name"), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            { FocusList(); return; }
            var wp = new WorkProgress(L("wp_title_ffmpeg"));
            var ct = BeginWork(wp);
            Say(L(automatic ? "st_ffmpeg_auto" : "st_ffmpeg_start"));
            try
            {
                await FfmpegInstaller.InstallAsync(wp, ct);
                Ui.Sound(true);
                Say(LF("st_ffmpeg_done", FfmpegInstaller.Folder) + " " + Took(wp));
            }
            catch (OperationCanceledException) { Say(L("st_ffmpeg_stopped")); }
            catch (Exception ex) { Ui.Sound(false); Say(LF("err_ffmpeg_download", ex.Message)); }
            finally { EndWork(); }
        }

        // ── playback ─────────────────────────────────────────────────────────

        private bool RequirePlayer()
        {
            if (!RequireProject()) return false;
            if (_player == null || !_player.Loaded) { Say(L("err_player_missing")); return false; }
            return true;
        }

        /// <summary>Plays the video from a little before the description, with the description at its place, then pauses.</summary>
        private async Task PreviewAsync(AdLine l)
        {
            if (!RequirePlayer()) return;
            ApplyText(false);
            _player.Stop();
            if (l.AudioPath == null || !File.Exists(l.AudioPath))
            {
                StatusText.Text = L("st_preparing_one");
                try { await Timing.FitAsync(_project, _project.Voice, null, CancellationToken.None, new[] { l }); }
                catch (Exception ex) { AppLog.Error("Preparing the voice for a preview", ex); Say(LF("err_failed", ex.Message)); return; }
                foreach (var i in _items.Where(i => i.Line == l)) i.Refresh();
                LoadEditor();
            }
            double at = l.PlacedAt > 0 ? l.PlacedAt : l.Time;
            _player.Narrate = true;
            _player.StopAt = at + Math.Max(0.5, l.Duration) + 1.0;
            _player.Play(Math.Max(0, at - AppSettings.Current.PreviewLeadIn));
            UpdatePlayButton();
            StatusText.Text = LF("st_previewing", TimeText.Format(l.Time));
        }

        /// <summary>Hear only the voice of the selected description (no video).</summary>
        private async Task ListenAsync()
        {
            var l = Selected;
            if (l == null || _project == null) return;
            ApplyText(false);
            try
            {
                if (l.AudioPath == null || !File.Exists(l.AudioPath))
                {
                    StatusText.Text = L("st_preparing_one");
                    await Timing.FitAsync(_project, _project.Voice, null, CancellationToken.None, new[] { l });
                    foreach (var i in _items.Where(i => i.Line == l)) i.Refresh();
                    LoadEditor();
                }
                if (l.AudioPath != null) _player?.Listen(l.AudioPath);
            }
            catch (Exception ex) { AppLog.Error("Listening to a description", ex); Say(LF("err_failed", ex.Message)); }
        }

        private void PlayPause()
        {
            if (!RequirePlayer()) return;
            if (_player.IsPlaying)
            {
                _player.Pause();
                Say(LF("st_paused", TimeText.Format(_player.Position)));
            }
            else
            {
                _player.StopAt = null;
                _player.Narrate = true;
                _player.Play();
            }
            UpdatePlayButton();
        }

        /// <summary>Escape: stops the preview or the playback. False when nothing was playing.</summary>
        private bool StopPlayback()
        {
            bool was = _player != null && (_player.IsPlaying || _player.IsSpeaking);
            if (!was) return false;
            StopAll(false);
            return true;
        }

        /// <summary>Stops long work (AI, voices, export, downloads) — only after a yes, so nothing is lost by accident.</summary>
        private void CancelWork()
        {
            if (!Busy) { Say(L("st_no_work")); return; }
            string what = _wp?.Title ?? "";
            if (MessageBox.Show(this, LF("q_cancel_work", what), L("app_name"), MessageBoxButton.YesNo, MessageBoxImage.Question,
                                MessageBoxResult.No) == MessageBoxResult.Yes)
                _work?.Cancel();
            else if (_wp != null) Say(_wp.Describe(true));
        }

        private void StopAll(bool quiet)
        {
            bool was = _player != null && (_player.IsPlaying || _player.IsSpeaking);
            _player?.Stop();
            NowSpeaking.Text = "";
            UpdatePlayButton();
            if (was && !quiet) Say(LF("st_stopped_at", TimeText.Format(_player.Position)));
        }

        private void SeekBy(double by)
        {
            if (!RequirePlayer()) return;
            double to = Math.Clamp(_player.Position + by, 0, _project.Duration);
            if (_player.IsPlaying) _player.Play(to); else _player.Seek(to);
            StatusText.Text = TimeText.Format(to);
        }

        private void GoToTime()
        {
            if (!RequirePlayer()) return;
            string answer = InputDialog.Ask(this, L("m_goto").Replace("_", ""), L("lbl_goto"), TimeText.Format(_player.Position));
            FocusList();
            double? t = TimeText.Parse(answer);
            if (t == null) { if (answer != null) Say(L("err_time_format")); return; }
            _player.Seek(Math.Clamp(t.Value, 0, _project.Duration));
            var near = _project.Lines.LastOrDefault(l => l.Time <= t.Value + 0.05) ?? _project.Lines.FirstOrDefault();
            if (near != null) RefreshList(near);
            FocusList();
            Say(LF("st_went_to", TimeText.Format(t.Value)));
        }

        private void GoToSelected()
        {
            var l = Selected;
            if (l == null || !RequirePlayer()) return;
            _player.Seek(l.Time);
            Say(LF("st_went_to", TimeText.Format(l.Time)));
        }

        private void SayPosition()
        {
            if (Busy && _wp != null) { Say(_wp.Describe(true)); return; }   // during long work F9 says how far it is
            if (!RequirePlayer()) return;
            double pos = _player.Position;
            var next = _project.Lines.FirstOrDefault(l => l.Time > pos + 0.05);
            string text = LF("st_position", TimeText.Spoken(pos, Strings.Language), TimeText.Spoken(_project.Duration, Strings.Language),
                             _player.IsPlaying ? L("st_playing") : L("st_not_playing"));
            if (next != null) text += " " + LF("st_next_desc", TimeText.Spoken(next.Time - pos, Strings.Language));
            if (_project.SpeechAt(pos) != null) text += " " + L("st_in_dialogue");
            Say(text);
        }

        // ── problems and report ──────────────────────────────────────────────

        private void GoProblem(int dir)
        {
            if (!RequireProject()) return;
            var problems = Timing.Problems(_project);
            if (problems.Count == 0) { Say(L("st_no_problems")); return; }
            double from = Selected?.Time ?? (dir > 0 ? -1 : double.MaxValue);
            var target = dir > 0 ? problems.FirstOrDefault(l => l.Time > from + 0.001) ?? problems[0]
                                 : problems.LastOrDefault(l => l.Time < from - 0.001) ?? problems[^1];
            RefreshList(target);
            FocusList();
        }

        private void ShowReport()
        {
            if (!RequireProject()) return;
            var w = new ReportWindow(this, _project);
            w.ShowDialog();
            if (w.Chosen != null) RefreshList(w.Chosen);
            FocusList();
        }

        // ── settings, help ───────────────────────────────────────────────────

        private void OpenSettings()
        {
            string oldUi = AppSettings.Current.UiLanguage;
            var w = new SettingsWindow(this, _project);
            if (w.ShowDialog() == true)
            {
                App.ApplyTheme(AppSettings.Current.Theme);
                ApplyVideoVisibility();
                if (_project != null && w.ProjectChanged)
                {
                    foreach (var l in _project.Lines) l.Invalidate();
                    Changed();
                }
                if (oldUi != AppSettings.Current.UiLanguage)
                {
                    Strings.Language = AppSettings.Current.UiLanguage;
                    BuildMenu();
                    BuildButtons();
                    ApplyTexts();
                    ShowProject();
                    if (Busy) _btnStop.Visibility = Visibility.Visible;
                }
                else RefreshStatuses();
                Say(L("st_settings_saved"));
            }
            FocusList();
        }

        private void OpenLog()
        {
            try
            {
                if (!File.Exists(AppLog.TodayFile)) File.WriteAllText(AppLog.TodayFile, "");
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("notepad.exe", $"\"{AppLog.TodayFile}\"") { UseShellExecute = true });
            }
            catch (Exception ex) { Say(LF("err_failed", ex.Message)); }
        }

        private void ShowKeys()
        {
            new TextWindow(this, L("m_keys").Replace("_", ""), L("lbl_keys"), L("keys_text")).ShowDialog();
            FocusList();
        }

        private void ShowAbout()
        {
            new AboutWindow(this).ShowDialog();
            FocusList();
        }
    }
}
