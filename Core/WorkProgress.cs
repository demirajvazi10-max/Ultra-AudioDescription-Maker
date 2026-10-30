using System;

namespace UltraAudioDescriptionMaker.Core
{
    /// <summary>
    /// Where long work is: step "2 of 5", what that step does, how far it is (percent, or
    /// unknown while waiting for an answer), a detail ("part 2 of 3, 14 descriptions so far")
    /// and how long it has been running. The work fills it in from any thread; the window shows
    /// it in the progress bar and says it when the step changes, at every quarter, and whenever
    /// F9 is pressed — never more often.
    /// </summary>
    public sealed class WorkProgress
    {
        private readonly object _lock = new();

        /// <summary>Raised on the thread that changed something; <c>stepChanged</c> = a new step began.</summary>
        public event Action<bool> Changed;

        public string Title { get; }
        public DateTime Started { get; } = DateTime.Now;
        public int Step { get; private set; }
        public int Steps { get; private set; } = 1;
        public string StepText { get; private set; } = "";
        /// <summary>0–100, or −1 while it cannot be known (waiting for an answer).</summary>
        public double Percent { get; private set; } = -1;
        public string Detail { get; private set; }
        /// <summary>Steps that will follow the AI work (preparing the voices).</summary>
        public int ExtraSteps { get; set; }

        public WorkProgress(string title) { Title = title; }

        public TimeSpan Elapsed => DateTime.Now - Started;

        public void SetStep(int step, int steps, string text)
        {
            lock (_lock) { Step = step; Steps = Math.Max(step, steps); StepText = text ?? ""; Percent = -1; Detail = null; }
            Changed?.Invoke(true);
        }

        public void SetPercent(double percent)
        {
            percent = Math.Clamp(percent, 0, 100);
            lock (_lock)
            {
                if (Math.Abs(percent - Percent) < 0.5) return;   // nothing new to show
                Percent = percent;
            }
            Changed?.Invoke(false);
        }

        public void SetPercent(int done, int total) => SetPercent(total <= 0 ? 0 : done * 100.0 / total);

        public void SetDetail(string detail)
        {
            lock (_lock) Detail = detail;
            Changed?.Invoke(false);
        }

        /// <summary>The whole state in one sentence, for the screen reader (F9) and the progress text.</summary>
        public string Describe(bool withElapsed)
        {
            lock (_lock)
            {
                string s = Strings.LF("wp_step", Step, Steps, StepText);
                if (Percent >= 0) s += " " + Strings.LF("wp_percent", (int)Percent);
                if (!string.IsNullOrEmpty(Detail)) s += " " + Detail;
                if (withElapsed) s += " " + Strings.LF("wp_elapsed", Clock(Elapsed));
                return s;
            }
        }

        public static string Clock(TimeSpan t) =>
            t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes}:{t.Seconds:00}";
    }
}
