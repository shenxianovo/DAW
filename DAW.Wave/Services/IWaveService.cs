using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAW.Wave.Services;

public interface IWaveService
{
    public void Open();
    public void Close();
    public void Play();
    public void Resume();
    public void Stop();
}
