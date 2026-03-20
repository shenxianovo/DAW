using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation.Metadata;

namespace DAW.Utils;

/// <summary>
/// 峰值数组的步长（stride）= 3，每块存储 [min, max, rms]。
/// </summary>
public static class WaveDataHelper
{
    /// <summary>
    /// 每块的步长：min, max, rms。
    /// </summary>
    public const int Stride = 3;

    /// <summary>
    /// 按块大小生成峰值+RMS数组 [min, max, rms, min, max, rms, ...]。
    /// </summary>
    public static float[][] GeneratePeakArrays(float[] audioData, int channels, int samplesPerPeak)
    {
        var results = new float[channels][];
        var totalSamples = audioData.Length / channels;
        int totalPeaks = (int)Math.Ceiling((double)totalSamples / samplesPerPeak);

        for (int c = 0; c < channels; c++)
        {
            var data = new float[totalPeaks * Stride];
            for (int peakIndex = 0; peakIndex < totalPeaks; peakIndex++)
            {
                int start = peakIndex * samplesPerPeak;
                int end = Math.Min(start + samplesPerPeak, totalSamples);

                float minVal = float.MaxValue;
                float maxVal = float.MinValue;
                double sumSq = 0;
                int count = 0;
                for (int i = start; i < end; i++)
                {
                    float sample = audioData[i * channels + c];
                    if (sample < minVal) minVal = sample;
                    if (sample > maxVal) maxVal = sample;
                    sumSq += (double)sample * sample;
                    count++;
                }
                if (minVal == float.MaxValue) minVal = 0;
                if (maxVal == float.MinValue) maxVal = 0;
                float rms = count > 0 ? MathF.Sqrt((float)(sumSq / count)) : 0;

                int idx = peakIndex * Stride;
                data[idx] = minVal;
                data[idx + 1] = maxVal;
                data[idx + 2] = rms;
            }
            results[c] = data;
        }

        return results;
    }

    /// <summary>
    /// 按块大小对指定帧范围 [startFrame, endFrame] 生成峰值+RMS数组。
    /// </summary>
    public static float[][] GenerateRangePeakArrays(float[] audioData, int channels, int samplesPerPeak, long startFrame, long endFrame)
    {
        var results = new float[channels][];
        long rangeFrames = endFrame - startFrame + 1;
        int totalPeaks = (int)Math.Ceiling((double)rangeFrames / samplesPerPeak);

        for (int c = 0; c < channels; c++)
        {
            var data = new float[totalPeaks * Stride];
            for (int peakIndex = 0; peakIndex < totalPeaks; peakIndex++)
            {
                long start = startFrame + (long)peakIndex * samplesPerPeak;
                long end = Math.Min(start + samplesPerPeak, endFrame + 1);

                float minVal = float.MaxValue;
                float maxVal = float.MinValue;
                double sumSq = 0;
                int count = 0;
                for (long i = start; i < end; i++)
                {
                    long idx = i * channels + c;
                    if (idx >= 0 && idx < audioData.Length)
                    {
                        float sample = audioData[idx];
                        if (sample < minVal) minVal = sample;
                        if (sample > maxVal) maxVal = sample;
                        sumSq += (double)sample * sample;
                        count++;
                    }
                }
                if (minVal == float.MaxValue) minVal = 0;
                if (maxVal == float.MinValue) maxVal = 0;
                float rms = count > 0 ? MathF.Sqrt((float)(sumSq / count)) : 0;

                int di = peakIndex * Stride;
                data[di] = minVal;
                data[di + 1] = maxVal;
                data[di + 2] = rms;
            }
            results[c] = data;
        }

        return results;
    }
}