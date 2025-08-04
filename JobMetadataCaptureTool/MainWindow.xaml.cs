using PuppeteerSharp;
using System.Windows.Interop;
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

            var screenWidth = (int)SystemParameters.PrimaryScreenWidth;
            var screenHeight = (int)SystemParameters.PrimaryScreenHeight;

            var wpfWindow = Window.GetWindow(this);
            wpfWindow.Left = 0;
            wpfWindow.Top = 0;
            wpfWindow.Width = screenWidth / 2;
            wpfWindow.Height = screenHeight;

            // Position the browser to the right of the WPF UI 

            _browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = false,
                DefaultViewport = null,
                Args = new[]
                    {
                        $"--window-position={screenWidth / 2},0",
                        $"--window-size={screenWidth / 2},{screenHeight}",
                        $"--disable-popup-blocking"
                    }
            });

            // grab the default first page too
           var pages = await _browser.PagesAsync();
            _page = pages.First();

            LaunchButton.IsEnabled = false;
            MessageBox.Show("Browser launched. Navigate to a job search page, then click 'Capture Metadata'.");
        }

        private async void CaptureButton_Click(object sender, RoutedEventArgs e)
        {
           
            if (_browser == null)
            {
                MessageBox.Show("Browser not launched.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var pages = await _browser.PagesAsync();

            foreach (var page in pages)
            {
                try
                {
                    var isVisible = await page.EvaluateExpressionAsync<string>("document.visibilityState") == "visible";
                    if (isVisible)
                    {
                        _page = page;
                        break;
                    }
                }
                catch
                {
                    // In case page is in a weird state
                    continue;
                }
            }

            if (_page == null)
            {
                MessageBox.Show("No visible tab found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            await _page.BringToFrontAsync();

            var html = await _page.GetContentAsync();
            Console.WriteLine("Capturing HTML content...", html.ToString());

            if (string.IsNullOrEmpty(html))
            {
                MessageBox.Show("No HTML content found on the page.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                 return;
            }

            var anchorHandle = await _page.QuerySelectorAsync("a.navbar-brand");

            if (anchorHandle != null)
            {
                var href = await anchorHandle.EvaluateFunctionAsync<string>("a => a.href");
                if (!string.IsNullOrEmpty(href))
                {
                    var uti = new Uri(href);
                    var host = uti.Host;

                    var parts = host.Split('.');
                    string compName = (parts.Length >= 2 && parts[0] == "www")
                        ? parts[1]
                        : parts[0];

                    Console.WriteLine("Company name extracted from anchor: " + compName);

                }
                else
                {
                    MessageBox.Show("Anchor found but no href attribute.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            else
            {
                Console.WriteLine("No anchor with class 'navbar-brand' found on the page.");
            }

            var url = _page.Url;
            var uri = new Uri(url);
            Console.WriteLine("Capturing from: " + url);

            // getting the company name from the url might now work, maybe get it from the page data
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