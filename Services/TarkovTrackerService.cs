using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TarkovPriceViewer.Models;
using System.Diagnostics;

namespace TarkovPriceViewer.Services
{
    public interface ITarkovTrackerService
    {
        TarkovTrackerAPI.Root TrackerData { get; }
        bool IsLoaded { get; }
        Task UpdateTarkovTrackerAPI(bool force = false);
    }

    public class TarkovTrackerService : ITarkovTrackerService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ISettingsService _settingsService;
        
        public TarkovTrackerAPI.Root TrackerData { get; private set; }
        public bool IsLoaded { get; private set; }
        public DateTime LastUpdated { get; private set; } = DateTime.Now.AddHours(-5);
        private readonly object _lockObject = new object();

        public TarkovTrackerService(IHttpClientFactory httpClientFactory, ISettingsService settingsService)
        {
            _httpClientFactory = httpClientFactory;
            _settingsService = settingsService;
        }

        public async Task UpdateTarkovTrackerAPI(bool force = false)
        {
            var settings = _settingsService.Settings;
            string apiKey = settings.TarkovTrackerApiKey;

            if (settings.UseTarkovTrackerApi && !string.Equals(apiKey, "APIKey") && !string.IsNullOrWhiteSpace(apiKey))
            {
                // If Outdated by 10 seconds
                if (force || ((DateTime.Now - LastUpdated).TotalSeconds >= 10))
                {
                    try
                    {
                        Debug.WriteLine("\n--> Updating TarkovTracker API...");

                        var client = _httpClientFactory.CreateClient();
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                        var httpResponse = await client.GetAsync("https://tarkovtracker.io/api/v2/progress");
                        if (httpResponse.IsSuccessStatusCode)
                        {
                            string responseContent = await httpResponse.Content.ReadAsStringAsync();

                            lock (_lockObject)
                            {
                                TrackerData = JsonConvert.DeserializeObject<TarkovTrackerAPI.Root>(responseContent);
                                LastUpdated = DateTime.Now;
                                IsLoaded = true;
                                Debug.WriteLine("\n--> TarkovTracker API Updated!");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("--> Error trying to update TarkovTracker API: " + ex.Message);
                    }
                }
                else
                {
                    Debug.WriteLine("--> No need to update TarkovTracker API!");
                }
            }
        }
    }
}
