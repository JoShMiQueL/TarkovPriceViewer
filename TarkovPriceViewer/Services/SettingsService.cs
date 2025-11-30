using System;
using System.Diagnostics;
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
        private const string SettingPath = "settings.json";

        public AppSettings Settings { get; private set; }

        public SettingsService()
        {
            Settings = new AppSettings();
        }

        public void Load()
        {
            try
            {
                if (!File.Exists(SettingPath))
                {
                    Save();
                    return;
                }

                var json = File.ReadAllText(SettingPath);
                Settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading settings: {ex}");
                Settings = new AppSettings();
            }
        }

        public void Save()
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                var json = JsonSerializer.Serialize(Settings, options);
                File.WriteAllText(SettingPath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving settings: {ex}");
            }
        }
    }
}
