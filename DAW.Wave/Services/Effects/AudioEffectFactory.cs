using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAW.Wave.Services.Effects;

public class AudioEffectFactory
{
    public IAudioEffect CreateEffect(string name, int sampleRate)
    {
        return name switch
        {
            "Volume" => new VolumeEffect(),
            "Distortion" => new DistortionEffect(),
            "GraphicEQ" => new GraphicEQEffect(sampleRate),
            "Reverb" => new ReverbEffect(sampleRate),
            _ => null
        };
    }
}
