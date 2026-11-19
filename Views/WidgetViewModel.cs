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
        private string _locationName = string.Empty;
        private DateTime _scheduleDate;

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

        public string LocationName
        {
            get => _locationName;
            set { _locationName = value; OnPropertyChanged(); }
        }

        public WidgetViewModel()
        {
            _prayerService = new PrayerService();
            Prayers = new ObservableCollection<PrayerItem>();

            RefreshSchedule();

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;
            _timer.Start();
            
            UpdateTime(_prayerService.GetCurrentTime());
        }

        public void RefreshSchedule()
        {
            var now = _prayerService.GetCurrentTime();
            Prayers.Clear();
            var list = _prayerService.GetTodayPrayers(now.Date);
            foreach (var item in list)
            {
                Prayers.Add(item);
            }

            _scheduleDate = now.Date;
            LocationName = SettingsService.Load().LocationName;
            var next = Prayers.FirstOrDefault(p => p.IsNext);
            NextPrayerName = next != null ? next.Name : "Subuh (Besok)";
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            var now = _prayerService.GetCurrentTime();
            if (now.Date != _scheduleDate)
            {
                RefreshSchedule();
            }

            UpdateTime(now);
        }

        private void UpdateTime(DateTime now)
        {
            CurrentTime = now.ToString("HH:mm:ss");

            var next = Prayers.FirstOrDefault(p => p.IsAdzan && p.Time > now);
            if (next == null)
            {
                next = _prayerService.GetTodayPrayers(now.Date.AddDays(1))
                    .First(prayer => prayer.Name == "Subuh");
            }

            NextPrayerName = next.Name;
            var remaining = next.Time - now;
            Countdown = $"Menuju {next.Name}: {(int)remaining.TotalHours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}";
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}