using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
    #region Services

    private readonly IWaveService _waveService;
    private readonly IAudioDevice _audioDevice;

    #endregion

    #region Private Fields

    private bool _isPlaying = false;
    // 定时器，用于刷新播放进度
    private readonly DispatcherTimer _timer;

    #endregion

    #region Observable Properties
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

    #endregion

    public WaveViewModel(IWaveService waveService, IAudioDevice audioDevice)
    {
        _waveService = waveService;
        _audioDevice = audioDevice;

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _timer.Tick += (s, e) => UpdatePlaybackPosition();
    }

    #region Events

    partial void OnSelectedAudioIndexChanged(int oldValue, int newValue)
    {
        // 切换选中的 AudioFile 时，先移除原来的事件，再订阅新的事件
        if (oldValue >= 0 && oldValue < AudioList.Count)
        {
            AudioList[oldValue].PropertyChanged -= OnAudioFilePropertyChanged;
        }

        if (newValue >= 0 && newValue < AudioList.Count)
        {
            AudioList[newValue].PropertyChanged += OnAudioFilePropertyChanged;
        }
    }

    private void OnAudioFilePropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (sender is AudioFile file && e.PropertyName == nameof(AudioFile.PlaybackPositionFrameIndex))
        {
            // 当波形控件通过 TwoWay 绑定修改 PlaybackPositionFrameIndex，
            // 这里就能监听到，然后通知 waveService 去更新实际播放位置
            _waveService.SetPlaybackPositionFrame(file, file.PlaybackPositionFrameIndex);
        }
    }

    #endregion

    #region Relay Commands

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

    public async Task ExportFileAsync(string targetPath)
    {
        await _waveService.ExportAsync(CurrentAudioFile, targetPath);
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
        _waveService.Play(CurrentAudioFile);
        _isPlaying = true;
        _timer.Start();
    }

    [RelayCommand]
    private void Pause()
    {
        _waveService.Pause(CurrentAudioFile);
        _isPlaying = false;
        _timer.Stop();
    }

    [RelayCommand]
    private void Close()
    {
        _waveService.Close(CurrentAudioFile);
        AudioList.Remove(CurrentAudioFile);
        _isPlaying = false;
        _timer.Stop();
    }

    [RelayCommand]
    private void AddEffect(string effectName)
    {
        _waveService.AddEffect(CurrentAudioFile, effectName);
    }

    public void RevomeEffect(IAudioEffect effect)
    {
        _waveService.RemoveEffect(CurrentAudioFile, effect.Name);
    }

    #endregion

    #region Helper Methods

    private void UpdatePlaybackPosition()
    {
        if (!_isPlaying || SelectedAudioIndex < 0 || SelectedAudioIndex >= AudioList.Count)
            return;

        long index = _waveService.GetPlaybackPositionFrame(CurrentAudioFile);
        CurrentAudioFile.PlaybackPositionFrameIndex = index;
    }

    #endregion
}
