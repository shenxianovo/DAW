using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using DAW.ViewModels.Effects;
using DAW.Extensions;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DAW.Views.Effects
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ReverbEffectPage : Page
    {
        public ReverbEffectViewModel ViewModel { get; set; }

        public ReverbEffectPage(ReverbEffectViewModel vm)
        {
            this.InitializeComponent();
            this.SetDesiredHeight(300);
            this.SetDesiredWidth(400);
            ViewModel = vm;
        }
    }
}
