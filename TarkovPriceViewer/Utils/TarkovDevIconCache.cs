using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Windows.Media.Imaging;
using TarkovPriceViewer.Models;
using TarkovPriceViewer.Services;

namespace TarkovPriceViewer.Utils
{
    public static class TarkovDevIconCache
    {
        private static readonly Dictionary<string, BitmapImage> _iconCache = new Dictionary<string, BitmapImage>();
        private static readonly object _iconCacheLock = new object();
        private static readonly HttpClient _httpClient = new HttpClient();

        public static BitmapImage GetIcon(TarkovDevAPI.Item item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.iconLink))
            {
                return null;
            }

            string iconUrl = item.iconLink;
            BitmapImage bitmap = null;

            // 1) In-memory cache by URL
            lock (_iconCacheLock)
            {
                if (_iconCache.TryGetValue(iconUrl, out BitmapImage cached))
                {
                    AppLogger.Info("TarkovDevIconCache.GetIcon", $"Memory cache hit for icon '{item.name}' ({iconUrl})");
                    return cached;
                }
            }

            // 2) Disk cache by id/normalizedName
            string cacheFileName = (item.id ?? item.normalizedName ?? "icon") + ".png";
            string localPath = Path.Combine(CachePaths.TarkovDevIconsFolder, cacheFileName);

            if (File.Exists(localPath) && new FileInfo(localPath).Length > 0)
            {
                bitmap = LoadBitmapFromFile(localPath);
                if (bitmap != null)
                {
                    AppLogger.Info("TarkovDevIconCache.GetIcon", $"Disk cache hit for icon '{item.name}' -> {localPath}");
                }
            }

            // 3) Download from URL and save to disk if needed
            if (bitmap == null)
            {
                try
                {
                    // Download raw bytes and save to disk
                    AppLogger.Info("TarkovDevIconCache.GetIcon", $"Downloading icon from '{iconUrl}'");
                    byte[] data = _httpClient.GetByteArrayAsync(iconUrl).GetAwaiter().GetResult();
                    if (data != null && data.Length > 0)
                    {
                        File.WriteAllBytes(localPath, data);
                        AppLogger.Info("TarkovDevIconCache.GetIcon", $"Saved icon for '{item.name}' to {localPath} ({data.Length} bytes)");
                        bitmap = LoadBitmapFromFile(localPath);
                    }
                }
                catch
                {
                    bitmap = null;
                    AppLogger.Error("TarkovDevIconCache.GetIcon", $"Error downloading or saving icon for '{item?.name}' from '{iconUrl}'");
                }
            }

            // 4) Store in in-memory cache
            if (bitmap != null)
            {
                lock (_iconCacheLock)
                {
                    if (!_iconCache.ContainsKey(iconUrl))
                    {
                        _iconCache[iconUrl] = bitmap;
                    }
                }
            }

            return bitmap;
        }

        private static BitmapImage LoadBitmapFromFile(string path)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(path, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null;
            }
        }
    }
}
