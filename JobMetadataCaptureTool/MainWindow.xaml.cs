using PuppeteerSharp;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;

namespace JobMetadataCaptureTool
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private dynamic _capturedMetadata;
        private Browser _browser;
        private Page _page;
        public MainWindow()
        {
            InitializeComponent();
        }

        private async void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            if (_browser != null && !_browser.IsClosed)
            {
                MessageBox.Show("Browser already launched.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var browserFetcher = new BrowserFetcher();
            await browserFetcher.DownloadAsync();

            _browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = false,
                DefaultViewport = null
            });

            var pages = await _browser.PagesAsync();
            _page = pages.First();

            LaunchButton.IsEnabled = false;
            MessageBox.Show("Browser launched. Navigate to a job search page, then click 'Capture Metadata'.");
        }

        private async void CaptureButton_Click(object sender, RoutedEventArgs e)
        {
            if (_page == null || _page.IsClosed)
            {
                MessageBox.Show("Browser is not open or tab is closed.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var url = _page.Url;
            var uri = new Uri(url);

            string companyName = GetCompanyNameFromUri.ExtractNameFromTitle(uri);


            _capturedMetadata = new
            {
                companyName,
                domain = uri.Host.Replace("www.", ""),
                subdomain = uri.Host.Split('.')[0],
                jobSearchPath = uri.AbsolutePath,
                jobSearchUrl = url,
                lastVerified = DateTime.UtcNow,
                notes = "Captured manually",
                searchStrategies = new[]
                {
            new
            {   
                companyName,
                searchType = "queryParam",
                exampleQuery = uri.Query,
                method = "GET",
                matchPattern = uri.Query.Replace("?", "").Replace("=", ":"),
                headers = new { }
            }
        }
            };

            OutputBox.Text = JsonSerializer.Serialize(_capturedMetadata, new JsonSerializerOptions { WriteIndented = true });
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            if (_capturedMetadata == null)
            {
                MessageBox.Show("No metadata captured.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            // using locally
            HttpClientHandler handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
            var httpClient = new HttpClient(handler);

            var json = JsonSerializer.Serialize(_capturedMetadata );
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync("https://localhost:7255/api/company", content);

            MessageBox.Show(response.IsSuccessStatusCode ? "Sent!" : "Failed to send.", "Status", MessageBoxButton.OK);
        }
    }
}