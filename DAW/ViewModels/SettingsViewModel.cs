using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DAW.Wave.Services;

namespace DAW.ViewModels;

public partial class SettingsViewModel : ObservableRecipient
{
    private readonly IAudioDevice _audioDevice;

    public ObservableCollection<string> InputDevices { get; } = [];
    public ObservableCollection<string> OutputDevices { get; } = [];

    [ObservableProperty]
    public partial string SelectedInputDevice { get; set; }

    [ObservableProperty]
    public partial string SelectedOutputDevice { get; set; }

    public SettingsViewModel(IAudioDevice audioDevice)
    {
        _audioDevice = audioDevice;

        GetInputDevice();
        GetOutputDevices();

        if (InputDevices.Count > 0)
            SelectedInputDevice = InputDevices[0];

        if (OutputDevices.Count > 0)
            SelectedOutputDevice = OutputDevices[0];
    }

    [RelayCommand]
    private void SetInputDevice(string deviceName)
    {
        _audioDevice.SetInputDevice(deviceName);
    }

    [RelayCommand]
    private void SetOutputDevice(string deviceName)
    {
        _audioDevice.SetOutputDevice(deviceName);
    }

    private void GetInputDevice()
    {
        InputDevices.Clear();
        var inputDevices = _audioDevice.GetInputDevices();
        foreach (var inputDevice in inputDevices)
        {
            InputDevices.Add(inputDevice);
        }
    }

    private void GetOutputDevices()
    {
        OutputDevices.Clear();
        var outputDevices = _audioDevice.GetOutputDevices();
        foreach (var outputDevice in outputDevices)
        {
            OutputDevices.Add(outputDevice);
        }
    }
}
