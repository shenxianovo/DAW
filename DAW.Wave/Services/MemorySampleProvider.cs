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
            // --- BEGIN DIAGNOSTIC CODE ---
            if (_sourceData == null)
            {
                System.Diagnostics.Debug.WriteLine("MemorySampleProvider.Read: _sourceData is null. Returning 0.");
                return 0;
            }
            if (buffer == null)
            {
                System.Diagnostics.Debug.WriteLine("MemorySampleProvider.Read: buffer is null. Returning 0 (or throw ArgumentNullException).");
                return 0;
            }

            // System.Diagnostics.Debug.WriteLine($"MemorySampleProvider.Read ENTER:");
            // System.Diagnostics.Debug.WriteLine($"  _sourceData.GetType(): {_sourceData.GetType()}, Length: {_sourceData.Length}");
            // System.Diagnostics.Debug.WriteLine($"  buffer.GetType(): {buffer.GetType()}, Length: {buffer.Length}");
            // System.Diagnostics.Debug.WriteLine($"  _position: {_position}, offset: {offset}, count: {count}");
            // --- END DIAGNOSTIC CODE ---

            if (_position >= _sourceData.Length)
            {
                return 0; // 没有更多数据
            }

            // 如果设置了终止位置，限制可读范围
            long effectiveEnd = _sourceData.Length;
            if (_endSamplePosition >= 0 && _endSamplePosition < effectiveEnd)
                effectiveEnd = _endSamplePosition;

            if (_position >= effectiveEnd)
                return 0;

            long samplesAvailable = effectiveEnd - _position;
            long samplesToCopy = Math.Min(samplesAvailable, count);

            if (samplesToCopy <= 0)
            {
                // System.Diagnostics.Debug.WriteLine($"MemorySampleProvider.Read: samplesToCopy is {samplesToCopy}. Returning 0.");
                return 0;
            }

            // --- BEGIN BOUNDS CHECK (important for manual copy too) ---
            // Check source bounds: _position (long) + samplesToCopy (long) vs _sourceData.Length (int, but can be large)
            if (_position + samplesToCopy > _sourceData.Length)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR: Source read out of bounds. _position({_position}) + samplesToCopy({samplesToCopy}) > _sourceData.Length({_sourceData.Length})");
                samplesToCopy = _sourceData.Length - _position; // Correct it
                if (samplesToCopy <= 0) return 0;
            }
            // Check destination bounds: offset (int) + samplesToCopy (long, but effectively int due to 'count') vs buffer.Length (int)
            if (offset + samplesToCopy > buffer.Length)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR: Destination write out of bounds. offset({offset}) + samplesToCopy({samplesToCopy}) > buffer.Length({buffer.Length})");
                samplesToCopy = buffer.Length - offset;
                if (samplesToCopy <= 0) return 0;
            }
            // --- END BOUNDS CHECK ---

            // System.Diagnostics.Debug.WriteLine($"  Attempting MANUAL COPY from _sourceData (pos:{_position}) to buffer (offset:{offset}) for {samplesToCopy} samples.");

            int actualSamplesToCopy = (int)samplesToCopy; // samplesToCopy is now guaranteed to be within int range and positive

            try
            {
                for (int i = 0; i < actualSamplesToCopy; i++)
                {
                    // Direct assignment. If types are truly mismatched at a fundamental level,
                    // this might still throw an InvalidCastException or similar if C# can't implicitly convert.
                    // However, if both are float[], this should work.
                    buffer[offset + i] = _sourceData[_position + i];
                }

                _position += actualSamplesToCopy;
                return actualSamplesToCopy;
            }
            catch (IndexOutOfRangeException ioorex)
            {
                System.Diagnostics.Debug.WriteLine($"CRITICAL IndexOutOfRangeException in MemorySampleProvider.Read (Manual Copy):");
                System.Diagnostics.Debug.WriteLine($"  _sourceData.Length: {_sourceData?.Length}, buffer.Length: {buffer?.Length}");
                System.Diagnostics.Debug.WriteLine($"  _position: {_position}, offset: {offset}, actualSamplesToCopy: {actualSamplesToCopy}");
                System.Diagnostics.Debug.WriteLine($"  Exception: {ioorex}");
                throw;
            }
            catch (ArrayTypeMismatchException atmEx)
            {
                System.Diagnostics.Debug.WriteLine($"CRITICAL ArrayTypeMismatchException in MemorySampleProvider.Read (Manual Copy):");
                System.Diagnostics.Debug.WriteLine($"  _sourceData actual type: {_sourceData?.GetType().FullName}");
                System.Diagnostics.Debug.WriteLine($"  buffer actual type: {buffer?.GetType().FullName}");
                System.Diagnostics.Debug.WriteLine($"  Exception: {atmEx}");
                throw;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CRITICAL Exception in MemorySampleProvider.Read (Manual Copy): {ex}");
                throw;
            }
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
