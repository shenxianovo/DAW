using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DAW.Wave.Models;

[ObservableObject]
public partial class AudioFile
{
    #region Audio MetaInfo

    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public int SampleRate { get; set; }
    public int Channels { get; set; }
    public int BitDepth { get; set; }
    public string Format { get; set; } = string.Empty; // PCM or Float, etc.

    #endregion

    #region Editor Info

    [ObservableProperty]
    public partial long VisibleLeftSampleIndex { get; set; }
    [ObservableProperty]
    public partial long VisibleRightSampleIndex { get; set; }
    [ObservableProperty]
    public partial long PlaybackPositionSampleIndex { get; set; }
    [ObservableProperty]
    public partial long SelectedLeftSampleIndex { get; set; }
    [ObservableProperty]
    public partial long SelectedRightSampleIndex { get; set; }

    #endregion

    #region Audio Data

    public float[] AudioData { get; set; }

    #endregion

    public string DisplayInfo => $"{FileName}\n" +
                                 $"{Format}\n" +
                                 $"{SampleRate} Hz\n" +
                                 $"{BitDepth}-bit\n" +
                                 $"{Channels} ch" +
                                 $"\n{Duration:hh\\:mm\\:ss\\.fff}";
}
