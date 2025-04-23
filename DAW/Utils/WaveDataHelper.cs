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

    /// <summary>
    /// 将原始音频按指定 blockSize 分块，并返回简化后的「平均值数组」。
    /// </summary>
    /// <param name="sourceData">原始浮点采样</param>
    /// <param name="blockSize">块大小</param>
    /// <returns>新的简化后波形数组，每个元素为一块的平均值</returns>
    [Deprecated("绘制效果差", DeprecationType.Remove, 0)]
    public static float[] GenerateAverageArray(float[] sourceData, int blockSize)
    {
        if (sourceData == null || sourceData.Length == 0)
            return Array.Empty<float>();

        var avgList = new List<float>();
        for (int i = 0; i < sourceData.Length; i += blockSize)
        {
            int end = Math.Min(i + blockSize, sourceData.Length);
            double sum = 0.0;
            int count = end - i;
            for (int j = i; j < end; j++)
            {
                sum += sourceData[j];
            }

            float avg = (float)(sum / count);
            avgList.Add(avg);
        }

        return avgList.ToArray();
    }
}