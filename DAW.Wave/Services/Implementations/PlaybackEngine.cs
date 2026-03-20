using NAudio.Wave;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using DAW.Wave.Models;
using DAW.Wave.Services.Effects;

namespace DAW.Wave.Services.Implementations;

/// <summary>
/// 负责音频播放状态管理：Play / Pause / Seek / 效果器实时更新。
/// </summary>
internal class PlaybackEngine : IDisposable
{
    private readonly IAudioDevice _audioDevice;
    private bool _disposed;

    private readonly ConcurrentDictionary<AudioFile, WaveOutEvent> _playerMap = new();
    private readonly ConcurrentDictionary<AudioFile, RealtimeEffectSampleProvider> _realtimeProviders = new();
    private readonly ConcurrentDictionary<AudioFile, MemorySampleProvider> _memorySourceProviders = new();

    public PlaybackEngine(IAudioDevice audioDevice)
    {
        _audioDevice = audioDevice;
    }

    public void Play(AudioFile audioFile)
    {
        if (audioFile == null) return;
        EnsurePlayerComponentsExist(audioFile);

        if (_memorySourceProviders.TryGetValue(audioFile, out var msp))
            msp.ClearEndFrame();

        if (_playerMap.TryGetValue(audioFile, out var waveOut))
        {
            if (waveOut.PlaybackState == PlaybackState.Paused || waveOut.PlaybackState == PlaybackState.Stopped)
            {
                waveOut.Play();
            }
        }
    }

    public void PlayRange(AudioFile audioFile, long startFrame, long endFrame)
    {
        if (audioFile == null) return;
        EnsurePlayerComponentsExist(audioFile);

        if (_memorySourceProviders.TryGetValue(audioFile, out var msp))
        {
            msp.SetPositionByFrame(startFrame);
            msp.SetEndFrame(endFrame);
        }

        if (_playerMap.TryGetValue(audioFile, out var waveOut))
        {
            if (waveOut.PlaybackState == PlaybackState.Paused || waveOut.PlaybackState == PlaybackState.Stopped)
            {
                waveOut.Play();
            }
        }
    }

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

    public long GetPlaybackPositionFrame(AudioFile audioFile)
    {
        if (audioFile == null) return 0;
        if (_memorySourceProviders.TryGetValue(audioFile, out var msp) &&
            _playerMap.TryGetValue(audioFile, out var wo) &&
            wo.PlaybackState != PlaybackState.Stopped)
        {
            return msp.GetPositionInFrames();
        }
        return audioFile.PlaybackPositionFrameIndex;
    }

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

    /// <summary>
    /// 通知实时效果器链更新。
    /// </summary>
    public void UpdateEffects(AudioFile audioFile, IList<IAudioEffect> effects)
    {
        if (_realtimeProviders.TryGetValue(audioFile, out var rp))
        {
            rp.UpdateEffects(effects);
        }
    }

    /// <summary>
    /// 清理指定 AudioFile 的播放组件（停止播放、释放 WaveOutEvent）。
    /// </summary>
    public void CleanUp(AudioFile audioFile)
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

    private void EnsurePlayerComponentsExist(AudioFile audioFile)
    {
        if (audioFile == null || audioFile.AudioData == null || audioFile.AudioData.Length == 0)
        {
            CleanUp(audioFile);
            return;
        }

        if (_playerMap.ContainsKey(audioFile) &&
            _memorySourceProviders.TryGetValue(audioFile, out var existingMemoryProvider) &&
            ReferenceEquals(existingMemoryProvider.GetSourceDataReference(), audioFile.AudioData))
        {
            existingMemoryProvider.SetPositionByFrame(audioFile.PlaybackPositionFrameIndex);
            return;
        }

        CleanUp(audioFile);

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

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var kvp in _playerMap)
        {
            try
            {
                kvp.Value.Stop();
                kvp.Value.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error disposing WaveOutEvent: {ex.Message}");
            }
        }
        _playerMap.Clear();
        _realtimeProviders.Clear();
        _memorySourceProviders.Clear();
    }
}
