using System;
using System.Windows;
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
        
        // Tambahan untuk Murottal Al-Kahf
        private KahfSchedulerService? _kahfSchedulerService; 

        public WidgetWindow()
        {
            InitializeComponent();
            DataContext = new WidgetViewModel();

            // Scheduler Adzan
            _audioService = new AudioService();
            _schedulerService = new SchedulerService(_audioService);
            _schedulerService.Start();

            // Scheduler Murottal Al-Kahf
            _kahfSchedulerService = new KahfSchedulerService(new QuranAudioService());
            _kahfSchedulerService.Start();

            WindowStartupLocation = WindowStartupLocation.Manual;
            this.Loaded += WidgetWindow_Loaded;
        }

        private void WidgetWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var workArea = SystemParameters.WorkArea;
            
            this.Left = workArea.Right - this.ActualWidth - 20;
            this.Top = workArea.Top + 0;

            SetupTrayIcon();
        }

        private void SetupTrayIcon()
        {
            _trayIcon = new TaskbarIcon();
            _trayIcon.ToolTipText = "EzPrayer - Jadwal Sholat";
            
            _trayIcon.IconSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                System.Drawing.SystemIcons.Application.Handle,
                Int32Rect.Empty,
                System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());

            // Buka MainWindow saat klik kiri ikon tray
            _trayIcon.TrayLeftMouseUp += (s, e) =>
            {
                if (_mainWindow == null || !_mainWindow.IsVisible)
                {
                    _mainWindow = new MainWindow();
                    _mainWindow.Show();
                }
                else
                {
                    _mainWindow.Activate();
                }
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
                _kahfSchedulerService?.Stop(); // Matikan juga scheduler Al-Kahf
                _trayIcon?.Dispose();
                Application.Current.Shutdown();
            };
            
            contextMenu.Items.Add(exitMenuItem);
            _trayIcon.ContextMenu = contextMenu;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            WallpaperHost.AttachToWallpaper(this);
        }
    }
}