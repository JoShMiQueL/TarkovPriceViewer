using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using TarkovPriceViewer.Services;
using TarkovPriceViewer.Utils;

namespace TarkovPriceViewer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ISettingsService _settingsService;
        private readonly ITarkovDataService _tarkovDataService;
        private readonly ITarkovTrackerService _tarkovTrackerService;

        public MainWindow(
            ISettingsService settingsService,
            ITarkovDataService tarkovDataService,
            ITarkovTrackerService tarkovTrackerService)
        {
            InitializeComponent();

            _settingsService = settingsService;
            _tarkovDataService = tarkovDataService;
            _tarkovTrackerService = tarkovTrackerService;

            _settingsService.Load();

            _ = _tarkovDataService.UpdateItemListAPIAsync();
            _ = _tarkovTrackerService.UpdateTarkovTrackerAPI();

            var version = VersionHelper.GetDisplayVersion();
            Title = $"Tarkov Price Viewer v{version}";
        }
    }
}