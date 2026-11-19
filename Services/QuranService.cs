using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace EzPrayer.Services
{
    public class NameTranslations
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
    }

    public class VerseData
    {
        [JsonPropertyName("number")]
        public int Number { get; set; }

        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("translation_id")]
        public string Translation { get; set; } = string.Empty;

        public string ArabicNumber
        {
            get
            {
                char[] arabicDigits = { '٠', '١', '٢', '٣', '٤', '٥', '٦', '٧', '٨', '٩' };
                string result = "";
                foreach (char c in Number.ToString())
                {
                    if (char.IsDigit(c)) result += arabicDigits[c - '0'];
                }
                return result;
            }
        }
    }

    public class SurahData
    {
        [JsonPropertyName("number_of_surah")]
        public int Number { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("name_translations")]
        public NameTranslations? NameTranslations { get; set; }

        [JsonPropertyName("verses")]
        public List<VerseData> Verses { get; set; } = new();
    }

    public class QuranService
    {
        private readonly string _surahFolderPath;

        public QuranService()
        {
            _surahFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "surah");
        }

        public SurahData? GetSurah(int surahNumber)
        {
            string filePath = Path.Combine(_surahFolderPath, $"{surahNumber}.json");
            if (!File.Exists(filePath)) return null;

            try
            {
                string jsonString = File.ReadAllText(filePath);
                var surah = JsonSerializer.Deserialize<SurahData>(jsonString);

                if (surah != null && surah.Verses.Count > 0)
                {
                    var firstVerse = surah.Verses[0];
                    if (firstVerse.Text.Trim().StartsWith("بِسْمِ"))
                    {
                        int lengthToRemove = Math.Min(39, firstVerse.Text.Length);
                        firstVerse.Text = firstVerse.Text.Substring(lengthToRemove).Trim();
                    }
                }
                return surah;
            }
            catch { return null; }
        }
    }
}