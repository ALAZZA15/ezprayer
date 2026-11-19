using System;
using System.IO;
using System.Text.Json;

namespace EzPrayer.Services
{
    public class AppSettings
    {
        public double Latitude { get; set; } = -7.8014; // Default Yogyakarta
        public double Longitude { get; set; } = 110.3647;
    }

    public static class SettingsService
    {
        private static readonly string _settingsFilePath;

        static SettingsService()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string folderPath = Path.Combine(appData, "EzPrayer");
            
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
            
            _settingsFilePath = Path.Combine(folderPath, "settings.json");
        }

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    string json = File.ReadAllText(_settingsFilePath);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error load settings: {ex.Message}");
            }
            return new AppSettings(); // Kalau gagal, pakai default
        }

        public static void Save(AppSettings settings)
        {
            try
            {
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error save settings: {ex.Message}");
            }
        }
    }
}