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
    private readonly ConcurrentDictionary<string, WaveOutEvent> _waveOuts = new();
    public WaveService(IAudioDevice audioDevice)
    {
        _audioDevice = audioDevice;
    }

    public async Task<AudioFile> OpenAsync(string filePath)
    {
        // 首先转换文件到32位PCM
        var pcmPath = await ConvertToPcm32(filePath);

        // 读取元数据信息并创建WaveOutEvent
        var audioFileReader = new AudioFileReader(pcmPath);
        var audioFile = new AudioFile
        {
            FilePath = pcmPath,
            FileName = System.IO.Path.GetFileName(filePath),
            Duration = audioFileReader.TotalTime,
            SampleRate = audioFileReader.WaveFormat.SampleRate,
            Channels = audioFileReader.WaveFormat.Channels,
            BitDepth = audioFileReader.WaveFormat.BitsPerSample,
            Format = "PCM 32-bit"
        };

        // 加载完整音频数据
        audioFile.AudioData = await LoadWaveAsync(pcmPath);

        // 生成预览数据
        int blockSize = 2048; // 可调整块大小
        audioFile.AudioDataPreview = WaveDataHelper.GeneratePeakArray(audioFile.AudioData, blockSize);

        var waveOut = new WaveOutEvent
        {
            DeviceNumber = _audioDevice.GetCurrentOutputDeviceId()
        };
        waveOut.Init(audioFileReader);
        _waveOuts[pcmPath] = waveOut; // 以转换后路径为Key
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

    public void Close(string filePath)
    {
        if (_waveOuts.TryRemove(filePath, out var waveOut))
        {
            waveOut.Stop();
            waveOut.Dispose();
        }
    }

    public void Play(string filePath)
    {
        if (_waveOuts.TryGetValue(filePath, out var waveOut))
        {
            waveOut.Play();
        }
    }

    public void Pause(string filePath)
    {
        if (_waveOuts.TryGetValue(filePath, out var waveOut))
        {
            waveOut.Stop();
        }
    }

    private async Task<string> ConvertToPcm32(string sourcePath)
    {
        return await Task.Run(() =>
        {
            // 这里简单用一个临时文件名
            // TODO: 更改路径
            var fileName = System.IO.Path.GetFileName(sourcePath);
            var targetPath = System.IO.Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "DAW", fileName);

            using var reader = new MediaFoundationReader(sourcePath);
            using var resampler = new MediaFoundationResampler(reader,
                new WaveFormat(reader.WaveFormat.SampleRate, 32, reader.WaveFormat.Channels));
            WaveFileWriter.CreateWaveFile(targetPath, resampler);
            return targetPath;
        });
    }
}
