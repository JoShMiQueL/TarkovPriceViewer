using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Interop;
using TarkovPriceViewer.Services;
using TarkovPriceViewer.UI;
using TarkovPriceViewer.Utils;

namespace TarkovPriceViewer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ISettingsService _settingsService;
        private readonly ITarkovDataService _tarkovDataService;
        private readonly ITarkovTrackerService _tarkovTrackerService;

        private const int HotkeyIdShowOverlay = 1;
        private const int HotkeyIdHideOverlay = 2;
        private HwndSource _hwndSource;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public MainWindow(
            ISettingsService settingsService,
            ITarkovDataService tarkovDataService,
            ITarkovTrackerService tarkovTrackerService)
        {
            InitializeComponent();

            _settingsService = settingsService;
            _tarkovDataService = tarkovDataService;
            _tarkovTrackerService = tarkovTrackerService;

            _settingsService.Load();

            _ = _tarkovDataService.UpdateItemListAPIAsync();
            _ = _tarkovTrackerService.UpdateTarkovTrackerAPI();

            var version = VersionHelper.GetDisplayVersion();
            Title = $"Tarkov Price Viewer v{version}";
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            _hwndSource = (HwndSource)PresentationSource.FromVisual(this);
            if (_hwndSource != null)
            {
                _hwndSource.AddHook(WndProc);

                var showKey = _settingsService.Settings.ShowOverlayKey;
                var hideKey = _settingsService.Settings.HideOverlayKey;

                if (Enum.TryParse(showKey, out Key showParsed))
                {
                    var vk = (uint)KeyInterop.VirtualKeyFromKey(showParsed);
                    RegisterHotKey(_hwndSource.Handle, HotkeyIdShowOverlay, 0, vk);
                }

                if (Enum.TryParse(hideKey, out Key hideParsed))
                {
                    var vk = (uint)KeyInterop.VirtualKeyFromKey(hideParsed);
                    RegisterHotKey(_hwndSource.Handle, HotkeyIdHideOverlay, 0, vk);
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_hwndSource != null)
            {
                UnregisterHotKey(_hwndSource.Handle, HotkeyIdShowOverlay);
                UnregisterHotKey(_hwndSource.Handle, HotkeyIdHideOverlay);
                _hwndSource.RemoveHook(WndProc);
            }

            base.OnClosed(e);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                if (id == HotkeyIdShowOverlay)
                {
                    ShowOverlay();
                    handled = true;
                }
                else if (id == HotkeyIdHideOverlay)
                {
                    HideOverlay();
                    handled = true;
                }
            }

            return IntPtr.Zero;
        }

        private void ShowOverlay()
        {
            var overlay = App.Host.Services.GetService(typeof(OverlayWindow)) as OverlayWindow;
            if (overlay == null)
            {
                return;
            }

            if (!overlay.IsVisible)
            {
                overlay.Show();
            }

            overlay.Topmost = true;
            overlay.Activate();
        }

        private void HideOverlay()
        {
            var overlay = App.Host.Services.GetService(typeof(OverlayWindow)) as OverlayWindow;
            if (overlay == null)
            {
                return;
            }

            if (overlay.IsVisible)
            {
                overlay.Hide();
            }
        }
    }
}