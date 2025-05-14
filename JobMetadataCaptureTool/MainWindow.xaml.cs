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

        private void AttachBrowserEventHandlers()
        {
            _browser.TargetCreated += async (s, eArgs) =>
            {
                var newPage = await eArgs.Target.PageAsync();
                if (newPage != null)
                {
                    try
                    {
                        await newPage.BringToFrontAsync();

                        await newPage.WaitForNavigationAsync(new NavigationOptions
                        {
                            WaitUntil = new[] { WaitUntilNavigation.Networkidle0 }
                        });

                        _page = newPage; // Set this as the new active page
                        Console.WriteLine("New tab detected and assigned as active: " + _page.Url);
                    }
                    catch
                    {
                        await newPage.WaitForTimeoutAsync(1000);
                        _page = newPage;
                    }
                }
            };
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

            var browserFetcher = new BrowserFetcher();
            await browserFetcher.DownloadAsync();

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

            AttachBrowserEventHandlers();

            var pages = await _browser.PagesAsync();
            _page = pages.First();

            LaunchButton.IsEnabled = false;
            MessageBox.Show("Browser launched. Navigate to a job search page, then click 'Capture Metadata'.");
        }

        private void CaptureButton_Click(object sender, RoutedEventArgs e)
        {
            if (_page == null || _page.IsClosed)
            {
                MessageBox.Show("Browser is not open or tab is closed.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var url = _page.Url;
            var uri = new Uri(url);
            Console.WriteLine("Capturing from: " + url);
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