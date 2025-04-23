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

    public Task<float[]> LoadWaveAsync(string filePath);
    public void Close(string filePath);
    public void Play(string filePath);
    public void Pause(string filePath);
}