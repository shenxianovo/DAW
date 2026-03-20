using NAudio.Wave;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using DAW.Wave.Models;
using DAW.Wave.Services.Effects;

namespace DAW.Wave.Services.Implementations;

/// <summary>
/// IWaveService 门面实现，将职责委派给内部组件：
///   <see cref="AudioFileLoader"/> — 文件加载 / 导出 / 缓存
///   <see cref="PlaybackEngine"/>  — 播放控制 / Seek / 效果器实时更新
///   <see cref="AudioEditor"/>     — 音频数据编辑（剪辑等）
/// </summary>
public class WaveService : IWaveService, IDisposable
{
    private readonly AudioFileLoader _loader;
    private readonly PlaybackEngine _playback;
    private readonly AudioEditor _editor;
    private readonly AudioEffectFactory _audioEffectFactory;

    public WaveService(IAudioDevice audioDevice, AudioEffectFactory audioEffectFactory)
    {
        _audioEffectFactory = audioEffectFactory;
        _loader = new AudioFileLoader();
        _playback = new PlaybackEngine(audioDevice);
        _editor = new AudioEditor();
    }

    #region File I/O — delegated to AudioFileLoader

    public Task<AudioFile> OpenAsync(string filePath) => _loader.OpenAsync(filePath);
    public Task<float[]> LoadWaveAsync(string filePath) => _loader.LoadWaveAsync(filePath);
    public Task ExportAsync(AudioFile audioFile, string targetFilePath) => _loader.ExportAsync(audioFile, targetFilePath);

    public void Close(AudioFile audioFile)
    {
        if (audioFile == null) return;
        _playback.CleanUp(audioFile);
        _loader.DeleteCacheFile(audioFile.FilePath);
        System.Diagnostics.Debug.WriteLine($"Closed and cleaned up player for {audioFile.FileName}.");
    }

    #endregion

    #region Playback — delegated to PlaybackEngine

    public void Play(AudioFile audioFile) => _playback.Play(audioFile);
    public void PlayRange(AudioFile audioFile, long startFrame, long endFrame) => _playback.PlayRange(audioFile, startFrame, endFrame);
    public void Pause(AudioFile audioFile) => _playback.Pause(audioFile);
    public long GetPlaybackPositionFrame(AudioFile audioFile) => _playback.GetPlaybackPositionFrame(audioFile);
    public void SetPlaybackPositionFrame(AudioFile audioFile, long frameIndex) => _playback.SetPlaybackPositionFrame(audioFile, frameIndex);

    #endregion

    #region Effects — coordinates AudioEffectFactory + PlaybackEngine

    public void AddEffect(AudioFile audioFile, string effectName)
    {
        if (audioFile == null) return;

        IAudioEffect effect;
        try
        {
            effect = _audioEffectFactory.CreateEffect(effectName, audioFile.SampleRate);
        }
        catch (NotSupportedException ex)
        {
            System.Diagnostics.Debug.WriteLine($"AddEffect: {ex.Message}");
            return;
        }

        if (audioFile.AudioEffects == null) audioFile.AudioEffects = new ObservableCollection<IAudioEffect>();
        audioFile.AudioEffects.Add(effect);
        _playback.UpdateEffects(audioFile, audioFile.AudioEffects);
    }

    public void RemoveEffect(AudioFile audioFile, IAudioEffect effect)
    {
        if (audioFile == null || audioFile.AudioEffects == null || effect == null) return;
        audioFile.AudioEffects.Remove(effect);
        _playback.UpdateEffects(audioFile, audioFile.AudioEffects);
    }

    #endregion

    #region Editing — delegated to AudioEditor

    public void ClipAudio(AudioFile audioFile, long startFrame, long endFrame)
    {
        _editor.ClipAudio(audioFile, startFrame, endFrame);
        _playback.CleanUp(audioFile);
    }

    #endregion

    public void Dispose() => _playback.Dispose();
}
