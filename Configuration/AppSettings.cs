using System.Text.Json;
using System.IO;
using System.Collections.Generic;

namespace TarkovPriceViewer.Configuration
{
    public class AppSettings
    {
        public string Version { get; set; } = "v1.35";
        public bool MinimizeToTrayOnStartup { get; set; } = false;
        public bool CloseOverlayWhenMouseMoved { get; set; } = true;
        public bool RandomItem { get; set; } = false;
        public int ShowOverlayKey { get; set; } = 120; // F9
        public int HideOverlayKey { get; set; } = 121; // F10
        public int CompareOverlayKey { get; set; } = 119; // F8
        public int OverlayTransparent { get; set; } = 80;
        public bool ShowLastPrice { get; set; } = true;
        public bool ShowDayPrice { get; set; } = true;
        public bool ShowWeekPrice { get; set; } = true;
        public bool SellToTrader { get; set; } = true;
        public bool BuyFromTrader { get; set; } = true;
        public bool Needs { get; set; } = true;
        public bool BartersAndCrafts { get; set; } = true;
        public bool UseTarkovTrackerApi { get; set; } = false;
        public string TarkovTrackerApiKey { get; set; } = "APIKey";
        public bool ShowHideoutUpgrades { get; set; } = true;
        public string Language { get; set; } = "en";
        public string Mode { get; set; } = "regular";
        public int WorthPerSlotThreshold { get; set; } = 7500;
    }
}
