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
public double Latitude { get; set; } = -7.8150248;
public double Longitude { get; set; } = 110.3147359;

        public List<PrayerItem> GetTodayPrayers()
        {
            var now = DateTime.Now;
            var coordinates = new Coordinates(Latitude, Longitude);

            var parameters = CalculationMethod.Singapore();
            parameters.Madhab = Madhab.Shafi;

            var prayerTimes = new PrayerTimes(coordinates, now, parameters);
            bool isFriday = now.DayOfWeek == DayOfWeek.Friday;

            var wibZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            DateTime ToLocal(DateTime utcTime) => TimeZoneInfo.ConvertTimeFromUtc(utcTime, wibZone);

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
                if (item.Time > now)
                {
                    item.IsNext = true;
                    break;
                }
            }

            return list;
        }
    }
}