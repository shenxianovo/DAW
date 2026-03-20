using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using Windows.Media.Effects;

namespace DAW.Wave.Services.Effects;

public class RealtimeEffectSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    /// <summary>
    /// 使用 volatile + 不可变数组快照实现无锁线程安全。
    /// UI 线程通过 UpdateEffects 替换整个引用，音频线程在 Read 中读取快照。
    /// </summary>
    private volatile IAudioEffect[] _effectsSnapshot = Array.Empty<IAudioEffect>();
    public WaveFormat WaveFormat => _source.WaveFormat;

    public RealtimeEffectSampleProvider(ISampleProvider source, IList<IAudioEffect> effects)
    {
        _source = source;
        _effectsSnapshot = effects?.ToArray() ?? Array.Empty<IAudioEffect>();
    }

    public int Read(float[] buffer, int offset, int count)
    {
        int samplesRead = _source.Read(buffer, offset, count);
        if (samplesRead > 0)
        {
            // 读取快照引用（单次读取，线程安全）
            var effects = _effectsSnapshot;
            for (int i = 0; i < effects.Length; i++)
            {
                if (effects[i].Enabled)
                    effects[i].ProcessSamples(buffer, offset, samplesRead);
            }
        }
        return samplesRead;
    }

    /// <summary>
    /// 原子替换效果链快照。线程安全：UI 线程写入，音频线程读取。
    /// </summary>
    public void UpdateEffects(IList<IAudioEffect> newEffects)
    {
        _effectsSnapshot = newEffects?.ToArray() ?? Array.Empty<IAudioEffect>();
    }
}
