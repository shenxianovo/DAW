using System;
using DAW.Wave.Models;

namespace DAW.Wave.Services.Implementations;

/// <summary>
/// 负责音频数据的编辑操作（剪辑、裁切等）。
/// </summary>
internal class AudioEditor
{
    /// <summary>
    /// 从音频数据中删除 [startFrame, endFrame] 范围内的帧。
    /// </summary>
    public void ClipAudio(AudioFile audioFile, long startFrame, long endFrame)
    {
        if (audioFile == null || audioFile.AudioData == null || audioFile.Channels <= 0) return;

        long originalTotalFrames = audioFile.AudioData.Length / audioFile.Channels;
        if (startFrame > endFrame) (startFrame, endFrame) = (endFrame, startFrame);
        startFrame = Math.Max(0, startFrame);
        endFrame = Math.Min(originalTotalFrames > 0 ? originalTotalFrames - 1 : 0, endFrame);

        if (startFrame > endFrame || startFrame >= originalTotalFrames || originalTotalFrames == 0)
        {
            System.Diagnostics.Debug.WriteLine($"ClipAudio: Invalid clip range or empty audio. No action.");
            return;
        }

        long framesToClipCount = endFrame - startFrame + 1;
        long samplesToClipCount = framesToClipCount * audioFile.Channels;
        long startSampleToClipIndex = startFrame * audioFile.Channels;

        float[] originalData = audioFile.AudioData;
        int newAudioDataLength = originalData.Length - (int)samplesToClipCount;
        if (newAudioDataLength < 0) newAudioDataLength = 0;

        float[] newAudioData = new float[newAudioDataLength];
        Array.Copy(originalData, 0, newAudioData, 0, (int)startSampleToClipIndex);
        long originalSourceIndexAfterClip = startSampleToClipIndex + samplesToClipCount;
        if (originalSourceIndexAfterClip < originalData.Length)
        {
            Array.Copy(originalData, (int)originalSourceIndexAfterClip,
                       newAudioData, (int)startSampleToClipIndex,
                       originalData.Length - (int)originalSourceIndexAfterClip);
        }

        long oldPlaybackPos = audioFile.PlaybackPositionFrameIndex;
        audioFile.AudioData = newAudioData;

        long newTotalFrames = newAudioDataLength > 0 ? (newAudioData.Length / audioFile.Channels) : 0;
        audioFile.Duration = TimeSpan.FromSeconds((double)newTotalFrames / audioFile.SampleRate);

        long newPlaybackPos;
        if (oldPlaybackPos > endFrame) newPlaybackPos = oldPlaybackPos - framesToClipCount;
        else if (oldPlaybackPos >= startFrame) newPlaybackPos = startFrame;
        else newPlaybackPos = oldPlaybackPos;
        audioFile.PlaybackPositionFrameIndex = Math.Clamp(newPlaybackPos, 0, Math.Max(0, newTotalFrames > 0 ? newTotalFrames - 1 : 0));

        if (audioFile.VisibleLeftFrameIndex >= newTotalFrames) audioFile.VisibleLeftFrameIndex = 0;
        audioFile.VisibleRightFrameIndex = Math.Clamp(audioFile.VisibleRightFrameIndex, audioFile.VisibleLeftFrameIndex, Math.Max(0, newTotalFrames > 0 ? newTotalFrames - 1 : 0));
        if (audioFile.VisibleLeftFrameIndex > audioFile.VisibleRightFrameIndex && newTotalFrames > 0)
        {
            audioFile.VisibleLeftFrameIndex = 0;
            audioFile.VisibleRightFrameIndex = newTotalFrames - 1;
        }
        if (newTotalFrames == 0)
        {
            audioFile.VisibleLeftFrameIndex = 0;
            audioFile.VisibleRightFrameIndex = 0;
        }

        audioFile.SelectedLeftFrameIndex = 0;
        audioFile.SelectedRightFrameIndex = 0;
    }
}
