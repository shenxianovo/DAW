using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAW.Wave.Services;

public interface IAudioDevice
{
    IList<string> GetOutputDevices();
    IList<string> GetInputDevices();

    void SetOutputDevice(string deviceName);
    void SetInputDevice(string deviceName);
}
