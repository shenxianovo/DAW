using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using DAW.ViewModels;
using DAW.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DAW
{
    public partial class App : Application
    {
        public IHost Host { get; }
        public static Window MainWindow { get; } = new MainWindow();

        public static T GetService<T>() where T : class
        {
            if ((App.Current as App)!.Host.Services.GetService(typeof(T)) is not T service)
            {
                throw new ArgumentException($"{typeof(T)} needs to be registered in ConfigureServices within App.xaml.cs.");
            }

            return service;
        }
        public App()
        {
            this.InitializeComponent();

            Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder().
                ConfigureServices((content, services) =>
                {
                    // Services

                    // ViewModels and Views
                    services.AddTransient<ShellPage>();
                    services.AddTransient<SettingsPage>();
                    services.AddSingleton<WaveViewModel>();
                    services.AddTransient<WavePage>();
                }).
                Build();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            MainWindow.Activate();
            MainWindow.ExtendsContentIntoTitleBar = true;
            MainWindow.Content = App.GetService<ShellPage>();
        }
    }
}
