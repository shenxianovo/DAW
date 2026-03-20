using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DAW.Wave.Models;
using DAW.Wave.Services.Effects;

namespace DAW.Wave.Services.Implementations;

/// <summary>
/// 负责音频文件的加载、格式转换、导出和缓存管理。
/// </summary>
internal class AudioFileLoader
{
    /// <summary>
    /// 缓存文件夹路径：集中管理，避免硬编码散落各处
    /// </summary>
    private static readonly string CacheFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "DAW", "Cache");

    public AudioFileLoader()
    {
        EnsureCacheFolderExists();
    }

    /// <summary>
    /// 支持的音频文件扩展名（小写，含点号）。
    /// </summary>
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".wav", ".mp3", ".flac", ".aac", ".wma", ".ogg", ".m4a", ".aiff", ".aif"
    };

    public async Task<AudioFile> OpenAsync(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        if (string.IsNullOrEmpty(ext) || !SupportedExtensions.Contains(ext))
        {
            throw new NotSupportedException(
                $"不支持的文件格式 \"{ext}\"。支持的格式：{string.Join(", ", SupportedExtensions)}");
        }

        try
        {
            var cachedPath = await ConvertToPcm32Async(filePath);

            float[] audioData;
            WaveFormat waveFormat;
            TimeSpan totalTime;

            using (var tempReader = new AudioFileReader(cachedPath))
            {
                waveFormat = tempReader.WaveFormat;
                totalTime = tempReader.TotalTime;
                long totalSamplesInSource = tempReader.Length / (tempReader.WaveFormat.BitsPerSample / 8);
                audioData = new float[totalSamplesInSource];
                tempReader.Position = 0;
                int samplesRead = tempReader.Read(audioData, 0, audioData.Length);
                if (samplesRead != audioData.Length)
                {
                    Array.Resize(ref audioData, samplesRead);
                }
            }

            var audioFile = new AudioFile
            {
                FilePath = cachedPath,
                FileName = Path.GetFileName(filePath),
                Duration = totalTime,
                SampleRate = waveFormat.SampleRate,
                Channels = waveFormat.Channels,
                BitDepth = waveFormat.BitsPerSample,
                Format = "PCM 32-bit Float (In-Memory)",
                AudioData = audioData,
                AudioEffects = new ObservableCollection<IAudioEffect>()
            };

            long totalFrames = audioData.Length / audioFile.Channels;
            audioFile.VisibleLeftFrameIndex = 0;
            audioFile.VisibleRightFrameIndex = Math.Max(0, totalFrames > 0 ? totalFrames - 1 : 0);
            audioFile.PlaybackPositionFrameIndex = 0;
            audioFile.SelectedLeftFrameIndex = 0;
            audioFile.SelectedRightFrameIndex = 0;

            System.Diagnostics.Debug.WriteLine($"Loaded {audioFile.FileName}. Audio data in memory.");
            return audioFile;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in OpenAsync for {filePath}: {ex.Message}");
            throw new IOException($"Failed to open audio file: {filePath}", ex);
        }
    }

    public async Task<float[]> LoadWaveAsync(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        if (string.IsNullOrEmpty(ext) || !SupportedExtensions.Contains(ext))
        {
            throw new NotSupportedException(
                $"不支持的文件格式 \"{ext}\"。支持的格式：{string.Join(", ", SupportedExtensions)}");
        }

        try
        {
            var cachedPath = await ConvertToPcm32Async(filePath);

            float[] audioData;
            using (var tempReader = new AudioFileReader(cachedPath))
            {
                long totalSamplesInSource = tempReader.Length / (tempReader.WaveFormat.BitsPerSample / 8);
                audioData = new float[totalSamplesInSource];
                tempReader.Position = 0;
                int samplesRead = tempReader.Read(audioData, 0, audioData.Length);
                if (samplesRead != audioData.Length)
                {
                    Array.Resize(ref audioData, samplesRead);
                }
            }
            System.Diagnostics.Debug.WriteLine($"LoadWaveAsync: Loaded data from {filePath}.");
            return audioData;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in LoadWaveAsync for {filePath}: {ex.Message}");
            throw new IOException($"Failed to load wave data from file: {filePath}", ex);
        }
    }

    public async Task ExportAsync(AudioFile audioFile, string targetFilePath)
    {
        if (audioFile == null || audioFile.AudioData == null || audioFile.AudioData.Length == 0)
        {
            throw new ArgumentException("AudioFile or its AudioData is null or empty.", nameof(audioFile));
        }

        await Task.Run(() =>
        {
            var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(audioFile.SampleRate, audioFile.Channels);

            var memoryProvider = new MemorySampleProvider(audioFile.AudioData, waveFormat);
            ISampleProvider effectProvider = memoryProvider;

            if (audioFile.AudioEffects != null && audioFile.AudioEffects.Any())
            {
                effectProvider = new RealtimeEffectSampleProvider(memoryProvider, audioFile.AudioEffects);
            }

            // 流式分块写入，避免一次性分配全部音频内存
            const int chunkSize = 8192;
            float[] buffer = new float[chunkSize];

            using (var writer = new WaveFileWriter(targetFilePath, waveFormat))
            {
                int samplesRead;
                while ((samplesRead = effectProvider.Read(buffer, 0, chunkSize)) > 0)
                {
                    writer.WriteSamples(buffer, 0, samplesRead);
                }
            }

            System.Diagnostics.Debug.WriteLine($"Exported {audioFile.FileName} with effects to {targetFilePath}");
        });
    }

    /// <summary>
    /// 删除位于缓存目录下的文件。非缓存目录下的文件不会被删除。
    /// </summary>
    public void DeleteCacheFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;

        try
        {
            var fullPath = Path.GetFullPath(filePath);
            var fullCacheFolder = Path.GetFullPath(CacheFolder);
            if (fullPath.StartsWith(fullCacheFolder, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(filePath);
                System.Diagnostics.Debug.WriteLine($"Deleted cache file: {filePath}");
            }
        }
        catch (IOException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error deleting cache file {filePath}: {ex.Message}");
        }
    }

    private static void EnsureCacheFolderExists()
    {
        try
        {
            if (!Directory.Exists(CacheFolder))
            {
                Directory.CreateDirectory(CacheFolder);
                System.Diagnostics.Debug.WriteLine($"Created cache folder: {CacheFolder}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Warning: Failed to create cache folder '{CacheFolder}': {ex.Message}");
        }
    }

    private static async Task<string> ConvertToPcm32Async(string sourcePath)
    {
        return await Task.Run(() =>
        {
            EnsureCacheFolderExists();

            var tempFileName = $"{Path.GetFileNameWithoutExtension(sourcePath)}_{Guid.NewGuid():N}.wav";
            var cachedPath = Path.Combine(CacheFolder, tempFileName);

            using (var reader = new MediaFoundationReader(sourcePath))
            {
                var targetFormat = WaveFormat.CreateIeeeFloatWaveFormat(reader.WaveFormat.SampleRate, reader.WaveFormat.Channels);
                if (reader.WaveFormat.Equals(targetFormat) && reader.WaveFormat.Encoding == WaveFormatEncoding.IeeeFloat)
                {
                    File.Copy(sourcePath, cachedPath, true);
                    return cachedPath;
                }
                using (var resampler = new MediaFoundationResampler(reader, targetFormat))
                {
                    WaveFileWriter.CreateWaveFile(cachedPath, resampler);
                }
            }
            return cachedPath;
        });
    }
}
