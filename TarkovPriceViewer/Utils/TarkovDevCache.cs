using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Windows.Media.Imaging;
using TarkovPriceViewer.Models;
using TarkovPriceViewer.Services;

namespace TarkovPriceViewer.Utils
{
    public static class TarkovDevCache
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
                    AppLogger.Info("TarkovDevCache.GetIcon", $"Memory cache hit for icon '{item.name}' ({iconUrl})");
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
                    AppLogger.Info("TarkovDevCache.GetIcon", $"Disk cache hit for icon '{item.name}' -> {localPath}");
                }
            }

            // 3) Download from URL and save to disk if needed
            if (bitmap == null)
            {
                try
                {
                    AppLogger.Info("TarkovDevCache.GetIcon", $"Downloading icon from '{iconUrl}'");
                    byte[] data = _httpClient.GetByteArrayAsync(iconUrl).GetAwaiter().GetResult();
                    if (data != null && data.Length > 0)
                    {
                        File.WriteAllBytes(localPath, data);
                        AppLogger.Info("TarkovDevCache.GetIcon", $"Saved icon for '{item.name}' to {localPath} ({data.Length} bytes)");
                        bitmap = LoadBitmapFromFile(localPath);
                    }
                }
                catch (Exception ex)
                {
                    bitmap = null;
                    AppLogger.Error("TarkovDevCache.GetIcon", $"Error downloading or saving icon for '{item?.name}' from '{iconUrl}'", ex);
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

        public static bool TryLoadItemsJson(out string json)
        {
            json = string.Empty;
            string path = CachePaths.TarkovDevItemsCacheFilePath;

            try
            {
                if (!File.Exists(path))
                {
                    AppLogger.Info("TarkovDevCache.TryLoadItemsJson", $"Items cache file not found at '{path}'");
                    return false;
                }

                long size = new FileInfo(path).Length;
                string sizeText = TarkovPriceViewer.Utils.Formatting.FormatFileSize(size);
                json = File.ReadAllText(path);
                AppLogger.Info("TarkovDevCache.TryLoadItemsJson", $"Loaded items cache from '{path}' (size={sizeText})");
                return true;
            }
            catch (Exception ex)
            {
                long size = 0;
                try
                {
                    if (File.Exists(path))
                    {
                        size = new FileInfo(path).Length;
                    }
                }
                catch
                {
                }

                string sizeText = size > 0 ? TarkovPriceViewer.Utils.Formatting.FormatFileSize(size) : "0 B";
                AppLogger.Error("TarkovDevCache.TryLoadItemsJson", $"Error loading items cache from '{path}' (size={sizeText})", ex);
                json = string.Empty;
                return false;
            }
        }

        public static void SaveItemsJson(string json)
        {
            string path = CachePaths.TarkovDevItemsCacheFilePath;

            try
            {
                File.WriteAllText(path, json);
                long size = new FileInfo(path).Length;
                string sizeText = Formatting.FormatFileSize(size);
                AppLogger.Info("TarkovDevCache.SaveItemsJson", $"Saved items cache to '{path}' (size={sizeText})");
            }
            catch (Exception ex)
            {
                AppLogger.Error("TarkovDevCache.SaveItemsJson", $"Error saving items cache to '{path}'", ex);
            }
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
