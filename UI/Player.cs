using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows.Threading;
using LibVLCSharp.Shared;
using NAudio.Wave;
using UltraAudioDescriptionMaker.Core;

namespace UltraAudioDescriptionMaker.UI
{
    /// <summary>
    /// Plays the video (LibVLC) and the descriptions over it (NAudio), the way the finished
    /// video will sound: each description starts at its place and the film sound goes down
    /// while it is heard. Used for previewing one description and for watching with descriptions.
    /// </summary>
    public sealed class Player : IDisposable
    {
        private readonly LibVLC _vlc;
        private readonly MediaPlayer _mp;
        private Media _media;
        private readonly DispatcherTimer _timer;
        private readonly Dispatcher _ui;

        private WaveOutEvent _out;
        private AudioFileReader _reader;
        private AdLine _speaking;

        // VLC reports the time in steps of about a quarter of a second: between the steps the
        // position is counted on with a stopwatch, so a description starts on its tenth of a second
        private long _lastVlcMs = -1;
        private readonly Stopwatch _sinceVlc = new Stopwatch();
        private double _lastTick;
        private double _pendingSeek = -1;
        private double _parked;   // position while nothing is loaded into VLC's playback yet

        /// <summary>The descriptions to play over the video (null = none).</summary>
        public Func<IReadOnlyList<AdLine>> Lines;
        /// <summary>Descriptions are heard while the video plays.</summary>
        public bool Narrate = true;
        /// <summary>Pause when this position is reached (previewing one description).</summary>
        public double? StopAt;

        /// <summary>About every 40 ms while playing, and after a seek (seconds).</summary>
        public event Action<double> Tick;
        public event Action PreviewEnded;
        public event Action EndReached;
        /// <summary>A description has no voice yet (the window prepares it).</summary>
        public event Action<AdLine> NeedsVoice;
        public event Action<AdLine> DescriptionStarted;

        public double Length { get; private set; }
        public bool Loaded => _media != null;

        public Player(Dispatcher ui)
        {
            _ui = ui;
            LibVLCSharp.Shared.Core.Initialize();
            _vlc = new LibVLC("--no-video-title-show", "--quiet", "--no-sub-autodetect-file");
            _mp = new MediaPlayer(_vlc) { EnableHardwareDecoding = true };
            _mp.Playing += (s, e) =>
            {
                double seek = _pendingSeek;
                _pendingSeek = -1;
                if (seek >= 0) ThreadPool.QueueUserWorkItem(_ => { try { _mp.Time = (long)(seek * 1000); } catch { } });
            };
            _mp.EndReached += (s, e) => _ui.BeginInvoke(new Action(() =>
            {
                StopVoice();
                _parked = Length;
                ThreadPool.QueueUserWorkItem(_ => { try { _mp.Stop(); } catch { } });
                EndReached?.Invoke();
            }));
            _timer = new DispatcherTimer(DispatcherPriority.Normal, ui) { Interval = TimeSpan.FromMilliseconds(40) };
            _timer.Tick += (s, e) => OnTick();
        }

        public MediaPlayer MediaPlayer => _mp;

        public void Open(string path, double length)
        {
            Stop();
            _media?.Dispose();
            _media = new Media(_vlc, new Uri(path));
            _mp.Media = _media;
            Length = length;
            _parked = 0;
            _lastTick = 0;
        }

        public bool IsPlaying => _mp.IsPlaying || (_pendingSeek >= 0 && _timer.IsEnabled);

        public bool IsSpeaking => _out != null && _out.PlaybackState == PlaybackState.Playing;

        /// <summary>The current position in seconds.</summary>
        public double Position
        {
            get
            {
                var state = _mp.State;
                if (state is not (VLCState.Playing or VLCState.Paused or VLCState.Buffering) || _pendingSeek >= 0)
                    return _pendingSeek >= 0 ? _pendingSeek : _parked;
                long t = _mp.Time;
                if (t < 0) return _parked;
                if (t != _lastVlcMs) { _lastVlcMs = t; _sinceVlc.Restart(); }
                double extra = state == VLCState.Playing ? Math.Min(0.5, _sinceVlc.Elapsed.TotalSeconds) : 0;
                return Math.Min(Length > 0 ? Length : double.MaxValue, t / 1000.0 + extra);
            }
        }

        /// <summary>Plays from <paramref name="from"/> seconds (null = from where it is).</summary>
        public void Play(double? from = null)
        {
            if (_media == null) return;
            double start = from ?? Position;
            if (Length > 0 && start >= Length - 0.2) start = 0;
            var state = _mp.State;
            if (state is VLCState.Playing or VLCState.Paused or VLCState.Buffering)
            {
                _mp.Time = (long)(start * 1000);
                if (state == VLCState.Paused) _mp.SetPause(false);
            }
            else
            {
                _pendingSeek = start > 0.05 ? start : -1;
                _mp.Play();
            }
            _parked = start;
            _lastTick = start - 0.001;
            _lastVlcMs = -1;
            _timer.Start();
        }

        public void Pause()
        {
            if (_mp.State == VLCState.Playing) _mp.SetPause(true);
            _parked = Position;
            StopVoice();
            _timer.Stop();
            Tick?.Invoke(_parked);
        }

        public void Stop()
        {
            StopAt = null;
            StopVoice();
            _timer.Stop();
            _pendingSeek = -1;
            if (_mp.State is VLCState.Playing or VLCState.Paused or VLCState.Buffering)
            {
                _parked = Position;
                _mp.SetPause(true);
            }
        }

        /// <summary>Moves to a position without starting playback.</summary>
        public void Seek(double seconds)
        {
            seconds = Math.Clamp(seconds, 0, Length > 0 ? Length : double.MaxValue);
            StopVoice();
            var state = _mp.State;
            if (state is VLCState.Playing or VLCState.Paused or VLCState.Buffering) _mp.Time = (long)(seconds * 1000);
            _parked = seconds;
            _lastTick = seconds - 0.001;
            _lastVlcMs = -1;
            Tick?.Invoke(seconds);
        }

        private void OnTick()
        {
            double pos = Position;
            if (StopAt is double stop && pos >= stop && !IsSpeaking)
            {
                StopAt = null;
                Pause();
                PreviewEnded?.Invoke();
                return;
            }
            // a jump (seek) does not start every description on the way
            bool jumped = pos < _lastTick - 0.05 || pos > _lastTick + 1.0;
            if (!jumped && Narrate && Lines != null && _mp.State == VLCState.Playing && !IsSpeaking)
            {
                var lines = Lines();
                if (lines != null)
                {
                    foreach (var l in lines)
                    {
                        double at = l.PlacedAt > 0 ? l.PlacedAt : l.Time;
                        if (at > _lastTick && at <= pos)
                        {
                            if (l.AudioPath != null && System.IO.File.Exists(l.AudioPath)) Speak(l);
                            else NeedsVoice?.Invoke(l);
                            break;
                        }
                    }
                    // prepare the voices a little ahead
                    foreach (var l in lines)
                        if (l.AudioPath == null && l.Time > pos && l.Time < pos + 12) NeedsVoice?.Invoke(l);
                }
            }
            _lastTick = pos;
            Tick?.Invoke(pos);
        }

        /// <summary>Speaks one description now, with the film sound lowered.</summary>
        public void Speak(AdLine line)
        {
            if (line?.AudioPath == null) return;
            StopVoice();
            try
            {
                _reader = new AudioFileReader(line.AudioPath);
                _out = new WaveOutEvent { DesiredLatency = 100 };
                _out.Init(_reader);
                _out.PlaybackStopped += (s, e) => _ui.BeginInvoke(new Action(() =>
                {
                    if (_speaking == line) { _mp.Volume = 100; _speaking = null; }
                }));
                _speaking = line;
                _mp.Volume = Math.Clamp(AppSettings.Current.PreviewDuckVolume, 0, 100);
                _out.Play();
                DescriptionStarted?.Invoke(line);
            }
            catch (Exception ex)
            {
                AppLog.Error("Playing a description", ex);
                StopVoice();
            }
        }

        /// <summary>Plays a spoken file alone (no video), e.g. to hear a description or test a voice.</summary>
        public void Listen(string path)
        {
            StopVoice();
            try
            {
                _reader = new AudioFileReader(path);
                _out = new WaveOutEvent { DesiredLatency = 100 };
                _out.Init(_reader);
                _out.Play();
            }
            catch (Exception ex) { AppLog.Error("Playing " + path, ex); StopVoice(); }
        }

        public void StopVoice()
        {
            try { _out?.Stop(); } catch { }
            try { _out?.Dispose(); } catch { }
            try { _reader?.Dispose(); } catch { }
            _out = null;
            _reader = null;
            _speaking = null;
            try { _mp.Volume = 100; } catch { }
        }

        public void Dispose()
        {
            _timer.Stop();
            StopVoice();
            try { _mp.Stop(); } catch { }
            _mp.Dispose();
            _media?.Dispose();
            _vlc.Dispose();
        }
    }
}
