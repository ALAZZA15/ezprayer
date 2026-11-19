using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using EzPrayer.Services;
using Hardcodet.Wpf.TaskbarNotification;

namespace EzPrayer.Views
{
    public partial class WidgetWindow : Window
    {
        private TaskbarIcon? _trayIcon;
        private SchedulerService? _schedulerService;
        private AudioService? _audioService;
        private MainWindow? _mainWindow;
        private KahfSchedulerService? _kahfSchedulerService;
        private readonly WidgetViewModel _widgetViewModel;

        public WidgetWindow()
        {
            InitializeComponent();
            _widgetViewModel = new WidgetViewModel();
            DataContext = _widgetViewModel;

            _audioService = new AudioService();
            _schedulerService = new SchedulerService(_audioService);

            _kahfSchedulerService = new KahfSchedulerService(new QuranAudioService(), ShowKahfNotification);

            WindowStartupLocation = WindowStartupLocation.Manual;
            this.Loaded += WidgetWindow_Loaded;
        }

        private void WidgetWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var workArea = SystemParameters.WorkArea;
            
            this.Left = workArea.Right - this.ActualWidth - 20;
            this.Top = workArea.Top + 0;

            WallpaperHost.AttachToWallpaper(this);
            if (_trayIcon == null)
            {
                SetupTrayIcon();
            }

            _schedulerService?.Start();
            _kahfSchedulerService?.Start();
        }

        private void SetupTrayIcon()
        {
            _trayIcon = new TaskbarIcon();
            _trayIcon.ToolTipText = "EzPrayer - Jadwal Sholat";

            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Images", "EzPrayer.ico");
            if (File.Exists(iconPath))
            {
                var iconImage = new BitmapImage();
                iconImage.BeginInit();
                iconImage.UriSource = new Uri(iconPath);
                iconImage.CacheOption = BitmapCacheOption.OnLoad;
                iconImage.EndInit();
                iconImage.Freeze();
                _trayIcon.IconSource = iconImage;
            }
            else
            {
                Console.WriteLine($"Ikon EzPrayer tidak ditemukan: {iconPath}");
                _trayIcon.IconSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                    System.Drawing.SystemIcons.Application.Handle,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
            }

            _trayIcon.TrayLeftMouseUp += (s, e) =>
            {
                var mainWindow = GetOrCreateMainWindow();
                mainWindow.Show();
                mainWindow.Activate();
            };

            var contextMenu = new System.Windows.Controls.ContextMenu();

            var stopAdzanMenuItem = new System.Windows.Controls.MenuItem { Header = "Stop Adzan" };
            stopAdzanMenuItem.Click += (s, e) =>
            {
                _audioService?.Stop();
            };
            contextMenu.Items.Add(stopAdzanMenuItem);

            var exitMenuItem = new System.Windows.Controls.MenuItem { Header = "Keluar (Exit)" };
            exitMenuItem.Click += (s, e) =>
            {
                _schedulerService?.Stop();
                _kahfSchedulerService?.Stop();
                _trayIcon?.Dispose();
                Application.Current.Shutdown();
            };

            contextMenu.Items.Add(exitMenuItem);
            _trayIcon.ContextMenu = contextMenu;
        }

        private MainWindow GetOrCreateMainWindow()
        {
            if (_mainWindow == null || !_mainWindow.IsVisible)
            {
                _mainWindow = new MainWindow();
                _mainWindow.SettingsSaved += MainWindow_SettingsSaved;
            }

            return _mainWindow;
        }

        private void MainWindow_SettingsSaved(object? sender, EventArgs e)
        {
            _widgetViewModel.RefreshSchedule();
            _schedulerService?.RefreshSchedule();
        }

        private void ShowKahfNotification(string title, string message)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => ShowKahfNotification(title, message)));
                return;
            }

            if (_trayIcon == null)
            {
                Console.WriteLine($"[{title}] {message}");
                return;
            }

            var icon = message.Contains("tidak bisa diputar", StringComparison.OrdinalIgnoreCase) ||
                       message.Contains("tidak tersedia", StringComparison.OrdinalIgnoreCase)
                ? BalloonIcon.Error
                : BalloonIcon.Info;
            _trayIcon.ShowBalloonTip(title, message, icon);
        }

        protected override void OnClosed(EventArgs e)
        {
            _schedulerService?.Stop();
            _kahfSchedulerService?.Stop();
            _trayIcon?.Dispose();
            base.OnClosed(e);
        }
    }
}