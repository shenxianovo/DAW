using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAW.Wave.Services.Implementations;

public class AudioDevice : IAudioDevice
{
    private int _currentOutDeviceId;
    private int _currentInDeviceId;

    public IList<string> GetOutputDevices()
    {
        var devices = new List<string>();
        for (int i = 0; i < WaveOut.DeviceCount; i++)
        {
            var cap = WaveOut.GetCapabilities(i);
            devices.Add(cap.ProductName);
        }
        return devices;
    }

    public IList<string> GetInputDevices()
    {
        var devices = new List<string>();
        for (int i = 0; i < WaveIn.DeviceCount; i++)
        {
            var cap = WaveIn.GetCapabilities(i);
            devices.Add(cap.ProductName);
        }
        return devices;
    }

    public void SetOutputDevice(string deviceName)
    {
        var allOut = GetOutputDevices();
        _currentOutDeviceId = allOut.IndexOf(deviceName);
    }

    public void SetInputDevice(string deviceName)
    {
        var allIn = GetInputDevices();
        _currentInDeviceId = allIn.IndexOf(deviceName);
    }
}
