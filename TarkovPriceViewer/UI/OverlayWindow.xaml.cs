using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using TarkovPriceViewer.Services;

namespace TarkovPriceViewer.UI
{
    public partial class OverlayWindow : Window
    {
        private readonly ISettingsService _settingsService;
        private readonly ITarkovDataService _tarkovDataService;
        private readonly ITarkovTrackerService _tarkovTrackerService;

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

            var opacity = _settingsService.Settings.OverlayOpacity;
            if (opacity < 0) opacity = 0;
            if (opacity > 100) opacity = 100;
            Opacity = opacity * 0.01;
        }

        public void MoveToCursor()
        {
            if (!GetCursorPos(out POINT p))
            {
                return;
            }
            // Small negative offset so the cursor queda dentro del tooltip
            const int offsetX = -1;
            const int offsetY = -1;

            // Convert from device pixels (Win32) to WPF units (DIPs)
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
            Hide();
        }
    }
}
