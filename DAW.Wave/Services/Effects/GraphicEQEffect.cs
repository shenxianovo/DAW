using NAudio.Dsp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAW.Wave.Services.Effects;

public class GraphicEQEffect : IAudioEffect
{
    public bool Enabled { get; set; } = true;
    public string Name { get; set; } = "Graphic EQ";

    private readonly BiQuadFilter[] _filters = new BiQuadFilter[10];
    private readonly float[] _gains = new float[10];

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
            _filters[band] = BiQuadFilter.PeakingEQ(sampleRate, _frequencies[band], 1.0f, value);
        }
    }

    private readonly int sampleRate;

    public GraphicEQEffect(int sampleRate)
    {
        this.sampleRate = sampleRate;
        for (int i = 0; i < 10; i++)
        {
            _gains[i] = 0f; // 默认不增不减 (0 dB)
            _filters[i] = BiQuadFilter.PeakingEQ(sampleRate, _frequencies[i], 1.0f, 0f);
        }
    }

    public void ProcessSamples(float[] buffer, int offset, int count)
    {
        for (int i = 0; i < count; i++)
        {
            float sample = buffer[offset + i];
            for (int b = 0; b < 10; b++)
            {
                sample = _filters[b].Transform(sample);
            }

            buffer[offset + i] = sample;
        }
    }
}
