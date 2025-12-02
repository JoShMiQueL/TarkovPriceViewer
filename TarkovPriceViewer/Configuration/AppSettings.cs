using System.Collections.Generic;

namespace TarkovPriceViewer.Configuration
{
    public class AppSettings
    {
        public bool CloseOverlayOnMouseMove { get; set; } = true;

        public int OverlayOpacity { get; set; } = 80;

        public string ShowOverlayKey { get; set; } = "F9";

        public string HideOverlayKey { get; set; } = "F10";

        public string Language { get; set; } = "en";

        public string Mode { get; set; } = "regular";

        public bool UseTarkovTrackerApi { get; set; } = false;

        public string TarkovTrackerApiKey { get; set; } = string.Empty;

        public double FleaVsTraderThreshold { get; set; } = 0.20;
    }
}
