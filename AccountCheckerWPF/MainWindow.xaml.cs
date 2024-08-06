using System.Net.Http;
using Microsoft.Win32;
using System.Windows;
using AccountCheckerWPF.Enums;
using AccountCheckerWPF.Models;

namespace AccountCheckerWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
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

        private void CheckAccountBtn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SelectProxyFileTxt.Text))
            {
                MessageBox.Show("Please select a proxy file first.");
                return;
            }

            try
            {
                var proxyManager = new ProxyManager();
                int count = proxyManager.LoadFromFile(SelectProxyFileTxt.Text,
                    ProxyTypeEnums.HTTP);

                if (count > 0)
                {
                    Proxy proxy;
                    var handler = proxyManager.GetRandomProxyTransport(out proxy);
                    var client = new HttpClient(handler);

                    var response =  client.GetAsync("https://httpbin.org/get");
                    var content =  response.Result.Content.ReadAsStringAsync();

                    MessageBox.Show("Response from https://httpbin.org/get:\n" + content);
                }
                else
                {
                    MessageBox.Show("No proxies loaded from file.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }
        }
    }
}