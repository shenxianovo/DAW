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
using Microsoft.Graphics.Canvas;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DAW.Controls
{
    public sealed partial class WaveViewControl : UserControl
    {
        #region Themes

        private void OnActualThemeChanged(FrameworkElement sender, object args)
        {
            // 当主题变化时更新 ClearColor
            UpdateCanvasClearColor(this.ActualTheme);
        }

        private void UpdateCanvasClearColor(ElementTheme theme)
        {
            // 根据当前主题设置 ClearColor
            var isDarkTheme = theme == ElementTheme.Dark;
            var clearColor = isDarkTheme ? Colors.Black : Colors.White;

            PreviewCanvasControl.ClearColor = clearColor;
            EditorCanvasControl.ClearColor = clearColor;
        }

        #endregion

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

        public long VisibleLeftFrame
        {
            get => (long)GetValue(VisibleLeftFrameProperty);
            set => SetValue(VisibleLeftFrameProperty, value);
        }

        public static readonly DependencyProperty VisibleLeftFrameProperty =
            DependencyProperty.Register(
                nameof(VisibleLeftFrame),
                typeof(long),
                typeof(WaveViewControl),
                new PropertyMetadata(0L, OnBoundsChanged));

        public long VisibleRightFrame
        {
            get => (long)GetValue(VisibleRightFrameProperty);
            set => SetValue(VisibleRightFrameProperty, value);
        }

        public static readonly DependencyProperty VisibleRightFrameProperty =
            DependencyProperty.Register(
                nameof(VisibleRightFrame),
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

        #region Events

        private static void OnAudioDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveViewControl control)
            {
                control._previewPeakArrays = null;
                control._editorPeakArrays = null;
                control.PreviewCanvasControl.Invalidate();
                control.EditorCanvasControl.Invalidate();
                control.MelSpectrogramCanvasControl.Invalidate();
            }
        }

        private static void OnBoundsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveViewControl control)
            {
                control.PreviewCanvasControl.Invalidate();
                control.EditorCanvasControl.Invalidate();
                control.MelSpectrogramCanvasControl.Invalidate();
            }
        }

        #endregion

        public WaveViewControl()
        {
            this.InitializeComponent();

            // 监听控件的主题变化
            this.ActualThemeChanged += OnActualThemeChanged;

            // 初始化 ClearColor
            UpdateCanvasClearColor(this.ActualTheme);
        }

        #region Wave Preview

        #region Private Fields

        private float[][]? _previewPeakArrays;

        private bool _isDraggingLeft;
        private bool _isDraggingRight;
        private float _dragOffset; // 记录拖动时指针与线位置的偏移

        private bool _isDraggingRange;           // 是否正在整体拖拽可见区
        private float _dragRangeStartX;          // 鼠标按下时初始 X
        private long _panStartLeftSample;        // 鼠标按下时记录的 VisibleLeftFrame
        private long _panStartRightSample;       // 鼠标按下时记录的 VisibleRightFrame

        #endregion

        #region Events

        private void OnPreviewCanvasPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(PreviewCanvasControl);
            float x = (float)point.Position.X;

            float canvasWidth = (float)PreviewCanvasControl.ActualWidth;
            float canvasHeight = (float)PreviewCanvasControl.ActualHeight;
            if (canvasWidth <= 0 || canvasHeight <= 0 || AudioData == null) return;

            long totalSamples = AudioData.Length / Math.Max(Channels, 1);
            float pxPerSample = totalSamples > 0 ? canvasWidth / totalSamples : 0;

            float vLeftX = VisibleLeftFrame * pxPerSample;
            float vRightX = VisibleRightFrame * pxPerSample;
            if (vRightX < vLeftX) (vLeftX, vRightX) = (vRightX, vLeftX);

            // 允许 5 像素左右的可点击范围
            const float grabZone = 5f;

            // 判断是否单纯拖动左边界
            if (Math.Abs(x - vLeftX) <= grabZone)
            {
                _isDraggingLeft = true;
                _dragOffset = x - vLeftX;
            }
            // 判断是否单纯拖动右边界
            else if (Math.Abs(x - vRightX) <= grabZone)
            {
                _isDraggingRight = true;
                _dragOffset = x - vRightX;
            }
            else
            {
                // 如果鼠标落在可见区域中间，则进行整体平移
                if (x > vLeftX + grabZone && x < vRightX - grabZone)
                {
                    _isDraggingRange = true;
                    _dragRangeStartX = x;
                    // 记录当前可见区初始位置
                    _panStartLeftSample = VisibleLeftFrame;
                    _panStartRightSample = VisibleRightFrame;
                }
            }
        }

        private void OnPreviewCanvasPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(PreviewCanvasControl);
            float x = (float)point.Position.X;

            float canvasWidth = (float)PreviewCanvasControl.ActualWidth;
            if (canvasWidth <= 0 || AudioData == null) return;

            long totalSamples = AudioData.Length / Math.Max(Channels, 1);
            float pxPerSample = totalSamples > 0 ? canvasWidth / totalSamples : 0;

            if (_isDraggingLeft)
            {
                long newSample = (long)Math.Round((x - _dragOffset) / pxPerSample);
                newSample = Math.Clamp(newSample, 0, totalSamples - 1);
                VisibleLeftFrame = newSample;
            }
            else if (_isDraggingRight)
            {
                long newSample = (long)Math.Round((x - _dragOffset) / pxPerSample);
                newSample = Math.Clamp(newSample, 0, totalSamples - 1);
                VisibleRightFrame = newSample;
            }
            else if (_isDraggingRange)
            {
                // 整体平移
                float deltaX = x - _dragRangeStartX; // 鼠标移动的像素距离
                long deltaSamples = (long)Math.Round(deltaX / pxPerSample);

                long newLeft = _panStartLeftSample + deltaSamples;
                long newRight = _panStartRightSample + deltaSamples;

                // 约束在 [0, totalSamples-1]
                long length = _panStartRightSample - _panStartLeftSample;
                if (length < 0) length *= -1; // 保证为正数

                // 确保新的可见范围在合法区间内
                if (newLeft < 0)
                {
                    newLeft = 0;
                    newRight = newLeft + length;
                }
                else if (newRight > (totalSamples - 1))
                {
                    newRight = totalSamples - 1;
                    newLeft = newRight - length;
                    if (newLeft < 0) newLeft = 0; // 再次约束
                }

                VisibleLeftFrame = newLeft;
                VisibleRightFrame = newRight;
            }
        }

        private void OnPreviewCanvasPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            _isDraggingLeft = false;
            _isDraggingRight = false;
            _isDraggingRange = false;
            _dragOffset = 0;
        }

        private void PreviewCanvasControl_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            WavePreview_Draw(sender, args);
        }

        #endregion

        private void WavePreview_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            // If no data or channel count is invalid, skip drawing
            if (AudioData == null || AudioData.Length < 1 || Channels < 1) return;

            // Lazy-load peak arrays
            if (_previewPeakArrays == null)
            {
                int samplesPerPeak = (int)(AudioData.Length / sender.ActualWidth);
                _previewPeakArrays = WaveDataHelper.GeneratePeakArrays(AudioData, Channels, samplesPerPeak);
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
            if (canvasWidth <= 0 || canvasHeight <= 0 || _previewPeakArrays == null) return;

            // Spacing of 5px between channels
            float spacing = 5f;
            float totalSpacing = (Channels - 1) * spacing;
            float availableHeight = canvasHeight - totalSpacing;
            if (availableHeight <= 0) return;

            float channelHeight = availableHeight / Channels;

            // Draw each channel
            for (int ch = 0; ch < Channels; ch++)
            {
                float[] channelPeaks = _previewPeakArrays[ch];
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
            float vLeftX = VisibleLeftFrame * pxPerSample;
            float vRightX = VisibleRightFrame * pxPerSample;
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

        #region Wave Editor

        private bool _isSelecting;
        private float _editorPointerDownX;
        private float _resolution;

        private float[][]? _editorPeakArrays;
        private void EditorCanvasControl_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            if (AudioData == null || AudioData.Length < 1 || Channels < 1) return;

            GenerateEditorPeakArraysIfNeeded();

            var ds = args.DrawingSession;
            float canvasWidth = (float)sender.ActualWidth;
            float canvasHeight = (float)sender.ActualHeight;
            if (canvasWidth <= 0 || canvasHeight <= 0) return;

            // 1. 绘制可见范围内的波形子集
            DrawEditorWave(ds, canvasWidth, canvasHeight);

            // 2. 绘制选中区域
            float pxPerSample = canvasWidth / (VisibleRightFrame - VisibleLeftFrame + 1);
            float sLeftX = (SelectedLeftSample - VisibleLeftFrame) * pxPerSample;
            float sRightX = (SelectedRightSample - VisibleLeftFrame) * pxPerSample;
            if (sRightX < sLeftX) (sLeftX, sRightX) = (sRightX, sLeftX);

            ds.DrawLine(sLeftX, 0, sLeftX, canvasHeight, Colors.Orange);
            ds.DrawLine(sRightX, 0, sRightX, canvasHeight, Colors.Orange);
            ds.FillRectangle(sLeftX, 0, sRightX - sLeftX, canvasHeight, Color.FromArgb(100, 255, 165, 0));

            // 3. 绘制播放进度线
            float progressX = (PlaybackPositionSample - VisibleLeftFrame) * pxPerSample;
            ds.DrawLine(progressX, 0, progressX, canvasHeight, Colors.Red);
        }

        private void GenerateEditorPeakArraysIfNeeded()
        {
            if (_editorPeakArrays == null)
            {
                _editorPeakArrays = WaveDataHelper.GeneratePeakArrays(AudioData, Channels, 2048);
                _resolution = (VisibleRightFrame - VisibleLeftFrame + 1) / (AudioData.Length / Channels);
                return;
            }

            var visibleLength = (float)(VisibleRightFrame - VisibleLeftFrame + 1);
            var totalLength = (float)(AudioData.Length / Channels);

            var currentResolution = (float)Math.Round(totalLength / visibleLength);

            if (_resolution == currentResolution) return;

            _resolution = currentResolution;
            int blockSize = _resolution switch
            {
                < 2f => 2048,
                < 4f => 1024,
                < 8f => 512,
                _ => 1
            };

            // 根据新的 blockSize 重新生成可见区的数据或全量数据
            _editorPeakArrays = WaveDataHelper.GeneratePeakArrays(AudioData, Channels, blockSize);
        }

        private void DrawEditorWave(Microsoft.Graphics.Canvas.CanvasDrawingSession ds,
                                    float canvasWidth,
                                    float canvasHeight)
        {
            float spacing = 5f;
            float totalSpacing = (Channels - 1) * spacing;
            float availableHeight = canvasHeight - totalSpacing;
            if (availableHeight <= 0) return;

            float channelHeight = availableHeight / Channels;
            long visibleLength = VisibleRightFrame - VisibleLeftFrame + 1;
            if (visibleLength <= 1) return;

            bool useFill = (_resolution < 8f);

            for (int ch = 0; ch < Channels; ch++)
            {
                float[] peaks = _editorPeakArrays[ch];
                if (peaks.Length < 2) continue;

                float offsetY = ch * (channelHeight + spacing);

                if (useFill) // 填充
                    DrawEditorWave_Fill(ds, peaks, canvasWidth, channelHeight, offsetY);
                else // 连线
                    DrawEditorWave_Line(ds, peaks, canvasWidth, channelHeight, offsetY);
            }
        }

        private void DrawEditorWave_Fill(
            Microsoft.Graphics.Canvas.CanvasDrawingSession ds,
            float[] channelPeaks,
            float canvasWidth,
            float channelHeight,
            float offsetY)
        {
            long visibleLength = VisibleRightFrame - VisibleLeftFrame + 1;
            int totalPairs = channelPeaks.Length / 2;
            float samplesPerPixel = (float)visibleLength / canvasWidth;
            float vCenter = offsetY + channelHeight / 2;

            using var pathBuilder = new CanvasPathBuilder(ds);
            var topPoints = new List<Vector2>();
            var bottomPoints = new List<Vector2>();

            for (int x = 0; x < (int)canvasWidth; x++)
            {
                long sampleIndex = VisibleLeftFrame + (long)(x * samplesPerPixel);
                long pairIndex = sampleIndex * totalPairs / (AudioData.Length / Channels);
                pairIndex = Math.Clamp(pairIndex, 0, totalPairs - 1);

                float localMin = channelPeaks[pairIndex * 2];
                float localMax = channelPeaks[pairIndex * 2 + 1];

                float yMin = vCenter - localMin * (channelHeight / 2);
                float yMax = vCenter - localMax * (channelHeight / 2);
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
            ds.FillGeometry(geometry, Colors.MediumAquamarine);
        }

        private void DrawEditorWave_Line(
            Microsoft.Graphics.Canvas.CanvasDrawingSession ds,
            float[] channelPeaks,
            float canvasWidth,
            float channelHeight,
            float offsetY)
        {
            long visibleLength = VisibleRightFrame - VisibleLeftFrame + 1;
            int totalPairs = channelPeaks.Length / 2;
            float samplesPerPixel = (float)visibleLength / canvasWidth;
            float vCenter = offsetY + channelHeight / 2;

            var linePoints = new List<Vector2>();

            for (int x = 0; x < (int)canvasWidth; x++)
            {
                long sampleIndex = VisibleLeftFrame + (long)(x * samplesPerPixel);
                long pairIndex = sampleIndex * totalPairs / (AudioData.Length / Channels);
                pairIndex = Math.Clamp(pairIndex, 0, totalPairs - 1);

                float localMin = channelPeaks[pairIndex * 2];
                float localMax = channelPeaks[pairIndex * 2 + 1];
                float avgVal = (localMin + localMax) / 2;

                float y = vCenter - avgVal * (channelHeight / 2);
                linePoints.Add(new Vector2(x, y));
            }

            if (linePoints.Count > 1)
            {
                for (int i = 0; i < linePoints.Count - 1; i++)
                {
                    ds.DrawLine(linePoints[i], linePoints[i + 1], Colors.MediumAquamarine, 1f);
                }
            }
        }

        #region Wave Editor Events

        private void OnEditorCanvasPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (AudioData == null || AudioData.Length < 1 || Channels < 1) return;

            var point = e.GetCurrentPoint(EditorCanvasControl);

            _isSelecting = true;
            _editorPointerDownX = (float)point.Position.X;
        }

        private void OnEditorCanvasPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            // 若不移动超过一定像素，就视为单击而非拖拽
            if (!_isSelecting || AudioData == null) return;

            float movedDistance = Math.Abs((float)e.GetCurrentPoint(EditorCanvasControl).Position.X - _editorPointerDownX);
            if (movedDistance > 5f) // 超过5像素才算拖拽
            {
                // 若确定是拖拽，则执行原先的选区逻辑
                float canvasWidth = (float)EditorCanvasControl.ActualWidth;
                long visibleLen = VisibleRightFrame - VisibleLeftFrame + 1;
                if (canvasWidth <= 0 || visibleLen <= 0) return;

                var point = e.GetCurrentPoint(EditorCanvasControl);
                float x = (float)point.Position.X;
                float startX = Math.Min(_editorPointerDownX, x);
                float endX = Math.Max(_editorPointerDownX, x);

                float pxPerSample = canvasWidth / visibleLen;
                long newStart = (long)(startX / pxPerSample) + VisibleLeftFrame;
                long newEnd = (long)(endX / pxPerSample) + VisibleLeftFrame;
                newStart = Math.Clamp(newStart, VisibleLeftFrame, VisibleRightFrame);
                newEnd = Math.Clamp(newEnd, VisibleLeftFrame, VisibleRightFrame);

                SelectedLeftSample = newStart;
                SelectedRightSample = newEnd;

                EditorCanvasControl.Invalidate();
            }
        }

        private void OnEditorCanvasPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            // 如果鼠标没有明显拖拽，则当作单击，更新播放位置
            float movedDistance = Math.Abs((float)e.GetCurrentPoint(EditorCanvasControl).Position.X - _editorPointerDownX);
            if (movedDistance <= 5f && AudioData != null && Channels > 0)
            {
                float canvasWidth = (float)EditorCanvasControl.ActualWidth;
                long visibleLen = VisibleRightFrame - VisibleLeftFrame + 1;
                if (canvasWidth > 0 && visibleLen > 0)
                {
                    float x = (float)e.GetCurrentPoint(EditorCanvasControl).Position.X;
                    float pxPerSample = canvasWidth / visibleLen;
                    long newPosition = (long)(x / pxPerSample) + VisibleLeftFrame;
                    newPosition = Math.Clamp(newPosition, VisibleLeftFrame, VisibleRightFrame);

                    PlaybackPositionSample = newPosition;
                }
            }

            _isSelecting = false;
        }


        #endregion

        #endregion

        #region Mel Spectrogram
        private void MelSpectrogramCanvasControl_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
        }

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
