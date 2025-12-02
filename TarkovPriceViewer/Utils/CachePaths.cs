using System;
using System.IO;

namespace TarkovPriceViewer.Utils
{
    public static class CachePaths
    {
        public static readonly string BaseCacheFolder =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache");

        public static readonly string TarkovDevFolder =
            Path.Combine(BaseCacheFolder, "TarkovDev");

        public static readonly string TarkovDevItemsCacheFilePath =
            Path.Combine(TarkovDevFolder, "tarkovdev-items-cache.json");

        public static readonly string TarkovDevIconsFolder =
            Path.Combine(TarkovDevFolder, "Icons");

        public static readonly string TarkovDevTradersFolder =
            Path.Combine(TarkovDevFolder, "Traders");

        static CachePaths()
        {
            try
            {
                Directory.CreateDirectory(TarkovDevFolder);
                Directory.CreateDirectory(TarkovDevIconsFolder);
                Directory.CreateDirectory(TarkovDevTradersFolder);
            }
            catch
            {
            }
        }
    }
}
