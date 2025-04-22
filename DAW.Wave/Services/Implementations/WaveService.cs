using NAudio.Wave;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DAW.Wave.Models;

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
        var waveOut = new WaveOutEvent
        {
            DeviceNumber = _audioDevice.GetCurrentOutputDeviceId()
        };
        waveOut.Init(audioFileReader);
        _waveOuts[pcmPath] = waveOut; // 以转换后路径为Key

        // 给AudioFile赋值
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
        return audioFile;
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
