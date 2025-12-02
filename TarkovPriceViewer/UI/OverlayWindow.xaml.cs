using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using TarkovPriceViewer.Models;
using TarkovPriceViewer.Services;

namespace TarkovPriceViewer.UI
{
    public partial class OverlayWindow : Window
    {
        private readonly ISettingsService _settingsService;
        private readonly IItemSnapshotService _itemSnapshotService;
        private readonly ITarkovDevCacheService _tarkovDevCacheService;

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
            IItemSnapshotService itemSnapshotService,
            ITarkovDevCacheService tarkovDevCacheService)
        {
            InitializeComponent();

            _settingsService = settingsService;
            _itemSnapshotService = itemSnapshotService;
            _tarkovDevCacheService = tarkovDevCacheService;

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

        public async Task ShowTestItemAsync()
        {
            const string testItemName = "Salewa";

            // Find the item in the current snapshot by name
            ItemSnapshot snapshot = _itemSnapshotService.FindByName(testItemName);

            if (snapshot == null)
            {
                // If the item is not found, do not break the overlay; just leave the fields empty
                return;
            }

            // Name
            if (ResultItemNameText != null)
            {
                ResultItemNameText.Text = snapshot.Name;
            }

            // Icon using the new icon cache service
            if (!string.IsNullOrWhiteSpace(snapshot.Id) && !string.IsNullOrWhiteSpace(snapshot.IconLink) && ItemImage != null)
            {
                BitmapSource icon = await _tarkovDevCacheService
                    .GetItemIconAsync(snapshot.Id, snapshot.IconLink)
                    .ConfigureAwait(true);

                if (icon != null)
                {
                    ItemImage.Source = icon;
                    ItemImage.Visibility = Visibility.Visible;
                }
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
