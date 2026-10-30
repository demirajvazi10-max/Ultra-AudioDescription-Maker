using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using UltraAudioDescriptionMaker.Core;

namespace UltraAudioDescriptionMaker.UI
{
    /// <summary>Small builders for the windows made in code: every control has a label the screen reader reads.</summary>
    public static class Ui
    {
        public static Button Btn(string text, RoutedEventHandler click = null)
        {
            var b = new Button { Content = text, HorizontalAlignment = HorizontalAlignment.Left };
            if (click != null) b.Click += click;
            return b;
        }

        /// <summary>A label above a control. The label's access key (the letter after "_") jumps to the control.</summary>
        public static StackPanel Labelled(string label, Control control, string help = null)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 4, 0, 4) };
            var lbl = new Label { Content = label, Target = control };
            AutomationProperties.SetName(control, label.Replace("_", ""));
            if (help != null) AutomationProperties.SetHelpText(control, help);
            sp.Children.Add(lbl);
            sp.Children.Add(control);
            if (help != null)
            {
                var hint = new TextBlock { Text = help, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) };
                hint.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondary");
                sp.Children.Add(hint);
            }
            return sp;
        }

        public static CheckBox Check(string text, bool value) => new CheckBox { Content = text, IsChecked = value };

        public static ComboBox Combo(IEnumerable<(string Display, object Tag)> items, object selected)
        {
            var c = new ComboBox { MinWidth = 260, HorizontalAlignment = HorizontalAlignment.Left };
            int i = 0, pick = 0;
            foreach (var (d, t) in items)
            {
                c.Items.Add(new ComboBoxItem { Content = d, Tag = t });
                if (Equals(t, selected)) pick = i;
                i++;
            }
            c.SelectedIndex = c.Items.Count > 0 ? pick : -1;
            return c;
        }

        public static object Tag(ComboBox c) => (c.SelectedItem as ComboBoxItem)?.Tag;

        /// <summary>
        /// Shows the text in the status line and has the screen reader say it (UI Automation
        /// notification — JAWS 2022+, NVDA and Narrator read it), without moving the focus.
        /// </summary>
        public static void Announce(TextBlock status, string text, bool important = true)
        {
            if (status == null || string.IsNullOrEmpty(text)) return;
            status.Text = text;
            try
            {
                var peer = UIElementAutomationPeer.FromElement(status) ?? UIElementAutomationPeer.CreatePeerForElement(status);
                if (peer == null) return;
                peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
                peer.RaiseNotificationEvent(AutomationNotificationKind.ActionCompleted,
                    important ? AutomationNotificationProcessing.ImportantMostRecent : AutomationNotificationProcessing.MostRecent,
                    text, "UltraAudioDescriptionMaker");
            }
            catch (Exception ex) { AppLog.Error("Announcement", ex); }
        }

        /// <summary>Opens a web page in the default browser.</summary>
        public static void OpenLink(string url)
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (Exception ex) { AppLog.Error("Opening " + url, ex); }
        }

        /// <summary>A button that opens a web page; its help text is the address, so the screen reader can say where it goes.</summary>
        public static Button LinkButton(string text, string url)
        {
            var b = Btn(text, (s, e) => OpenLink(url));
            AutomationProperties.SetHelpText(b, url);
            b.ToolTip = url;
            return b;
        }

        /// <summary>Step-by-step instructions in a read-only text field, readable line by line with the arrow keys.</summary>
        public static StackPanel Instructions(string label, string text)
        {
            var box = new TextBox
            {
                Text = text, IsReadOnly = true, IsReadOnlyCaretVisible = true, TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true, MaxHeight = 220, VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            return Labelled(label, box);
        }

        public static void Sound(bool ok)
        {
            if (!AppSettings.Current.Sounds) return;
            try { (ok ? System.Media.SystemSounds.Asterisk : System.Media.SystemSounds.Hand).Play(); } catch { }
        }
    }

    /// <summary>
    /// A dialog built in code: themed, Escape closes it, a status line that the screen reader
    /// reads, and OK / Cancel buttons at the bottom when asked for.
    /// </summary>
    public class DialogWindow : Window
    {
        protected readonly StackPanel Body;
        protected readonly TextBlock Status;
        protected readonly StackPanel Buttons;

        public DialogWindow(Window owner, string title, double width = 620)
        {
            Owner = owner;
            Title = title;
            Width = width;
            SizeToContent = SizeToContent.Height;
            MaxHeight = SystemParameters.WorkArea.Height - 40;
            WindowStartupLocation = owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.CanResizeWithGrip;
            SetResourceReference(BackgroundProperty, "WindowBg");
            SetResourceReference(ForegroundProperty, "TextPrimary");

            var outer = new DockPanel { Margin = new Thickness(16) };
            Buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            DockPanel.SetDock(Buttons, Dock.Bottom);
            Status = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
            AutomationProperties.SetLiveSetting(Status, AutomationLiveSetting.Polite);
            DockPanel.SetDock(Status, Dock.Bottom);
            outer.Children.Add(Buttons);
            outer.Children.Add(Status);
            Body = new StackPanel();
            outer.Children.Add(new ScrollViewer { Content = Body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false });
            Content = outer;
        }

        /// <summary>Adds OK and Cancel (Enter on OK, Escape on Cancel). <paramref name="ok"/> returns false to keep the window open.</summary>
        protected void AddOkCancel(Func<bool> ok, string okText = null)
        {
            var okBtn = Ui.Btn(okText ?? Strings.L("btn_ok"), (s, e) => { if (ok == null || ok()) DialogResult = true; });
            okBtn.IsDefault = true;
            var cancel = Ui.Btn(Strings.L("btn_cancel"));
            cancel.IsCancel = true;
            Buttons.Children.Add(okBtn);
            Buttons.Children.Add(cancel);
        }

        /// <summary>Only a Close button (Escape closes too).</summary>
        protected void AddClose()
        {
            var close = Ui.Btn(Strings.L("btn_close"), (s, e) => Close());
            close.IsCancel = true;
            Buttons.Children.Add(close);
        }

        protected void Say(string text) => Ui.Announce(Status, text);

        protected void FocusFirst(UIElement element)
        {
            Loaded += (s, e) => { element?.Focus(); Keyboard.Focus(element as IInputElement); };
        }
    }

    /// <summary>A one-line question ("Go to time"): the text, or null when cancelled.</summary>
    public class InputDialog : DialogWindow
    {
        private readonly TextBox _box;

        public InputDialog(Window owner, string title, string label, string value) : base(owner, title, 420)
        {
            _box = new TextBox { Text = value ?? "" };
            Body.Children.Add(Ui.Labelled(label, _box));
            AddOkCancel(null);
            FocusFirst(_box);
            Loaded += (s, e) => _box.SelectAll();
        }

        public static string Ask(Window owner, string title, string label, string value = "")
        {
            var d = new InputDialog(owner, title, label, value);
            return d.ShowDialog() == true ? d._box.Text : null;
        }
    }

    /// <summary>A long read-only text (help, report) that the screen reader can read line by line.</summary>
    public class TextWindow : DialogWindow
    {
        public TextWindow(Window owner, string title, string label, string text) : base(owner, title, 760)
        {
            var box = new TextBox
            {
                Text = text, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true,
                Height = 440, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, IsReadOnlyCaretVisible = true
            };
            Body.Children.Add(Ui.Labelled(label, box));
            AddClose();
            FocusFirst(box);
        }
    }
}
