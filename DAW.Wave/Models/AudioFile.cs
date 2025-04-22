using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAW.Wave.Models;

public class AudioFile
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public int SampleRate { get; set; }
    public int Channels { get; set; }
    public int BitDepth { get; set; }
    public string Format { get; set; } = string.Empty; // PCM or Float, etc.

    public string DisplayInfo => $"{FileName}\n" +
                                 $"{Format}\n" +
                                 $"{SampleRate} Hz\n" +
                                 $"{BitDepth}-bit\n" +
                                 $"{Channels} ch" +
                                 $"\n{Duration:hh\\:mm\\:ss\\.fff}";
}
