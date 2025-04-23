using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation.Metadata;

namespace DAW.Wave.Utils;

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
}