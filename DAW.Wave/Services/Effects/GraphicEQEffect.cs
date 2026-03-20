using NAudio.Dsp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DAW.Wave.Services.Effects;

public class GraphicEQEffect : IAudioEffect
{
    public bool Enabled { get; set; } = true;
    public string Name { get; set; } = "Graphic EQ";

    private readonly float[] _gains = new float[10];

    /// <summary>
    /// 使用 volatile 不可变数组快照保证线程安全：
    /// UI 线程替换整个数组引用，音频线程读取快照。
    /// </summary>
    private volatile BiQuadFilter[] _filtersSnapshot;

    private readonly float[] _frequencies = new float[]
    {
        31.25f, 62.5f, 125f, 250f, 500f,
        1000f, 2000f, 4000f, 8000f, 16000f
    };

    public float this[int band]
    {
        get => _gains[band];
        set
        {
            _gains[band] = value;
            // 创建新快照替换整个数组引用（原子操作）
            var newFilters = (BiQuadFilter[])_filtersSnapshot.Clone();
            newFilters[band] = BiQuadFilter.PeakingEQ(_sampleRate, _frequencies[band], 1.0f, value);
            _filtersSnapshot = newFilters;
        }
    }

    private readonly int _sampleRate;

    public GraphicEQEffect(int sampleRate)
    {
        _sampleRate = sampleRate;
        var filters = new BiQuadFilter[10];
        for (int i = 0; i < 10; i++)
        {
            _gains[i] = 0f;
            filters[i] = BiQuadFilter.PeakingEQ(sampleRate, _frequencies[i], 1.0f, 0f);
        }
        _filtersSnapshot = filters;
    }

    public void ProcessSamples(float[] buffer, int offset, int count)
    {
        var filters = _filtersSnapshot; // 读取快照（单次引用读取）
        for (int i = 0; i < count; i++)
        {
            float sample = buffer[offset + i];
            for (int b = 0; b < filters.Length; b++)
            {
                sample = filters[b].Transform(sample);
            }
            buffer[offset + i] = sample;
        }
    }
}
