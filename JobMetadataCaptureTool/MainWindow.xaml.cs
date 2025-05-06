using PuppeteerSharp;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace JobMetadataCaptureTool
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private dynamic _capturedMetadata;
        public MainWindow()
        {
            InitializeComponent();
        }

        private async void CaptureButton_Click(object sender, RoutedEventArgs e)
        {
            var browserFetcher = new BrowserFetcher();
            await browserFetcher.DownloadAsync();

            var browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = false });
            var page = await browser.NewPageAsync();

            page.Request += (s, e) =>
            {
                var url = e.Request.Url;
                if (url.Contains("jobs") || url.Contains("career"))
                {
                    var uri = new Uri(url);
                    _capturedMetadata = new
                    {
                        companName = "",
                        domain = uri.Host.Replace("www.", ""),
                        subdomain = uri.Host.Split('.')[0],
                        jobSearchPath = uri.AbsolutePath,
                        jobSearchUrl = url,
                        lastVerified = DateTime.Now,
                        notes = "Auto-captured",
                        searchStrategies = new[]
                        {
                            new
                            {
                                searchType = "queryParam",
                                exampleQuery = uri.Query,
                                method = e.Request.Method,
                                matchPattern = uri.Query.Replace("?", "").Replace("=", ":"),
                                headers = new { }
                            }
                        }
                    };

                    Dispatcher.Invoke(() =>
                    {
                        OutputBox.Text = JsonSerializer.Serialize(_capturedMetadata, new JsonSerializerOptions { WriteIndented = true });
                    });
                }
            };
            await page.GoToAsync("https://careers.google.com/jobs/search");

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

            var json = JsonSerializer.Serialize(new[] { _capturedMetadata });
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync("http//localhost:5216/api/job-metadata", content);

            MessageBox.Show(response.IsSuccessStatusCode ? "Sent!" : "Failed to send.", "Status", MessageBoxButton.OK);
        }
    }
}