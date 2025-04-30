using NAudio.Wave;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DAW.Wave.Models;
using DAW.Wave.Services.Effects;

namespace DAW.Wave.Services.Implementations;

public class WaveService : IWaveService
{
    private readonly IAudioDevice _audioDevice;

    private readonly AudioEffectFactory _audioEffectFactory;

    // 存储 (reader, waveOut) 对，便于获取播放位置
    private readonly ConcurrentDictionary<AudioFile, (AudioFileReader reader, WaveOutEvent waveOut)> _playerMap =
        new();
    // 新增一张表，用于存储音频对应的 RealtimeEffectSampleProvider
    private readonly ConcurrentDictionary<AudioFile, RealtimeEffectSampleProvider> _realtimeProviders
        = new();

    public WaveService(IAudioDevice audioDevice, AudioEffectFactory audioEffectFactory)
    {
        _audioDevice = audioDevice;
        _audioEffectFactory = audioEffectFactory;
    }

    public async Task<AudioFile> OpenAsync(string filePath)
    {
        // 首先转换文件到32位PCM
        var cachedPath = await ConvertToPcm32(filePath);

        // 读取元数据信息并创建WaveOutEvent
        var convertedAudioReader = new AudioFileReader(cachedPath);
        var convertedAudioFile = new AudioFile
        {
            FilePath = cachedPath,
            FileName = System.IO.Path.GetFileName(filePath),
            Duration = convertedAudioReader.TotalTime,
            SampleRate = convertedAudioReader.WaveFormat.SampleRate,
            Channels = convertedAudioReader.WaveFormat.Channels,
            BitDepth = convertedAudioReader.WaveFormat.BitsPerSample,
            Format = "PCM 32-bit",
        };

        // 加载完整音频数据
        convertedAudioFile.AudioData = await LoadWaveAsync(cachedPath);
        long totalFrames = convertedAudioFile.AudioData.Length / convertedAudioFile.Channels;
        convertedAudioFile.VisibleLeftFrameIndex = 0;
        convertedAudioFile.VisibleRightFrameIndex = totalFrames - 1;

        var waveOut = new WaveOutEvent
        {
            DeviceNumber = _audioDevice.GetCurrentOutputDeviceId()
        };

        var realTimeProvider = new RealtimeEffectSampleProvider(convertedAudioReader, convertedAudioFile.AudioEffects);

        waveOut.Init(realTimeProvider);

        _playerMap[convertedAudioFile] = (convertedAudioReader, waveOut);
        _realtimeProviders[convertedAudioFile] = realTimeProvider;

        return convertedAudioFile;
    }

    public async Task<float[]> LoadWaveAsync(string filePath)
    {
        return await Task.Run(() =>
        {
            var samples = new List<float>();
            using var reader = new AudioFileReader(filePath);
            float[] buffer = new float[4096];
            int read;
            while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
            {
                // 只将有效样本收集到列表
                samples.AddRange(buffer.Take(read));
            }
            return samples.ToArray();
        });
    }

    public void Play(AudioFile audioFile)
    {
        if (_playerMap.TryGetValue(audioFile, out var pair))
        {
            pair.waveOut.Play();
        }
    }

    public void Pause(AudioFile audioFile)
    {
        if (_playerMap.TryGetValue(audioFile, out var pair))
        {
            pair.waveOut.Stop();
        }
    }

    public void Close(AudioFile audioFile)
    {
        if (_playerMap.TryRemove(audioFile, out var pair))
        {
            pair.waveOut.Stop();
            pair.waveOut.Dispose();
            pair.reader.Dispose();
        }
    }

    public long GetPlaybackPositionFrame(AudioFile audioFile)
    {
        if (_playerMap.TryGetValue(audioFile, out var pair))
        {
            var reader = pair.reader;
            // 每一帧字节数 = WaveFormat.BlockAlign
            long bytesPos = reader.Position;
            int blockAlign = reader.WaveFormat.BlockAlign;
            if (blockAlign > 0)
            {
                return bytesPos / blockAlign;
            }
        }
        return 0;
    }

    public void SetPlaybackPositionFrame(AudioFile audioFile, long frameIndex)
    {
        if (_playerMap.TryGetValue(audioFile, out var pair))
        {
            var reader = pair.reader;
            int blockAlign = reader.WaveFormat.BlockAlign;
            if (blockAlign > 0)
            {
                long newBytePos = frameIndex * blockAlign;
                // 确保不超出文件长度
                newBytePos = Math.Clamp(newBytePos, 0, reader.Length);
                reader.Position = newBytePos;
            }
        }
    }

    public void AddEffect(AudioFile audioFile, string effectName)
    {
        var effect = _audioEffectFactory.CreateEffect(effectName, audioFile.SampleRate);
        if (effect == null) return;

        audioFile.AudioEffects.Add(effect);

        // 若该 AudioFile 正在播放，直接更新实时效果链
        if (_realtimeProviders.TryGetValue(audioFile, out var rp))
        {
            rp.UpdateEffects(audioFile.AudioEffects);
        }
    }

    public void RemoveEffect(AudioFile audioFile, string effectName)
    {
        // 查找要移除的效果
        var effectToRemove = audioFile.AudioEffects.FirstOrDefault(e => e.Name.Equals(effectName, StringComparison.OrdinalIgnoreCase));
        if (effectToRemove == null) return; // 如果没有找到对应效果，直接返回

        // 从效果列表中移除
        audioFile.AudioEffects.Remove(effectToRemove);

        // 如果该音频文件正在播放，更新实时效果链
        if (_realtimeProviders.TryGetValue(audioFile, out var rp))
        {
            rp.UpdateEffects(audioFile.AudioEffects);
        }
    }

    private async Task<string> ConvertToPcm32(string sourcePath)
    {
        return await Task.Run(() =>
        {
            // 这里简单用一个临时文件名
            // TODO: 更改路径
            var fileName = System.IO.Path.GetFileName(sourcePath);
            var cachePath = System.IO.Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "DAW", fileName);

            using var reader = new MediaFoundationReader(sourcePath);
            using var resampler = new MediaFoundationResampler(reader,
                new WaveFormat(reader.WaveFormat.SampleRate, 32, reader.WaveFormat.Channels));
            WaveFileWriter.CreateWaveFile(cachePath, resampler);
            return cachePath;
        });
    }
}
