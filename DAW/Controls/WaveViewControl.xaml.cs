using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using DAW.Utils;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.Graphics.Canvas.Geometry;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DAW.Controls
{
    public sealed partial class WaveViewControl : UserControl
    {
        #region Dependency Properties

        public float[]? AudioData
        {
            get => (float[]?)GetValue(AudioDataProperty);
            set => SetValue(AudioDataProperty, value);
        }

        public static readonly DependencyProperty AudioDataProperty =
            DependencyProperty.Register(
                nameof(AudioData),
                typeof(float[]),
                typeof(WaveViewControl),
                new PropertyMetadata(null, OnAudioDataChanged));

        public int Channels
        {
            get => (int)GetValue(ChannelsProperty);
            set => SetValue(ChannelsProperty, value);
        }

        public static readonly DependencyProperty ChannelsProperty =
            DependencyProperty.Register(
                nameof(Channels),
                typeof(int),
                typeof(WaveViewControl),
                new PropertyMetadata(1));

        #endregion

        public WaveViewControl()
        {
            this.InitializeComponent();
        }

        #region Events

        private static void OnAudioDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveViewControl control)
            {
                control._peakArrays = null;
                control.PreviewCanvasControl.Invalidate();
            }
        }

        private void CanvasControl_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            WavePreview_Draw(sender, args);
        }

        #endregion


        #region Wave Preview

        private float[][]? _peakArrays;

        private void WavePreview_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            // If no data or channel count is invalid, skip drawing
            if (AudioData == null || AudioData.Length < 1 || Channels < 1) return;

            // Lazy-load peak arrays
            if (_peakArrays == null)
            {
                if (AudioData.Length > 2048 * 10)
                    _peakArrays = WaveDataHelper.GeneratePeakArrays(AudioData, Channels, 2048);
                else
                    _peakArrays = WaveDataHelper.GeneratePeakArrays(AudioData, Channels, 1);
            }

            WavePreview_DrawWave(sender, args);
        }

        #region Wave Preview Helpers

        private void WavePreview_DrawWave(CanvasControl sender, CanvasDrawEventArgs args)
        {
            var ds = args.DrawingSession;
            float canvasWidth = (float)sender.ActualWidth;
            float canvasHeight = (float)sender.ActualHeight;
            if (canvasWidth <= 0 || canvasHeight <= 0 || _peakArrays == null) return;

            // Spacing of 5px between channels
            float spacing = 5f;
            float totalSpacing = (Channels - 1) * spacing;
            float availableHeight = canvasHeight - totalSpacing;
            if (availableHeight <= 0) return;

            float channelHeight = availableHeight / Channels;

            // Draw each channel
            for (int ch = 0; ch < Channels; ch++)
            {
                float[] channelPeaks = _peakArrays[ch];
                if (channelPeaks.Length < 2) continue;

                // Calculate vertical offset for current channel
                float offsetY = ch * (channelHeight + spacing);
                DrawChannelWave(ds, channelPeaks, canvasWidth, channelHeight, offsetY);
            }
        }

        private void DrawChannelWave(Microsoft.Graphics.Canvas.CanvasDrawingSession ds,
                float[] channelPeaks,
                float canvasWidth,
                float channelHeight,
                float offsetY)
        {
            float verticalCenter = offsetY + channelHeight / 2;

            using var pathBuilder = new CanvasPathBuilder(ds);
            var topPoints = new List<Vector2>();
            var bottomPoints = new List<Vector2>();

            int canvasWidthInt = (int)canvasWidth;
            int totalPairs = channelPeaks.Length / 2;
            float samplesPerPixel = (float)totalPairs / canvasWidthInt;

            for (int x = 0; x < canvasWidthInt; x++)
            {
                int start = (int)(x * samplesPerPixel);
                int end = (int)((x + 1) * samplesPerPixel);
                if (end >= totalPairs) end = totalPairs - 1;

                float minVal = float.MaxValue;
                float maxVal = float.MinValue;
                for (int i = start; i <= end; i++)
                {
                    float localMin = channelPeaks[i * 2];
                    float localMax = channelPeaks[i * 2 + 1];
                    if (localMin < minVal) minVal = localMin;
                    if (localMax > maxVal) maxVal = localMax;
                }

                float yMin = verticalCenter - minVal * (channelHeight / 2);
                float yMax = verticalCenter - maxVal * (channelHeight / 2);
                topPoints.Add(new Vector2(x, yMax));
                bottomPoints.Add(new Vector2(x, yMin));
            }

            if (topPoints.Count > 0)
            {
                pathBuilder.BeginFigure(topPoints[0]);
                for (int i = 1; i < topPoints.Count; i++)
                {
                    pathBuilder.AddLine(topPoints[i]);
                }
                for (int i = bottomPoints.Count - 1; i >= 0; i--)
                {
                    pathBuilder.AddLine(bottomPoints[i]);
                }
                pathBuilder.EndFigure(CanvasFigureLoop.Closed);
            }

            using var geometry = CanvasGeometry.CreatePath(pathBuilder);
            ds.FillGeometry(geometry, Colors.SkyBlue);
        }


        #endregion


        #endregion


        #region Helpers

        //private void DrawWave(CanvasControl sender, CanvasDrawEventArgs args)
        //{
        //    if (_peakArray == null || _peakArray.Length < 2)
        //        return;

        //    float canvasWidth = (float)sender.ActualWidth;
        //    float canvasHeight = (float)sender.ActualHeight;
        //    if (canvasWidth <= 0) return;

        //    var ds = args.DrawingSession;
        //    float verticalCenter = canvasHeight / 2;

        //    using var pathBuilder = new CanvasPathBuilder(ds);
        //    var topPoints = new List<Vector2>();
        //    var bottomPoints = new List<Vector2>();

        //    int canvasWidthInt = (int)canvasWidth;
        //    int totalPairs = _peakArray.Length / 2;
        //    float samplesPerPixel = (float)totalPairs / canvasWidthInt;

        //    for (int x = 0; x < canvasWidthInt; x++)
        //    {
        //        int start = (int)(x * samplesPerPixel);
        //        int end = (int)((x + 1) * samplesPerPixel);
        //        if (end >= totalPairs) end = totalPairs - 1;

        //        float minVal = float.MaxValue;
        //        float maxVal = float.MinValue;
        //        for (int i = start; i <= end; i++)
        //        {
        //            float localMin = _peakArray[i * 2];
        //            float localMax = _peakArray[i * 2 + 1];
        //            if (localMin < minVal) minVal = localMin;
        //            if (localMax > maxVal) maxVal = localMax;
        //        }

        //        float yMin = verticalCenter - minVal * (canvasHeight / 2);
        //        float yMax = verticalCenter - maxVal * (canvasHeight / 2);
        //        topPoints.Add(new Vector2(x, yMax));
        //        bottomPoints.Add(new Vector2(x, yMin));
        //    }

        //    if (topPoints.Count > 0)
        //    {
        //        pathBuilder.BeginFigure(topPoints[0]);
        //        for (int i = 1; i < topPoints.Count; i++)
        //        {
        //            pathBuilder.AddLine(topPoints[i]);
        //        }
        //        for (int i = bottomPoints.Count - 1; i >= 0; i--)
        //        {
        //            pathBuilder.AddLine(bottomPoints[i]);
        //        }
        //        pathBuilder.EndFigure(CanvasFigureLoop.Closed);
        //    }

        //    using var geometry = CanvasGeometry.CreatePath(pathBuilder);
        //    ds.FillGeometry(geometry, Colors.SkyBlue);
        //}

        //private void DrawSelectedArea(CanvasControl sender, CanvasDrawEventArgs args)
        //{
        //    if (_isSelecting || IsValidSelection())
        //    {
        //        var ds = args.DrawingSession;
        //        float left = Math.Min(_selectStartX, _selectEndX);
        //        float right = Math.Max(_selectStartX, _selectEndX);
        //        float height = (float)sender.ActualHeight;

        //        ds.FillRectangle(left, 0, right - left, height, Color.FromArgb(60, 0, 120, 215));
        //        ds.DrawRectangle(left, 0, right - left, height, Color.FromArgb(255, 0, 120, 215));
        //    }
        //}

        //private bool IsValidSelection() => Math.Abs(_selectEndX - _selectStartX) > 2;

        //private void CalculateSampleRange()
        //{
        //    if (_peakArray == null || _peakArray.Length < 2) return;
        //    float canvasWidth = (float)PreviewCanvasControl.ActualWidth;
        //    if (canvasWidth <= 0) return;

        //    int totalPairs = _peakArray.Length / 2;
        //    float samplesPerPixel = totalPairs / canvasWidth;
        //    float left = Math.Min(_selectStartX, _selectEndX);
        //    float right = Math.Max(_selectStartX, _selectEndX);

        //    int start = (int)(left * samplesPerPixel);
        //    int end = (int)(right * samplesPerPixel);
        //    // 确保在范围内并更新依赖属性
        //    StartSampleIndex = Math.Clamp(start, 0, totalPairs - 1);
        //    EndSampleIndex = Math.Clamp(end, 0, totalPairs - 1);
        //}

        #endregion
    }
}
