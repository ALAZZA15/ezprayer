using System;
using NAudio.Wave;

namespace EzPrayer.Services
{
    public class QuranAudioService : IDisposable
    {
        private IWavePlayer? _waveOut;
        private MediaFoundationReader? _reader;

        public event Action<Exception>? PlaybackFailed;
        public PlaybackState State => _waveOut?.PlaybackState ?? PlaybackState.Stopped;
        public TimeSpan CurrentTime => _reader?.CurrentTime ?? TimeSpan.Zero;
        public TimeSpan TotalTime => _reader?.TotalTime ?? TimeSpan.Zero;
        public bool CanSeek => _reader?.CanSeek ?? false;

        public bool PlayFromUrl(string url)
        {
            Stop();

            try
            {
                _reader = new MediaFoundationReader(url);
                _waveOut = new WaveOutEvent();
                _waveOut.PlaybackStopped += (_, e) =>
                {
                    if (e.Exception != null)
                    {
                        Console.WriteLine($"[AUDIO] Playback gagal: {e.Exception.Message}");
                        PlaybackFailed?.Invoke(e.Exception);
                    }
                };
                _waveOut.Init(_reader);
                _waveOut.Play();
                Console.WriteLine($"[AUDIO] Memutar murottal dari URL: {url}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AUDIO] Gagal memutar: {ex.Message}");
                PlaybackFailed?.Invoke(ex);
                Stop();
                return false;
            }
        }

        public void Stop()
        {
            _waveOut?.Stop();
            _waveOut?.Dispose();
            _waveOut = null;

            _reader?.Dispose();
            _reader = null;
        }

        public void Pause()
        {
            if (State == PlaybackState.Playing)
            {
                _waveOut?.Pause();
            }
        }

        public void Resume()
        {
            if (State == PlaybackState.Paused)
            {
                _waveOut?.Play();
            }
        }

        public bool Seek(TimeSpan position)
        {
            if (_reader == null || !_reader.CanSeek)
            {
                return false;
            }

            double seconds = Math.Clamp(position.TotalSeconds, 0, _reader.TotalTime.TotalSeconds);
            _reader.CurrentTime = TimeSpan.FromSeconds(seconds);
            return true;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}