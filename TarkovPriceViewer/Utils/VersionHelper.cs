using System.Reflection;

namespace TarkovPriceViewer.Utils
{
    public static class VersionHelper
    {
        public static string GetDisplayVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var infoVersion = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;

            if (string.IsNullOrWhiteSpace(infoVersion))
            {
                return "dev";
            }

            // Drop any build metadata / commit hash suffix (e.g. "+60caae...")
            var core = infoVersion.Split('+')[0];

            return core;
        }
    }
}
