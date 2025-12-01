using System.Windows;
using TarkovPriceViewer.Services;

namespace TarkovPriceViewer.UI
{
    public partial class OverlayWindow : Window
    {
        private readonly ISettingsService _settingsService;
        private readonly ITarkovDataService _tarkovDataService;
        private readonly ITarkovTrackerService _tarkovTrackerService;

        public OverlayWindow(
            ISettingsService settingsService,
            ITarkovDataService tarkovDataService,
            ITarkovTrackerService tarkovTrackerService)
        {
            InitializeComponent();

            _settingsService = settingsService;
            _tarkovDataService = tarkovDataService;
            _tarkovTrackerService = tarkovTrackerService;

            var opacity = _settingsService.Settings.OverlayOpacity;
            if (opacity < 0) opacity = 0;
            if (opacity > 100) opacity = 100;
            Opacity = opacity * 0.01;

            Width = SystemParameters.PrimaryScreenWidth;
            Height = SystemParameters.PrimaryScreenHeight;
            Left = 0;
            Top = 0;
        }
    }
}
