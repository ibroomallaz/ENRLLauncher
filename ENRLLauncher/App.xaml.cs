using ENRLLauncher.Core.Interfaces;
using ENRLLauncher.Core.Logging;
using ENRLLauncher.MVVM.Model;
using ENRLLauncher.MVVM.ViewModel;
using Microsoft.Extensions.DependencyInjection;
using System.Configuration;
using System.Data;
using System.Windows;

namespace ENRLLauncher
{
    public partial class App : Application
    {
        private IServiceProvider _serviceProvider = null!;

        public static IServiceProvider Services { get; private set; } = default!;
        // DI container setup
        private void ConfigureServices()
        {
            var services = new ServiceCollection();
            //services
            services.AddSingleton<IAppLogger>(_ => new FileLogger(Globals.g_LogsDir));
            services.AddSingleton<ILauncherService>();

            //Views
            services.AddSingleton<MainWindowViewModel>();
            services.AddSingleton<SettingsViewModel>();

            _serviceProvider = services.BuildServiceProvider();
        }
    }

}
