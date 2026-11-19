using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Win32;

namespace EzPrayer.Services
{
    public class AppSettings
    {
        public string LocationName { get; set; } = "Yogyakarta";
        public double Latitude { get; set; } = -7.8014;
        public double Longitude { get; set; } = 110.3647;
        public string TimeZoneId { get; set; } = "SE Asia Standard Time";
        public string CalculationMethodName { get; set; } = "Kemenag";
        public int LastReadSurahNumber { get; set; } = 1;
        public int LastReadVerseNumber { get; set; } = 1;
        public List<QuranBookmark> QuranBookmarks { get; set; } = new();
        public double ArabicFontSize { get; set; } = 28;
        public double TranslationFontSize { get; set; } = 16;
        public int HijriDateAdjustmentDays { get; set; }
        public string CalendarTheme { get; set; } = "Biru";
        public double AdhanVolume { get; set; } = 0.8;
        public string AdhanAudioPath { get; set; } = string.Empty;
        public string SubuhAdhanAudioPath { get; set; } = string.Empty;
        public bool KahfAutoPlayEnabled { get; set; } = true;
    }

    public sealed class QuranBookmark
    {
        public int SurahNumber { get; set; }
        public int VerseNumber { get; set; }
    }

    public sealed record PrayerLocation(string Name, double Latitude, double Longitude, string TimeZoneId);

    public sealed record PrayerCalculationMethod(string Name, string Id);

    public static class SettingsService
    {
        private const string StartupRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string StartupRegistryValueName = "EzPrayer";
        private static readonly string[] CalendarThemes = { "Biru", "Hijau", "Ungu", "Gelap" };
        private static readonly string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EzPrayer",
            "settings.json");

        private static readonly IReadOnlyList<PrayerLocation> Locations = new[]
        {
            new PrayerLocation("Yogyakarta", -7.8014, 110.3647, "SE Asia Standard Time"),
            new PrayerLocation("Jakarta", -6.2088, 106.8456, "SE Asia Standard Time"),
            new PrayerLocation("Bandung", -6.9175, 107.6191, "SE Asia Standard Time"),
            new PrayerLocation("Medan", 3.5952, 98.6722, "SE Asia Standard Time"),
            new PrayerLocation("Surabaya", -7.2575, 112.7521, "SE Asia Standard Time"),
            new PrayerLocation("Denpasar", -8.6500, 115.2167, "Singapore Standard Time"),
            new PrayerLocation("Makassar", -5.1477, 119.4327, "Singapore Standard Time"),
            new PrayerLocation("Jayapura", -2.5337, 140.7181, "Tokyo Standard Time")
        };

        private static readonly IReadOnlyList<PrayerCalculationMethod> CalculationMethods = new[]
        {
            new PrayerCalculationMethod("Kemenag Indonesia", "Kemenag"),
            new PrayerCalculationMethod("Muslim World League", "MuslimWorldLeague"),
            new PrayerCalculationMethod("Egyptian General Authority", "Egyptian"),
            new PrayerCalculationMethod("University of Islamic Sciences, Karachi", "Karachi"),
            new PrayerCalculationMethod("Umm al-Qura University, Makkah", "UmmAlQura"),
            new PrayerCalculationMethod("Islamic Society of North America", "NorthAmerica"),
            new PrayerCalculationMethod("Singapore", "Singapore")
        };

        public static IReadOnlyList<PrayerLocation> GetLocations() => Locations;

        public static IReadOnlyList<PrayerCalculationMethod> GetCalculationMethods() => CalculationMethods;

        public static AppSettings Load()
        {
            if (!File.Exists(SettingsFilePath))
            {
                return new AppSettings();
            }

            try
            {
                string json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings == null)
                {
                    Console.WriteLine("Pengaturan kosong; menggunakan pengaturan bawaan.");
                    return new AppSettings();
                }

                settings.LocationName ??= "Yogyakarta";
                settings.TimeZoneId ??= "SE Asia Standard Time";
                settings.CalendarTheme ??= "Biru";
                settings.AdhanAudioPath ??= string.Empty;
                settings.SubuhAdhanAudioPath ??= string.Empty;
                settings.QuranBookmarks ??= new List<QuranBookmark>();
                if (settings.QuranBookmarks.Any(bookmark =>
                        bookmark == null || bookmark.SurahNumber < 1 ||
                        bookmark.SurahNumber > 114 || bookmark.VerseNumber < 1))
                {
                    Console.WriteLine("Bookmark Al-Quran tidak valid; entri yang rusak diabaikan.");
                    settings.QuranBookmarks = settings.QuranBookmarks
                        .Where(bookmark => bookmark != null &&
                            bookmark.SurahNumber >= 1 && bookmark.SurahNumber <= 114 &&
                            bookmark.VerseNumber >= 1)
                        .ToList();
                }

                if (settings.LastReadSurahNumber < 1 || settings.LastReadSurahNumber > 114 ||
                    settings.LastReadVerseNumber < 1)
                {
                    Console.WriteLine("Posisi baca Al-Quran tidak valid; menggunakan Al-Fatihah ayat 1.");
                    settings.LastReadSurahNumber = 1;
                    settings.LastReadVerseNumber = 1;
                }

                if (double.IsNaN(settings.AdhanVolume) || settings.AdhanVolume < 0 || settings.AdhanVolume > 1)
                {
                    Console.WriteLine("Volume adzan tidak valid; menggunakan volume 80%.");
                    settings.AdhanVolume = 0.8;
                }

                if (double.IsNaN(settings.ArabicFontSize) ||
                    settings.ArabicFontSize < 18 || settings.ArabicFontSize > 56)
                {
                    Console.WriteLine("Ukuran font Arab tidak valid; menggunakan ukuran 28.");
                    settings.ArabicFontSize = 28;
                }

                if (double.IsNaN(settings.TranslationFontSize) ||
                    settings.TranslationFontSize < 12 || settings.TranslationFontSize > 32)
                {
                    Console.WriteLine("Ukuran font terjemahan tidak valid; menggunakan ukuran 16.");
                    settings.TranslationFontSize = 16;
                }

                if (settings.HijriDateAdjustmentDays < -1 || settings.HijriDateAdjustmentDays > 1)
                {
                    Console.WriteLine("Koreksi tanggal Hijriah tidak valid; menggunakan tanpa koreksi.");
                    settings.HijriDateAdjustmentDays = 0;
                }

                if (!CalendarThemes.Contains(settings.CalendarTheme))
                {
                    Console.WriteLine($"Tema kalender '{settings.CalendarTheme}' tidak dikenal; menggunakan Biru.");
                    settings.CalendarTheme = "Biru";
                }

                if (!CalculationMethods.Any(method => method.Id == settings.CalculationMethodName))
                {
                    Console.WriteLine($"Metode hitung '{settings.CalculationMethodName}' tidak dikenal; menggunakan Kemenag.");
                    settings.CalculationMethodName = "Kemenag";
                }

                return settings;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Gagal membaca pengaturan: {ex.Message}");
                return new AppSettings();
            }
        }

        public static void Save(AppSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            settings.QuranBookmarks ??= new List<QuranBookmark>();
            if (double.IsNaN(settings.Latitude) || settings.Latitude < -90 || settings.Latitude > 90)
            {
                throw new ArgumentOutOfRangeException(nameof(settings), "Latitude harus berada di antara -90 dan 90.");
            }

            if (double.IsNaN(settings.Longitude) || settings.Longitude < -180 || settings.Longitude > 180)
            {
                throw new ArgumentOutOfRangeException(nameof(settings), "Longitude harus berada di antara -180 dan 180.");
            }

            if (!CalculationMethods.Any(method => method.Id == settings.CalculationMethodName))
            {
                throw new ArgumentException("Metode perhitungan sholat tidak dikenal.", nameof(settings));
            }

            if (settings.HijriDateAdjustmentDays < -1 || settings.HijriDateAdjustmentDays > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(settings), "Koreksi Hijriah hanya boleh -1, 0, atau 1 hari.");
            }

            if (!CalendarThemes.Contains(settings.CalendarTheme))
            {
                throw new ArgumentException("Tema kalender tidak dikenal.", nameof(settings));
            }

            if (double.IsNaN(settings.AdhanVolume) || settings.AdhanVolume < 0 || settings.AdhanVolume > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(settings), "Volume adzan harus berada di antara 0 dan 1.");
            }

            if (double.IsNaN(settings.ArabicFontSize) ||
                settings.ArabicFontSize < 18 || settings.ArabicFontSize > 56)
            {
                throw new ArgumentOutOfRangeException(nameof(settings), "Ukuran font Arab harus berada di antara 18 dan 56.");
            }

            if (double.IsNaN(settings.TranslationFontSize) ||
                settings.TranslationFontSize < 12 || settings.TranslationFontSize > 32)
            {
                throw new ArgumentOutOfRangeException(nameof(settings), "Ukuran font terjemahan harus berada di antara 12 dan 32.");
            }

            if (settings.LastReadSurahNumber < 1 || settings.LastReadSurahNumber > 114 ||
                settings.LastReadVerseNumber < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(settings), "Posisi baca Al-Quran tidak valid.");
            }

            if (settings.QuranBookmarks.Any(bookmark =>
                    bookmark == null || bookmark.SurahNumber < 1 ||
                    bookmark.SurahNumber > 114 || bookmark.VerseNumber < 1))
            {
                throw new ArgumentException("Daftar bookmark Al-Quran berisi nomor yang tidak valid.", nameof(settings));
            }

            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFilePath)!);
            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
        }

        public static bool IsAutostartEnabled()
        {
            using var startupKey = Registry.CurrentUser.OpenSubKey(StartupRegistryPath, writable: false);
            return startupKey?.GetValue(StartupRegistryValueName) is string;
        }

        public static void SetAutostart(bool enabled)
        {
            if (!enabled)
            {
                using var startupKey = Registry.CurrentUser.OpenSubKey(StartupRegistryPath, writable: true);
                startupKey?.DeleteValue(StartupRegistryValueName, throwOnMissingValue: false);
                return;
            }

            string executablePath = Environment.ProcessPath
                ?? throw new InvalidOperationException("Path executable EzPrayer tidak dapat ditentukan.");
            using var writableKey = Registry.CurrentUser.CreateSubKey(StartupRegistryPath, writable: true)
                ?? throw new InvalidOperationException("Registry autostart EzPrayer tidak dapat dibuka.");
            writableKey.SetValue(StartupRegistryValueName, $"\"{executablePath}\"", RegistryValueKind.String);
        }
    }
}
