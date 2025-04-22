using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DAW.ViewModels;

public partial class WaveViewModel : ObservableRecipient
{

    [ObservableProperty]
    public partial string Name { get; set; } = "WaveViewModel";
    public WaveViewModel()
    {
    }
}
