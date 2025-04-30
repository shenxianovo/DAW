using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DAW.Wave.Services;
using DAW.Wave.Services.Effects;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DAW.ViewModels.Effects;

public class DistortionEffectViewModel : ObservableRecipient
{
    private readonly DistortionEffect _effect;

    public float Gain
    {
        get => _effect.Gain;
        set
        {
            _effect.Gain = value;
            OnPropertyChanged();
        }
    }

    public float Level
    {
        get => _effect.Level;
        set
        {
            _effect.Level = value;
            OnPropertyChanged();
        }
    }
    public DistortionEffectViewModel(IAudioEffect audioEffect)
    {
        _effect = audioEffect as DistortionEffect;
    }
}
