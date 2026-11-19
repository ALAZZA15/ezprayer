using System;
using System.IO;
using System.Text.Json;
using System.Windows.Threading;

namespace EzPrayer.Services
{
    public class KahfSchedulerService
    {
        private readonly DispatcherTimer _timer;
        private readonly QuranAudioService _audioService;
        private readonly string _lastPlayedFilePath;
        private readonly Action<string, string> _notify;

        public KahfSchedulerService(QuranAudioService audioService, Action<string, string> notify)
        {
            _audioService = audioService;
            _notify = notify;
            _audioService.PlaybackFailed += AudioService_PlaybackFailed;
            
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string folderPath = Path.Combine(appData, "EzPrayer");
            
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
            
            _lastPlayedFilePath = Path.Combine(folderPath, "last_kahf.txt");

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMinutes(1);
            _timer.Tick += Timer_Tick;
        }

        public void Start()
        {
            _timer.Start();
            PlayIfMissed();
        }

        public void Stop()
        {
            _timer.Stop();
            _audioService.Stop();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (!SettingsService.Load().KahfAutoPlayEnabled)
            {
                return;
            }

            var now = DateTime.Now;

            // Hanya berjalan di hari Jumat
            if (now.DayOfWeek != DayOfWeek.Friday) return;

            // Jika sudah lewat jam 08:00 dan belum diputar hari ini
            if (now.Hour >= 8 && !HasPlayedToday())
            {
                PlayAlKahf();
            }
        }

        private void PlayIfMissed()
        {
            if (!SettingsService.Load().KahfAutoPlayEnabled)
            {
                return;
            }

            var now = DateTime.Now;
            if (now.DayOfWeek != DayOfWeek.Friday) return;

            if (now.Hour >= 8 && !HasPlayedToday())
            {
                PlayAlKahf();
            }
        }

        private bool HasPlayedToday()
        {
            if (!File.Exists(_lastPlayedFilePath)) return false;

            try
            {
                string lastDateStr = File.ReadAllText(_lastPlayedFilePath).Trim();
                if (DateTime.TryParse(lastDateStr, out DateTime lastDate))
                {
                    return lastDate.Date == DateTime.Now.Date;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Gagal membaca catatan pemutaran Al-Kahf: {ex.Message}");
            }
            return false;
        }

        private void SavePlayedToday()
        {
            try
            {
                File.WriteAllText(_lastPlayedFilePath, DateTime.Now.ToString("yyyy-MM-dd"));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Gagal menyimpan catatan pemutaran Al-Kahf: {ex.Message}");
            }
        }

        private void PlayAlKahf()
        {
            string? url = GetAudioUrlFromJson(18);
            if (string.IsNullOrWhiteSpace(url))
            {
                _notify("EzPrayer - Murottal Al-Kahf", "Tautan audio Al-Kahf tidak tersedia.");
                SavePlayedToday();
                return;
            }

            if (_audioService.PlayFromUrl(url))
            {
                _notify("EzPrayer - Murottal Al-Kahf", "Murottal Al-Kahf mulai diputar.");
            }

            SavePlayedToday();
        }

        private void AudioService_PlaybackFailed(Exception exception)
        {
            Console.WriteLine($"Gagal memutar Al-Kahf: {exception.Message}");
            _notify("EzPrayer - Murottal Al-Kahf", "Tidak ada koneksi, Al-Kahf tidak bisa diputar.");
        }

        private string? GetAudioUrlFromJson(int surahNumber)
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "surah", $"{surahNumber}.json");
                if (!File.Exists(path)) return null;

                string json = File.ReadAllText(path);
                using var doc = JsonDocument.Parse(json);
                
                if (doc.RootElement.TryGetProperty("recitations", out var recitations) && recitations.GetArrayLength() > 0)
                {
                    return recitations[0].GetProperty("audio_url").GetString();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error ambil audio Al-Kahf: {ex.Message}");
            }
            return null;
        }
    }
}