using System.Windows;
using AccountCheckerWPF.Managers;
using AccountCheckerWPF.Services;
using AccountCheckerWPF.Services.Interface;
using Microsoft.Extensions.DependencyInjection;

namespace AccountCheckerWPF
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private readonly ServiceProvider _serviceProvider;

        public App()
        {
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);
            _serviceProvider = serviceCollection.BuildServiceProvider();
        }

        private void ConfigureServices(ServiceCollection services)
        {
            services.AddSingleton<MainWindow>();
            services.AddHttpClient<IHttpServices, HttpServices>();
            services.AddSingleton<IHttpServices, HttpServices>();
            services.AddSingleton<ProxyManager>();
            services.AddSingleton<AccountManager>();
        }

        private void OnStartup(object sender, StartupEventArgs e)
        {
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
    }
}
