using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DAW.Utils;
using DAW.Wave.Services;
using Microsoft.UI.Xaml;
using Microsoft.VisualBasic.Devices;

namespace DAW.ViewModels;

public partial class WaveViewModel : ObservableRecipient
{
    private readonly IWaveService _waveService;
    private readonly IAudioDevice _audioDevice;

    public ObservableCollection<string> AudioList { get; } = [];

    [ObservableProperty]
    public partial int SelectedAudioIndex { get; set; } = 0;

    public WaveViewModel(IWaveService waveService, IAudioDevice audioDevice)
    {
        _waveService = waveService;
        _audioDevice = audioDevice;
    }

    private bool _isPlaying = false;

    [RelayCommand]
    private void TogglePlayPause()
    {
        if (_isPlaying)
        {
            Pause();
        }
        else
        {
            Play();
        }
    }

    [RelayCommand]
    private void Play()
    {
        _waveService.Play(AudioList[SelectedAudioIndex]);
        _isPlaying = true;
    }

    [RelayCommand]
    private void Pause()
    {
        _waveService.Pause(AudioList[SelectedAudioIndex]);
        _isPlaying = false;
    }

    [RelayCommand]
    private void Close()
    {
        _waveService.Close(AudioList[SelectedAudioIndex]);
        AudioList.Remove(AudioList[SelectedAudioIndex]);
    }

    [RelayCommand]
    public async Task OpenFileAsync()
    {
        var file = await FilePickerHelper.ShowOpenPickerAsync();
        if (file == null)
            return;

        _waveService.Open(file.Path);
        AudioList.Add(file.Path);
    }
}
