using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DAW.Wave.Models;

namespace DAW.Wave.Services;

public interface IWaveService
{
    public Task<AudioFile> OpenAsync(string filePath);
    public Task ExportAsync(AudioFile audioFile, string targetFilePath);

    public Task<float[]> LoadWaveAsync(string filePath);
    public void Close(AudioFile audioFile);
    public void Play(AudioFile audioFile);
    public void Pause(AudioFile audioFile);

    public long GetPlaybackPositionFrame(AudioFile audioFile);
    public void SetPlaybackPositionFrame(AudioFile audioFile, long frameIndex);

    public void AddEffect(AudioFile audioFile, string effectName);
    public void RemoveEffect(AudioFile audioFile, string effectName);

    void ClipAudio(AudioFile audioFile, long startFrame, long endFrame);
}