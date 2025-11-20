using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using TarkovPriceViewer.Configuration;

namespace TarkovPriceViewer.Services
{
    public interface ISettingsService
    {
        AppSettings Settings { get; }
        void Load();
        void Save();
    }

    public class SettingsService : ISettingsService
    {
        private const string SETTING_PATH = "settings.json";
        public AppSettings Settings { get; private set; }

        public SettingsService()
        {
            Settings = new AppSettings();
        }

        public void Load()
        {
            try
            {
                if (!File.Exists(SETTING_PATH))
                {
                    Save(); // Create default
                    return;
                }

                string json = File.ReadAllText(SETTING_PATH);
                // Handle legacy string-string dictionary if needed, but preferably we migrate to typed object.
                // The original code used Dictionary<string, string>. We need to be careful.
                // If the file is in the old format (Dictionary<string, string>), direct deserialization to AppSettings might fail 
                // because "true" (string) is not true (bool) in standard System.Text.Json without custom converters.
                // However, for simplicity in this refactor, let's try to read it as a dictionary first and map it, 
                // or assume the user is okay with resetting settings or we write a migration logic.
                
                // Safe approach: Read as JsonElement or Dictionary, then map to AppSettings
                try 
                {
                    var legacySettings = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (legacySettings != null)
                    {
                        MapLegacySettings(legacySettings);
                    }
                }
                catch
                {
                    // If it fails (maybe it's already the new format?), try deserializing directly
                    try 
                    {
                         Settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                    }
                    catch
                    {
                        Settings = new AppSettings();
                    }
                }
            }
            catch (Exception)
            {
                Settings = new AppSettings();
            }
        }

        private void MapLegacySettings(Dictionary<string, string> legacy)
        {
            if (legacy.TryGetValue("MinimizetoTrayWhenStartup", out var val)) Settings.MinimizetoTrayWhenStartup = bool.TryParse(val, out var b) ? b : false;
            if (legacy.TryGetValue("CloseOverlayWhenMouseMoved", out val)) Settings.CloseOverlayWhenMouseMoved = bool.TryParse(val, out var b) ? b : true;
            if (legacy.TryGetValue("RandomItem", out val)) Settings.RandomItem = bool.TryParse(val, out var b) ? b : false;
            if (legacy.TryGetValue("ShowOverlay_Key", out val)) Settings.ShowOverlay_Key = int.TryParse(val, out var i) ? i : 120;
            if (legacy.TryGetValue("HideOverlay_Key", out val)) Settings.HideOverlay_Key = int.TryParse(val, out var i) ? i : 121;
            if (legacy.TryGetValue("CompareOverlay_Key", out val)) Settings.CompareOverlay_Key = int.TryParse(val, out var i) ? i : 119;
            if (legacy.TryGetValue("Overlay_Transparent", out val)) Settings.Overlay_Transparent = int.TryParse(val, out var i) ? i : 80;
            if (legacy.TryGetValue("Show_Last_Price", out val)) Settings.Show_Last_Price = bool.TryParse(val, out var b) ? b : true;
            if (legacy.TryGetValue("Show_Day_Price", out val)) Settings.Show_Day_Price = bool.TryParse(val, out var b) ? b : true;
            if (legacy.TryGetValue("Show_Week_Price", out val)) Settings.Show_Week_Price = bool.TryParse(val, out var b) ? b : true;
            if (legacy.TryGetValue("Sell_to_Trader", out val)) Settings.Sell_to_Trader = bool.TryParse(val, out var b) ? b : true;
            if (legacy.TryGetValue("Buy_From_Trader", out val)) Settings.Buy_From_Trader = bool.TryParse(val, out var b) ? b : true;
            if (legacy.TryGetValue("Needs", out val)) Settings.Needs = bool.TryParse(val, out var b) ? b : true;
            if (legacy.TryGetValue("Barters_and_Crafts", out val)) Settings.Barters_and_Crafts = bool.TryParse(val, out var b) ? b : true;
            if (legacy.TryGetValue("useTarkovTrackerAPI", out val)) Settings.UseTarkovTrackerAPI = bool.TryParse(val, out var b) ? b : false;
            if (legacy.TryGetValue("TarkovTrackerAPIKey", out val)) Settings.TarkovTrackerAPIKey = val;
            if (legacy.TryGetValue("showHideoutUpgrades", out val)) Settings.ShowHideoutUpgrades = bool.TryParse(val, out var b) ? b : true;
            if (legacy.TryGetValue("Language", out val)) Settings.Language = val;
            if (legacy.TryGetValue("Mode", out val)) Settings.Mode = val;
            if (legacy.TryGetValue("WorthPerSlotThreshold", out val)) Settings.WorthPerSlotThreshold = int.TryParse(val, out var i) ? i : 7500;
        }

        public void Save()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                // We save as the new format (Strongly typed JSON)
                string json = JsonSerializer.Serialize(Settings, options);
                File.WriteAllText(SETTING_PATH, json);
            }
            catch (Exception)
            {
                // Log error
            }
        }
    }
}
