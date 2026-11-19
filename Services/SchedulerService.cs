using System;
using System.Collections.Generic;
using System.Windows.Threading;

namespace EzPrayer.Services
{
    public class SchedulerService
    {
        private readonly DispatcherTimer _timer;
        private readonly PrayerService _prayerService;
        private readonly AudioService _audioService;
        
        // Simpan jadwal di memori, jangan dihitung ulang tiap detik
        private List<PrayerItem> _todayPrayers = new();
        private string _lastPlayedKey = string.Empty; 

        public SchedulerService(AudioService audioService)
        {
            _prayerService = new PrayerService();
            _audioService = audioService;
            
            // Ambil jadwal SEKALI saat aplikasi pertama jalan
            _todayPrayers = _prayerService.GetTodayPrayers();

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;
        }

        public void Start()
        {
            _timer.Start();
            Console.WriteLine("[SCHEDULER] Timer berjalan. Menunggu waktu sholat...");
        }

        public void Stop()
        {
            _timer.Stop();
            _audioService.Stop();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            var now = DateTime.Now;

            // Cek setiap detik, tapi bandingkan dengan jadwal yang sudah disimpan (_todayPrayers)
            foreach (var prayer in _todayPrayers)
            {
                if (!prayer.IsAdzan) continue;

                if (now >= prayer.Time)
                {
                    string currentKey = $"{prayer.Name}_{now:yyyy-MM-dd}";
                    if (_lastPlayedKey == currentKey) continue;

                    // Toleransi 5 menit
                    if ((now - prayer.Time).TotalMinutes < 5)
                    {
                        Console.WriteLine($"[ADZAN] Waktu {prayer.Name} telah tiba! Memutar audio...");
                        _audioService.PlayAdzan(prayer.IsSubuh);
                        _lastPlayedKey = currentKey;
                        break;
                    }
                }
            }
        }
    }
}