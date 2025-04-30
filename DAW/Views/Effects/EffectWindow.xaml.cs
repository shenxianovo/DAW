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
        private AppWindow appWindow;
        public EffectWindow(IAudioEffect effect)
        {
            this.InitializeComponent();
            this.ExtendsContentIntoTitleBar = true;

            appWindow = GetAppWindowForCurrentWindow();
            appWindow.Resize(new Windows.Graphics.SizeInt32(400, 300));

            OverlappedPresenter presenter = OverlappedPresenter.Create();
            presenter.IsMinimizable = false;  // 禁用最小化
            presenter.IsMaximizable = false;  // 禁用最大化

            appWindow.SetPresenter(presenter);

            SetOwnership(appWindow, App.MainWindow);

            this.Content = EffectUiFactory.CreateEffectPage(effect);

            CenterToMainWindow();

            // 显示窗口
            appWindow.Show();
        }

        private AppWindow GetAppWindowForCurrentWindow()
        {
            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
            return AppWindow.GetFromWindowId(windowId);
        }

        private void CenterToMainWindow()
        {
            var mainAppWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(WindowNative.GetWindowHandle(App.MainWindow)));
            if (mainAppWindow is null) return;

            var mainPos = mainAppWindow.Position;
            var mainSize = mainAppWindow.Size;

            var newSize = appWindow.Size;

            int centerX = mainPos.X + (mainSize.Width - newSize.Width) / 2;
            int centerY = mainPos.Y + (mainSize.Height - newSize.Height) / 2;

            appWindow.Move(new Windows.Graphics.PointInt32(centerX, centerY));
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
