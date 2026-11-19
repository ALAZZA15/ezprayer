using System;
using System.Collections.Generic;
using AzanDotNet;

namespace EzPrayer.Services
{
    public class PrayerItem
    {
        public string Name { get; set; } = string.Empty;
        public DateTime Time { get; set; }
        public string FormattedTime => Time.ToString("HH:mm");
        public bool IsNext { get; set; }
        public bool IsSubuh { get; set; }
        public bool IsAdzan { get; set; } = true;
    }

    public class PrayerService
    {
        public DateTime GetCurrentTime()
        {
            var settings = SettingsService.Load();
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(settings.TimeZoneId);
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
        }

        public List<PrayerItem> GetTodayPrayers(DateTime? date = null)
        {
            var settings = SettingsService.Load();
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(settings.TimeZoneId);
            var now = GetCurrentTime();
            var prayerDate = (date ?? now).Date;
            var coordinates = new Coordinates(settings.Latitude, settings.Longitude);

            var parameters = CreateCalculationParameters(settings.CalculationMethodName);
            parameters.Madhab = Madhab.Shafi;

            var prayerTimes = new PrayerTimes(coordinates, prayerDate, parameters);
            bool isFriday = prayerDate.DayOfWeek == DayOfWeek.Friday;

            DateTime ToLocal(DateTime utcTime) =>
                TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcTime, DateTimeKind.Utc), timeZone);

            var subuh = ToLocal(prayerTimes.Fajr);
            var syuruq = ToLocal(prayerTimes.Sunrise);

            var list = new List<PrayerItem>
            {
                new PrayerItem { Name = "Imsak", Time = subuh.AddMinutes(-10), IsAdzan = false },
                new PrayerItem { Name = "Subuh", Time = subuh, IsSubuh = true },
                new PrayerItem { Name = "Dhuha", Time = syuruq.AddMinutes(20), IsAdzan = false },
                new PrayerItem { Name = isFriday ? "Sholat Jumat" : "Dzuhur", Time = ToLocal(prayerTimes.Dhuhr) },
                new PrayerItem { Name = "Ashar", Time = ToLocal(prayerTimes.Asr) },
                new PrayerItem { Name = "Maghrib", Time = ToLocal(prayerTimes.Maghrib) },
                new PrayerItem { Name = "Isya", Time = ToLocal(prayerTimes.Isha) }
            };

            foreach (var item in list)
            {
                if (item.IsAdzan && (item.Time > now || prayerDate != now.Date))
                {
                    item.IsNext = true;
                    break;
                }
            }

            return list;
        }

        private static CalculationParameters CreateCalculationParameters(string methodName)
        {
            return methodName switch
            {
                "MuslimWorldLeague" => CalculationMethod.MuslimWorldLeague(),
                "Egyptian" => CalculationMethod.Egyptian(),
                "Karachi" => CalculationMethod.Karachi(),
                "UmmAlQura" => CalculationMethod.UmmAlQura(),
                "NorthAmerica" => CalculationMethod.NorthAmerica(),
                "Singapore" => CalculationMethod.Singapore(),
                _ => new CalculationParameters("Kemenag", 20.0, 18.0)
            };
        }
    }
}