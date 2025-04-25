using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation.Metadata;

namespace DAW.Utils;

public static class WaveDataHelper
{
    /// <summary>
    /// 按块大小（blockSize）生成峰值数组 [min, max, min, max, ...]。
    /// </summary>
    public static float[] GeneratePeakArray(float[] sourceData, int blockSize)
    {
        if (sourceData == null || sourceData.Length == 0)
            return Array.Empty<float>();

        var result = new List<float>();
        for (int i = 0; i < sourceData.Length; i += blockSize)
        {
            int end = Math.Min(i + blockSize, sourceData.Length);
            float minVal = float.MaxValue;
            float maxVal = float.MinValue;

            for (int j = i; j < end; j++)
            {
                float sample = sourceData[j];
                if (sample < minVal) minVal = sample;
                if (sample > maxVal) maxVal = sample;
            }

            result.Add(minVal);
            result.Add(maxVal);
        }

        return result.ToArray();
    }

    public static float[][] GeneratePeakArrays(float[] audioData, int channels, int samplesPerPeak)
    {
        // This is a simple example; adapt as needed for efficiency
        var results = new float[channels][];
        var totalSamples = audioData.Length / channels;
        int totalPeaks = (int)Math.Ceiling((double)totalSamples / samplesPerPeak);

        for (int c = 0; c < channels; c++)
        {
            var channelPeaks = new List<float>(totalPeaks * 2);
            for (int peakIndex = 0; peakIndex < totalPeaks; peakIndex++)
            {
                int start = peakIndex * samplesPerPeak;
                int end = Math.Min(start + samplesPerPeak, totalSamples);

                float minVal = float.MaxValue;
                float maxVal = float.MinValue;
                for (int i = start; i < end; i++)
                {
                    float sample = audioData[i * channels + c];
                    if (sample < minVal) minVal = sample;
                    if (sample > maxVal) maxVal = sample;
                }
                if (minVal == float.MaxValue) minVal = 0;
                if (maxVal == float.MinValue) maxVal = 0;

                channelPeaks.Add(minVal);
                channelPeaks.Add(maxVal);
            }
            results[c] = channelPeaks.ToArray();
        }

        return results;
    }
}