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

namespace DAW.Controls
{
    public sealed partial class WaveViewControl : UserControl
    {
        #region Themes

        // 与 App.xaml ThemeDictionaries 中定义的颜色保持一致
        private static readonly Color ClearColorLight = Color.FromArgb(0xFF, 0xFA, 0xFA, 0xFA); // #FFFAFAFA
        private static readonly Color ClearColorDark  = Color.FromArgb(0xFF, 0x20, 0x20, 0x20); // #FF202020

        private void OnActualThemeChanged(FrameworkElement sender, object args)
        {
            UpdateCanvasClearColor(this.ActualTheme);
            PreviewCanvasControl.Invalidate();
            EditorCanvasControl.Invalidate();
        }

        private void UpdateCanvasClearColor(ElementTheme theme)
        {
            // ActualTheme 只会返回 Light 或 Dark，不会返回 Default
            var clearColor = theme == ElementTheme.Dark ? ClearColorDark : ClearColorLight;
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

        public int SampleRate
        {
            get => (int)GetValue(SampleRateProperty);
            set => SetValue(SampleRateProperty, value);
        }

        public static readonly DependencyProperty SampleRateProperty =
            DependencyProperty.Register(
                nameof(SampleRate),
                typeof(int),
                typeof(WaveViewControl),
                new PropertyMetadata(44100));

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
                new PropertyMetadata(0L, OnVisibleRangeChanged));

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
                new PropertyMetadata(0L, OnVisibleRangeChanged));

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
                new PropertyMetadata(0L, OnOverlayChanged));

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
                new PropertyMetadata(0L, OnOverlayChanged));

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
                new PropertyMetadata(0L, OnOverlayChanged));

        #endregion

        #region Property Change Callbacks

        private static void OnAudioDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveViewControl control)
            {
                control._previewPeakArrays = null;
                control._editorPeakArrays = null;
                control._previewGeometryDirty = true;
                control._editorGeometryDirty = true;
                control.PreviewCanvasControl.Invalidate();
                control.EditorCanvasControl.Invalidate();
            }
        }

        /// <summary>
        /// 可见帧范围改变  Editor 波形几何体需要重建
        /// </summary>
        private static void OnVisibleRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveViewControl control)
            {
                control._editorGeometryDirty = true;
                control.PreviewCanvasControl.Invalidate();
                control.EditorCanvasControl.Invalidate();
            }
        }

        /// <summary>
        /// 选区 / 播放位置改变  只需重绘覆盖层线条，波形几何体不变
        /// </summary>
        private static void OnOverlayChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveViewControl control)
            {
                control.PreviewCanvasControl.Invalidate();
                control.EditorCanvasControl.Invalidate();
            }
        }

        #endregion

        #region Geometry Caching

        private CanvasGeometry? _previewWaveGeometry;
        private bool _previewGeometryDirty = true;

        private CanvasGeometry? _editorWaveGeometry;
        private bool _editorGeometryDirty = true;

        private void DisposeGeometryCache()
        {
            _previewWaveGeometry?.Dispose();
            _previewWaveGeometry = null;
            _editorWaveGeometry?.Dispose();
            _editorWaveGeometry = null;
        }

        #endregion

        public WaveViewControl()
        {
            this.InitializeComponent();

            // 主题
            this.ActualThemeChanged += OnActualThemeChanged;
            this.Loaded += (s, e) => UpdateCanvasClearColor(this.ActualTheme);

            // Canvas 尺寸变化时使缓存失效
            PreviewCanvasControl.SizeChanged += (s, e) =>
            {
                _previewPeakArrays = null;
                _previewGeometryDirty = true;
                PreviewCanvasControl.Invalidate();
            };
            EditorCanvasControl.SizeChanged += (s, e) =>
            {
                _editorGeometryDirty = true;
                EditorCanvasControl.Invalidate();
            };

            // 释放 GPU 几何资源
            this.Unloaded += (s, e) => DisposeGeometryCache();
        }

        #region Wave Preview

        #region Private Fields

        private float[][]? _previewPeakArrays;

        private bool _isDraggingLeft;
        private bool _isDraggingRight;
        private float _dragOffset;

        private bool _isDraggingRange;
        private float _dragRangeStartX;
        private long _panStartLeftSample;
        private long _panStartRightSample;

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
            else if (x > vLeftX + grabZone && x < vRightX - grabZone)
            {
                _isDraggingRange = true;
                _dragRangeStartX = x;
                _panStartLeftSample = VisibleLeftFrame;
                _panStartRightSample = VisibleRightFrame;
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
                float deltaX = x - _dragRangeStartX;
                long deltaSamples = (long)Math.Round(deltaX / pxPerSample);

                long newLeft = _panStartLeftSample + deltaSamples;
                long newRight = _panStartRightSample + deltaSamples;

                long length = Math.Abs(_panStartRightSample - _panStartLeftSample);

                if (newLeft < 0)
                {
                    newLeft = 0;
                    newRight = newLeft + length;
                }
                else if (newRight > (totalSamples - 1))
                {
                    newRight = totalSamples - 1;
                    newLeft = newRight - length;
                    if (newLeft < 0) newLeft = 0;
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
            if (AudioData == null || AudioData.Length < 1 || Channels < 1) return;

            // 延迟生成峰值数组（仅在数据变化或 Canvas 大小变化时重建）
            if (_previewPeakArrays == null)
            {
                int samplesPerPeak = Math.Max(1, (int)(AudioData.Length / sender.ActualWidth));
                _previewPeakArrays = WaveDataHelper.GeneratePeakArrays(AudioData, Channels, samplesPerPeak);
                _previewGeometryDirty = true;
            }

            var ds = args.DrawingSession;
            float canvasWidth = (float)sender.ActualWidth;
            float canvasHeight = (float)sender.ActualHeight;
            if (canvasWidth <= 0 || canvasHeight <= 0) return;

            //  只在数据/尺寸变化时重建波形几何体，播放位置变化时直接复用
            if (_previewGeometryDirty || _previewWaveGeometry == null)
            {
                _previewWaveGeometry?.Dispose();
                _previewWaveGeometry = BuildPreviewWaveGeometry(sender, canvasWidth, canvasHeight);
                _previewGeometryDirty = false;
            }

            if (_previewWaveGeometry != null)
            {
                ds.FillGeometry(_previewWaveGeometry, Colors.SkyBlue);
            }

            // 覆盖层（边界线、选区、播放位置）开销很小
            WavePreview_DrawBoundaries(sender, args);
        }

        #region Wave Preview Helpers

        /// <summary>
        /// 构建预览波形的缓存几何体（所有声道合并到一个 Path）
        /// </summary>
        private CanvasGeometry? BuildPreviewWaveGeometry(
            ICanvasResourceCreator creator, float canvasWidth, float canvasHeight)
        {
            if (_previewPeakArrays == null) return null;

            float spacing = 5f;
            float totalSpacing = (Channels - 1) * spacing;
            float availableHeight = canvasHeight - totalSpacing;
            if (availableHeight <= 0) return null;

            float channelHeight = availableHeight / Channels;
            int canvasWidthInt = (int)canvasWidth;
            if (canvasWidthInt <= 0) return null;

            using var pathBuilder = new CanvasPathBuilder(creator);

            for (int ch = 0; ch < Channels; ch++)
            {
                float[] channelPeaks = _previewPeakArrays[ch];
                if (channelPeaks.Length < 2) continue;

                float offsetY = ch * (channelHeight + spacing);
                float verticalCenter = offsetY + channelHeight / 2;
                int totalPairs = channelPeaks.Length / 2;
                float samplesPerPixel = (float)totalPairs / canvasWidthInt;

                // 使用预分配数组代替 List<Vector2>
                var topY = new float[canvasWidthInt];
                var bottomY = new float[canvasWidthInt];

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

                    topY[x] = verticalCenter - maxVal * (channelHeight / 2);
                    bottomY[x] = verticalCenter - minVal * (channelHeight / 2);
                }

                // 闭合图形：上沿正向，下沿反向
                pathBuilder.BeginFigure(new Vector2(0, topY[0]));
                for (int x = 1; x < canvasWidthInt; x++)
                    pathBuilder.AddLine(new Vector2(x, topY[x]));
                for (int x = canvasWidthInt - 1; x >= 0; x--)
                    pathBuilder.AddLine(new Vector2(x, bottomY[x]));
                pathBuilder.EndFigure(CanvasFigureLoop.Closed);
            }

            return CanvasGeometry.CreatePath(pathBuilder);
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

            float vLeftX = VisibleLeftFrame * pxPerSample;
            float vRightX = VisibleRightFrame * pxPerSample;
            if (vRightX < vLeftX) (vLeftX, vRightX) = (vRightX, vLeftX);

            ds.DrawLine(vLeftX, 0, vLeftX, canvasHeight, Colors.Gray);
            ds.DrawLine(vRightX, 0, vRightX, canvasHeight, Colors.Gray);
            ds.FillRectangle(vLeftX, 0, vRightX - vLeftX, canvasHeight, Color.FromArgb(30, 0, 0, 0));

            float sLeftX = SelectedLeftSample * pxPerSample;
            float sRightX = SelectedRightSample * pxPerSample;
            if (sRightX < sLeftX) (sLeftX, sRightX) = (sRightX, sLeftX);

            ds.DrawLine(sLeftX, 0, sLeftX, canvasHeight, Colors.Red);
            ds.DrawLine(sRightX, 0, sRightX, canvasHeight, Colors.Red);

            if (sRightX > sLeftX)
                ds.FillRectangle(sLeftX, 0, sRightX - sLeftX, canvasHeight, Color.FromArgb(60, 0, 120, 215));

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

            //  只在可见范围 / 分辨率 / 数据变化时重建，播放期间直接复用
            if (_editorGeometryDirty || _editorWaveGeometry == null)
            {
                _editorWaveGeometry?.Dispose();
                bool useFill = (_resolution < 8f);
                _editorWaveGeometry = BuildEditorWaveGeometry(sender, canvasWidth, canvasHeight, useFill);
                _editorGeometryDirty = false;
            }

            if (_editorWaveGeometry != null)
            {
                bool useFill = (_resolution < 8f);
                if (useFill)
                    ds.FillGeometry(_editorWaveGeometry, Colors.MediumAquamarine);
                else
                    ds.DrawGeometry(_editorWaveGeometry, Colors.MediumAquamarine, 1f);
            }

            // 覆盖层
            DrawEditorOverlays(ds, canvasWidth, canvasHeight);
        }

        /// <summary>
        /// 按需生成编辑器峰值数组。
        /// 修复：统一使用 total/visible 作为分辨率指标，并使用容差避免浮点比较问题。
        /// </summary>
        private void GenerateEditorPeakArraysIfNeeded()
        {
            if (AudioData == null || Channels <= 0) return;

            var visibleLength = (float)(VisibleRightFrame - VisibleLeftFrame + 1);
            var totalLength = (float)(AudioData.Length / Channels);
            if (visibleLength <= 0 || totalLength <= 0) return;

            var currentResolution = (float)Math.Round(totalLength / visibleLength);

            // 使用容差比较，避免每帧重新生成
            if (_editorPeakArrays != null && Math.Abs(_resolution - currentResolution) < 0.5f)
                return;

            _resolution = currentResolution;
            int blockSize = _resolution switch
            {
                < 2f => 2048,
                < 4f => 1024,
                < 8f => 512,
                _ => 1
            };

            _editorPeakArrays = WaveDataHelper.GeneratePeakArrays(AudioData, Channels, blockSize);
            _editorGeometryDirty = true;
        }

        /// <summary>
        /// 构建编辑器波形的缓存几何体
        /// </summary>
        private CanvasGeometry? BuildEditorWaveGeometry(
            ICanvasResourceCreator creator, float canvasWidth, float canvasHeight, bool useFill)
        {
            if (_editorPeakArrays == null) return null;

            float spacing = 5f;
            float totalSpacing = (Channels - 1) * spacing;
            float availableHeight = canvasHeight - totalSpacing;
            if (availableHeight <= 0) return null;

            float channelHeight = availableHeight / Channels;
            long visibleLength = VisibleRightFrame - VisibleLeftFrame + 1;
            if (visibleLength <= 1) return null;

            int canvasWidthInt = (int)canvasWidth;
            if (canvasWidthInt <= 0) return null;

            using var pathBuilder = new CanvasPathBuilder(creator);

            for (int ch = 0; ch < Channels; ch++)
            {
                float[] peaks = _editorPeakArrays[ch];
                if (peaks.Length < 2) continue;

                float offsetY = ch * (channelHeight + spacing);
                float vCenter = offsetY + channelHeight / 2;
                int totalPairs = peaks.Length / 2;
                float samplesPerPixel = (float)visibleLength / canvasWidthInt;
                long totalFrames = AudioData.Length / Channels;

                if (useFill)
                {
                    var topY = new float[canvasWidthInt];
                    var bottomY = new float[canvasWidthInt];

                    for (int x = 0; x < canvasWidthInt; x++)
                    {
                        long sampleIndex = VisibleLeftFrame + (long)(x * samplesPerPixel);
                        long pairIndex = totalFrames > 0
                            ? sampleIndex * totalPairs / totalFrames
                            : 0;
                        pairIndex = Math.Clamp(pairIndex, 0, totalPairs - 1);

                        float localMin = peaks[pairIndex * 2];
                        float localMax = peaks[pairIndex * 2 + 1];

                        topY[x] = vCenter - localMax * (channelHeight / 2);
                        bottomY[x] = vCenter - localMin * (channelHeight / 2);
                    }

                    pathBuilder.BeginFigure(new Vector2(0, topY[0]));
                    for (int x = 1; x < canvasWidthInt; x++)
                        pathBuilder.AddLine(new Vector2(x, topY[x]));
                    for (int x = canvasWidthInt - 1; x >= 0; x--)
                        pathBuilder.AddLine(new Vector2(x, bottomY[x]));
                    pathBuilder.EndFigure(CanvasFigureLoop.Closed);
                }
                else
                {
                    bool started = false;
                    for (int x = 0; x < canvasWidthInt; x++)
                    {
                        long sampleIndex = VisibleLeftFrame + (long)(x * samplesPerPixel);
                        long pairIndex = totalFrames > 0
                            ? sampleIndex * totalPairs / totalFrames
                            : 0;
                        pairIndex = Math.Clamp(pairIndex, 0, totalPairs - 1);

                        float localMin = peaks[pairIndex * 2];
                        float localMax = peaks[pairIndex * 2 + 1];
                        float avgVal = (localMin + localMax) / 2;
                        float y = vCenter - avgVal * (channelHeight / 2);

                        if (!started)
                        {
                            pathBuilder.BeginFigure(new Vector2(x, y));
                            started = true;
                        }
                        else
                        {
                            pathBuilder.AddLine(new Vector2(x, y));
                        }
                    }
                    if (started)
                        pathBuilder.EndFigure(CanvasFigureLoop.Open);
                }
            }

            return CanvasGeometry.CreatePath(pathBuilder);
        }

        /// <summary>
        /// 绘制编辑器覆盖层（选区 + 播放位置线）
        /// </summary>
        private void DrawEditorOverlays(CanvasDrawingSession ds, float canvasWidth, float canvasHeight)
        {
            long visibleLength = VisibleRightFrame - VisibleLeftFrame + 1;
            if (visibleLength <= 0) return;

            float pxPerSample = canvasWidth / visibleLength;

            float sLeftX = (SelectedLeftSample - VisibleLeftFrame) * pxPerSample;
            float sRightX = (SelectedRightSample - VisibleLeftFrame) * pxPerSample;
            if (sRightX < sLeftX) (sLeftX, sRightX) = (sRightX, sLeftX);

            ds.DrawLine(sLeftX, 0, sLeftX, canvasHeight, Colors.Orange);
            ds.DrawLine(sRightX, 0, sRightX, canvasHeight, Colors.Orange);
            ds.FillRectangle(sLeftX, 0, sRightX - sLeftX, canvasHeight, Color.FromArgb(100, 255, 165, 0));

            float progressX = (PlaybackPositionSample - VisibleLeftFrame) * pxPerSample;
            ds.DrawLine(progressX, 0, progressX, canvasHeight, Colors.Red);
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
            if (!_isSelecting || AudioData == null) return;

            float movedDistance = Math.Abs((float)e.GetCurrentPoint(EditorCanvasControl).Position.X - _editorPointerDownX);
            if (movedDistance > 5f)
            {
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
    }
}
