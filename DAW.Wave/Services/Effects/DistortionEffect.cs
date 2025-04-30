using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAW.Wave.Services.Effects;

public class DistortionEffect : IAudioEffect
{
    public bool Enabled { get; set; } = true;
    public string Name { get; set; } = "Distortion";

    public float Gain { get; set; } = 5.0f;       // 输入增益
    public float Level { get; set; } = 0.8f;      // 输出音量

    public void ProcessSamples(float[] buffer, int offset, int count)
    {
        for (int i = 0; i < count; i++)
        {
            // 应用增益
            float sample = buffer[offset + i] * Gain;

            // 简单的 soft clipping
            if (sample > 1.0f)
                sample = 1.0f;
            else if (sample < -1.0f)
                sample = -1.0f;

            // 调整输出音量
            buffer[offset + i] = sample * Level;
        }
    }
}
