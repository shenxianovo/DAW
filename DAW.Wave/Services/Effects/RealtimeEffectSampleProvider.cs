using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NAudio.Wave;
using Windows.Media.Effects;

namespace DAW.Wave.Services.Effects;

public class RealtimeEffectSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private IList<IAudioEffect> _effects;
    public WaveFormat WaveFormat => _source.WaveFormat;

    public RealtimeEffectSampleProvider(ISampleProvider source, IList<IAudioEffect> effects)
    {
        _source = source;
        _effects = effects;
    }
    public int Read(float[] buffer, int offset, int count)
    {
        // 先从原始音源中读取
        int samplesRead = _source.Read(buffer, offset, count);
        if (samplesRead > 0)
        {
            // 再对读取到的样本应用所有启用的效果
            foreach (var effect in _effects)
            {
                if (effect.Enabled)
                    effect.ProcessSamples(buffer, offset, samplesRead);
            }
        }
        return samplesRead;
    }

    public void UpdateEffects(IList<IAudioEffect> newEffects)
    {
        var newList = new List<IAudioEffect>();
        foreach (var effect in newEffects)
        {
            newList.Add(effect);
        }
        _effects = newList;
    }

}
