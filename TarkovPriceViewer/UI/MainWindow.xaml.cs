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

        public MainWindow()
        {
            InitializeComponent();

            _settingsService = new SettingsService();
            _settingsService.Load();

            var version = VersionHelper.GetDisplayVersion();
            Title = $"Tarkov Price Viewer v{version}";
        }
    }
}