using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAW.Wave.Services
{
    public class MemorySampleProvider : ISampleProvider
    {
        private float[] _sourceData; // 数据源
        private long _position;      // 当前样本位置 (非帧位置)
        private long _endSamplePosition = -1; // 播放终止位置（样本级）, -1 表示不限
        public WaveFormat WaveFormat { get; }

        public MemorySampleProvider(float[] sourceData, WaveFormat waveFormat)
        {
            _sourceData = sourceData ?? throw new ArgumentNullException(nameof(sourceData));
            WaveFormat = waveFormat ?? throw new ArgumentNullException(nameof(waveFormat));
            _position = 0;
        }

        /// <summary>
        /// 设置播放终止帧。到达此帧后 Read 返回 0（模拟 EOF）。
        /// 传 -1 取消限制。
        /// </summary>
        public void SetEndFrame(long endFrame)
        {
            if (endFrame < 0)
                _endSamplePosition = -1;
            else
                _endSamplePosition = endFrame * WaveFormat.Channels;
        }

        /// <summary>
        /// 清除播放终止帧限制。
        /// </summary>
        public void ClearEndFrame() => _endSamplePosition = -1;

        public int Read(float[] buffer, int offset, int count)
        {
            if (_sourceData == null || buffer == null)
                return 0;

            if (_position >= _sourceData.Length)
                return 0;

            // 如果设置了终止位置，限制可读范围
            long effectiveEnd = _sourceData.Length;
            if (_endSamplePosition >= 0 && _endSamplePosition < effectiveEnd)
                effectiveEnd = _endSamplePosition;

            if (_position >= effectiveEnd)
                return 0;

            long samplesAvailable = effectiveEnd - _position;
            int samplesToCopy = (int)Math.Min(samplesAvailable, count);
            samplesToCopy = Math.Min(samplesToCopy, _sourceData.Length - (int)_position);
            samplesToCopy = Math.Min(samplesToCopy, buffer.Length - offset);

            if (samplesToCopy <= 0)
                return 0;

            Buffer.BlockCopy(_sourceData, (int)_position * sizeof(float), buffer, offset * sizeof(float), samplesToCopy * sizeof(float));
            _position += samplesToCopy;
            return samplesToCopy;
        }

        public void SetPositionByFrame(long frameIndex)
        {
            if (WaveFormat.Channels == 0)
            {
                _position = 0;
                return;
            }
            long samplePosition = frameIndex * WaveFormat.Channels;
            _position = Math.Clamp(samplePosition, 0, _sourceData?.Length ?? 0);
        }

        public long GetPositionInFrames()
        {
            if (WaveFormat.Channels == 0 || _sourceData == null || _sourceData.Length == 0)
            {
                return 0;
            }
            return _position / WaveFormat.Channels;
        }

        public long LengthInFrames
        {
            get
            {
                if (WaveFormat.Channels == 0 || _sourceData == null) return 0;
                return _sourceData.Length / WaveFormat.Channels;
            }
        }

        public float[] GetSourceDataReference()
        {
            return _sourceData;
        }
    }
}
