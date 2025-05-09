using NAudio.Wave;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DAW.Wave.Models;
using DAW.Wave.Services.Effects;
using System.IO;

namespace DAW.Wave.Services.Implementations;

public class WaveService : IWaveService // 确保实现了正确的接口
{
    private readonly IAudioDevice _audioDevice;
    private readonly AudioEffectFactory _audioEffectFactory;

    private readonly ConcurrentDictionary<AudioFile, WaveOutEvent> _playerMap = new();
    private readonly ConcurrentDictionary<AudioFile, RealtimeEffectSampleProvider> _realtimeProviders = new();
    private readonly ConcurrentDictionary<AudioFile, MemorySampleProvider> _memorySourceProviders = new();

    public WaveService(IAudioDevice audioDevice, AudioEffectFactory audioEffectFactory)
    {
        _audioDevice = audioDevice;
        _audioEffectFactory = audioEffectFactory;
    }

    // 1. OpenAsync - 返回 Task<AudioFile> (非 Task<(AudioFile?, string?)>)
    public async Task<AudioFile> OpenAsync(string filePath)
    {
        // 与上一版本类似，但确保返回类型匹配，并在错误时抛出异常或返回null（取决于接口约定）
        // 为简单起见，这里在错误时抛出异常
        try
        {
            var cachedPath = await ConvertToPcm32(filePath);

            float[] audioData;
            WaveFormat waveFormat;
            TimeSpan totalTime;

            using (var tempReader = new AudioFileReader(cachedPath))
            {
                waveFormat = tempReader.WaveFormat;
                totalTime = tempReader.TotalTime;
                long totalSamplesInSource = tempReader.Length / (tempReader.WaveFormat.BitsPerSample / 8);
                audioData = new float[totalSamplesInSource];
                tempReader.Position = 0;
                int samplesRead = tempReader.Read(audioData, 0, audioData.Length);
                if (samplesRead != audioData.Length)
                {
                    Array.Resize(ref audioData, samplesRead);
                }
            }

            var audioFile = new AudioFile
            {
                FilePath = cachedPath,
                FileName = Path.GetFileName(filePath),
                Duration = totalTime,
                SampleRate = waveFormat.SampleRate,
                Channels = waveFormat.Channels,
                BitDepth = waveFormat.BitsPerSample,
                Format = "PCM 32-bit Float (In-Memory)",
                AudioData = audioData,
                AudioEffects = new ObservableCollection<IAudioEffect>() // 初始化效果列表
            };

            long totalFrames = audioData.Length / audioFile.Channels;
            audioFile.VisibleLeftFrameIndex = 0;
            audioFile.VisibleRightFrameIndex = Math.Max(0, totalFrames > 0 ? totalFrames - 1 : 0);
            audioFile.PlaybackPositionFrameIndex = 0;
            audioFile.SelectedLeftFrameIndex = 0;
            audioFile.SelectedRightFrameIndex = 0;

            System.Diagnostics.Debug.WriteLine($"Loaded {audioFile.FileName}. Audio data in memory.");
            return audioFile;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in OpenAsync for {filePath}: {ex.Message}");
            throw new IOException($"Failed to open audio file: {filePath}", ex); // 或者根据接口设计返回 null
        }
    }

    // 2. ExportAsync
    public async Task ExportAsync(AudioFile audioFile, string targetFilePath)
    {
        if (audioFile == null || audioFile.AudioData == null || audioFile.AudioData.Length == 0)
        {
            throw new ArgumentException("AudioFile or its AudioData is null or empty.", nameof(audioFile));
        }

        await Task.Run(() =>
        {
            var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(audioFile.SampleRate, audioFile.Channels);
            // 使用 WaveFileWriter 实例来写入
            using (var writer = new WaveFileWriter(targetFilePath, waveFormat))
            {
                writer.WriteSamples(audioFile.AudioData, 0, audioFile.AudioData.Length);
            }
            System.Diagnostics.Debug.WriteLine($"Exported {audioFile.FileName} to {targetFilePath}");
        });
    }

    // 3. LoadWaveAsync - 这个方法在当前设计中（OpenAsync直接加载到AudioData）变得多余，
    // 但如果接口强制要求，我们需要实现它。它将与OpenAsync中的加载逻辑非常相似。
    public async Task<float[]> LoadWaveAsync(string filePath)
    {
        try
        {
            // 注意：这个方法可能与 OpenAsync 中的逻辑重复。
            // 如果 OpenAsync 是主要的加载方式，这个方法可能只是一个辅助。
            var cachedPath = await ConvertToPcm32(filePath); // 确保是PCM float

            float[] audioData;
            using (var tempReader = new AudioFileReader(cachedPath))
            {
                long totalSamplesInSource = tempReader.Length / (tempReader.WaveFormat.BitsPerSample / 8);
                audioData = new float[totalSamplesInSource];
                tempReader.Position = 0;
                int samplesRead = tempReader.Read(audioData, 0, audioData.Length);
                if (samplesRead != audioData.Length)
                {
                    Array.Resize(ref audioData, samplesRead);
                }
            }
            System.Diagnostics.Debug.WriteLine($"LoadWaveAsync: Loaded data from {filePath}.");
            return audioData;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in LoadWaveAsync for {filePath}: {ex.Message}");
            throw new IOException($"Failed to load wave data from file: {filePath}", ex);
        }
    }

    private void EnsurePlayerComponentsExist(AudioFile audioFile)
    {
        if (audioFile == null || audioFile.AudioData == null || audioFile.AudioData.Length == 0)
        {
            CleanUpPlayerComponents(audioFile, false);
            return;
        }

        if (_playerMap.ContainsKey(audioFile) &&
            _memorySourceProviders.TryGetValue(audioFile, out var existingMemoryProvider) &&
            ReferenceEquals(existingMemoryProvider.GetSourceDataReference(), audioFile.AudioData))
        {
            existingMemoryProvider.SetPositionByFrame(audioFile.PlaybackPositionFrameIndex);
            return;
        }

        CleanUpPlayerComponents(audioFile, false);

        var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(audioFile.SampleRate, audioFile.Channels);
        var memoryProvider = new MemorySampleProvider(audioFile.AudioData, waveFormat);
        memoryProvider.SetPositionByFrame(audioFile.PlaybackPositionFrameIndex);
        _memorySourceProviders[audioFile] = memoryProvider;

        var effectProvider = new RealtimeEffectSampleProvider(memoryProvider, audioFile.AudioEffects ?? new ObservableCollection<IAudioEffect>());
        _realtimeProviders[audioFile] = effectProvider;

        var waveOut = new WaveOutEvent { DeviceNumber = _audioDevice.GetCurrentOutputDeviceId() };
        waveOut.Init(effectProvider);
        _playerMap[audioFile] = waveOut;
    }

    // 4. Close
    public void Close(AudioFile audioFile)
    {
        if (audioFile == null) return;
        CleanUpPlayerComponents(audioFile, false);
        System.Diagnostics.Debug.WriteLine($"Closed and cleaned up player for {audioFile.FileName}.");
        // 可选：删除 audioFile.FilePath 指向的缓存文件
        if (!string.IsNullOrEmpty(audioFile.FilePath) && File.Exists(audioFile.FilePath) && audioFile.FilePath.Contains(Path.Combine("DAW", "Cache")))
        {
            try
            {
                File.Delete(audioFile.FilePath);
                System.Diagnostics.Debug.WriteLine($"Deleted cache file: {audioFile.FilePath}");
            }
            catch (IOException ex) { System.Diagnostics.Debug.WriteLine($"Error deleting cache file {audioFile.FilePath}: {ex.Message}"); }
        }
    }

    // 5. Play - 移除 TimeSpan? startTime 参数以匹配接口
    public void Play(AudioFile audioFile)
    {
        if (audioFile == null) return;
        EnsurePlayerComponentsExist(audioFile);

        if (_playerMap.TryGetValue(audioFile, out var waveOut))
        {
            if (waveOut.PlaybackState == PlaybackState.Paused || waveOut.PlaybackState == PlaybackState.Stopped)
            {
                waveOut.Play();
            }
        }
    }

    // 6. Pause
    public void Pause(AudioFile audioFile)
    {
        if (_playerMap.TryGetValue(audioFile, out var waveOut))
        {
            if (waveOut.PlaybackState == PlaybackState.Playing)
            {
                waveOut.Pause();
            }
        }
    }

    // Stop 方法不在您的接口中，但通常与 Play/Pause 一起出现。如果需要，可以添加。
    // public void Stop(AudioFile audioFile) { ... }

    private void CleanUpPlayerComponents(AudioFile audioFile, bool fromNotify)
    {
        if (audioFile == null) return;
        if (_playerMap.TryRemove(audioFile, out var waveOut))
        {
            waveOut.Stop();
            waveOut.Dispose();
        }
        _realtimeProviders.TryRemove(audioFile, out _);
        _memorySourceProviders.TryRemove(audioFile, out _);
    }

    // 7. GetPlaybackPositionFrame
    public long GetPlaybackPositionFrame(AudioFile audioFile)
    {
        if (audioFile == null) return 0;
        if (_memorySourceProviders.TryGetValue(audioFile, out var msp) &&
            _playerMap.TryGetValue(audioFile, out var wo) &&
            wo.PlaybackState != PlaybackState.Stopped) // 仅当播放器活动时从播放器获取
        {
            return msp.GetPositionInFrames();
        }
        return audioFile.PlaybackPositionFrameIndex; // 否则返回模型中的值
    }

    // 8. SetPlaybackPositionFrame
    public void SetPlaybackPositionFrame(AudioFile audioFile, long frameIndex)
    {
        if (audioFile == null) return;

        long totalFrames = 0;
        if (audioFile.AudioData != null && audioFile.Channels > 0)
        {
            totalFrames = audioFile.AudioData.Length / audioFile.Channels;
        }

        audioFile.PlaybackPositionFrameIndex = Math.Clamp(frameIndex, 0, Math.Max(0, totalFrames > 0 ? totalFrames - 1 : 0));

        if (_memorySourceProviders.TryGetValue(audioFile, out var msp))
        {
            msp.SetPositionByFrame(audioFile.PlaybackPositionFrameIndex);
        }
    }

    // GetCurrentTime 和 IsPlaying 不在您的接口中，但通常有用。
    // public TimeSpan GetCurrentTime(AudioFile audioFile) { ... }
    // public bool IsPlaying(AudioFile audioFile) { ... }

    // 9. AddEffect
    public void AddEffect(AudioFile audioFile, string effectName)
    {
        if (audioFile == null) return;
        var effect = _audioEffectFactory.CreateEffect(effectName, audioFile.SampleRate);
        if (effect == null) return;

        if (audioFile.AudioEffects == null) audioFile.AudioEffects = new ObservableCollection<IAudioEffect>();
        if (!audioFile.AudioEffects.Any(e => e.Name.Equals(effectName, StringComparison.OrdinalIgnoreCase))) // 避免重复添加
        {
            audioFile.AudioEffects.Add(effect);
        }

        if (_realtimeProviders.TryGetValue(audioFile, out var rp))
        {
            rp.UpdateEffects(audioFile.AudioEffects);
        }
    }

    // 10. RemoveEffect
    public void RemoveEffect(AudioFile audioFile, string effectName)
    {
        if (audioFile == null || audioFile.AudioEffects == null) return;
        var effectToRemove = audioFile.AudioEffects.FirstOrDefault(e => e.Name.Equals(effectName, StringComparison.OrdinalIgnoreCase));
        if (effectToRemove == null) return;

        audioFile.AudioEffects.Remove(effectToRemove);

        if (_realtimeProviders.TryGetValue(audioFile, out var rp))
        {
            rp.UpdateEffects(audioFile.AudioEffects);
        }
    }

    // 11. ClipAudio
    public void ClipAudio(AudioFile audioFile, long startFrame, long endFrame)
    {
        if (audioFile == null || audioFile.AudioData == null || audioFile.Channels <= 0) return;

        long originalTotalFrames = audioFile.AudioData.Length / audioFile.Channels;
        if (startFrame > endFrame) (startFrame, endFrame) = (endFrame, startFrame);
        startFrame = Math.Max(0, startFrame);
        endFrame = Math.Min(originalTotalFrames > 0 ? originalTotalFrames - 1 : 0, endFrame);

        if (startFrame > endFrame || startFrame >= originalTotalFrames || originalTotalFrames == 0)
        {
            System.Diagnostics.Debug.WriteLine($"ClipAudio: Invalid clip range or empty audio. No action.");
            return;
        }

        long framesToClipCount = endFrame - startFrame + 1;
        long samplesToClipCount = framesToClipCount * audioFile.Channels;
        long startSampleToClipIndex = startFrame * audioFile.Channels;

        float[] originalData = audioFile.AudioData;
        int newAudioDataLength = originalData.Length - (int)samplesToClipCount;
        if (newAudioDataLength < 0) newAudioDataLength = 0;

        float[] newAudioData = new float[newAudioDataLength];
        Array.Copy(originalData, 0, newAudioData, 0, (int)startSampleToClipIndex);
        long originalSourceIndexAfterClip = startSampleToClipIndex + samplesToClipCount;
        if (originalSourceIndexAfterClip < originalData.Length)
        {
            Array.Copy(originalData, (int)originalSourceIndexAfterClip,
                       newAudioData, (int)startSampleToClipIndex,
                       originalData.Length - (int)originalSourceIndexAfterClip);
        }

        long oldPlaybackPos = audioFile.PlaybackPositionFrameIndex;
        audioFile.AudioData = newAudioData;

        long newTotalFrames = newAudioDataLength > 0 ? (newAudioData.Length / audioFile.Channels) : 0;
        audioFile.Duration = TimeSpan.FromSeconds((double)newTotalFrames / audioFile.SampleRate);

        long newPlaybackPos;
        if (oldPlaybackPos > endFrame) newPlaybackPos = oldPlaybackPos - framesToClipCount;
        else if (oldPlaybackPos >= startFrame) newPlaybackPos = startFrame;
        else newPlaybackPos = oldPlaybackPos;
        audioFile.PlaybackPositionFrameIndex = Math.Clamp(newPlaybackPos, 0, Math.Max(0, newTotalFrames > 0 ? newTotalFrames - 1 : 0));

        if (audioFile.VisibleLeftFrameIndex >= newTotalFrames) audioFile.VisibleLeftFrameIndex = 0;
        audioFile.VisibleRightFrameIndex = Math.Clamp(audioFile.VisibleRightFrameIndex, audioFile.VisibleLeftFrameIndex, Math.Max(0, newTotalFrames > 0 ? newTotalFrames - 1 : 0));
        if (audioFile.VisibleLeftFrameIndex > audioFile.VisibleRightFrameIndex && newTotalFrames > 0)
        {
            audioFile.VisibleLeftFrameIndex = 0;
            audioFile.VisibleRightFrameIndex = newTotalFrames - 1;
        }
        if (newTotalFrames == 0)
        {
            audioFile.VisibleLeftFrameIndex = 0;
            audioFile.VisibleRightFrameIndex = 0;
        }

        audioFile.SelectedLeftFrameIndex = 0;
        audioFile.SelectedRightFrameIndex = 0;

        CleanUpPlayerComponents(audioFile, false);
    }

    // NotifyAudioDataChanged 方法不在您的接口中，但如果 AudioFile 内部有撤销/重做逻辑，
    // 并且这些逻辑直接修改 AudioData，则可能需要一个类似的方法来通知 WaveService 清理播放器。
    // public void NotifyAudioDataChanged(AudioFile audioFile) { ... }

    private async Task<string> ConvertToPcm32(string sourcePath)
    {
        return await Task.Run(() =>
        {
            var tempFileName = $"{Path.GetFileNameWithoutExtension(sourcePath)}_{Guid.NewGuid():N}.wav";
            var cacheFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "DAW", "Cache");
            Directory.CreateDirectory(cacheFolder);
            var cachedPath = Path.Combine(cacheFolder, tempFileName);

            using (var reader = new MediaFoundationReader(sourcePath))
            {
                var targetFormat = WaveFormat.CreateIeeeFloatWaveFormat(reader.WaveFormat.SampleRate, reader.WaveFormat.Channels);
                if (reader.WaveFormat.Equals(targetFormat) && reader.WaveFormat.Encoding == WaveFormatEncoding.IeeeFloat) // 确保编码也是IEEE Float
                {
                    // 如果已经是目标格式，直接复制（或返回原路径，如果允许）
                    // 为简单起见，我们总是创建一个新的缓存文件，以确保我们控制其生命周期
                    File.Copy(sourcePath, cachedPath, true);
                    return cachedPath;
                }
                using (var resampler = new MediaFoundationResampler(reader, targetFormat))
                {
                    WaveFileWriter.CreateWaveFile(cachedPath, resampler);
                }
            }
            return cachedPath;
        });
    }
}
