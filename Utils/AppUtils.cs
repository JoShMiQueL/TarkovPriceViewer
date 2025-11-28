using System;

namespace TarkovPriceViewer.Utils
{
    public static class AppUtils
    {
        public static string GetVersion()
        {
            var version = typeof(AppUtils).Assembly.GetName().Version;
            if (version == null) return "v1.0";

            // If patch is 0, use major.minor (e.g., v1.35)
            // Otherwise use major.minor.patch (e.g., v1.35.1)
            return version.Build == 0
                ? $"v{version.Major}.{version.Minor}"
                : $"v{version.Major}.{version.Minor}.{version.Build}";
        }

        public static string GetVersionWithoutPrefix()
        {
            var tag = GetVersion();
            return string.IsNullOrEmpty(tag) ? tag : tag.TrimStart('v', 'V');
        }
    }
}
