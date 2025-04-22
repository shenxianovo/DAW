using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.ApplicationSettings;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DAW.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ShellPage : Page
    {
        public ShellPage()
        {
            this.InitializeComponent();
            ContentFrame.Navigate(typeof(WavePage));

            foreach (var menuItem in NavView.MenuItems.OfType<NavigationViewItem>())
            {
                if (menuItem.Tag?.ToString() == "WavePage")
                {
                    NavView.SelectedItem = menuItem;
                    break;
                }
            }
        }

        private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.IsSettingsInvoked == true)
            {
                ContentFrame.Navigate(typeof(SettingsPage), args.RecommendedNavigationTransitionInfo);
            }

            NavigationViewItem? itemContent = args.InvokedItemContainer as NavigationViewItem;

            if (itemContent == null)
            {
                return;
            }

            switch (itemContent.Tag)
            {
                case "WavePage":
                    ContentFrame.Navigate(typeof(WavePage));
                    break;
                default:
                    break;
            }
        }
    }
}