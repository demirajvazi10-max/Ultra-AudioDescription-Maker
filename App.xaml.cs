using System;
using System.IO;
using System.Linq;
using System.Windows;
using UltraAudioDescriptionMaker.Core;
using UltraAudioDescriptionMaker.UI;

namespace UltraAudioDescriptionMaker
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            AppSettings.Load();
            Strings.Language = AppSettings.Current.UiLanguage;
            ApplyTheme(AppSettings.Current.Theme);

            DispatcherUnhandledException += (s, ex) =>
            {
                AppLog.Error("Unhandled error", ex.Exception);
                MessageBox.Show(Strings.LF("err_unexpected", ex.Exception.Message), Strings.L("app_name"),
                                MessageBoxButton.OK, MessageBoxImage.Error);
                ex.Handled = true;
            };
            AppDomain.CurrentDomain.UnhandledException += (s, ex) => AppLog.Error("Unhandled error (background)", ex.ExceptionObject as Exception);
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, ex) => { AppLog.Error("Unobserved task error", ex.Exception); ex.SetObserved(); };

            // a project or a video given on the command line (double-click on a .uad file)
            string arg = e.Args.FirstOrDefault(a => File.Exists(a));
            var main = new MainWindow(arg);
            MainWindow = main;
            main.Show();
        }

        /// <summary>Dark, Light, Contrast, or System (Windows high contrast → Contrast, otherwise Dark).</summary>
        public static void ApplyTheme(string theme)
        {
            if (theme == "System" || string.IsNullOrEmpty(theme))
                theme = SystemParameters.HighContrast ? "Contrast" : "Dark";
            if (theme is not ("Dark" or "Light" or "Contrast")) theme = "Dark";
            var dict = new ResourceDictionary { Source = new Uri($"pack://application:,,,/Themes/{theme}.xaml", UriKind.Absolute) };
            var merged = Current.Resources.MergedDictionaries;
            if (merged.Count > 0) merged[0] = dict; else merged.Add(dict);
        }
    }
}
