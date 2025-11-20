using System.Text.Json;
using System.IO;
using System.Collections.Generic;

namespace TarkovPriceViewer.Configuration
{
    public class AppSettings
    {
        public string Version { get; set; } = "v1.34";
        public bool MinimizetoTrayWhenStartup { get; set; } = false;
        public bool CloseOverlayWhenMouseMoved { get; set; } = true;
        public bool RandomItem { get; set; } = false;
        public int ShowOverlay_Key { get; set; } = 120; // F9
        public int HideOverlay_Key { get; set; } = 121; // F10
        public int CompareOverlay_Key { get; set; } = 119; // F8
        public int Overlay_Transparent { get; set; } = 80;
        public bool Show_Last_Price { get; set; } = true;
        public bool Show_Day_Price { get; set; } = true;
        public bool Show_Week_Price { get; set; } = true;
        public bool Sell_to_Trader { get; set; } = true;
        public bool Buy_From_Trader { get; set; } = true;
        public bool Needs { get; set; } = true;
        public bool Barters_and_Crafts { get; set; } = true;
        public bool UseTarkovTrackerAPI { get; set; } = false;
        public string TarkovTrackerAPIKey { get; set; } = "APIKey";
        public bool ShowHideoutUpgrades { get; set; } = true;
        public string Language { get; set; } = "en";
        public string Mode { get; set; } = "regular";
        public int WorthPerSlotThreshold { get; set; } = 7500;
    }
}
