using CommunityToolkit.Mvvm.ComponentModel;
using DAW.Wave.Services.Effects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DAW.Wave.Services;

namespace DAW.ViewModels.Effects;

public class ReverbEffectViewModel : ObservableObject
{
    private readonly ReverbEffect _effect;

    public float WetMix
    {
        get => _effect.WetMix;
        set
        {
            if (_effect.WetMix != value)
            {
                _effect.WetMix = value;
                OnPropertyChanged();
            }
        }
    }

    public float Decay
    {
        get => _effect.Decay;
        set
        {
            if (_effect.Decay != value)
            {
                _effect.Decay = value;
                OnPropertyChanged();
            }
        }
    }

    public ReverbEffectViewModel(IAudioEffect effect)
    {
        _effect = effect as ReverbEffect;
    }
}
