using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAW.Wave.Models;

public class AudioFile
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

    public long DisplayStartSampleIndex { get; set; }
    public long DisplayEndSampleIndex { get; set; }

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
