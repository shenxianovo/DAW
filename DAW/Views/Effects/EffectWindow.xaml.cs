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
using Microsoft.UI.Windowing;
using System.Runtime.InteropServices;
using DAW.Extensions;
using DAW.Factories;
using DAW.ViewModels.Effects;
using DAW.Wave.Services;
using Microsoft.UI;
using WinRT.Interop;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DAW.Views.Effects
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class EffectWindow : Window
    {
        private AppWindow? appWindow;
        private bool _firstActivation = true;

        public EffectWindow(IAudioEffect effect)
        {
            this.InitializeComponent();
            this.ExtendsContentIntoTitleBar = true;

            try
            {
                appWindow = GetAppWindowForCurrentWindow();

                OverlappedPresenter presenter = OverlappedPresenter.Create();
                presenter.IsMinimizable = false;
                presenter.IsMaximizable = false;
                appWindow.SetPresenter(presenter);

                var page = EffectUiFactory.CreateEffectPage(effect);

                var width = (int)page.GetDesiredWidth();
                var height = (int)page.GetDesiredHeight();
                // 兜底：确保窗口有最小尺寸
                if (width <= 0) width = 400;
                if (height <= 0) height = 300;

                appWindow.Resize(new Windows.Graphics.SizeInt32(width, height));
                this.Content = page;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing EffectWindow: {ex.Message}");
            }

            // 延迟到窗口首次激活后再设置 Owner 和居中，避免 HWND 未就绪的竞态
            this.Activated += OnFirstActivated;

            // 显示窗口
            this.Activate();
        }

        private void OnFirstActivated(object sender, WindowActivatedEventArgs args)
        {
            if (!_firstActivation) return;
            _firstActivation = false;

            try
            {
                if (appWindow != null && App.MainWindow != null)
                {
                    SetOwnership(appWindow, App.MainWindow);
                    CenterToMainWindow();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting EffectWindow ownership/position: {ex.Message}");
            }
        }

        private AppWindow GetAppWindowForCurrentWindow()
        {
            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
            return AppWindow.GetFromWindowId(windowId);
        }

        private void CenterToMainWindow()
        {
            if (appWindow == null) return;

            try
            {
                var mainHwnd = WindowNative.GetWindowHandle(App.MainWindow);
                var mainWindowId = Win32Interop.GetWindowIdFromWindow(mainHwnd);
                var mainAppWindow = AppWindow.GetFromWindowId(mainWindowId);
                if (mainAppWindow is null) return;

                var mainPos = mainAppWindow.Position;
                var mainSize = mainAppWindow.Size;
                var newSize = appWindow.Size;

                int centerX = mainPos.X + (mainSize.Width - newSize.Width) / 2;
                int centerY = mainPos.Y + (mainSize.Height - newSize.Height) / 2;

                appWindow.Move(new Windows.Graphics.PointInt32(centerX, centerY));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CenterToMainWindow failed: {ex.Message}");
            }
        }

        private static void SetOwnership(AppWindow ownedAppWindow, Window ownerWindow)
        {
            IntPtr parentHwnd = WindowNative.GetWindowHandle(ownerWindow);
            IntPtr ownedHwnd = Win32Interop.GetWindowFromWindowId(ownedAppWindow.Id);

            if (IntPtr.Size == 8)
                SetWindowLongPtr(ownedHwnd, -8, parentHwnd); // GWLP_HWNDPARENT
            else
                SetWindowLong(ownedHwnd, -8, parentHwnd);
        }


        [DllImport("User32.dll", CharSet = CharSet.Auto, EntryPoint = "SetWindowLongPtr")]
        public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("User32.dll", CharSet = CharSet.Auto, EntryPoint = "SetWindowLong")]
        public static extern IntPtr SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
    }
}
