using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Threading;
using EzPrayer.Services;
using NAudio.Wave;

namespace EzPrayer.Views
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private readonly QuranService _quranService;
        private readonly QuranAudioService _audioService = new QuranAudioService();
        private readonly AudioService _adhanPreviewService = new AudioService();
        private readonly PrayerService _prayerService = new PrayerService();
        private readonly WindowsLocationService _locationService = new WindowsLocationService();
        private readonly DispatcherTimer _homeTimer;
        private readonly DispatcherTimer _calendarTimer;
        private readonly DispatcherTimer _murottalTimer;
        private List<PrayerItem> _todayPrayers = new();
        private string? _currentAudioUrl;
        private string? _murottalAudioUrl;
        private string? _currentlyPlayingSurahName;
        private string _homeSettingsFingerprint = string.Empty;
        private DateTime _homeScheduleDate = DateTime.MinValue;
        private DateTime _tomorrowScheduleDate = DateTime.MinValue;
        private PrayerItem? _tomorrowFajr;
        private List<SurahData> _allSurahs = new();
        private SurahData? _pendingSurahToOpen;
        private VerseData? _pendingVerseToOpen;
        private double _arabicFontSize = 28;
        private double _translationFontSize = 16;
        private DateTime _calendarMonth = DateTime.Today;
        private DateTime _selectedCalendarDate = DateTime.Today;
        private DateTime _calendarTodayDate = DateTime.MinValue;
        private int _calendarAdjustmentDays;
        private bool _calendarInitialized;
        private bool _isUpdatingMurottalSlider;

        private static readonly string[] HijriMonthNames =
        {
            "Muharam", "Safar", "Rabiulawal", "Rabiulakhir",
            "Jumadilawal", "Jumadilakhir", "Rajab", "Syakban",
            "Ramadan", "Syawal", "Zulkaidah", "Zulhijah"
        };
        private static readonly string[] CalendarThemes = { "Biru", "Hijau", "Ungu", "Gelap" };

        public event EventHandler? SettingsSaved;
        public event PropertyChangedEventHandler? PropertyChanged;

        public double ArabicFontSize
        {
            get => _arabicFontSize;
            private set
            {
                if (_arabicFontSize == value)
                {
                    return;
                }

                _arabicFontSize = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ArabicFontSize)));
            }
        }

        public double TranslationFontSize
        {
            get => _translationFontSize;
            private set
            {
                if (_translationFontSize == value)
                {
                    return;
                }

                _translationFontSize = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TranslationFontSize)));
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            _quranService = new QuranService();

            string logoPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "Assets", "Images", "EzPrayer-icon.png");
            if (File.Exists(logoPath))
            {
                var logoImage = new BitmapImage();
                logoImage.BeginInit();
                logoImage.UriSource = new Uri(logoPath);
                logoImage.CacheOption = BitmapCacheOption.OnLoad;
                logoImage.EndInit();
                logoImage.Freeze();
                AboutLogoImage.Source = logoImage;
                Icon = BitmapFrame.Create(logoImage);
            }
            else
            {
                Console.WriteLine($"Logo EzPrayer tidak ditemukan: {logoPath}");
            }

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

            _homeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _homeTimer.Tick += HomeTimer_Tick;
            _calendarTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _calendarTimer.Tick += CalendarTimer_Tick;
            _murottalTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _murottalTimer.Tick += MurottalTimer_Tick;
            _audioService.PlaybackFailed += AudioService_PlaybackFailed;
            SettingsLocationComboBox.ItemsSource = SettingsService.GetLocations();
            SettingsCalculationMethodComboBox.ItemsSource = SettingsService.GetCalculationMethods();
            SettingsThemeComboBox.ItemsSource = CalendarThemes;
            MurottalSurahListBox.ItemsSource = _allSurahs;
            AboutVersionText.Text = $"Versi {typeof(MainWindow).Assembly.GetName().Version?.ToString() ?? "1.0.0"}";
            var settings = SettingsService.Load();
            ArabicFontSize = settings.ArabicFontSize;
            TranslationFontSize = settings.TranslationFontSize;
            ApplyApplicationTheme(settings.CalendarTheme);
            RefreshBookmarks();
            NavigateTo("Home");
        }

        private void LoadAllSurahs()
        {
            _allSurahs = new List<SurahData>();
            for (int i = 1; i <= 114; i++)
            {
                var surah = _quranService.GetSurah(i);
                if (surah != null)
                {
                    _allSurahs.Add(surah);
                }
            }
            SurahListBox.ItemsSource = _allSurahs;
        }

        private void NavigationButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string page)
            {
                NavigateTo(page);
            }
        }

        private void LoadSettingsPage()
        {
            var settings = SettingsService.Load();
            SettingsAutostartCheckBox.IsEnabled = true;
            var availableLocations = SettingsService.GetLocations().ToList();
            var selectedLocation = availableLocations
                .FirstOrDefault(location => location.Name == settings.LocationName);
            if (selectedLocation == null)
            {
                selectedLocation = new PrayerLocation(
                    settings.LocationName, settings.Latitude, settings.Longitude, settings.TimeZoneId);
                availableLocations.Add(selectedLocation);
            }

            SettingsLocationComboBox.ItemsSource = availableLocations;
            SettingsLocationComboBox.SelectedItem = selectedLocation;
            SettingsCalculationMethodComboBox.SelectedItem = SettingsService.GetCalculationMethods()
                .FirstOrDefault(method => method.Id == settings.CalculationMethodName);
            SettingsHijriAdjustmentComboBox.SelectedValue =
                settings.HijriDateAdjustmentDays.ToString(CultureInfo.InvariantCulture);
            SettingsThemeComboBox.SelectedItem = Array.IndexOf(CalendarThemes, settings.CalendarTheme) >= 0
                ? settings.CalendarTheme
                : "Biru";
            AdhanVolumeSlider.Value = settings.AdhanVolume * 100;
            ArabicFontSizeSlider.Value = settings.ArabicFontSize;
            TranslationFontSizeSlider.Value = settings.TranslationFontSize;
            SettingsKahfAutoplayCheckBox.IsChecked = settings.KahfAutoPlayEnabled;
            AdhanAudioPathTextBox.Text = settings.AdhanAudioPath;
            SubuhAdhanAudioPathTextBox.Text = settings.SubuhAdhanAudioPath;
            SettingsStatusText.Text = string.Empty;

            try
            {
                SettingsAutostartCheckBox.IsChecked = SettingsService.IsAutostartEnabled();
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
            {
                SettingsAutostartCheckBox.IsChecked = false;
                SettingsAutostartCheckBox.IsEnabled = false;
                SettingsStatusText.Text = $"Status autostart tidak dapat dibaca: {ex.Message}";
            }

            UpdateAdhanVolumeText();
        }

        private async void DetectLocationButton_Click(object sender, RoutedEventArgs e)
        {
            DetectLocationButton.IsEnabled = false;
            SettingsStatusText.Text = "Meminta lokasi dari Windows...";
            try
            {
                PrayerLocation location = await _locationService.DetectAsync();
                var locations = SettingsService.GetLocations().ToList();
                locations.Add(location);
                SettingsLocationComboBox.ItemsSource = locations;
                SettingsLocationComboBox.SelectedItem = location;
                SettingsStatusText.Text =
                    $"Lokasi: {location.Latitude:F4}, {location.Longitude:F4}; zona waktu mengikuti Windows. Klik Simpan pengaturan.";
            }
            catch (UnauthorizedAccessException ex)
            {
                SettingsStatusText.Text = $"Izin lokasi ditolak: {ex.Message}";
            }
            catch (TimeoutException ex)
            {
                SettingsStatusText.Text = ex.Message;
            }
            catch (InvalidOperationException ex)
            {
                SettingsStatusText.Text = ex.Message;
            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                SettingsStatusText.Text = $"Layanan lokasi Windows gagal: {ex.Message}";
            }
            finally
            {
                DetectLocationButton.IsEnabled = true;
            }
        }

        private void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            if (SettingsLocationComboBox.SelectedItem is not PrayerLocation location ||
                SettingsCalculationMethodComboBox.SelectedItem is not PrayerCalculationMethod method ||
                SettingsThemeComboBox.SelectedItem is not string theme ||
                !int.TryParse(SettingsHijriAdjustmentComboBox.SelectedValue?.ToString(),
                    NumberStyles.Integer, CultureInfo.InvariantCulture, out int adjustmentDays))
            {
                SettingsStatusText.Text = "Pilih kota, metode hitung, koreksi Hijriah, dan tema.";
                return;
            }

            var settings = SettingsService.Load();
            settings.LocationName = location.Name;
            settings.Latitude = location.Latitude;
            settings.Longitude = location.Longitude;
            settings.TimeZoneId = location.TimeZoneId;
            settings.CalculationMethodName = method.Id;
            settings.HijriDateAdjustmentDays = adjustmentDays;
            settings.CalendarTheme = theme;
            settings.AdhanVolume = AdhanVolumeSlider.Value / 100;
            settings.ArabicFontSize = ArabicFontSizeSlider.Value;
            settings.TranslationFontSize = TranslationFontSizeSlider.Value;
            settings.KahfAutoPlayEnabled = SettingsKahfAutoplayCheckBox.IsChecked == true;
            settings.AdhanAudioPath = AdhanAudioPathTextBox.Text.Trim();
            settings.SubuhAdhanAudioPath = SubuhAdhanAudioPathTextBox.Text.Trim();

            try
            {
                SettingsService.Save(settings);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                SettingsStatusText.Text = $"Pengaturan gagal disimpan: {ex.Message}";
                return;
            }

            ApplyApplicationTheme(settings.CalendarTheme);
            ArabicFontSize = settings.ArabicFontSize;
            TranslationFontSize = settings.TranslationFontSize;
            SettingsSaved?.Invoke(this, EventArgs.Empty);
            SettingsStatusText.Text = "Pengaturan tersimpan.";

            try
            {
                SettingsService.SetAutostart(SettingsAutostartCheckBox.IsChecked == true);
            }
            catch (Exception ex) when (
                ex is UnauthorizedAccessException or System.Security.SecurityException or InvalidOperationException)
            {
                SettingsStatusText.Text = $"Pengaturan tersimpan, tetapi autostart gagal: {ex.Message}";
            }
        }

        private void AdhanVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateAdhanVolumeText();
        }

        private void UpdateAdhanVolumeText()
        {
            if (AdhanVolumeText != null)
            {
                AdhanVolumeText.Text = $"{Math.Round(AdhanVolumeSlider.Value):0}%";
            }
        }

        private void ArabicFontSizeSlider_ValueChanged(
            object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateQuranFontSizeTexts();
        }

        private void TranslationFontSizeSlider_ValueChanged(
            object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateQuranFontSizeTexts();
        }

        private void UpdateQuranFontSizeTexts()
        {
            if (ArabicFontSizeText != null && ArabicFontSizeSlider != null)
            {
                ArabicFontSizeText.Text = $"{ArabicFontSizeSlider.Value:0} pt";
            }

            if (TranslationFontSizeText != null && TranslationFontSizeSlider != null)
            {
                TranslationFontSizeText.Text = $"{TranslationFontSizeSlider.Value:0} pt";
            }
        }

        private void SelectAdhanFileButton_Click(object sender, RoutedEventArgs e)
        {
            SelectAdhanFile(AdhanAudioPathTextBox);
        }

        private void SelectSubuhAdhanFileButton_Click(object sender, RoutedEventArgs e)
        {
            SelectAdhanFile(SubuhAdhanAudioPathTextBox);
        }

        private void ResetAdhanFileButton_Click(object sender, RoutedEventArgs e)
        {
            AdhanAudioPathTextBox.Clear();
        }

        private void ResetSubuhAdhanFileButton_Click(object sender, RoutedEventArgs e)
        {
            SubuhAdhanAudioPathTextBox.Clear();
        }

        private void SelectAdhanFile(TextBox target)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Pilih file suara adzan",
                Filter = "File audio (*.mp3;*.wav)|*.mp3;*.wav|Semua file (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };

            if (dialog.ShowDialog(this) == true)
            {
                target.Text = dialog.FileName;
            }
        }

        private void TestAdhanButton_Click(object sender, RoutedEventArgs e)
        {
            bool started = _adhanPreviewService.PlayAdzan(false, AdhanAudioPathTextBox.Text,
                (float)(AdhanVolumeSlider.Value / 100));
            SettingsStatusText.Text = started ? "Adzan sedang diputar." : "Suara adzan gagal diputar.";
        }

        private void TestSubuhAdhanButton_Click(object sender, RoutedEventArgs e)
        {
            bool started = _adhanPreviewService.PlayAdzan(true, SubuhAdhanAudioPathTextBox.Text,
                (float)(AdhanVolumeSlider.Value / 100));
            SettingsStatusText.Text = started
                ? "Adzan Subuh sedang diputar."
                : "Suara adzan Subuh gagal diputar.";
        }

        private void StopAdhanPreviewButton_Click(object sender, RoutedEventArgs e)
        {
            _adhanPreviewService.Stop();
        }

        private void NavigateTo(string page)
        {
            bool isHome = page == "Home";
            bool isQuran = page == "Quran";
            bool isCalendar = page == "Calendar";
            bool isSettings = page == "Settings";
            bool isMurottal = page == "Murottal";
            bool isAbout = page == "About";
            HomePage.Visibility = isHome ? Visibility.Visible : Visibility.Collapsed;
            QuranPage.Visibility = isQuran ? Visibility.Visible : Visibility.Collapsed;
            CalendarPage.Visibility = isCalendar ? Visibility.Visible : Visibility.Collapsed;
            SettingsPage.Visibility = isSettings ? Visibility.Visible : Visibility.Collapsed;
            MurottalPage.Visibility = isMurottal ? Visibility.Visible : Visibility.Collapsed;
            AboutPage.Visibility = isAbout ? Visibility.Visible : Visibility.Collapsed;
            PlaceholderPage.Visibility = isHome || isQuran || isCalendar || isSettings || isMurottal || isAbout
                ? Visibility.Collapsed
                : Visibility.Visible;
            _murottalTimer.Stop();

            if (isHome)
            {
                _calendarTimer.Stop();
                UpdateHomePage();
                _homeTimer.Start();
                return;
            }

            _homeTimer.Stop();
            _calendarTimer.Stop();
            if (isQuran)
            {
                if (SurahListBox.Items.Count > 0 && SurahListBox.SelectedIndex < 0)
                {
                    int lastReadSurah = SettingsService.Load().LastReadSurahNumber;
                    SurahListBox.SelectedItem = _allSurahs.FirstOrDefault(surah => surah.Number == lastReadSurah)
                        ?? _allSurahs.FirstOrDefault();
                }
                else if (SurahListBox.SelectedItem is SurahData selectedSurah)
                {
                    var settings = SettingsService.Load();
                    if (settings.LastReadSurahNumber == selectedSurah.Number)
                    {
                        ScrollToVerse(selectedSurah.Verses.FirstOrDefault(
                            verse => verse.Number == settings.LastReadVerseNumber));
                    }
                }
                return;
            }

            if (isCalendar)
            {
                RefreshCalendarPage();
                _calendarTimer.Start();
                return;
            }

            if (isSettings)
            {
                LoadSettingsPage();
                return;
            }

            if (isMurottal)
            {
                if (MurottalSurahListBox.SelectedItem == null && _allSurahs.Count > 0)
                {
                    int lastReadSurah = SettingsService.Load().LastReadSurahNumber;
                    MurottalSurahListBox.SelectedItem =
                        _allSurahs.FirstOrDefault(surah => surah.Number == lastReadSurah)
                        ?? _allSurahs[0];
                }

                UpdateMurottalPlayback();
                _murottalTimer.Start();
                return;
            }

            _calendarTimer.Stop();
            if (isAbout)
            {
                return;
            }

            PlaceholderTitleText.Text = "EzPrayer";
        }

        private void HomeTimer_Tick(object? sender, EventArgs e)
        {
            UpdateHomePage();
        }

        private void CalendarTimer_Tick(object? sender, EventArgs e)
        {
            var now = _prayerService.GetCurrentTime();
            var settings = SettingsService.Load();
            CalendarTodayText.Text = now.ToString("dddd, d MMMM yyyy", CultureInfo.GetCultureInfo("id-ID"));
            CalendarClockText.Text = now.ToString("HH:mm:ss");

            if (_calendarTodayDate != now.Date ||
                _calendarAdjustmentDays != settings.HijriDateAdjustmentDays)
            {
                RenderCalendarPage(now, settings);
            }
        }

        private void RefreshCalendarPage()
        {
            var now = _prayerService.GetCurrentTime();
            var settings = SettingsService.Load();
            if (!_calendarInitialized)
            {
                _calendarMonth = new DateTime(now.Year, now.Month, 1);
                _selectedCalendarDate = now.Date;
                _calendarInitialized = true;
            }

            CalendarTodayText.Text = now.ToString("dddd, d MMMM yyyy", CultureInfo.GetCultureInfo("id-ID"));
            CalendarClockText.Text = now.ToString("HH:mm:ss");
            RenderCalendarPage(now, settings);
        }

        private void RenderCalendarPage(DateTime now, AppSettings settings)
        {
            _calendarTodayDate = now.Date;
            _calendarAdjustmentDays = settings.HijriDateAdjustmentDays;
            var culture = CultureInfo.GetCultureInfo("id-ID");
            CalendarMonthText.Text = _calendarMonth.ToString("MMMM yyyy", culture);
            CalendarHijriMonthText.Text = GetHijriMonthRange(_calendarMonth, _calendarAdjustmentDays);
            CalendarSelectedDateText.Text =
                $"{_selectedCalendarDate.ToString("dddd, d MMMM yyyy", culture)}  •  " +
                $"Hijriah: {FormatHijriDate(_selectedCalendarDate, _calendarAdjustmentDays)}";

            int daysInMonth = DateTime.DaysInMonth(_calendarMonth.Year, _calendarMonth.Month);
            int leadingDays = ((int)new DateTime(_calendarMonth.Year, _calendarMonth.Month, 1).DayOfWeek + 6) % 7;
            var days = new List<CalendarDayItem>(42);
            var hijriCalendar = new UmAlQuraCalendar();

            for (int index = 0; index < 42; index++)
            {
                int dayNumber = index - leadingDays + 1;
                if (dayNumber < 1 || dayNumber > daysInMonth)
                {
                    days.Add(new CalendarDayItem());
                    continue;
                }

                var date = new DateTime(_calendarMonth.Year, _calendarMonth.Month, dayNumber);
                string hijriDay = TryGetAdjustedHijriDate(date, _calendarAdjustmentDays, hijriCalendar,
                    out var adjustedDate)
                    ? hijriCalendar.GetDayOfMonth(adjustedDate).ToString(CultureInfo.InvariantCulture)
                    : string.Empty;
                days.Add(new CalendarDayItem
                {
                    Date = date,
                    DayNumber = dayNumber.ToString(CultureInfo.InvariantCulture),
                    HijriDay = hijriDay,
                    IsSelectable = true,
                    IsToday = date == now.Date,
                    IsSelected = date == _selectedCalendarDate
                });
            }

            CalendarDaysList.ItemsSource = days;
        }

        private static string GetHijriMonthRange(DateTime month, int adjustmentDays)
        {
            var calendar = new UmAlQuraCalendar();
            if (!TryGetAdjustedHijriDate(month, adjustmentDays, calendar, out var start) ||
                !TryGetAdjustedHijriDate(month.AddMonths(1).AddDays(-1), adjustmentDays, calendar, out var end))
            {
                return "Tanggal Hijriah tidak tersedia";
            }

            string firstMonth = $"{HijriMonthNames[calendar.GetMonth(start) - 1]} {calendar.GetYear(start)} H";
            string lastMonth = $"{HijriMonthNames[calendar.GetMonth(end) - 1]} {calendar.GetYear(end)} H";
            return firstMonth == lastMonth ? firstMonth : $"{firstMonth} – {lastMonth}";
        }

        private void PreviousMonthButton_Click(object sender, RoutedEventArgs e)
        {
            ChangeCalendarMonth(-1);
        }

        private void NextMonthButton_Click(object sender, RoutedEventArgs e)
        {
            ChangeCalendarMonth(1);
        }

        private void ChangeCalendarMonth(int monthDelta)
        {
            try
            {
                _calendarMonth = _calendarMonth.AddMonths(monthDelta);
            }
            catch (ArgumentOutOfRangeException)
            {
                return;
            }

            int day = Math.Min(_selectedCalendarDate.Day,
                DateTime.DaysInMonth(_calendarMonth.Year, _calendarMonth.Month));
            _selectedCalendarDate = new DateTime(_calendarMonth.Year, _calendarMonth.Month, day);
            RefreshCalendarPage();
        }

        private void CalendarTodayButton_Click(object sender, RoutedEventArgs e)
        {
            var now = _prayerService.GetCurrentTime().Date;
            _calendarMonth = new DateTime(now.Year, now.Month, 1);
            _selectedCalendarDate = now;
            RefreshCalendarPage();
        }

        private void CalendarDayButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { DataContext: CalendarDayItem { Date: not null } day })
            {
                _selectedCalendarDate = day.Date.Value;
                RefreshCalendarPage();
            }
        }

        private void ApplyApplicationTheme(string theme)
        {
            var colors = theme switch
            {
                "Hijau" => (
                    Color.FromRgb(15, 29, 26), Color.FromRgb(17, 43, 37),
                    Color.FromRgb(20, 91, 73), Color.FromRgb(13, 92, 75), Color.FromRgb(16, 23, 32)),
                "Ungu" => (
                    Color.FromRgb(25, 20, 34), Color.FromRgb(35, 27, 49),
                    Color.FromRgb(78, 50, 124), Color.FromRgb(76, 49, 126), Color.FromRgb(16, 23, 32)),
                "Gelap" => (
                    Color.FromRgb(13, 15, 19), Color.FromRgb(23, 26, 32),
                    Color.FromRgb(48, 57, 70), Color.FromRgb(48, 57, 70), Color.FromRgb(11, 14, 19)),
                _ => (
                    Color.FromRgb(16, 23, 32), Color.FromRgb(20, 30, 43),
                    Color.FromRgb(28, 78, 120), Color.FromRgb(18, 83, 132), Color.FromRgb(16, 23, 32))
            };
            AppRootGrid.Background = new SolidColorBrush(colors.Item1);
            SidebarBorder.Background = new SolidColorBrush(colors.Item2);
            HomeHighlightBorder.Background = new SolidColorBrush(colors.Item3);
            CalendarBackgroundBorder.Background = new LinearGradientBrush(colors.Item4, colors.Item5, 45);
            MurottalBackgroundBorder.Background = new LinearGradientBrush(colors.Item4, colors.Item5, 45);
            MurottalPlayerCard.Background = new SolidColorBrush(colors.Item2);
            MurottalSurahListBox.Background = new SolidColorBrush(colors.Item2);
            AboutBackgroundBorder.Background = new LinearGradientBrush(colors.Item4, colors.Item5, 45);
            AboutCardBorder.Background = new SolidColorBrush(colors.Item2);

            var aboutAccentColor = theme switch
            {
                "Hijau" => Color.FromRgb(102, 220, 176),
                "Ungu" => Color.FromRgb(190, 160, 255),
                "Gelap" => Color.FromRgb(150, 190, 255),
                _ => Color.FromRgb(133, 200, 255)
            };
            var aboutAccentBrush = new SolidColorBrush(aboutAccentColor);
            AboutSloganText.Foreground = aboutAccentBrush;
            AboutFeaturesHeaderText.Foreground = aboutAccentBrush;
            AboutEmailHyperlink.Foreground = aboutAccentBrush;
            AboutStatusText.Foreground = aboutAccentBrush;
            AboutGitHubButton.Background = new SolidColorBrush(colors.Item3);
            AboutGitHubButton.Foreground = Brushes.White;
        }

        private void UpdateHomePage()
        {
            var now = _prayerService.GetCurrentTime();
            var settings = SettingsService.Load();
            string settingsFingerprint = string.Format(
                CultureInfo.InvariantCulture,
                "{0}|{1}|{2}|{3}|{4}|{5}",
                settings.LocationName,
                settings.Latitude,
                settings.Longitude,
                settings.TimeZoneId,
                settings.CalculationMethodName,
                settings.HijriDateAdjustmentDays);

            if (_homeScheduleDate != now.Date || _homeSettingsFingerprint != settingsFingerprint)
            {
                RefreshHomeSchedule(now, settings, settingsFingerprint);
            }

            var culture = CultureInfo.GetCultureInfo("id-ID");
            TodaySummaryText.Text = now.ToString("dddd, d MMMM yyyy", culture);
            TodayHijriText.Text = FormatHijriDate(now, settings.HijriDateAdjustmentDays);
            HomeCityText.Text = settings.LocationName;

            var nextPrayer = _todayPrayers.FirstOrDefault(prayer => prayer.Time > now);
            if (nextPrayer == null)
            {
                if (_tomorrowFajr == null || _tomorrowScheduleDate != now.Date.AddDays(1))
                {
                    _tomorrowFajr = _prayerService.GetTodayPrayers(now.Date.AddDays(1))
                        .First(prayer => prayer.Name == "Subuh");
                    _tomorrowScheduleDate = now.Date.AddDays(1);
                }

                nextPrayer = _tomorrowFajr;
            }

            NextPrayerNameText.Text = nextPrayer.Name;
            var remaining = nextPrayer.Time - now;
            NextPrayerCountdownText.Text =
                $"Pukul {nextPrayer.Time:HH:mm} • {FormatDuration(remaining)} lagi";
        }

        private void RefreshHomeSchedule(DateTime now, AppSettings settings, string settingsFingerprint)
        {
            _todayPrayers = _prayerService.GetTodayPrayers(now.Date)
                .Where(prayer => prayer.IsAdzan)
                .ToList();
            TodayPrayerList.ItemsSource = _todayPrayers;
            _homeScheduleDate = now.Date;
            _homeSettingsFingerprint = settingsFingerprint;
            _tomorrowFajr = null;
            HomeCityText.Text = settings.LocationName;
        }

        private static string FormatHijriDate(DateTime date, int adjustmentDays = 0)
        {
            var calendar = new UmAlQuraCalendar();
            if (!TryGetAdjustedHijriDate(date, adjustmentDays, calendar, out var adjustedDate))
            {
                return "Tanggal Hijriah tidak tersedia";
            }

            int month = calendar.GetMonth(adjustedDate);
            return $"{calendar.GetDayOfMonth(adjustedDate)} {HijriMonthNames[month - 1]} " +
                   $"{calendar.GetYear(adjustedDate)} H";
        }

        private static bool TryGetAdjustedHijriDate(
            DateTime date, int adjustmentDays, UmAlQuraCalendar calendar, out DateTime adjustedDate)
        {
            try
            {
                adjustedDate = date.AddDays(adjustmentDays);
            }
            catch (ArgumentOutOfRangeException)
            {
                adjustedDate = default;
                return false;
            }

            return adjustedDate.Date >= calendar.MinSupportedDateTime.Date &&
                   adjustedDate.Date <= calendar.MaxSupportedDateTime.Date;
        }

        private static string FormatDuration(TimeSpan duration)
        {
            return $"{(int)duration.TotalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}";
        }

        private void ContinueReadingButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateTo("Quran");
        }

        private void QuranSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (QuranSearchBox == null)
            {
                return;
            }

            string query = QuranSearchBox.Text.Trim();
            if (query.Length == 0)
            {
                SurahListBox.Visibility = Visibility.Visible;
                QuranSearchResultsList.Visibility = Visibility.Collapsed;
                QuranSearchResultsList.ItemsSource = null;
                QuranSearchStatusText.Text = string.Empty;
                return;
            }

            var results = new List<QuranSearchResult>();
            bool limitReached = false;
            foreach (var surah in _allSurahs)
            {
                string arabicName = surah.Name ?? string.Empty;
                string translatedName = surah.NameTranslations?.Id ?? string.Empty;
                if (arabicName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    translatedName.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(new QuranSearchResult(surah, null));
                }

                if (query.Length < 2)
                {
                    continue;
                }

                foreach (var verse in surah.Verses)
                {
                    string translation = verse.Translation ?? string.Empty;
                    string arabicText = verse.Text ?? string.Empty;
                    if (translation.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        arabicText.Contains(query, StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add(new QuranSearchResult(surah, verse));
                        if (results.Count >= 100)
                        {
                            limitReached = true;
                            break;
                        }
                    }
                }

                if (limitReached)
                {
                    break;
                }
            }

            SurahListBox.Visibility = Visibility.Collapsed;
            QuranSearchResultsList.Visibility = Visibility.Visible;
            QuranSearchResultsList.ItemsSource = results;
            QuranSearchStatusText.Text = query.Length < 2
                ? "Ketik minimal 2 karakter untuk mencari ayat."
                : results.Count == 0
                    ? "Tidak ada hasil."
                    : $"{results.Count} hasil{(limitReached ? " (maksimal 100)" : string.Empty)}.";
        }

        private void QuranSearchResultsList_SelectionChanged(
            object sender, SelectionChangedEventArgs e)
        {
            if (QuranSearchResultsList.SelectedItem is QuranSearchResult result)
            {
                QuranSearchResultsList.SelectedItem = null;
                OpenQuranLocation(result.Surah, result.Verse);
            }
        }

        private void OpenBookmarkButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { DataContext: QuranBookmarkItem item })
            {
                OpenQuranLocation(item.Surah, item.Verse);
            }
        }

        private void RemoveBookmarkButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { DataContext: QuranBookmarkItem item })
            {
                return;
            }

            var settings = SettingsService.Load();
            settings.QuranBookmarks.RemoveAll(bookmark =>
                bookmark.SurahNumber == item.SurahNumber &&
                bookmark.VerseNumber == item.VerseNumber);
            if (TrySaveQuranSettings(settings))
            {
                QuranStatusText.Text = "Bookmark dihapus.";
                RefreshBookmarks();
            }
        }

        private void BookmarkVerseButton_Click(object sender, RoutedEventArgs e)
        {
            if (SurahListBox.SelectedItem is not SurahData selectedSurah ||
                sender is not Button { DataContext: VerseData verse })
            {
                return;
            }

            var settings = SettingsService.Load();
            int existingIndex = settings.QuranBookmarks.FindIndex(bookmark =>
                bookmark.SurahNumber == selectedSurah.Number &&
                bookmark.VerseNumber == verse.Number);
            if (existingIndex >= 0)
            {
                settings.QuranBookmarks.RemoveAt(existingIndex);
            }
            else
            {
                settings.QuranBookmarks.Add(new QuranBookmark
                {
                    SurahNumber = selectedSurah.Number,
                    VerseNumber = verse.Number
                });
            }

            if (TrySaveQuranSettings(settings))
            {
                QuranStatusText.Text = existingIndex >= 0
                    ? "Bookmark dihapus."
                    : "Ayat ditambahkan ke bookmark.";
                RefreshBookmarks();
            }
        }

        private void MarkLastReadVerseButton_Click(object sender, RoutedEventArgs e)
        {
            if (SurahListBox.SelectedItem is not SurahData selectedSurah ||
                sender is not Button { DataContext: VerseData verse })
            {
                return;
            }

            var settings = SettingsService.Load();
            settings.LastReadSurahNumber = selectedSurah.Number;
            settings.LastReadVerseNumber = verse.Number;
            if (TrySaveQuranSettings(settings))
            {
                QuranStatusText.Text = $"Posisi terakhir disimpan: {selectedSurah.Name}, ayat {verse.Number}.";
            }
        }

        private void OpenQuranLocation(SurahData surah, VerseData? verse)
        {
            _pendingSurahToOpen = surah;
            _pendingVerseToOpen = verse;
            QuranSearchBox.Clear();
            NavigateTo("Quran");
            SurahListBox.SelectedItem = surah;

            if (SurahListBox.SelectedItem is SurahData selectedSurah &&
                selectedSurah.Number == surah.Number &&
                _pendingSurahToOpen != null)
            {
                CompletePendingQuranLocation(selectedSurah);
            }
        }

        private void SurahListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SurahListBox.SelectedItem is SurahData selectedSurah)
            {
                if (_audioService.State != PlaybackState.Stopped)
                {
                    _audioService.Stop();
                    _currentlyPlayingSurahName = null;
                    MurottalStatusText.Text = "Pemutaran dihentikan.";
                }

                PlayMurottalButton.Content = "▶ Putar Murottal";

                SurahTitle.Text =
                    $"{selectedSurah.Name} ({selectedSurah.NameTranslations?.Id}) • {selectedSurah.Verses.Count} ayat";
                VersesList.ItemsSource = selectedSurah.Verses;

                if (selectedSurah.Number == 9)
                    BismillahText.Visibility = Visibility.Collapsed;
                else
                    BismillahText.Visibility = Visibility.Visible;

                _currentAudioUrl = GetAudioUrlFromJson(selectedSurah.Number);

                if (_pendingSurahToOpen?.Number == selectedSurah.Number)
                {
                    CompletePendingQuranLocation(selectedSurah);
                }
                else
                {
                    var settings = SettingsService.Load();
                    if (settings.LastReadSurahNumber != selectedSurah.Number)
                    {
                        settings.LastReadSurahNumber = selectedSurah.Number;
                        settings.LastReadVerseNumber = selectedSurah.Verses.FirstOrDefault()?.Number ?? 1;
                        TrySaveQuranSettings(settings);
                    }

                    var verseToRestore = selectedSurah.Verses.FirstOrDefault(
                        verse => verse.Number == settings.LastReadVerseNumber)
                        ?? selectedSurah.Verses.FirstOrDefault();
                    ScrollToVerse(verseToRestore);
                }

                RefreshBookmarks();
            }
        }

        private void CompletePendingQuranLocation(SurahData surah)
        {
            var settings = SettingsService.Load();
            settings.LastReadSurahNumber = surah.Number;
            VerseData? targetVerse = _pendingVerseToOpen
                ?? surah.Verses.FirstOrDefault();
            if (targetVerse != null)
            {
                settings.LastReadVerseNumber = targetVerse.Number;
            }

            TrySaveQuranSettings(settings);
            _pendingSurahToOpen = null;
            _pendingVerseToOpen = null;
            ScrollToVerse(targetVerse);
        }

        private bool TrySaveQuranSettings(AppSettings settings)
        {
            try
            {
                SettingsService.Save(settings);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                QuranStatusText.Text = $"Pengaturan Al-Quran gagal disimpan: {ex.Message}";
                return false;
            }
        }

        private void RefreshBookmarks()
        {
            var bookmarks = new List<QuranBookmarkItem>();
            var savedBookmarks = SettingsService.Load().QuranBookmarks;
            for (int index = savedBookmarks.Count - 1; index >= 0; index--)
            {
                var bookmark = savedBookmarks[index];
                var surah = _allSurahs.FirstOrDefault(item => item.Number == bookmark.SurahNumber);
                var verse = surah?.Verses.FirstOrDefault(item => item.Number == bookmark.VerseNumber);
                if (surah != null && verse != null)
                {
                    bookmarks.Add(new QuranBookmarkItem(surah, verse));
                }
            }

            BookmarksListBox.ItemsSource = bookmarks;
        }

        private void ScrollToVerse(VerseData? verse)
        {
            if (verse == null)
            {
                return;
            }

            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                VersesList.UpdateLayout();
                if (VersesList.ItemContainerGenerator.ContainerFromItem(verse) is FrameworkElement container)
                {
                    container.BringIntoView();
                }
            }));
        }

        private void MurottalSurahListBox_SelectionChanged(
            object sender, SelectionChangedEventArgs e)
        {
            if (MurottalSurahListBox.SelectedItem is not SurahData surah)
            {
                return;
            }

            if (_audioService.State != PlaybackState.Stopped)
            {
                _audioService.Stop();
            }

            _currentlyPlayingSurahName = null;
            _murottalAudioUrl = GetAudioUrlFromJson(surah.Number);
            MurottalNowPlayingText.Text = $"{surah.Name} ({surah.NameTranslations?.Id})";
            MurottalStatusText.Text = string.IsNullOrWhiteSpace(_murottalAudioUrl)
                ? "Tautan audio surah ini tidak tersedia."
                : string.Empty;
            UpdateMurottalPlayback();
        }

        private void MurottalPlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_audioService.State == PlaybackState.Playing)
            {
                _audioService.Pause();
                MurottalStatusText.Text = "Pemutaran dijeda.";
            }
            else if (_audioService.State == PlaybackState.Paused)
            {
                _audioService.Resume();
                MurottalStatusText.Text = "Murottal dilanjutkan.";
            }
            else if (string.IsNullOrWhiteSpace(_murottalAudioUrl))
            {
                MurottalStatusText.Text = "Pilih surah dengan tautan audio yang tersedia.";
            }
            else if (_audioService.PlayFromUrl(_murottalAudioUrl))
            {
                var surah = MurottalSurahListBox.SelectedItem as SurahData;
                _currentlyPlayingSurahName = surah?.Name ?? "Murottal";
                MurottalStatusText.Text = "Murottal sedang diputar.";
            }
            else
            {
                MurottalStatusText.Text = "Murottal gagal diputar. Periksa koneksi internet.";
            }

            UpdateMurottalPlayback();
        }

        private void MurottalStopButton_Click(object sender, RoutedEventArgs e)
        {
            _audioService.Stop();
            _currentlyPlayingSurahName = null;
            MurottalStatusText.Text = "Pemutaran dihentikan.";
            UpdateMurottalPlayback();
        }

        private void MurottalProgressSlider_ValueChanged(
            object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingMurottalSlider || !_audioService.CanSeek)
            {
                return;
            }

            if (!_audioService.Seek(TimeSpan.FromSeconds(e.NewValue)))
            {
                MurottalStatusText.Text = "Audio ini tidak mendukung pencarian durasi.";
            }
        }

        private void MurottalTimer_Tick(object? sender, EventArgs e)
        {
            if (_audioService.State == PlaybackState.Stopped && _currentlyPlayingSurahName != null)
            {
                _currentlyPlayingSurahName = null;
                MurottalStatusText.Text = "Pemutaran selesai.";
            }

            UpdateMurottalPlayback();
        }

        private void UpdateMurottalPlayback()
        {
            TimeSpan duration = _audioService.TotalTime;
            TimeSpan current = _audioService.CurrentTime;
            double maximum = Math.Max(1, duration.TotalSeconds);

            _isUpdatingMurottalSlider = true;
            MurottalProgressSlider.Maximum = maximum;
            MurottalProgressSlider.Value = Math.Clamp(current.TotalSeconds, 0, maximum);
            MurottalProgressSlider.IsEnabled = _audioService.CanSeek && duration > TimeSpan.Zero;
            _isUpdatingMurottalSlider = false;

            MurottalElapsedText.Text = FormatAudioTime(current);
            MurottalDurationText.Text = FormatAudioTime(duration);
            MurottalPlayPauseButton.Content = _audioService.State switch
            {
                PlaybackState.Playing => "⏸ Jeda",
                PlaybackState.Paused => "▶ Lanjut",
                _ => "▶ Putar"
            };
            PlayMurottalButton.Content = _audioService.State == PlaybackState.Stopped
                ? "▶ Putar Murottal"
                : "⏹ Stop Murottal";

            if (_currentlyPlayingSurahName != null &&
                _audioService.State != PlaybackState.Stopped)
            {
                MurottalNowPlayingText.Text = _currentlyPlayingSurahName;
            }
        }

        private static string FormatAudioTime(TimeSpan duration)
        {
            return $"{(int)duration.TotalMinutes:00}:{duration.Seconds:00}";
        }

        private void AudioService_PlaybackFailed(Exception exception)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _currentlyPlayingSurahName = null;
                MurottalStatusText.Text = $"Gagal memutar murottal: {exception.Message}";
                UpdateMurottalPlayback();
            }));
        }

        private void OpenGitHubButton_Click(object sender, RoutedEventArgs e)
        {
            const string repositoryUrl = "https://github.com/alazza15/ezprayer";
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = repositoryUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                AboutStatusText.Text = $"Tautan GitHub gagal dibuka: {ex.Message}";
            }
        }

        private void AboutEmailLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = e.Uri.AbsoluteUri,
                    UseShellExecute = true
                });
            }
            catch (Exception ex) when (
                ex is System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                AboutStatusText.Text = $"Aplikasi email gagal dibuka: {ex.Message}";
            }

            e.Handled = true;
        }

        private void PlayMurottalButton_Click(object sender, RoutedEventArgs e)
        {
            if (_audioService.State != PlaybackState.Stopped)
            {
                _audioService.Stop();
                _currentlyPlayingSurahName = null;
                MurottalStatusText.Text = "Pemutaran dihentikan.";
            }
            else if (string.IsNullOrEmpty(_currentAudioUrl))
            {
                MessageBox.Show("Link audio untuk surah ini tidak tersedia.");
                return;
            }
            else if (_audioService.PlayFromUrl(_currentAudioUrl))
            {
                var surah = SurahListBox.SelectedItem as SurahData;
                _currentlyPlayingSurahName = surah?.Name ?? "Murottal";
                MurottalStatusText.Text = "Murottal sedang diputar.";
            }

            UpdateMurottalPlayback();
        }

        // --- EVENT SAAT JENDELA DITUTUP ---
        private void Window_Closed(object sender, EventArgs e)
        {
            _homeTimer.Stop();
            _calendarTimer.Stop();
            _murottalTimer.Stop();
            _adhanPreviewService.Dispose();
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