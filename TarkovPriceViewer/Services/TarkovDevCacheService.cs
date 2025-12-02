using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace TarkovPriceViewer.Services
{
    public interface ITarkovDevCacheService
    {
        string ItemsCacheFilePath { get; }
        TimeSpan ItemsCacheDuration { get; }

        bool TryLoadItemsJson(out string json, out DateTime lastWriteTimeUtc);
        void SaveItemsJson(string json);

        Task<BitmapSource> GetItemIconAsync(string itemId, string iconUrl, CancellationToken cancellationToken = default);
        Task<BitmapSource> GetTraderIconAsync(string traderId, string imageUrl, CancellationToken cancellationToken = default);
    }

    public class TarkovDevCacheService : ITarkovDevCacheService
    {
        private readonly IHttpClientFactory _httpClientFactory;

        private static readonly string BaseCacheDirectory =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TarkovPriceViewer", "TarkovDev");

        private static readonly string ItemsCacheFileName = "items.json";

        public string ItemsCacheFilePath => Path.Combine(BaseCacheDirectory, ItemsCacheFileName);

        public TimeSpan ItemsCacheDuration { get; } = TimeSpan.FromMinutes(15);

        public TarkovDevCacheService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public bool TryLoadItemsJson(out string json, out DateTime lastWriteTimeUtc)
        {
            json = null;
            lastWriteTimeUtc = DateTime.MinValue;

            try
            {
                if (!File.Exists(ItemsCacheFilePath))
                {
                    return false;
                }

                lastWriteTimeUtc = File.GetLastWriteTimeUtc(ItemsCacheFilePath);
                json = File.ReadAllText(ItemsCacheFilePath);
                return !string.IsNullOrEmpty(json);
            }
            catch
            {
                json = null;
                lastWriteTimeUtc = DateTime.MinValue;
                return false;
            }
        }

        public void SaveItemsJson(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            try
            {
                Directory.CreateDirectory(BaseCacheDirectory);
                File.WriteAllText(ItemsCacheFilePath, json);
            }
            catch
            {
                // Swallow exceptions: cache failures should not crash the app
            }
        }

        public Task<BitmapSource> GetItemIconAsync(string itemId, string iconUrl, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                throw new ArgumentException("itemId must not be null or empty", nameof(itemId));
            }

            string fileName = SanitizeFileName(itemId) + ".png";
            string path = Path.Combine(BaseCacheDirectory, "icons", "items", fileName);

            return GetOrDownloadImageAsync(path, iconUrl, cancellationToken);
        }

        public Task<BitmapSource> GetTraderIconAsync(string traderId, string imageUrl, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(traderId))
            {
                throw new ArgumentException("traderId must not be null or empty", nameof(traderId));
            }

            string fileName = SanitizeFileName(traderId) + ".png";
            string path = Path.Combine(BaseCacheDirectory, "icons", "traders", fileName);

            return GetOrDownloadImageAsync(path, imageUrl, cancellationToken);
        }

        private async Task<BitmapSource> GetOrDownloadImageAsync(string cachePath, string url, CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath));

            if (File.Exists(cachePath))
            {
                try
                {
                    return LoadBitmapFromFile(cachePath);
                }
                catch
                {
                    // If the cache is corrupt, fall through to re-download
                }
            }

            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }

            try
            {
                using HttpClient client = _httpClientFactory.CreateClient();
                byte[] data = await client.GetByteArrayAsync(url, cancellationToken).ConfigureAwait(false);

                await File.WriteAllBytesAsync(cachePath, data, cancellationToken).ConfigureAwait(false);

                return LoadBitmapFromFile(cachePath);
            }
            catch
            {
                return null;
            }
        }

        private static BitmapSource LoadBitmapFromFile(string path)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }

        private static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }

            return name;
        }
    }
}
