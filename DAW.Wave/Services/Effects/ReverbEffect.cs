using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAW.Wave.Services.Effects;

public class ReverbEffect : IAudioEffect
{
    public bool Enabled { get; set; } = true;
    public string Name { get; set; } = "Reverb";

    private readonly int[] _delayTimesMs = [29, 37, 41, 53]; // prime delays for decorrelation
    private readonly float[][] _delayLines;
    private readonly int[] _positions;
    private readonly int _sampleRate;

    public float WetMix { get; set; } = 0.3f;   // 0 = dry only, 1 = wet only
    public float Decay { get; set; } = 0.5f;    // 0 = no reverb tail, 1 = infinite feedback

    public ReverbEffect(int sampleRate)
    {
        this._sampleRate = sampleRate;
        _delayLines = new float[_delayTimesMs.Length][];
        _positions = new int[_delayTimesMs.Length];

        for (int i = 0; i < _delayTimesMs.Length; i++)
        {
            int delaySamples = (int)(_delayTimesMs[i] * sampleRate / 1000f);
            _delayLines[i] = new float[delaySamples];
            _positions[i] = 0;
        }
    }

    public void ProcessSamples(float[] buffer, int offset, int count)
    {
        if (!Enabled || WetMix == 0)
            return;

        for (int i = 0; i < count; i++)
        {
            float input = buffer[offset + i];
            float reverbSample = 0f;

            for (int d = 0; d < _delayLines.Length; d++)
            {
                var delayLine = _delayLines[d];
                int pos = _positions[d];

                float delayed = delayLine[pos];
                reverbSample += delayed;

                // 反馈混响
                delayLine[pos] = input + delayed * Decay;

                _positions[d] = (pos + 1) % delayLine.Length;
            }

            reverbSample /= _delayLines.Length;

            // 混合干湿信号
            buffer[offset + i] = input * (1 - WetMix) + reverbSample * WetMix;
        }
    }
}

