using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAW.Wave.Services.Implementations;

public class AudioDevice : IAudioDevice
{
    private static int _currentOutDeviceId = 0;
    private static int _currentInDeviceId = 0;

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

    public int GetCurrentInputDeviceId() => _currentInDeviceId;

    public int GetCurrentOutputDeviceId() => _currentOutDeviceId;

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
