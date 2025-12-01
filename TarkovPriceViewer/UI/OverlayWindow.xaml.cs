using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using TarkovPriceViewer.Models;
using TarkovPriceViewer.Services;

namespace TarkovPriceViewer.UI
{
    public partial class OverlayWindow : Window
    {
        private readonly ISettingsService _settingsService;
        private readonly ITarkovDataService _tarkovDataService;
        private readonly ITarkovTrackerService _tarkovTrackerService;

        // Debug flag: when true, do NOT auto-hide on mouse leave (useful while debugging overlay)
        public bool DebugDisableAutoHideOnLeave { get; set; }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        public OverlayWindow(
            ISettingsService settingsService,
            ITarkovDataService tarkovDataService,
            ITarkovTrackerService tarkovTrackerService)
        {
            InitializeComponent();

            _settingsService = settingsService;
            _tarkovDataService = tarkovDataService;
            _tarkovTrackerService = tarkovTrackerService;

            // For debugging: keep overlay open even when mouse leaves
            DebugDisableAutoHideOnLeave = true;

            var opacity = _settingsService.Settings.OverlayOpacity;
            if (opacity < 0)
            {
                opacity = 0;
            }

            if (opacity > 100)
            {
                opacity = 100;
            }

            Opacity = opacity * 0.01;
        }

        public async void SimulateScanHardcodedItemAsync()
        {
            string itemName = "Bolts";
            string baseText = $"Scanning {itemName}";

            int[] fakeOcrRangeMs = { 100, 3000 }; // [min, max] fake OCR time
            const int frameDelayMs = 250;          // change this value for faster/slower animation

            int[] pattern = { 3, 1, 2 };  // ..., ., ..
            int step = 0;

            var rnd = new Random();
            int fakeOcrProcessingMs = rnd.Next(fakeOcrRangeMs[0], fakeOcrRangeMs[1] + 1);

            // Estado inicial: mostrar panel de scanning, ocultar resto
            ScanningPanel.Visibility = Visibility.Visible;
            ErrorPanel.Visibility = Visibility.Collapsed;
            ResultPanel.Visibility = Visibility.Collapsed;

            Stopwatch sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < fakeOcrProcessingMs)
            {
                int dotCount = pattern[step % pattern.Length];
                string dots = new string('.', dotCount);
                ScanningTextBlock.Text = baseText + dots;
                step++;

                await Task.Delay(frameDelayMs).ConfigureAwait(true);
            }

            var data = _tarkovDataService.Data;
            if (data == null || data.items == null)
            {
                ScanningPanel.Visibility = Visibility.Collapsed;
                ErrorPanel.Visibility = Visibility.Visible;
                ResultPanel.Visibility = Visibility.Collapsed;
                ErrorText.Text = $"{baseText}...\r\n(Items data not loaded yet)";
                return;
            }

            TarkovDevAPI.Item item = null;
            try
            {
                item = data.items.FirstOrDefault(i => string.Equals(i.name, itemName, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                item = null;
            }

            if (item == null)
            {
                ScanningPanel.Visibility = Visibility.Collapsed;
                ErrorPanel.Visibility = Visibility.Visible;
                ResultPanel.Visibility = Visibility.Collapsed;
                ErrorText.Text = $"Scanning {itemName}...\r\nItem not found in TarkovDev data.";
                return;
            }

            // Mostrar resultado
            ScanningPanel.Visibility = Visibility.Collapsed;
            ErrorPanel.Visibility = Visibility.Collapsed;
            ResultPanel.Visibility = Visibility.Visible;

            ResultItemNameText.Text = item.name;

            if (item.lastLowPrice != null)
            {
                ResultLastPriceText.Text = $"Last price: {item.lastLowPrice.Value:N0}₽";
            }
            else
            {
                ResultLastPriceText.Text = string.Empty;
            }

            if (item.avg24hPrice != null && item.avg24hPrice.Value > 0)
            {
                ResultAvgPriceText.Text = $"24h avg: {item.avg24hPrice.Value:N0}₽";
            }
            else
            {
                ResultAvgPriceText.Text = string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(item.link))
            {
                ResultLinkLabelText.Visibility = Visibility.Visible;
                ResultLinkText.Visibility = Visibility.Visible;
                ResultLinkText.Text = item.link;
            }
            else
            {
                ResultLinkLabelText.Visibility = Visibility.Collapsed;
                ResultLinkText.Visibility = Visibility.Collapsed;
                ResultLinkText.Text = string.Empty;
            }
        }

        public void MoveToCursor()
        {
            if (!GetCursorPos(out POINT p))
            {
                return;
            }

            const int offsetX = -16;
            const int offsetY = -16;

            var hwndSource = (HwndSource)PresentationSource.FromVisual(this);
            if (hwndSource?.CompositionTarget != null)
            {
                var transform = hwndSource.CompositionTarget.TransformFromDevice;
                var dipPoint = transform.Transform(new Point(p.X, p.Y));
                Left = dipPoint.X + offsetX;
                Top = dipPoint.Y + offsetY;
            }
            else
            {
                Left = p.X + offsetX;
                Top = p.Y + offsetY;
            }
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            if (!DebugDisableAutoHideOnLeave)
            {
                Hide();
            }
        }
    }
}
