using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TarkovPriceViewer.Models;
using System.Diagnostics;

namespace TarkovPriceViewer.Services
{
    public interface ITarkovDataService
    {
        TarkovAPI.Data Data { get; }
        DateTime LastUpdated { get; }
        bool IsLoaded { get; }
        Task UpdateItemListAPI(bool force = false);
        string GetLastUpdatedText();
    }

    public class TarkovDataService : ITarkovDataService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ISettingsService _settingsService;
        private const string API_FILE_PATH = @"Resources\TarkovAPI.json";
        
        public TarkovAPI.Data Data { get; private set; }
        public DateTime LastUpdated { get; private set; } = DateTime.Now.AddHours(-5);
        public bool IsLoaded { get; private set; }
        private readonly object _lockObject = new object();

        public TarkovDataService(IHttpClientFactory httpClientFactory, ISettingsService settingsService)
        {
            _httpClientFactory = httpClientFactory;
            _settingsService = settingsService;
            
            EnsureResourcesDirectory();
            if (File.Exists(API_FILE_PATH))
                LastUpdated = File.GetLastWriteTime(API_FILE_PATH);
        }

        private void EnsureResourcesDirectory()
        {
             DirectoryInfo di = new DirectoryInfo(@"Resources");
             if (!di.Exists) di.Create();
        }

        public async Task UpdateItemListAPI(bool force = false)
        {
            var settings = _settingsService.Settings;

            // If Outdated by 15 minutes or forced
            if (force || (DateTime.Now - LastUpdated).TotalMinutes >= 15)
            {
                try
                {
                    Debug.WriteLine("\n--> Updating API...");
                    // Construct query
                    var queryDictionary = new Dictionary<string, string>
                    {
                        { "query", GetGraphQLQuery(settings.Language, settings.Mode) }
                    };

                    var client = _httpClientFactory.CreateClient();
                    var httpResponse = await client.PostAsJsonAsync("https://api.tarkov.dev/graphql", queryDictionary);
                    string responseContent = await httpResponse.Content.ReadAsStringAsync();

                    // Basic cleanup of response if needed (original code did some string manipulation)
                    int index = responseContent.IndexOf("{\"data\":");
                    if (index != -1)
                    {
                        responseContent = responseContent.Remove(index, 8);
                        responseContent = responseContent.Remove(responseContent.Length - 1, 1);
                    }

                    lock (_lockObject)
                    {
                        Data = JsonConvert.DeserializeObject<TarkovAPI.Data>(responseContent);
                        // Fallback if schema is wrapped
                        if (Data?.items == null)
                        {
                            var temp = JsonConvert.DeserializeObject<ResponseShell>(responseContent);
                            Data = temp?.data;
                        }
                    }

                    LastUpdated = DateTime.Now;
                    IsLoaded = true;
                    Debug.WriteLine("\n--> TarkovDev API Updated!");
                    File.WriteAllText(API_FILE_PATH, responseContent);
                }
                catch (Exception ex)
                {
                     Debug.WriteLine("--> Error trying to update Tarkov API: " + ex.Message);
                     // Retry logic could go here, or just let it fail for now
                }
            }
            else if (Data == null)
            {
                LoadFromLocalFile();
            }
            else
            {
                 Debug.WriteLine("--> No need to update TarkovDev API! \n--> " + GetLastUpdatedText() + "\n\n");
            }
        }

        private void LoadFromLocalFile()
        {
            try
            {
                if (File.Exists(API_FILE_PATH))
                {
                    string responseContent = File.ReadAllText(API_FILE_PATH);
                    lock (_lockObject)
                    {
                        Data = JsonConvert.DeserializeObject<TarkovAPI.Data>(responseContent);
                        if (Data?.items == null)
                        {
                            var temp = JsonConvert.DeserializeObject<ResponseShell>(responseContent);
                            Data = temp?.data;
                        }
                    }
                    Debug.WriteLine("\n--> TarkovDev API Loaded from local File! \n--> " + GetLastUpdatedText() + "\n\n");
                    IsLoaded = true;
                }
            }
            catch (Exception ex)
            {
                 Debug.WriteLine("\n--> Error trying to load Tarkov API from local file: " + ex.Message);
            }
        }

        public string GetLastUpdatedText()
        {
            TimeSpan elapsed = DateTime.Now - LastUpdated;
            if (elapsed.TotalHours < 1)
                return $"Updated: {(int)elapsed.TotalMinutes} minute(s) ago";
            else if (elapsed.TotalDays < 1)
                return $"Updated: {(int)elapsed.TotalHours} hour(s) ago";
            else
                return $"Updated: {(int)elapsed.TotalDays} day(s) ago";
        }

        private string GetGraphQLQuery(string lang, string gameMode)
        {
            // TODO: Make it more readable
            return "{\r\n  items(" + $"lang:{lang}, gameMode:{gameMode}" + ") {\r\n    id\r\n    name\r\n    normalizedName\r\n    types\r\n    lastLowPrice\r\n    avg24hPrice\r\n    updated\r\n    fleaMarketFee\r\n    link\r\n    wikiLink\r\n    width\r\n    height\r\n    properties {\r\n      ... on ItemPropertiesArmor {\r\n        class\r\n      }\r\n      ... on ItemPropertiesArmorAttachment {\r\n        class\r\n      }\r\n      ... on ItemPropertiesChestRig {\r\n        class\r\n      }\r\n      ... on ItemPropertiesGlasses {\r\n        class\r\n      }\r\n      ... on ItemPropertiesHelmet {\r\n        class\r\n      }\r\n      ... on ItemPropertiesKey {\r\n        uses\r\n      }\r\n      ... on ItemPropertiesAmmo {\r\n        caliber\r\n        damage\r\n        projectileCount\r\n        penetrationPower\r\n        armorDamage\r\n        fragmentationChance\r\n        ammoType\r\n      }\r\n      ... on ItemPropertiesWeapon {\r\n        caliber\r\n        ergonomics\r\n        defaultRecoilVertical\r\n        defaultRecoilHorizontal\r\n        defaultWidth\r\n        defaultHeight\r\n        defaultAmmo {\r\n          name\r\n        }\r\n      }\r\n      ... on ItemPropertiesWeaponMod {\r\n        accuracyModifier\r\n      }\r\n    }\r\n    sellFor {\r\n      currency\r\n      priceRUB\r\n      vendor {\r\n        name\r\n        ... on TraderOffer {\r\n          minTraderLevel\r\n        }\r\n      }\r\n    }\r\n    buyFor {\r\n      currency\r\n      priceRUB\r\n      vendor {\r\n        name\r\n        ... on TraderOffer {\r\n          minTraderLevel\r\n        }\r\n      }\r\n    }\r\n    bartersUsing {\r\n      trader {\r\n        name\r\n        levels {\r\n          level\r\n        }\r\n      }\r\n      requiredItems {\r\n        item {\r\n          name\r\n        }\r\n        count\r\n        quantity\r\n      }\r\n      rewardItems {\r\n        item {\r\n          name\r\n        }\r\n        count\r\n        quantity\r\n      }\r\n    }\r\n    bartersFor {\r\n      trader {\r\n        name\r\n        levels {\r\n          level\r\n        }\r\n      }\r\n      requiredItems {\r\n        item {\r\n          name\r\n        }\r\n        count\r\n        quantity\r\n      }\r\n      rewardItems {\r\n        item {\r\n          name\r\n        }\r\n        count\r\n        quantity\r\n      }\r\n      taskUnlock {\r\n        name\r\n      }\r\n    }\r\n    craftsUsing {\r\n      station {\r\n        name\r\n        levels {\r\n          level\r\n        }\r\n      }\r\n      requiredItems {\r\n        item {\r\n          name\r\n        }\r\n        count\r\n        quantity\r\n      }\r\n      rewardItems {\r\n        item {\r\n          name\r\n        }\r\n        count\r\n        quantity\r\n      }\r\n    }\r\n    craftsFor {\r\n      station {\r\n        name\r\n        levels {\r\n          level\r\n        }\r\n      }\r\n      requiredItems {\r\n        item {\r\n          name\r\n        }\r\n        count\r\n        quantity\r\n      }\r\n      rewardItems {\r\n        item {\r\n          name\r\n        }\r\n        count\r\n        quantity\r\n      }\r\n    }\r\n    usedInTasks {\r\n      id\r\n      name\r\n      map {\r\n        name\r\n      }\r\n      minPlayerLevel\r\n    }\r\n  }\r\n  hideoutStations {\r\n    name\r\n    levels {\r\n      level\r\n      id\r\n      itemRequirements {\r\n        item {\r\n          id\r\n        }\r\n        count\r\n      }\r\n    }\r\n  }\r\n}";
        }
    }
}
