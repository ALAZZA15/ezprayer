using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;

namespace EzPrayer.Services
{
    public class KahfSchedulerService
    {
        private readonly DispatcherTimer _timer;
        private readonly QuranAudioService _audioService;
        private readonly string _lastPlayedFilePath;

        public KahfSchedulerService(QuranAudioService audioService)
        {
            _audioService = audioService;
            
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
            CheckAndPromptIfMissed();
        }

        public void Stop()
        {
            _timer.Stop();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            var now = DateTime.Now;

            // Hanya berjalan di hari Jumat
            if (now.DayOfWeek != DayOfWeek.Friday) return;

            // Jika sudah lewat jam 08:00 dan belum diputar hari ini
            if (now.Hour >= 8 && !HasPlayedToday())
            {
                PlayAlKahf();
                SavePlayedToday();
            }
        }

        private void CheckAndPromptIfMissed()
        {
            var now = DateTime.Now;
            if (now.DayOfWeek != DayOfWeek.Friday) return;

            if (now.Hour >= 8 && !HasPlayedToday())
            {
                var result = MessageBox.Show(
                    "Hari ini hari Jumat dan sudah lewat jam 08:00. Apakah Anda ingin memutar Murottal Al-Kahf sekarang?",
                    "EzPrayer - Murottal Al-Kahf",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    PlayAlKahf();
                    SavePlayedToday();
                }
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
            catch { }
            return false;
        }

        private void SavePlayedToday()
        {
            try
            {
                File.WriteAllText(_lastPlayedFilePath, DateTime.Now.ToString("yyyy-MM-dd"));
            }
            catch { }
        }

        private void PlayAlKahf()
        {
            string? url = GetAudioUrlFromJson(18);
            if (!string.IsNullOrEmpty(url))
            {
                _audioService.PlayFromUrl(url);
                MessageBox.Show("Memutar Murottal Al-Kahf.", "EzPrayer", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Gagal menemukan link audio Al-Kahf.", "EzPrayer", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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