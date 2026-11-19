using System;
using System.IO;
using NAudio.Wave;

namespace EzPrayer.Services
{
    public class AudioService : IDisposable
    {
        private IWavePlayer? _waveOut;
        private AudioFileReader? _audioFile;

        public void PlayAdzan(bool isSubuh)
        {
            Stop(); 

            string fileName = isSubuh ? "adzan_subuh.mp3" : "adzan.mp3";
            string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Audio", fileName);

            if (!File.Exists(fullPath))
            {
                Console.WriteLine($"ERROR: File audio tidak ditemukan di {fullPath}");
                return;
            }

            try
            {
                _waveOut = new WaveOutEvent();
                _audioFile = new AudioFileReader(fullPath);
                
                _waveOut.Init(_audioFile);
                _waveOut.Play();
                Console.WriteLine($"INFO: Memutar audio {fileName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR SAAT MEMUTAR AUDIO: {ex.Message}");
            }
        }

        public void Stop()
        {
            _waveOut?.Stop();
            _waveOut?.Dispose();
            _waveOut = null;

            _audioFile?.Dispose();
            _audioFile = null;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}