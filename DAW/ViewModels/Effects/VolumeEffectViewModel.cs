using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using DAW.Wave.Services;
using DAW.Wave.Services.Effects;

namespace DAW.ViewModels.Effects;

public class VolumeEffectViewModel : ObservableRecipient
{
    private readonly VolumeEffect _volumeEffect;

    public float Volume
    {
        get => _volumeEffect.Volume;
        set
        {
            _volumeEffect.Volume = value;
            OnPropertyChanged();
        }
    }

    public VolumeEffectViewModel(IAudioEffect audioEffect)
    {
        _volumeEffect = audioEffect as VolumeEffect;
    }
}
