using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using EzPrayer.Services;

namespace EzPrayer.Views
{
    public class WidgetViewModel : INotifyPropertyChanged
    {
        private readonly PrayerService _prayerService;
        private readonly DispatcherTimer _timer;
        private string _currentTime = string.Empty;
        private string _nextPrayerName = string.Empty;
        private string _countdown = string.Empty;

        public ObservableCollection<PrayerItem> Prayers { get; set; }

        public string CurrentTime
        {
            get => _currentTime;
            set { _currentTime = value; OnPropertyChanged(); }
        }

        public string NextPrayerName
        {
            get => _nextPrayerName;
            set { _nextPrayerName = value; OnPropertyChanged(); }
        }

        public string Countdown
        {
            get => _countdown;
            set { _countdown = value; OnPropertyChanged(); }
        }

        public WidgetViewModel()
        {
            _prayerService = new PrayerService();
            Prayers = new ObservableCollection<PrayerItem>();

            LoadPrayers();

            // Timer untuk update jam setiap detik
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;
            _timer.Start();
            
            UpdateTime();
        }

        private void LoadPrayers()
        {
            Prayers.Clear();
            var list = _prayerService.GetTodayPrayers();
            foreach (var item in list)
            {
                Prayers.Add(item);
            }
            
            var next = Prayers.FirstOrDefault(p => p.IsNext);
            NextPrayerName = next != null ? next.Name : "Subuh (Besok)";
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            UpdateTime();
        }

        private void UpdateTime()
        {
            var now = DateTime.Now;
            CurrentTime = now.ToString("HH:mm:ss");

            var next = Prayers.FirstOrDefault(p => p.Time > now);
            if (next != null)
            {
                NextPrayerName = next.Name;
                var diff = next.Time - now;
                Countdown = $"Menuju waktu {next.Name}";
            }
            else
            {
                NextPrayerName = "Subuh (Besok)";
                Countdown = "Menunggu Subuh";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}