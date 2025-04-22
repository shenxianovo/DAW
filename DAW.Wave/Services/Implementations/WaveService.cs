using NAudio.Wave;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAW.Wave.Services.Implementations;

public class WaveService : IWaveService
{
    private readonly IAudioDevice _audioDevice;
    private readonly ConcurrentDictionary<string, WaveOutEvent> _waveOuts = new();

    public void Open(string filePath)
    {
        if (!_waveOuts.ContainsKey(filePath))
        {
            var audioFile = new AudioFileReader(filePath);
            var waveOut = new WaveOutEvent
            {
                DeviceNumber = _audioDevice.GetCurrentOutputDeviceId()
            };
            waveOut.Init(audioFile);
            _waveOuts[filePath] = waveOut;
        }
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

    public WaveService(IAudioDevice audioDevice)
    {
        _audioDevice = audioDevice;
    }
}
