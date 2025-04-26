using NAudio.Wave;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DAW.Wave.Models;
using DAW.Wave.Utils;

namespace DAW.Wave.Services.Implementations;

public class WaveService : IWaveService
{
    private readonly IAudioDevice _audioDevice;
    // 存储 (reader, waveOut) 对，便于获取播放位置
    private readonly ConcurrentDictionary<string, (AudioFileReader reader, WaveOutEvent waveOut)> _playerMap =
        new();
    public WaveService(IAudioDevice audioDevice)
    {
        _audioDevice = audioDevice;
    }

    public async Task<AudioFile> OpenAsync(string filePath)
    {
        // 首先转换文件到32位PCM
        var cachedPath = await ConvertToPcm32(filePath);

        // 读取元数据信息并创建WaveOutEvent
        var reader = new AudioFileReader(cachedPath);
        var audioFile = new AudioFile
        {
            FilePath = cachedPath,
            FileName = System.IO.Path.GetFileName(filePath),
            Duration = reader.TotalTime,
            SampleRate = reader.WaveFormat.SampleRate,
            Channels = reader.WaveFormat.Channels,
            BitDepth = reader.WaveFormat.BitsPerSample,
            Format = "PCM 32-bit",
        };

        // 加载完整音频数据
        audioFile.AudioData = await LoadWaveAsync(cachedPath);
        long totalFrames = audioFile.AudioData.Length / audioFile.Channels;
        audioFile.VisibleLeftSampleIndex = 0;
        audioFile.VisibleRightSampleIndex = totalFrames - 1;

        var waveOut = new WaveOutEvent
        {
            DeviceNumber = _audioDevice.GetCurrentOutputDeviceId()
        };
        waveOut.Init(reader);

        // 存起来方便后面查进度
        _playerMap[cachedPath] = (reader, waveOut);
        return audioFile;
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

    public void Play(string filePath)
    {
        if (_playerMap.TryGetValue(filePath, out var pair))
        {
            pair.waveOut.Play();
        }
    }

    public void Pause(string filePath)
    {
        if (_playerMap.TryGetValue(filePath, out var pair))
        {
            pair.waveOut.Stop();
        }
    }

    public void Close(string filePath)
    {
        if (_playerMap.TryRemove(filePath, out var pair))
        {
            pair.waveOut.Stop();
            pair.waveOut.Dispose();
            pair.reader.Dispose();
        }
    }

    public long GetPlaybackPositionSamples(string filePath)
    {
        if (_playerMap.TryGetValue(filePath, out var pair))
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
