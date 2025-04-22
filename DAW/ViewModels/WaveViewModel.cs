using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DAW.Utils;
using DAW.Wave.Models;
using DAW.Wave.Services;
using Microsoft.UI.Xaml;
using Microsoft.VisualBasic.Devices;

namespace DAW.ViewModels;

public partial class WaveViewModel : ObservableRecipient
{
    private readonly IWaveService _waveService;
    private readonly IAudioDevice _audioDevice;

    private bool _isPlaying = false;

    public ObservableCollection<AudioFile> AudioList { get; } = [];

    public AudioFile CurrentAudioFile
    {
        get
        {
            if (SelectedAudioIndex >= 0 && SelectedAudioIndex < AudioList.Count)
            {
                return AudioList[SelectedAudioIndex];
            }
            return new AudioFile();
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentAudioFile))]
    public partial int SelectedAudioIndex { get; set; } = -1;

    public WaveViewModel(IWaveService waveService, IAudioDevice audioDevice)
    {
        _waveService = waveService;
        _audioDevice = audioDevice;
    }

    [RelayCommand]
    public async Task OpenFileAsync()
    {
        var file = await FilePickerHelper.ShowOpenPickerAsync();
        if (file == null)
            return;

        var audioFile = await _waveService.OpenAsync(file.Path);
        AudioList.Add(audioFile);
        SelectedAudioIndex = AudioList.Count - 1;
    }

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
        if (AudioList.Count > 0 && SelectedAudioIndex >= 0)
        {
            _waveService.Play(AudioList[SelectedAudioIndex].FilePath);
            _isPlaying = true;
        }
    }

    [RelayCommand]
    private void Pause()
    {
        if (AudioList.Count > 0 && SelectedAudioIndex >= 0)
        {
            _waveService.Pause(AudioList[SelectedAudioIndex].FilePath);
            _isPlaying = false;
        }
    }

    [RelayCommand]
    private void Close()
    {
        if (AudioList.Count > 0 && SelectedAudioIndex >= 0)
        {
            var currentFile = AudioList[SelectedAudioIndex];
            _waveService.Close(currentFile.FilePath);
            AudioList.Remove(currentFile);
            _isPlaying = false;
        }
    }
}
