using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NAudio.Wave;

namespace DAW.Wave.Services;

public interface IAudioEffect
{
    public bool Enabled { get; set; } 

    public string Name { get; set; }

    void ProcessSamples(float[] buffer, int offset, int count);
}
