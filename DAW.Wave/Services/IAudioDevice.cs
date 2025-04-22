using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAW.Wave.Services;

public interface IAudioDevice
{
    public IList<string> GetOutputDevices();
    public IList<string> GetInputDevices();

    public int GetCurrentInputDeviceId();
    public int GetCurrentOutputDeviceId();
    public void SetOutputDevice(string deviceName);
    public void SetInputDevice(string deviceName);
}
