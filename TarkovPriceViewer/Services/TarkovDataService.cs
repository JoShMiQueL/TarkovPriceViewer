using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TarkovPriceViewer.Configuration;
using TarkovPriceViewer.Models;

namespace TarkovPriceViewer.Services
{
    public interface ITarkovDataService
    {
        TarkovDevAPI.Data Data { get; }
        DateTime LastUpdated { get; }
        bool IsLoaded { get; }
        Task UpdateItemListAPIAsync(bool force = false);
        string GetLastUpdatedText();
    }

    public class TarkovDataService : ITarkovDataService
    {
        private const string TarkovDevItemsCacheFilePath = "tarkovdev-items-cache.json";
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);

        private readonly ISettingsService _settingsService;
        private readonly object _lockObject = new object();

        public TarkovDevAPI.Data Data { get; private set; }
        public DateTime LastUpdated { get; private set; } = DateTime.Now.AddHours(-5);
        public bool IsLoaded { get; private set; }

        public TarkovDataService(ISettingsService settingsService)
        {
            _settingsService = settingsService;

            if (File.Exists(TarkovDevItemsCacheFilePath))
            {
                LastUpdated = File.GetLastWriteTime(TarkovDevItemsCacheFilePath);
            }
        }

        public async Task UpdateItemListAPIAsync(bool force = false)
        {
            AppSettings settings = _settingsService.Settings;

            if (!force && (DateTime.Now - LastUpdated) < CacheDuration)
            {
                if (Data == null)
                {
                    LoadFromLocalFile();
                }
                else
                {
                    AppLogger.Info("TarkovDataService.UpdateItemListAPIAsync", $"No need to update TarkovDev API. {GetLastUpdatedText()}");
                }

                return;
            }

            try
            {
                AppLogger.Info("TarkovDataService.UpdateItemListAPIAsync", "Updating TarkovDev API...");

                var queryDictionary = new Dictionary<string, string>
                {
                    { "query", GetGraphQLQuery(settings.Language, settings.Mode) }
                };

                using var client = new HttpClient();
                HttpResponseMessage httpResponse = await client.PostAsJsonAsync("https://api.tarkov.dev/graphql", queryDictionary).ConfigureAwait(false);
                string responseContent = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);

                int index = responseContent.IndexOf("{\"data\":", StringComparison.Ordinal);
                if (index != -1)
                {
                    responseContent = responseContent.Remove(index, 8);
                    responseContent = responseContent.Remove(responseContent.Length - 1, 1);
                }

                lock (_lockObject)
                {
                    Data = JsonConvert.DeserializeObject<TarkovDevAPI.Data>(responseContent);
                    if (Data?.items == null)
                    {
                        ResponseShell temp = JsonConvert.DeserializeObject<ResponseShell>(responseContent);
                        Data = temp?.data;
                    }
                }

                LastUpdated = DateTime.Now;
                IsLoaded = true;
                File.WriteAllText(TarkovDevItemsCacheFilePath, responseContent);

                AppLogger.Info("TarkovDataService.UpdateItemListAPIAsync", "TarkovDev API updated.");
            }
            catch (Exception ex)
            {
                AppLogger.Error("TarkovDataService.UpdateItemListAPIAsync", "Error updating TarkovDev API", ex);
            }
        }

        private void LoadFromLocalFile()
        {
            try
            {
                if (!File.Exists(TarkovDevItemsCacheFilePath))
                {
                    return;
                }

                string responseContent = File.ReadAllText(TarkovDevItemsCacheFilePath);

                if (!responseContent.Contains("foundInRaid") || !responseContent.Contains("\"itemRequirements\":[{\"id\":"))
                {
                    long size = 0;
                    try
                    {
                        size = new FileInfo(TarkovDevItemsCacheFilePath).Length;
                    }
                    catch
                    {
                    }

                    AppLogger.Info("TarkovDataService.LoadFromLocalFile", $"TarkovDevAPI cache outdated in '{TarkovDevItemsCacheFilePath}' (size={size} bytes), forcing update...");

                    UpdateItemListAPIAsync(force: true).GetAwaiter().GetResult();
                    return;
                }

                lock (_lockObject)
                {
                    Data = JsonConvert.DeserializeObject<TarkovDevAPI.Data>(responseContent);
                    if (Data?.items == null)
                    {
                        ResponseShell temp = JsonConvert.DeserializeObject<ResponseShell>(responseContent);
                        Data = temp?.data;
                    }
                }

                IsLoaded = true;
                AppLogger.Info("TarkovDataService.LoadFromLocalFile", $"TarkovDev API loaded from local file. {GetLastUpdatedText()}");
            }
            catch (Exception ex)
            {
                long size = 0;
                try
                {
                    if (File.Exists(TarkovDevItemsCacheFilePath))
                    {
                        size = new FileInfo(TarkovDevItemsCacheFilePath).Length;
                    }
                }
                catch
                {
                }

                AppLogger.Error("TarkovDataService.LoadFromLocalFile", $"Error loading Tarkov API from local file '{TarkovDevItemsCacheFilePath}' (size={size} bytes)", ex);
            }
        }

        public string GetLastUpdatedText()
        {
            TimeSpan elapsed = DateTime.Now - LastUpdated;
            if (elapsed.TotalHours < 1)
            {
                return $"Updated: {(int)elapsed.TotalMinutes} minute(s) ago";
            }

            if (elapsed.TotalDays < 1)
            {
                return $"Updated: {(int)elapsed.TotalHours} hour(s) ago";
            }

            return $"Updated: {(int)elapsed.TotalDays} day(s) ago";
        }

        private string GetGraphQLQuery(string lang, string gameMode)
        {
            return $@"{{
  items(lang:{lang}, gameMode:{gameMode}) {{
    id
    name
    normalizedName
    types
    lastLowPrice
    avg24hPrice
    updated
    fleaMarketFee
    link
    wikiLink
    width
    height
    properties {{
      ... on ItemPropertiesArmor {{
        class
      }}
      ... on ItemPropertiesArmorAttachment {{
        class
      }}
      ... on ItemPropertiesChestRig {{
        class
      }}
      ... on ItemPropertiesGlasses {{
        class
      }}
      ... on ItemPropertiesHelmet {{
        class
      }}
      ... on ItemPropertiesKey {{
        uses
      }}
      ... on ItemPropertiesAmmo {{
        caliber
        damage
        projectileCount
        penetrationPower
        armorDamage
        fragmentationChance
        ammoType
      }}
      ... on ItemPropertiesWeapon {{
        caliber
        ergonomics
        defaultRecoilVertical
        defaultRecoilHorizontal
        defaultWidth
        defaultHeight
        defaultAmmo {{
          name
        }}
      }}
      ... on ItemPropertiesWeaponMod {{
        accuracyModifier
      }}
    }}
    sellFor {{
      currency
      priceRUB
      vendor {{
        name
        ... on TraderOffer {{
          minTraderLevel
        }}
      }}
    }}
    buyFor {{
      currency
      priceRUB
      vendor {{
        name
        ... on TraderOffer {{
          minTraderLevel
        }}
      }}
    }}
    bartersUsing {{
      trader {{
        name
        levels {{
          level
        }}
      }}
      requiredItems {{
        item {{
          name
        }}
        count
        quantity
      }}
      rewardItems {{
        item {{
          name
        }}
        count
        quantity
      }}
    }}
    craftsFor {{
      station {{
        name
        levels {{
          level
        }}
      }}
      requiredItems {{
        item {{
          name
        }}
        count
        quantity
      }}
      rewardItems {{
        item {{
          name
        }}
        count
        quantity
      }}
    }}
    craftsUsing {{
      station {{
        name
        levels {{
          level
        }}
      }}
      requiredItems {{
        item {{
          name
        }}
        count
        quantity
      }}
      rewardItems {{
        item {{
          name
        }}
        count
        quantity
      }}
    }}
    bartersFor {{
      trader {{
        name
        levels {{
          level
        }}
      }}
      requiredItems {{
        item {{
          name
        }}
        count
        quantity
      }}
      rewardItems {{
        item {{
          name
        }}
        count
        quantity
      }}
      taskUnlock {{
        name
      }}
    }}
    usedInTasks {{
      id
      name
      trader {{
        name
      }}
      map {{
        name
      }}
      minPlayerLevel
      traderLevelRequirements {{
        level
      }}
      objectives {{
        id
        description
        maps {{
          name
        }}
        type
        ... on TaskObjectiveItem {{
          id
          count
          type
          foundInRaid
          items {{
            id
            name
          }}
        }}
      }}
    }}
  }}
  hideoutStations {{
    name
    levels {{
      id
      level
      itemRequirements {{
        id
        item {{
          id
          name
        }}
        count
      }}
    }}
  }}
}}";
        }
    }
}
