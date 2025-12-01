using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TarkovPriceViewer.Configuration;
using TarkovPriceViewer.Models;
using TarkovPriceViewer.Utils;

namespace TarkovPriceViewer.Services
{
    public interface ITarkovDataService
    {
        TarkovDevAPI.Data Data { get; }
        DateTime LastUpdated { get; }
        bool IsLoaded { get; }
        Task UpdateItemListAPIAsync(bool force = false);
    }

    public class TarkovDataService : ITarkovDataService
    {
        private static readonly string TarkovDevItemsCacheFilePath = CachePaths.TarkovDevItemsCacheFilePath;
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

            TimeSpan elapsed = GetLastUpdatedElapsed();
            if (!force && elapsed < CacheDuration)
            {
                if (Data == null)
                {
                    AppLogger.Info("TarkovDataService.UpdateItemListAPIAsync", $"Items not loaded in memory, trying to obtain Tarkov items from local cache file (LastUpdated={LastUpdated:yyyy-MM-dd HH:mm:ss}, {FormatElapsed(elapsed)}).");
                    LoadFromLocalFile();
                }
                else
                {
                    AppLogger.Info("TarkovDataService.UpdateItemListAPIAsync", $"Using Tarkov items already loaded in memory (from previous tarkov.dev API call). {FormatElapsed(elapsed)}.");
                }

                return;
            }

            try
            {
                AppLogger.Info("TarkovDataService.UpdateItemListAPIAsync", "Updating items from tarkov.dev GraphQL endpoint (https://api.tarkov.dev/graphql)...");

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

                // Persist updated items JSON via shared TarkovDev cache helper
                TarkovDevCache.SaveItemsJson(responseContent);

                TimeSpan updatedAge = GetLastUpdatedElapsed();
                AppLogger.Info("TarkovDataService.UpdateItemListAPIAsync", $"Tarkov items updated successfully from tarkov.dev. {FormatElapsed(updatedAge)}.");
            }
            catch (Exception ex)
            {
                AppLogger.Error("TarkovDataService.UpdateItemListAPIAsync", "Error updating Tarkov items from tarkov.dev", ex);
            }
        }

        private void LoadFromLocalFile()
        {
            try
            {
                if (!TarkovDevCache.TryLoadItemsJson(out string responseContent))
                {
                    return;
                }

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

                    AppLogger.Info("TarkovDataService.LoadFromLocalFile", $"Local Tarkov items cache in '{TarkovDevItemsCacheFilePath}' is outdated (size={size} bytes), forcing remote update from tarkov.dev...");

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
                TimeSpan age = GetLastUpdatedElapsed();
                AppLogger.Info("TarkovDataService.LoadFromLocalFile", $"Tarkov items obtained from local cache file ({FormatElapsed(age)}).");
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

                AppLogger.Error("TarkovDataService.LoadFromLocalFile", $"Error loading Tarkov items from local cache file '{TarkovDevItemsCacheFilePath}' (size={size} bytes)", ex);
            }
        }

        private TimeSpan GetLastUpdatedElapsed()
        {
            return DateTime.Now - LastUpdated;
        }

        private string FormatElapsed(TimeSpan elapsed)
        {
            if (elapsed.TotalSeconds < 60)
            {
                return $"{(int)elapsed.TotalSeconds} second(s) ago";
            }

            if (elapsed.TotalMinutes < 60)
            {
                return $"{(int)elapsed.TotalMinutes} minute(s) ago";
            }

            return $"{(int)elapsed.TotalHours} hour(s) ago";
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
    iconLink
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
