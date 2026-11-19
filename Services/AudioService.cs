using System;
using System.IO;
using NAudio.Wave;

namespace EzPrayer.Services
{
    public class AudioService : IDisposable
    {
        private IWavePlayer? _waveOut;
        private AudioFileReader? _audioFile;

        public bool PlayAdzan(bool isSubuh, string? audioPathOverride = null, float? volumeOverride = null)
        {
            Stop();

            var settings = SettingsService.Load();
            string configuredPath = audioPathOverride ??
                (isSubuh ? settings.SubuhAdhanAudioPath : settings.AdhanAudioPath);
            string fileName = isSubuh ? "adzan_subuh.mp3" : "adzan.mp3";
            string fullPath = string.IsNullOrWhiteSpace(configuredPath)
                ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Audio", fileName)
                : configuredPath;

            if (!File.Exists(fullPath))
            {
                Console.WriteLine($"ERROR: File audio tidak ditemukan di {fullPath}");
                return false;
            }

            try
            {
                _waveOut = new WaveOutEvent();
                _audioFile = new AudioFileReader(fullPath);
                _audioFile.Volume = Math.Clamp(volumeOverride ?? (float)settings.AdhanVolume, 0f, 1f);
                
                _waveOut.Init(_audioFile);
                _waveOut.Play();
                Console.WriteLine($"INFO: Memutar audio {fileName}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR SAAT MEMUTAR AUDIO: {ex.Message}");
                Stop();
                return false;
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