using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TarkovPriceViewer.Services;
using TarkovPriceViewer.UI;

namespace TarkovPriceViewer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static IHost _host;

        public static IHost Host => _host ??= CreateHostBuilder().Build();

        private static IHostBuilder CreateHostBuilder()
        {
            return Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    services.AddHttpClient();

                    services.AddSingleton<ISettingsService, SettingsService>();

                    services.AddSingleton<MainWindow>();
                    services.AddSingleton<OverlayWindow>();
                });
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            await Host.StartAsync();

            var mainWindow = Host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();

            base.OnStartup(e);
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            if (_host != null)
            {
                await _host.StopAsync();
                _host.Dispose();
            }

            base.OnExit(e);
        }
    }
}
