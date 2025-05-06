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

            _page = await _browser.NewPageAsync();
            await _page.GoToAsync("about:blank");

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

            var pageTitle = await _page.GetTitleAsync();
            string companyName = ExtractCompanyNameFromTitle.ExtractNameFromTitle(pageTitle);
            if (string.IsNullOrWhiteSpace(companyName))
            {
                var domainParts = uri.Host.Replace("www.", "").Split('.');
                companyName = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(domainParts.Length > 1
                    ? domainParts[domainParts.Length - 2]
                    : domainParts[0]);
            }

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

        //private async void CaptureButton_Click(object sender, RoutedEventArgs e)
        //{
        //    var browserFetcher = new BrowserFetcher();
        //    await browserFetcher.DownloadAsync();

        //    var browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = false });
        //    var page = await browser.NewPageAsync();
        //    var pageTitle = await page.GetTitleAsync();
        //    string cName = ExtractCompanyNameFromTitle.ExtractNameFromTitle(pageTitle);
        //    page.Request += (s, e) =>
        //    {
        //        var url = e.Request.Url;
        //        if (url.Contains("jobs") || url.Contains("career"))
        //        {
        //            var uri = new Uri(url);
        //            _capturedMetadata = new
        //            {
        //                companyName = cName,
        //                domain = uri.Host.Replace("www.", ""),
        //                subdomain = uri.Host.Split('.')[0],
        //                jobSearchPath = uri.AbsolutePath,
        //                jobSearchUrl = url,
        //                lastVerified = DateTime.Now,
        //                notes = "Auto-captured",
        //                searchStrategies = new[]
        //                {
        //                    new
        //                    {
        //                        searchType = "queryParam",
        //                        exampleQuery = uri.Query,
        //                        method = e.Request.Method,
        //                        matchPattern = uri.Query.Replace("?", "").Replace("=", ":"),
        //                        headers = new { }
        //                    }
        //                }
        //            };

        //            Dispatcher.Invoke(() =>
        //            {
        //                OutputBox.Text = JsonSerializer.Serialize(_capturedMetadata, new JsonSerializerOptions { WriteIndented = true });
        //            });
        //        }
        //    };
        //    //await page.GoToAsync("https://careers.google.com/jobs/search");

        //}

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