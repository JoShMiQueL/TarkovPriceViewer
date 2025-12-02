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

        public async Task ShowTestItemAsync(string id = null, string name = null, string shortName = null)
        {
            const string defaultTestItemName = "Bolts";

            ItemSnapshot snapshot = null;

            // Priority: id -> shortName -> name -> default test name
            if (!string.IsNullOrWhiteSpace(id))
            {
                snapshot = _itemSnapshotService.GetById(id);
            }

            if (snapshot == null && !string.IsNullOrWhiteSpace(shortName))
            {
                snapshot = _itemSnapshotService.FindByShortName(shortName);
            }

            if (snapshot == null && !string.IsNullOrWhiteSpace(name))
            {
                snapshot = _itemSnapshotService.FindByName(name);
            }

            if (snapshot == null)
            {
                snapshot = _itemSnapshotService.FindByName(defaultTestItemName);
            }

            if (snapshot == null)
            {
                // If the item is not found, do not break the overlay; just leave the fields empty
                return;
            }

            snapshot.LogDebug();

            // Name
            if (ResultItemNameText != null)
            {
                // Include slots information in the title, e.g. "Salewa (3 Slots)"
                int slots = snapshot.Slots;
                if (slots > 1)
                {
                    ResultItemNameText.Text = $"{snapshot.Name} ({slots} Slots)";
                }
                else
                {
                    ResultItemNameText.Text = snapshot.Name;
                }
            }

            int snapshotSlots = snapshot.Slots;

            // Last price (total and per slot if available)
            if (ResultLastPriceText != null)
            {
                if (snapshot.LastLowPrice.HasValue)
                {
                    string text = $"{snapshot.LastLowPrice.Value:N0}₽";

                    if (snapshot.PricePerSlot.HasValue && snapshotSlots > 0)
                    {
                        int perSlot = (int)Math.Round(snapshot.PricePerSlot.Value, MidpointRounding.AwayFromZero);
                        text = $"{snapshot.LastLowPrice.Value:N0}₽ ({perSlot:N0}₽)";
                    }

                    ResultLastPriceText.Text = text;
                }
                else
                {
                    ResultLastPriceText.Text = string.Empty;
                }
            }

            // 24h average price (total and per slot if available)
            if (ResultAvgPriceText != null)
            {
                if (snapshot.Avg24hPrice.HasValue && snapshot.Avg24hPrice.Value > 0)
                {
                    string text = $"{snapshot.Avg24hPrice.Value:N0}₽";

                    if (snapshotSlots > 0)
                    {
                        double perSlotAvg = (double)snapshot.Avg24hPrice.Value / snapshotSlots;
                        int perSlot = (int)Math.Round(perSlotAvg, MidpointRounding.AwayFromZero);
                        text = $"{snapshot.Avg24hPrice.Value:N0}₽ ({perSlot:N0}₽)";
                    }

                    ResultAvgPriceText.Text = text;
                }
                else
                {
                    ResultAvgPriceText.Text = string.Empty;
                }
            }

            // Profit Flea vs Trader (difference total; positive means Flea is better)
            if (ResultProfitFleaVsTraderText != null)
            {
                if (snapshot.FleaNetPrice.HasValue && snapshot.BestTraderPrice.HasValue)
                {
                    int diff = snapshot.FleaNetPrice.Value - snapshot.BestTraderPrice.Value;
                    string sign = diff > 0 ? "+" : string.Empty;

                    string text = $"{sign}{diff:N0}₽";

                    if (snapshotSlots > 0)
                    {
                        double perSlotDiff = (double)diff / snapshotSlots;
                        int perSlot = (int)Math.Round(perSlotDiff, MidpointRounding.AwayFromZero);
                        text = $"{text} ({sign}{perSlot:N0}₽)";
                    }

                    ResultProfitFleaVsTraderText.Text = text;
                }
                else
                {
                    ResultProfitFleaVsTraderText.Text = string.Empty;
                }
            }

            // Preferred sell target (Flea / trader name / unknown)
            if (ResultSellToText != null)
            {
                if (!string.IsNullOrWhiteSpace(snapshot.PreferredSellTarget))
                {
                    ResultSellToText.Text = snapshot.PreferredSellTarget;
                }
                else if (!string.IsNullOrWhiteSpace(snapshot.BestTraderName))
                {
                    ResultSellToText.Text = snapshot.BestTraderName;
                }
                else
                {
                    ResultSellToText.Text = string.Empty;
                }
            }

            if (ResultBestTraderLabelText != null)
            {
                if (!string.IsNullOrWhiteSpace(snapshot.BestTraderName))
                {
                    ResultBestTraderLabelText.Text = snapshot.BestTraderName;
                }
            }

            if (ResultBestTraderText != null)
            {
                if (snapshot.BestTraderPrice.HasValue)
                {
                    string text = $"{snapshot.BestTraderPrice.Value:N0}₽";

                    if (snapshotSlots > 0)
                    {
                        double perSlotTrader = (double)snapshot.BestTraderPrice.Value / snapshotSlots;
                        int perSlot = (int)Math.Round(perSlotTrader, MidpointRounding.AwayFromZero);
                        text = $"{text} ({perSlot:N0}₽)";
                    }

                    ResultBestTraderText.Text = text;
                }
                else
                {
                    ResultBestTraderText.Text = string.Empty;
                }
            }

            // Trader icon using cache service
            if (!string.IsNullOrWhiteSpace(snapshot.BestTraderName) && TraderImageBrush != null)
            {
                BitmapSource traderIcon = await _tarkovDevCacheService
                    .GetTraderIconAsync(snapshot.BestTraderName, snapshot.BestTraderImageLink)
                    .ConfigureAwait(true);

                if (traderIcon != null)
                {
                    TraderImageBrush.ImageSource = traderIcon;
                }
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
