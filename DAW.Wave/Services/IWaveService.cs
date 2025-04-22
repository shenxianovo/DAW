using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NAudio.Wave;

namespace DAW.Wave.Services;

public interface IWaveService
{
    public void Open(string filePath);
    public void Close(string filePath);
    public void Play(string filePath);
    public void Pause(string filePath);
}