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

        public long VisibleLeftSample
        {
            get => (long)GetValue(VisibleLeftSampleProperty);
            set => SetValue(VisibleLeftSampleProperty, value);
        }

        public static readonly DependencyProperty VisibleLeftSampleProperty =
            DependencyProperty.Register(
                nameof(VisibleLeftSample),
                typeof(long),
                typeof(WaveViewControl),
                new PropertyMetadata(0L, OnBoundsChanged));

        public long VisibleRightSample
        {
            get => (long)GetValue(VisibleRightSampleProperty);
            set => SetValue(VisibleRightSampleProperty, value);
        }

        public static readonly DependencyProperty VisibleRightSampleProperty =
            DependencyProperty.Register(
                nameof(VisibleRightSample),
                typeof(long),
                typeof(WaveViewControl),
                new PropertyMetadata(0L, OnBoundsChanged));

        public long SelectedLeftSample
        {
            get => (long)GetValue(SelectedLeftSampleProperty);
            set => SetValue(SelectedLeftSampleProperty, value);
        }

        public static readonly DependencyProperty SelectedLeftSampleProperty =
            DependencyProperty.Register(
                nameof(SelectedLeftSample),
                typeof(long),
                typeof(WaveViewControl),
                new PropertyMetadata(0L, OnBoundsChanged));

        public long SelectedRightSample
        {
            get => (long)GetValue(SelectedRightSampleProperty);
            set => SetValue(SelectedRightSampleProperty, value);
        }

        public static readonly DependencyProperty SelectedRightSampleProperty =
            DependencyProperty.Register(
                nameof(SelectedRightSample),
                typeof(long),
                typeof(WaveViewControl),
                new PropertyMetadata(0L, OnBoundsChanged));

        public long PlaybackPositionSample
        {
            get => (long)GetValue(PlaybackPositionSampleProperty);
            set => SetValue(PlaybackPositionSampleProperty, value);
        }

        public static readonly DependencyProperty PlaybackPositionSampleProperty =
            DependencyProperty.Register(
                nameof(PlaybackPositionSample),
                typeof(long),
                typeof(WaveViewControl),
                new PropertyMetadata(0L, OnBoundsChanged));

        #endregion

        #region Private Fields

        private bool _isDraggingLeft;
        private bool _isDraggingRight;
        private float _dragOffset; // 记录拖动时指针与线位置的偏移

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

        private static void OnBoundsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveViewControl control)
            {
                control.PreviewCanvasControl.Invalidate();
            }
        }

        private void OnCanvasPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(PreviewCanvasControl);
            float x = (float)point.Position.X;

            float canvasWidth = (float)PreviewCanvasControl.ActualWidth;
            float canvasHeight = (float)PreviewCanvasControl.ActualHeight;
            if (canvasWidth <= 0 || canvasHeight <= 0 || AudioData == null) return;

            long totalSamples = AudioData.Length / Math.Max(Channels, 1);
            float pxPerSample = totalSamples > 0 ? canvasWidth / totalSamples : 0;

            float vLeftX = VisibleLeftSample * pxPerSample;
            float vRightX = VisibleRightSample * pxPerSample;
            if (vRightX < vLeftX) (vLeftX, vRightX) = (vRightX, vLeftX);

            // 允许 5 像素左右的可点击范围
            const float grabZone = 5f;

            if (Math.Abs(x - vLeftX) <= grabZone)
            {
                _isDraggingLeft = true;
                _dragOffset = x - vLeftX;
            }
            else if (Math.Abs(x - vRightX) <= grabZone)
            {
                _isDraggingRight = true;
                _dragOffset = x - vRightX;
            }
        }

        private void OnCanvasPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_isDraggingLeft && !_isDraggingRight) return;

            var point = e.GetCurrentPoint(PreviewCanvasControl);
            float x = (float)point.Position.X;

            float canvasWidth = (float)PreviewCanvasControl.ActualWidth;
            if (canvasWidth <= 0 || AudioData == null) return;

            long totalSamples = AudioData.Length / Math.Max(Channels, 1);
            float pxPerSample = totalSamples > 0 ? canvasWidth / totalSamples : 0;

            // 计算新的采样索引，并限制在范围内
            long newSample = (long)Math.Round((x - _dragOffset) / pxPerSample);
            newSample = Math.Clamp(newSample, 0, totalSamples - 1);

            if (_isDraggingLeft)
            {
                VisibleLeftSample = newSample;
            }
            else if (_isDraggingRight)
            {
                VisibleRightSample = newSample;
            }
        }

        private void OnCanvasPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            _isDraggingLeft = false;
            _isDraggingRight = false;
            _dragOffset = 0;
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
            WavePreview_DrawBoundaries(sender, args);
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

        private void WavePreview_DrawBoundaries(CanvasControl sender, CanvasDrawEventArgs args)
        {
            if (AudioData == null || AudioData.Length < 1 || Channels < 1) return;

            var ds = args.DrawingSession;
            float canvasWidth = (float)sender.ActualWidth;
            float canvasHeight = (float)sender.ActualHeight;
            if (canvasWidth <= 0 || canvasHeight <= 0) return;

            int totalSamples = AudioData.Length / Channels;
            if (totalSamples <= 0) return;

            float pxPerSample = canvasWidth / totalSamples;

            // --- Visible range lines ---
            float vLeftX = VisibleLeftSample * pxPerSample;
            float vRightX = VisibleRightSample * pxPerSample;
            if (vRightX < vLeftX) (vLeftX, vRightX) = (vRightX, vLeftX);

            ds.DrawLine(vLeftX, 0, vLeftX, canvasHeight, Colors.Gray);
            ds.DrawLine(vRightX, 0, vRightX, canvasHeight, Colors.Gray);

            // Optional: fill the visible region if desired
            ds.FillRectangle(vLeftX, 0, vRightX - vLeftX, canvasHeight, Color.FromArgb(30, 0, 0, 0));

            // --- Selected range lines + semi-transparent fill ---
            float sLeftX = SelectedLeftSample * pxPerSample;
            float sRightX = SelectedRightSample * pxPerSample;
            if (sRightX < sLeftX) (sLeftX, sRightX) = (sRightX, sLeftX);

            ds.DrawLine(sLeftX, 0, sLeftX, canvasHeight, Colors.Red);
            ds.DrawLine(sRightX, 0, sRightX, canvasHeight, Colors.Red);

            // Fill selection area with semi-transparency
            if (sRightX > sLeftX)
            {
                ds.FillRectangle(sLeftX, 0, sRightX - sLeftX, canvasHeight, Color.FromArgb(60, 0, 120, 215));
            }

            float progressX = PlaybackPositionSample * pxPerSample;
            ds.DrawLine(progressX, 0, progressX, canvasHeight, Colors.Red);
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
