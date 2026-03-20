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

        /// <summary>
        /// 追踪已打开的效果器窗口，防止同一效果器多开
        /// </summary>
        private readonly Dictionary<IAudioEffect, EffectWindow> _openEffectWindows = new();

        public WavePage()
        {
            this.InitializeComponent();
        }

        private void OnEffectItemDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is IAudioEffect effect)
            {
                // 如果该效果器窗口已打开，激活已有窗口
                if (_openEffectWindows.TryGetValue(effect, out var existingWindow))
                {
                    try
                    {
                        existingWindow.Activate();
                        return;
                    }
                    catch
                    {
                        // 窗口已关闭或无效，移除引用
                        _openEffectWindows.Remove(effect);
                    }
                }

                var window = new EffectWindow(effect);
                _openEffectWindows[effect] = window;

                // 窗口关闭时自动移除引用
                window.Closed += (s, args) => _openEffectWindows.Remove(effect);
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

        private void Close(object sender, RoutedEventArgs e)
        {
            ViewModel.CloseCommand.Execute(new object());
        }
    }
}
