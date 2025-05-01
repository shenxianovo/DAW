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
using DAW.Extensions;
using DAW.ViewModels.Effects;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DAW.Views.Effects
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class GraphicEQEffectPage : Page
    {
        public GraphicEQEffectViewModel ViewModel { get; set; }
        public GraphicEQEffectPage(GraphicEQEffectViewModel vm)
        {
            this.InitializeComponent();
            this.SetDesiredHeight(500);
            this.SetDesiredWidth(1100);
            ViewModel = vm;
        }
    }
}
