using DAW.ViewModels.Effects;
using DAW.Views.Effects;
using DAW.Wave.Services.Effects;
using DAW.Wave.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;

namespace DAW.Factories;

public class EffectUiFactory
{
    public static Page CreateEffectPage(IAudioEffect effect)
    {
        return effect switch
        {
            VolumeEffect => new VolumeEffectPage(new VolumeEffectViewModel(effect)),
            DistortionEffect => new DistortionEffectPage(new DistortionEffectViewModel(effect)),
            GraphicEQEffect => new GraphicEQEffectPage(new GraphicEQEffectViewModel(effect)),
            ReverbEffect => new ReverbEffectPage(new ReverbEffectViewModel(effect)),
            _ => throw new NotSupportedException($"Unsupported effect type: {effect.GetType().Name}")
        };
    }
}
