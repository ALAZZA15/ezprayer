using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EzPrayer.Services;

namespace EzPrayer.Views
{
    public partial class MainWindow : Window
    {
        private readonly QuranService _quranService;
        private readonly QuranAudioService _audioService = new QuranAudioService();
        private string? _currentAudioUrl;

        public MainWindow()
        {
            InitializeComponent();
            _quranService = new QuranService();

            // Load Font Amiri
            string fontPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Fonts", "Amiri-Regular.ttf");
            if (File.Exists(fontPath))
            {
                var amiriFont = new FontFamily(new Uri(fontPath), "./#Amiri");
                this.Resources["AmiriFont"] = amiriFont;
            }
            else
            {
                MessageBox.Show("File font Amiri tidak ditemukan di: " + fontPath);
            }

            LoadAllSurahs();
        }

        private void LoadAllSurahs()
        {
            var surahList = new List<SurahData>();
            for (int i = 1; i <= 114; i++)
            {
                var surah = _quranService.GetSurah(i);
                if (surah != null)
                {
                    surahList.Add(surah);
                }
            }
            SurahListBox.ItemsSource = surahList;
        }

        private void SurahListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SurahListBox.SelectedItem is SurahData selectedSurah)
            {
                // Jika sedang memutar audio, hentikan dulu saat ganti surah
                _audioService.Stop();
                PlayMurottalButton.Content = "▶ Putar Murottal";

                SurahTitle.Text = $"{selectedSurah.Name} ({selectedSurah.NameTranslations?.Id})";
                VersesList.ItemsSource = selectedSurah.Verses;

                if (selectedSurah.Number == 9)
                    BismillahText.Visibility = Visibility.Collapsed;
                else
                    BismillahText.Visibility = Visibility.Visible;

                _currentAudioUrl = GetAudioUrlFromJson(selectedSurah.Number);
            }
        }

        private void PlayMurottalButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentAudioUrl))
            {
                MessageBox.Show("Link audio untuk surah ini tidak tersedia.");
                return;
            }

            // Jika tombol sedang "Stop", maka hentikan audio
            if (PlayMurottalButton.Content.ToString() == "⏹ Stop Murottal")
            {
                _audioService.Stop();
                PlayMurottalButton.Content = "▶ Putar Murottal";
            }
            else
            {
                // Putar audio
                _audioService.PlayFromUrl(_currentAudioUrl);
                PlayMurottalButton.Content = "⏹ Stop Murottal";
            }
        }

        // --- EVENT SAAT JENDELA DITUTUP ---
        private void Window_Closed(object sender, EventArgs e)
        {
            // Hentikan semua audio dan lepaskan resource
            _audioService.Stop();
            _audioService.Dispose();
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
                Console.WriteLine($"Error ambil audio: {ex.Message}");
            }
            return null;
        }
    }
}