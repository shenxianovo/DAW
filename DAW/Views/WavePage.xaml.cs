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
using DAW.ViewModels;
using DAW.ViewModels.Effects;
using DAW.Views.Effects;
using System.Drawing.Imaging.Effects;
using DAW.Utils;
using DAW.Wave.Services;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DAW.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class WavePage : Page
    {
        public WaveViewModel ViewModel { get; set; } = App.GetService<WaveViewModel>();

        public WavePage()
        {
            this.InitializeComponent();
        }

        private void OnEffectItemDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is IAudioEffect effect)
            {
                var window = new EffectWindow(effect);
            }
        }

        private void RemoveEffect(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is IAudioEffect effect)
            {
                ViewModel.RevomeEffect(effect);
            }
        }

        private async void Export(object sender, RoutedEventArgs e)
        {
            var file = await FilePickerHelper.ShowSavePickerAsync(Path.GetFileNameWithoutExtension(ViewModel.CurrentAudioFile.FileName));
            if (file == null) return;

            try
            {
                await ViewModel.ExportFileAsync(file.Path);

                ContentDialog dialog = new ContentDialog
                {
                    Title = "导出成功",
                    Content = $"成功导出为{file.Path}",
                    CloseButtonText = "确定",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();

            }
            catch (Exception ex)
            {
                ContentDialog dialog = new ContentDialog
                {
                    Title = "导出失败",
                    Content = ex.Message,
                    CloseButtonText = "确定",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
            }
        }
    }
}
