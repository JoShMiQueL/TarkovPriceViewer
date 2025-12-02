using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TarkovPriceViewer.Configuration;
using TarkovPriceViewer.Models;

namespace TarkovPriceViewer.Services
{
    public interface IItemSnapshotService
    {
        bool IsLoaded { get; }
        DateTime LocalLastUpdated { get; }

        IReadOnlyDictionary<string, ItemSnapshot> ItemsById { get; }

        Task EnsureLoadedAsync(bool forceRefresh = false, CancellationToken cancellationToken = default);

        ItemSnapshot GetById(string id);
        ItemSnapshot FindByName(string namePart);
        ItemSnapshot FindByShortName(string shortNamePart);
    }

    public class ItemSnapshotService : IItemSnapshotService
    {
        private readonly ITarkovDevApiClient _apiClient;
        private readonly ITarkovDevCacheService _cacheService;
        private readonly ISettingsService _settingsService;
        private readonly object _lockObject = new object();

        private Dictionary<string, ItemSnapshot> _itemsById = new Dictionary<string, ItemSnapshot>(StringComparer.OrdinalIgnoreCase);

        public bool IsLoaded { get; private set; }

        public DateTime LocalLastUpdated { get; private set; } = DateTime.MinValue;

        public IReadOnlyDictionary<string, ItemSnapshot> ItemsById
        {
            get
            {
                lock (_lockObject)
                {
                    return new Dictionary<string, ItemSnapshot>(_itemsById, StringComparer.OrdinalIgnoreCase);
                }
            }
        }

        public ItemSnapshotService(
            ITarkovDevApiClient apiClient,
            ITarkovDevCacheService cacheService,
            ISettingsService settingsService)
        {
            _apiClient = apiClient;
            _cacheService = cacheService;
            _settingsService = settingsService;
        }

        public async Task EnsureLoadedAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
        {
            if (IsLoaded && !forceRefresh)
            {
                // Snapshot already built in memory, nothing to do
                return;
            }

            string json = null;
            DateTime lastWriteUtc = DateTime.MinValue;
            bool hasCache = _cacheService.TryLoadItemsJson(out json, out lastWriteUtc);

            bool cacheExpired = true;
            if (hasCache)
            {
                cacheExpired = DateTime.UtcNow - lastWriteUtc > _cacheService.ItemsCacheDuration;
            }

            if (!hasCache || (forceRefresh || cacheExpired))
            {
                // Descargar desde la API de tarkov.dev
                AppSettings settings = _settingsService.Settings;
                string lang = settings.Language;
                string gameMode = settings.Mode;

                json = await _apiClient.GetItemsSnapshotJsonAsync(lang, gameMode, cancellationToken).ConfigureAwait(false);

                // Guardar en cache JSON local
                _cacheService.SaveItemsJson(json);
            }

            // Construir el snapshot en memoria a partir del JSON (siempre que tengamos json válido)
            if (!string.IsNullOrEmpty(json))
            {
                BuildSnapshotFromJson(json);
            }
        }

        public ItemSnapshot GetById(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            lock (_lockObject)
            {
                _itemsById.TryGetValue(id, out ItemSnapshot snapshot);
                return snapshot;
            }
        }

        public ItemSnapshot FindByName(string namePart)
        {
            if (string.IsNullOrWhiteSpace(namePart))
            {
                return null;
            }

            lock (_lockObject)
            {
                foreach (var snapshot in _itemsById.Values)
                {
                    if (snapshot.Name != null &&
                        snapshot.Name.IndexOf(namePart, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return snapshot;
                    }
                }
            }

            return null;
        }

        public ItemSnapshot FindByShortName(string shortNamePart)
        {
            if (string.IsNullOrWhiteSpace(shortNamePart))
            {
                return null;
            }

            lock (_lockObject)
            {
                foreach (var snapshot in _itemsById.Values)
                {
                    if (snapshot.ShortName != null &&
                        snapshot.ShortName.IndexOf(shortNamePart, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return snapshot;
                    }
                }
            }

            return null;
        }

        private void BuildSnapshotFromJson(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            try
            {
                DateTime localSnapshotTime = DateTime.Now;

                // The original GraphQL response comes as { "data": { ... } },
                // but the client may store it without the wrapper. Try both shapes.
                GraphQlRoot root = null;

                if (json.TrimStart().StartsWith("{\"data\"", StringComparison.Ordinal))
                {
                    root = JsonConvert.DeserializeObject<GraphQlEnvelope>(json)?.data;
                }
                else
                {
                    root = JsonConvert.DeserializeObject<GraphQlRoot>(json);
                }

                if (root == null || root.items == null)
                {
                    return;
                }

                AppSettings settings = _settingsService.Settings;
                double threshold = settings.FleaVsTraderThreshold;

                var dict = new Dictionary<string, ItemSnapshot>(StringComparer.OrdinalIgnoreCase);

                foreach (var item in root.items)
                {
                    if (item == null || string.IsNullOrEmpty(item.id))
                    {
                        continue;
                    }

                    int width = item.width ?? 1;
                    int height = item.height ?? 1;

                    int? lastLowPrice = item.lastLowPrice;
                    int? avg24hPrice = item.avg24hPrice;
                    int? fleaFee = item.fleaMarketFee;
                    int? fleaNet = (lastLowPrice.HasValue && fleaFee.HasValue)
                        ? lastLowPrice.Value - fleaFee.Value
                        : (int?)null;

                    string bestTraderName = null;
                    int? bestTraderPrice = null;
                    string bestTraderImageLink = null;

                    if (item.sellFor != null)
                    {
                        foreach (var sf in item.sellFor)
                        {
                            if (sf == null || sf.vendor == null || string.IsNullOrEmpty(sf.vendor.name))
                            {
                                continue;
                            }

                            // Flea Market offers are represented here as well, but for the
                            // trader comparison we only want NPC traders. Flea net price is
                            // already handled via lastLowPrice - fleaFee above.
                            if (string.Equals(sf.vendor.name, "Flea Market", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            int? price = sf.priceRUB;
                            if (!price.HasValue)
                            {
                                continue;
                            }

                            if (!bestTraderPrice.HasValue || price.Value > bestTraderPrice.Value)
                            {
                                bestTraderPrice = price.Value;
                                bestTraderName = sf.vendor.name;
                                bestTraderImageLink = sf.vendor.trader?.imageLink;
                            }
                        }
                    }

                    string preferredTarget = null;
                    int? preferredPrice = null;

                    if (fleaNet.HasValue && bestTraderPrice.HasValue)
                    {
                        if (fleaNet.Value >= bestTraderPrice.Value * (1.0 + threshold))
                        {
                            preferredTarget = "Flea";
                            preferredPrice = fleaNet.Value;
                        }
                        else
                        {
                            preferredTarget = bestTraderName ?? "Trader";
                            preferredPrice = bestTraderPrice.Value;
                        }
                    }
                    else if (fleaNet.HasValue)
                    {
                        preferredTarget = "Flea";
                        preferredPrice = fleaNet.Value;
                    }
                    else if (bestTraderPrice.HasValue)
                    {
                        preferredTarget = bestTraderName ?? "Trader";
                        preferredPrice = bestTraderPrice.Value;
                    }

                    double? pricePerSlot = null;
                    int slots = Math.Max(1, Math.Max(1, width) * Math.Max(1, height));
                    if (preferredPrice.HasValue)
                    {
                        pricePerSlot = (double)preferredPrice.Value / slots;
                    }

                    DateTime? apiUpdated = null;
                    if (!string.IsNullOrWhiteSpace(item.updated))
                    {
                        if (DateTime.TryParse(item.updated, out DateTime parsed))
                        {
                            apiUpdated = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
                        }
                    }

                    var snapshot = new ItemSnapshot
                    {
                        Id = item.id,
                        Name = item.name,
                        ShortName = item.shortName,
                        NormalizedName = item.normalizedName,
                        IconLink = item.iconLink,
                        Width = width,
                        Height = height,
                        LastLowPrice = lastLowPrice,
                        Avg24hPrice = avg24hPrice,
                        FleaMarketFee = fleaFee,
                        FleaNetPrice = fleaNet,
                        ApiLastUpdated = apiUpdated,
                        LocalLastUpdated = localSnapshotTime,
                        BestTraderName = bestTraderName,
                        BestTraderPrice = bestTraderPrice,
                        BestTraderImageLink = bestTraderImageLink,
                        PreferredSellTarget = preferredTarget,
                        PreferredSellPrice = preferredPrice,
                        PricePerSlot = pricePerSlot
                    };

                    dict[item.id] = snapshot;
                }

                lock (_lockObject)
                {
                    _itemsById = dict;
                    LocalLastUpdated = localSnapshotTime;
                    IsLoaded = true;
                }
            }
            catch
            {
                // If there is any error deserializing or building the snapshot, keep the previous snapshot.
            }
        }

        // Helper classes to deserialize only what is needed from the GraphQL response
        private class GraphQlEnvelope
        {
            public GraphQlRoot data { get; set; }
        }

        private class GraphQlRoot
        {
            public System.Collections.Generic.List<ItemDto> items { get; set; }
        }

        private class ItemDto
        {
            public string id { get; set; }
            public string name { get; set; }
            public string shortName { get; set; }
            public string normalizedName { get; set; }
            public string iconLink { get; set; }
            public int? width { get; set; }
            public int? height { get; set; }
            public int? lastLowPrice { get; set; }
            public int? avg24hPrice { get; set; }
            public string updated { get; set; }
            public int? fleaMarketFee { get; set; }
            public System.Collections.Generic.List<SellForDto> sellFor { get; set; }
        }

        private class SellForDto
        {
            public int? priceRUB { get; set; }
            public VendorDto vendor { get; set; }
        }

        private class VendorDto
        {
            public string name { get; set; }
            public TraderDto trader { get; set; }
        }

        private class TraderDto
        {
            public string imageLink { get; set; }
        }
    }
}
