using NAudio.Wave.SampleProviders;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAW.Wave.Services.Effects;

public class VolumeEffect : IAudioEffect
{
    public bool Enabled { get; set; } = true;
    public string Name { get; set; } = "Volume";
    public float Volume { get; set; } = 1.0f; // 默认音量为 1.0 (100%)

    public void ProcessSamples(float[] buffer, int offset, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var sample = buffer[offset + i] * Volume;
            if (sample > 1.0f) buffer[offset + i] = 1;
            else buffer[offset + i] = sample;
        }
    }
}
