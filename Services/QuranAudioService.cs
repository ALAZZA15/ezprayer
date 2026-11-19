using System;
using NAudio.Wave;

namespace EzPrayer.Services
{
    public class QuranAudioService : IDisposable
    {
        private IWavePlayer? _waveOut;
        private MediaFoundationReader? _reader;

        public void PlayFromUrl(string url)
        {
            Stop();

            try
            {
                // MediaFoundationReader bisa langsung membaca mp3 dari URL internet
                _reader = new MediaFoundationReader(url);
                _waveOut = new WaveOutEvent();
                _waveOut.Init(_reader);
                _waveOut.Play();
                Console.WriteLine($"[AUDIO] Memutar murottal dari URL: {url}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AUDIO] Gagal memutar: {ex.Message}");
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

        public void Dispose()
        {
            Stop();
        }
    }
}