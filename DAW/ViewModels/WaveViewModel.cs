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

    // 定时器，用于刷新播放进度
    private readonly DispatcherTimer _timer;

    #endregion

    #region Observable Properties
    public ObservableCollection<AudioFile> AudioList { get; } = [];

    [ObservableProperty]
    public partial bool IsPlaying { get; set; } = false;

    /// <summary>
    /// 缓存的空 AudioFile 对象，防止每次属性访问都创建新实例。
    /// </summary>
    private static readonly AudioFile EmptyAudioFile = new();

    public AudioFile CurrentAudioFile
    {
        get
        {
            if (SelectedAudioIndex >= 0 && SelectedAudioIndex < AudioList.Count)
            {
                return AudioList[SelectedAudioIndex];
            }
            return EmptyAudioFile;
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
        if (IsPlaying)
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
        var af = CurrentAudioFile;
        if (af.SelectedRightFrameIndex > af.SelectedLeftFrameIndex)
        {
            // 有框选区域：仅播放框选范围
            _waveService.PlayRange(af, af.SelectedLeftFrameIndex, af.SelectedRightFrameIndex);
        }
        else
        {
            _waveService.Play(af);
        }
        IsPlaying = true;
        _timer.Start();
    }

    [RelayCommand]
    private void Pause()
    {
        _waveService.Pause(CurrentAudioFile);
        IsPlaying = false;
        _timer.Stop();
    }

    [RelayCommand]
    private void Close()
    {
        _waveService.Close(CurrentAudioFile);
        AudioList.Remove(CurrentAudioFile);
        IsPlaying = false;
        _timer.Stop();
    }

    [RelayCommand]
    private void AddEffect(string effectName)
    {
        _waveService.AddEffect(CurrentAudioFile, effectName);
    }

    public void RemoveEffect(IAudioEffect effect)
    {
        _waveService.RemoveEffect(CurrentAudioFile, effect);
    }

    [RelayCommand]
    private void ClipAudio()
    {
        if (CurrentAudioFile == null || CurrentAudioFile.AudioData == null || CurrentAudioFile.AudioData.Length == 0)
            return;

        long startFrame = CurrentAudioFile.SelectedLeftFrameIndex;
        long endFrame = CurrentAudioFile.SelectedRightFrameIndex;

        // 确保 startFrame <= endFrame
        if (startFrame > endFrame)
        {
            (startFrame, endFrame) = (endFrame, startFrame);
        }

        // 再次检查有效性，尽管服务层也会检查
        long totalFrames = CurrentAudioFile.AudioData.Length / CurrentAudioFile.Channels;
        if (startFrame < 0 || endFrame >= totalFrames || startFrame > endFrame)
        {
            System.Diagnostics.Debug.WriteLine($"WaveViewModel.ClipAudio: 无效选区 {startFrame}-{endFrame} for total frames {totalFrames}");
            return;
        }

        _waveService.ClipAudio(CurrentAudioFile, startFrame, endFrame);

        // WaveService 直接修改 CurrentAudioFile 对象。
        // AudioFile 中的属性更改应触发 UI 更新。
    }

    #endregion

    #region Helper Methods

    private void UpdatePlaybackPosition()
    {
        if (!IsPlaying || SelectedAudioIndex < 0 || SelectedAudioIndex >= AudioList.Count)
            return;

        long index = _waveService.GetPlaybackPositionFrame(CurrentAudioFile);
        CurrentAudioFile.PlaybackPositionFrameIndex = index;

        // 检查是否到达有效播放范围末尾 → 自动暂停
        var af = CurrentAudioFile;
        long totalFrames = af.AudioData != null && af.Channels > 0 ? af.AudioData.Length / af.Channels : 0;
        bool atEnd = index >= totalFrames - 1;
        if (!atEnd && af.SelectedRightFrameIndex > af.SelectedLeftFrameIndex)
            atEnd = index >= af.SelectedRightFrameIndex;

        if (atEnd)
        {
            Pause();
        }
    }

    #endregion
}
