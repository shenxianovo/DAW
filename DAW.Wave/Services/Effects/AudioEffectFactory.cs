using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAW.Wave.Services.Effects;

public class AudioEffectFactory
{
    private readonly Dictionary<string, Func<IAudioEffect>> _effectCreators =
        new()
        {
            { "volume", () => new VolumeEffect() },
            { "distortion", () => new DistortionEffect()}
            //{ "echo", () => new EchoEffect() },
            //{ "reverb", () => new ReverbEffect() }
        };

    public IAudioEffect CreateEffect(string effectName)
    {
        return _effectCreators.TryGetValue(effectName, out var creator)
            ? creator()
            : null;
    }
}
