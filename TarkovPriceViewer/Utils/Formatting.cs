using System;

namespace TarkovPriceViewer.Utils
{
    public static class Formatting
    {
        public static string FormatElapsed(TimeSpan elapsed)
        {
            return FormatCore(elapsed, includeMilliseconds: false, suffix: " ago");
        }

        public static string FormatDuration(TimeSpan duration)
        {
            return FormatCore(duration, includeMilliseconds: true, suffix: string.Empty);
        }

        public static string FormatFileSize(long bytes)
        {
            const double kb = 1024.0;
            const double mb = kb * 1024.0;
            const double gb = mb * 1024.0;

            if (bytes < kb)
            {
                return $"{bytes} B";
            }

            if (bytes < mb)
            {
                double value = bytes / kb;
                return $"{value:0.#} KB";
            }

            if (bytes < gb)
            {
                double value = bytes / mb;
                return $"{value:0.#} MB";
            }

            double gbValue = bytes / gb;
            return $"{gbValue:0.#} GB";
        }

        private static string FormatCore(TimeSpan value, bool includeMilliseconds, string suffix)
        {
            if (includeMilliseconds && value.TotalSeconds < 1)
            {
                return $"{(int)value.TotalMilliseconds} ms{suffix}";
            }

            if (value.TotalSeconds < 60)
            {
                return $"{(int)value.TotalSeconds} second(s){suffix}";
            }

            if (value.TotalMinutes < 60)
            {
                return $"{(int)value.TotalMinutes} minute(s){suffix}";
            }

            return $"{(int)value.TotalHours} hour(s){suffix}";
        }
    }
}
