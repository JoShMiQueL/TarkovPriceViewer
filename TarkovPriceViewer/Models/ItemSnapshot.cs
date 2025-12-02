using System;

namespace TarkovPriceViewer.Models
{
    public class ItemSnapshot
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ShortName { get; set; }
        public string NormalizedName { get; set; }
        public string IconLink { get; set; }

        public int Width { get; set; }
        public int Height { get; set; }
        public int Slots => Math.Max(1, Math.Max(1, Width) * Math.Max(1, Height));

        public int? LastLowPrice { get; set; }
        public int? Avg24hPrice { get; set; }
        public int? FleaMarketFee { get; set; }
        public int? FleaNetPrice { get; set; }

        // Timestamp reported by tarkov.dev for this item (UTC)
        public DateTime? ApiLastUpdated { get; set; }

        // Timestamp when this item snapshot was built locally (local time)
        public DateTime LocalLastUpdated { get; set; }

        public string BestTraderName { get; set; }
        public int? BestTraderPrice { get; set; }

        public string PreferredSellTarget { get; set; }
        public int? PreferredSellPrice { get; set; }

        public double? PricePerSlot { get; set; }

        public void LogDebug()
        {
            Services.AppLogger.Info(nameof(ItemSnapshot), "=== ItemSnapshot Debug ===");
            Services.AppLogger.Info(nameof(ItemSnapshot), $"Id: {Id}");
            Services.AppLogger.Info(nameof(ItemSnapshot), $"Name: {Name}");
            Services.AppLogger.Info(nameof(ItemSnapshot), $"ShortName: {ShortName}");
            Services.AppLogger.Info(nameof(ItemSnapshot), $"Slots: {Slots}");
            Services.AppLogger.Info(nameof(ItemSnapshot), $"LastLowPrice: {LastLowPrice}");
            Services.AppLogger.Info(nameof(ItemSnapshot), $"Avg24hPrice: {Avg24hPrice}");
            Services.AppLogger.Info(nameof(ItemSnapshot), $"FleaNetPrice: {FleaNetPrice}");
            Services.AppLogger.Info(nameof(ItemSnapshot), $"BestTraderName: {BestTraderName}");
            Services.AppLogger.Info(nameof(ItemSnapshot), $"BestTraderPrice: {BestTraderPrice}");
            Services.AppLogger.Info(nameof(ItemSnapshot), $"PreferredSellTarget: {PreferredSellTarget}");
            Services.AppLogger.Info(nameof(ItemSnapshot), $"PreferredSellPrice: {PreferredSellPrice}");
            Services.AppLogger.Info(nameof(ItemSnapshot), $"PricePerSlot: {PricePerSlot}");
            Services.AppLogger.Info(nameof(ItemSnapshot), $"ApiLastUpdated (UTC): {ApiLastUpdated}");
            Services.AppLogger.Info(nameof(ItemSnapshot), $"LocalLastUpdated: {LocalLastUpdated}");
            Services.AppLogger.Info(nameof(ItemSnapshot), "==========================");
        }
    }
}
