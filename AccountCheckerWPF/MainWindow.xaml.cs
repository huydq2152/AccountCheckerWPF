using System.Collections.Concurrent;
using System.IO;
using Microsoft.Win32;
using System.Windows;
using AccountCheckerWPF.Models;
using AccountCheckerWPF.Services;

namespace AccountCheckerWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        static BlockingCollection<string> AccCh = new BlockingCollection<string>();
        static SemaphoreSlim semaphore;
        static int botCount = 10;
        WorkerService _workerService = new WorkerService();
        
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

            if (string.IsNullOrWhiteSpace(SelectAccountFileTxt.Text))
            {
                MessageBox.Show("Please select an account file first.");
                return;
            }
            
            var comboManager = new ComboManager();
            
            int comboCount;
            try
            {
                comboCount = comboManager.LoadFromFile(SelectAccountFileTxt.Text);
                Console.WriteLine($"Loaded {comboCount} combos");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Please ensure proxies.txt & combos.txt exist!");
                Console.ReadLine();
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

            string hitFilePath = Path.Combine(hitsDirectory, DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".txt");
            using (StreamWriter hitFileWriter = new StreamWriter(hitFilePath, false))
            {
                semaphore = new SemaphoreSlim(botCount, botCount);

                List<Task> tasks = new List<Task>();
                for (int i = 0; i < botCount; i++)
                {
                    tasks.Add(Task.Run(() => _workerService.WorkerFunc()));
                }

                foreach (string combo in comboManager.ComboList)
                {
                    AccCh.Add(combo);
                }
                AccCh.CompleteAdding();

                Task.WhenAll(tasks);

                Console.WriteLine("Done checking!");
            }
        }
    }
}