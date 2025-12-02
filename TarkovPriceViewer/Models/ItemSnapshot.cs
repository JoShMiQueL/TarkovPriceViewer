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
    }
}
