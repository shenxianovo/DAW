using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
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
using Microsoft.Graphics.Canvas.Text;
using Microsoft.UI;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Dispatching;

namespace DAW.Controls
{
    public sealed partial class WaveViewControl : UserControl
    {
        #region Constants

        private static readonly float[] DbLevels = { 0, -6, -12, -18, -24, -36, -48 };
        private const long MinVisibleFrames = 100;
        private const float DbFloor = -60f;
        private const float ChannelSpacing = 8f;

        /// <summary>
        /// 将线性振幅映射到 Y 坐标（对数/dB 刻度）。
        /// amplitude 可以为正负，center 为中心线 Y，halfHeight 为一半高度。
        /// </summary>
        private static float AmplitudeToY(float amplitude, float center, float halfHeight)
        {
            if (amplitude == 0f) return center;
            float sign = MathF.Sign(amplitude);
            float absAmp = MathF.Abs(amplitude);
            float db = 20f * MathF.Log10(absAmp);
            if (db <= DbFloor) return center;
            float normalized = 1f - (db / DbFloor); // 1 at 0dB, 0 at floor
            return center - sign * normalized * halfHeight;
        }

        #endregion

        #region Theme Colors

        private static readonly Color ClearColorLight = Color.FromArgb(0xFF, 0xFA, 0xFA, 0xFA);
        private static readonly Color ClearColorDark  = Color.FromArgb(0xFF, 0x20, 0x20, 0x20);

        private Color _gridColor;
        private Color _gridMajorColor;
        private Color _axisTextColor;
        private Color _centerLineColor;

        private void OnActualThemeChanged(FrameworkElement sender, object args)
        {
            ApplyTheme(this.ActualTheme);
            InvalidateAllCanvases();
        }

        private void ApplyTheme(ElementTheme theme)
        {
            bool isDark = theme == ElementTheme.Dark;
            var clearColor = isDark ? ClearColorDark : ClearColorLight;

            PreviewCanvasControl.ClearColor = clearColor;
            EditorCanvasControl.ClearColor = clearColor;
            DbScaleCanvasControl.ClearColor = clearColor;
            TimeAxisCanvasControl.ClearColor = clearColor;

            _gridColor       = isDark ? Color.FromArgb(30, 255, 255, 255) : Color.FromArgb(30, 0, 0, 0);
            _gridMajorColor  = isDark ? Color.FromArgb(60, 255, 255, 255) : Color.FromArgb(60, 0, 0, 0);
            _axisTextColor   = isDark ? Color.FromArgb(160, 255, 255, 255) : Color.FromArgb(160, 0, 0, 0);
            _centerLineColor = isDark ? Color.FromArgb(50, 255, 255, 255) : Color.FromArgb(50, 0, 0, 0);
        }

        private void InvalidateAllCanvases()
        {
            PreviewCanvasControl.Invalidate();
            EditorCanvasControl.Invalidate();
            DbScaleCanvasControl.Invalidate();
            TimeAxisCanvasControl.Invalidate();
        }

        #endregion

        #region Dependency Properties

        public float[]? AudioData
        {
            get => (float[]?)GetValue(AudioDataProperty);
            set => SetValue(AudioDataProperty, value);
        }

        public static readonly DependencyProperty AudioDataProperty =
            DependencyProperty.Register(nameof(AudioData), typeof(float[]),
                typeof(WaveViewControl), new PropertyMetadata(null, OnAudioDataChanged));

        public int Channels
        {
            get => (int)GetValue(ChannelsProperty);
            set => SetValue(ChannelsProperty, value);
        }

        public static readonly DependencyProperty ChannelsProperty =
            DependencyProperty.Register(nameof(Channels), typeof(int),
                typeof(WaveViewControl), new PropertyMetadata(1));

        public int SampleRate
        {
            get => (int)GetValue(SampleRateProperty);
            set => SetValue(SampleRateProperty, value);
        }

        public static readonly DependencyProperty SampleRateProperty =
            DependencyProperty.Register(nameof(SampleRate), typeof(int),
                typeof(WaveViewControl), new PropertyMetadata(44100));

        public long VisibleLeftFrame
        {
            get => (long)GetValue(VisibleLeftFrameProperty);
            set => SetValue(VisibleLeftFrameProperty, value);
        }

        public static readonly DependencyProperty VisibleLeftFrameProperty =
            DependencyProperty.Register(nameof(VisibleLeftFrame), typeof(long),
                typeof(WaveViewControl), new PropertyMetadata(0L, OnVisibleRangeChanged));

        public long VisibleRightFrame
        {
            get => (long)GetValue(VisibleRightFrameProperty);
            set => SetValue(VisibleRightFrameProperty, value);
        }

        public static readonly DependencyProperty VisibleRightFrameProperty =
            DependencyProperty.Register(nameof(VisibleRightFrame), typeof(long),
                typeof(WaveViewControl), new PropertyMetadata(0L, OnVisibleRangeChanged));

        public long SelectedLeftSample
        {
            get => (long)GetValue(SelectedLeftSampleProperty);
            set => SetValue(SelectedLeftSampleProperty, value);
        }

        public static readonly DependencyProperty SelectedLeftSampleProperty =
            DependencyProperty.Register(nameof(SelectedLeftSample), typeof(long),
                typeof(WaveViewControl), new PropertyMetadata(0L, OnOverlayChanged));

        public long SelectedRightSample
        {
            get => (long)GetValue(SelectedRightSampleProperty);
            set => SetValue(SelectedRightSampleProperty, value);
        }

        public static readonly DependencyProperty SelectedRightSampleProperty =
            DependencyProperty.Register(nameof(SelectedRightSample), typeof(long),
                typeof(WaveViewControl), new PropertyMetadata(0L, OnOverlayChanged));

        public long PlaybackPositionSample
        {
            get => (long)GetValue(PlaybackPositionSampleProperty);
            set => SetValue(PlaybackPositionSampleProperty, value);
        }

        public static readonly DependencyProperty PlaybackPositionSampleProperty =
            DependencyProperty.Register(nameof(PlaybackPositionSample), typeof(long),
                typeof(WaveViewControl), new PropertyMetadata(0L, OnPlaybackPositionChanged));

        public bool IsPlaying
        {
            get => (bool)GetValue(IsPlayingProperty);
            set => SetValue(IsPlayingProperty, value);
        }

        public static readonly DependencyProperty IsPlayingProperty =
            DependencyProperty.Register(nameof(IsPlaying), typeof(bool),
                typeof(WaveViewControl), new PropertyMetadata(false, OnIsPlayingChanged));

        #endregion

        #region Property Change Callbacks

        private static void OnAudioDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveViewControl c)
            {
                c._previewPeakArrays = null;
                c._editorPeakArrays = null;
                c._editorPeakBlockSize = 0;
                c._editorPeakRangeStart = 0;
                c._editorPeakRangeEnd = 0;
                c._previewGeometryDirty = true;
                c._editorGeometryDirty = true;
                c.InvalidateAllCanvases();
                c.UpdateStatusBar();
            }
        }

        private static void OnVisibleRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveViewControl c)
            {
                c._editorGeometryDirty = true;
                c.PreviewCanvasControl.Invalidate();
                c.EditorCanvasControl.Invalidate();
                c.TimeAxisCanvasControl.Invalidate();
                c.UpdateStatusBar();
            }
        }

        private static void OnOverlayChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveViewControl c)
            {
                c.PreviewCanvasControl.Invalidate();
                c.EditorCanvasControl.Invalidate();
                c.UpdateStatusBar();
            }
        }

        private static void OnPlaybackPositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveViewControl c)
            {
                c._lastPlaybackSample = (long)e.NewValue;
                c._lastPlaybackUpdateTime = Environment.TickCount64;

                // 自动追随播放位置
                if (c._autoFollowPlayback && c.IsPlaying)
                    c.AutoFollowPlaybackPosition();

                c.PreviewCanvasControl.Invalidate();
                c.EditorCanvasControl.Invalidate();
                c.UpdateStatusBar();
            }
        }

        private static void OnIsPlayingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveViewControl c)
            {
                bool playing = (bool)e.NewValue;
                if (playing)
                {
                    c._autoFollowPlayback = true;
                    c._lastPlaybackSample = c.PlaybackPositionSample;
                    c._lastPlaybackUpdateTime = Environment.TickCount64;
                    c.StartPlaybackTimer();
                }
                else
                {
                    c.StopPlaybackTimer();
                }
            }
        }

        #endregion

        #region Geometry Caching

        private CanvasGeometry? _previewPeakGeometry;
        private CanvasGeometry? _previewRmsGeometry;
        private bool _previewGeometryDirty = true;
        private CanvasGeometry? _editorPeakGeometry;
        private CanvasGeometry? _editorRmsGeometry;
        private bool _editorGeometryDirty = true;

        private void DisposeGeometryCache()
        {
            _previewPeakGeometry?.Dispose();
            _previewPeakGeometry = null;
            _previewRmsGeometry?.Dispose();
            _previewRmsGeometry = null;
            _editorPeakGeometry?.Dispose();
            _editorPeakGeometry = null;
            _editorRmsGeometry?.Dispose();
            _editorRmsGeometry = null;
        }

        #endregion

        #region Playback Timer & Auto-Follow

        private DispatcherTimer? _playbackTimer;
        private long _lastPlaybackSample;
        private long _lastPlaybackUpdateTime;
        private bool _autoFollowPlayback = true;
        private bool _isAutoFollowScrolling;

        private void StartPlaybackTimer()
        {
            if (_playbackTimer != null) return;
            _playbackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _playbackTimer.Tick += OnPlaybackTimerTick;
            _playbackTimer.Start();
        }

        private void StopPlaybackTimer()
        {
            if (_playbackTimer == null) return;
            _playbackTimer.Stop();
            _playbackTimer.Tick -= OnPlaybackTimerTick;
            _playbackTimer = null;
            // 回落到真实位置
            EditorCanvasControl.Invalidate();
            PreviewCanvasControl.Invalidate();
        }

        private void OnPlaybackTimerTick(object? sender, object e)
        {
            EditorCanvasControl.Invalidate();
            PreviewCanvasControl.Invalidate();
        }

        /// <summary>
        /// 计算插值后的播放帧位置，消除定时器更新间隙带来的跳动。
        /// </summary>
        private long GetInterpolatedPlaybackFrame()
        {
            if (!IsPlaying || SampleRate <= 0) return PlaybackPositionSample;
            long elapsed = Environment.TickCount64 - _lastPlaybackUpdateTime;
            if (elapsed < 0 || elapsed > 500) return PlaybackPositionSample; // 防止溢出
            long extraFrames = (long)(SampleRate * (elapsed / 1000.0));
            long totalF = AudioData != null && Channels > 0 ? AudioData.Length / Channels : long.MaxValue;
            return Math.Min(_lastPlaybackSample + extraFrames, totalF - 1);
        }

        /// <summary>
        /// 自动追随播放指示线：当指示线超出可见区域右侧 80% 时，平移至约 20%。
        /// </summary>
        private void AutoFollowPlaybackPosition()
        {
            if (AudioData == null || Channels <= 0) return;
            long visLen = VisibleRightFrame - VisibleLeftFrame + 1;
            if (visLen <= 0) return;
            long totalF = AudioData.Length / Channels;

            long pos = PlaybackPositionSample;
            float ratio = (float)(pos - VisibleLeftFrame) / visLen;
            // 当指针位置超过可视范围 80% 或已经跑出范围
            if (ratio > 0.8f || pos > VisibleRightFrame || pos < VisibleLeftFrame)
            {
                _isAutoFollowScrolling = true;
                long newLeft = pos - (long)(visLen * 0.2);
                long newRight = newLeft + visLen - 1;
                if (newLeft < 0) { newLeft = 0; newRight = visLen - 1; }
                if (newRight >= totalF) { newRight = totalF - 1; newLeft = Math.Max(0, newRight - visLen + 1); }
                VisibleLeftFrame = newLeft;
                VisibleRightFrame = newRight;
                _isAutoFollowScrolling = false;
            }
        }

        /// <summary>
        /// 用户主动操作（缩放/平移/预览拖拽）时打断自动追随。
        /// </summary>
        private void BreakAutoFollow()
        {
            if (_isAutoFollowScrolling) return; // 跳过自动追随引起的范围变化
            _autoFollowPlayback = false;
        }

        #endregion

        #region Constructor

        public WaveViewControl()
        {
            this.InitializeComponent();

            this.ActualThemeChanged += OnActualThemeChanged;
            this.Loaded += (s, e) => ApplyTheme(this.ActualTheme);

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
            DbScaleCanvasControl.SizeChanged += (s, e) => DbScaleCanvasControl.Invalidate();
            TimeAxisCanvasControl.SizeChanged += (s, e) => TimeAxisCanvasControl.Invalidate();

            this.Unloaded += (s, e) =>
            {
                StopPlaybackTimer();
                DisposeGeometryCache();
            };
        }

        #endregion

        #region Wave Preview

        private float[][]? _previewPeakArrays;
        private bool _isDraggingLeft;
        private bool _isDraggingRight;
        private float _dragOffset;
        private bool _isDraggingRange;
        private float _dragRangeStartX;
        private long _panStartLeftSample;
        private long _panStartRightSample;

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

            // 保证最小可拖拽宽度
            const float minRangeWidth = 12f;
            float rangeWidth = vRightX - vLeftX;
            if (rangeWidth < minRangeWidth)
            {
                float center = (vLeftX + vRightX) / 2;
                vLeftX = center - minRangeWidth / 2;
                vRightX = center + minRangeWidth / 2;
            }

            const float grabZone = 6f;
            if (Math.Abs(x - vLeftX) <= grabZone)
            {
                _isDraggingLeft = true;
                _dragOffset = x - vLeftX;
                PreviewCanvasControl.CapturePointer(e.Pointer);
            }
            else if (Math.Abs(x - vRightX) <= grabZone)
            {
                _isDraggingRight = true;
                _dragOffset = x - vRightX;
                PreviewCanvasControl.CapturePointer(e.Pointer);
            }
            else if (x > vLeftX + grabZone && x < vRightX - grabZone)
            {
                _isDraggingRange = true;
                _dragRangeStartX = x;
                _panStartLeftSample = VisibleLeftFrame;
                _panStartRightSample = VisibleRightFrame;
                PreviewCanvasControl.CapturePointer(e.Pointer);
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
                BreakAutoFollow();
                long s = (long)Math.Round((x - _dragOffset) / pxPerSample);
                VisibleLeftFrame = Math.Clamp(s, 0, totalSamples - 1);
            }
            else if (_isDraggingRight)
            {
                BreakAutoFollow();
                long s = (long)Math.Round((x - _dragOffset) / pxPerSample);
                VisibleRightFrame = Math.Clamp(s, 0, totalSamples - 1);
            }
            else if (_isDraggingRange)
            {
                BreakAutoFollow();
                float deltaX = x - _dragRangeStartX;
                long deltaSamples = (long)Math.Round(deltaX / pxPerSample);
                long len = Math.Abs(_panStartRightSample - _panStartLeftSample);
                long newL = _panStartLeftSample + deltaSamples;
                long newR = _panStartRightSample + deltaSamples;
                if (newL < 0) { newL = 0; newR = len; }
                else if (newR > totalSamples - 1) { newR = totalSamples - 1; newL = Math.Max(0, newR - len); }
                VisibleLeftFrame = newL;
                VisibleRightFrame = newR;
            }
        }

        private void OnPreviewCanvasPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            _isDraggingLeft = false;
            _isDraggingRight = false;
            _isDraggingRange = false;
            _dragOffset = 0;
            PreviewCanvasControl.ReleasePointerCapture(e.Pointer);
        }

        private void OnPreviewCanvasPointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            _isDraggingLeft = false;
            _isDraggingRight = false;
            _isDraggingRange = false;
            _dragOffset = 0;
        }

        private void PreviewCanvasControl_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            if (AudioData == null || AudioData.Length < 1 || Channels < 1) return;

            if (_previewPeakArrays == null)
            {
                int spp = Math.Max(1, (int)(AudioData.Length / sender.ActualWidth));
                _previewPeakArrays = WaveDataHelper.GeneratePeakArrays(AudioData, Channels, spp);
                _previewGeometryDirty = true;
            }

            var ds = args.DrawingSession;
            float w = (float)sender.ActualWidth, h = (float)sender.ActualHeight;
            if (w <= 0 || h <= 0) return;

            if (_previewGeometryDirty || _previewPeakGeometry == null)
            {
                _previewPeakGeometry?.Dispose();
                _previewRmsGeometry?.Dispose();
                long totalF = AudioData.Length / Channels;
                _previewPeakGeometry = BuildPreviewPeakGeometry(sender, _previewPeakArrays, w, h);
                _previewRmsGeometry = BuildPreviewRmsGeometry(sender, _previewPeakArrays, w, h);
                _previewGeometryDirty = false;
            }
            if (_previewPeakGeometry != null)
                ds.FillGeometry(_previewPeakGeometry, Color.FromArgb(80, 135, 206, 235));
            if (_previewRmsGeometry != null)
                ds.FillGeometry(_previewRmsGeometry, Color.FromArgb(180, 100, 149, 237));

            PreviewDrawOverlays(ds, w, h);
        }

        private void PreviewDrawOverlays(CanvasDrawingSession ds, float w, float h)
        {
            if (AudioData == null || Channels < 1) return;
            int totalSamples = AudioData.Length / Channels;
            if (totalSamples <= 0) return;
            float px = w / totalSamples;

            float vL = VisibleLeftFrame * px, vR = VisibleRightFrame * px;
            if (vR < vL) (vL, vR) = (vR, vL);

            // 保证最小可视宽度
            const float minVisualWidth = 6f;
            float rangeW = vR - vL;
            if (rangeW < minVisualWidth)
            {
                float c = (vL + vR) / 2;
                vL = c - minVisualWidth / 2;
                vR = c + minVisualWidth / 2;
            }

            ds.DrawLine(vL, 0, vL, h, Colors.Gray);
            ds.DrawLine(vR, 0, vR, h, Colors.Gray);
            ds.FillRectangle(vL, 0, vR - vL, h, Color.FromArgb(30, 0, 0, 0));

            float sL = SelectedLeftSample * px, sR = SelectedRightSample * px;
            if (sR < sL) (sL, sR) = (sR, sL);
            ds.DrawLine(sL, 0, sL, h, Colors.Red);
            ds.DrawLine(sR, 0, sR, h, Colors.Red);
            if (sR > sL)
                ds.FillRectangle(sL, 0, sR - sL, h, Color.FromArgb(60, 0, 120, 215));

            long playFrame = GetInterpolatedPlaybackFrame();
            float pX = playFrame * px;
            ds.DrawLine(pX, 0, pX, h, Colors.Red);
        }

        #endregion

        #region dB Scale

        private static readonly string[] ChannelLabels = { "L", "R", "C", "LFE", "SL", "SR" };

        private void DbScaleCanvasControl_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            var ds = args.DrawingSession;
            float w = (float)sender.ActualWidth, h = (float)sender.ActualHeight;
            if (w <= 0 || h <= 0 || Channels < 1 || AudioData == null) return;

            float available = h - (Channels - 1) * ChannelSpacing;
            if (available <= 0) return;
            float chH = available / Channels;

            using var fmt = new CanvasTextFormat
            {
                FontSize = 9,
                FontFamily = "Segoe UI",
                HorizontalAlignment = CanvasHorizontalAlignment.Right,
                VerticalAlignment = CanvasVerticalAlignment.Center
            };

            using var chLabelFmt = new CanvasTextFormat
            {
                FontSize = 10,
                FontFamily = "Segoe UI",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                HorizontalAlignment = CanvasHorizontalAlignment.Left,
                VerticalAlignment = CanvasVerticalAlignment.Top
            };

            for (int ch = 0; ch < Channels; ch++)
            {
                float offY = ch * (chH + ChannelSpacing);
                float vc = offY + chH / 2;
                float half = chH / 2;
                float minSpacing = 14f;

                // 声道标签
                string chLabel = Channels == 1 ? "M" :
                    (ch < ChannelLabels.Length ? ChannelLabels[ch] : $"Ch{ch + 1}");
                ds.DrawText(chLabel, new Rect(3, offY + 2, w - 6, 14), _axisTextColor, chLabelFmt);

                // 上半部分 dB 标签（从 0dB 向中心）
                float lastDrawnYUpper = float.NegativeInfinity;
                foreach (float db in DbLevels)
                {
                    float normalized = 1f - (db / DbFloor);
                    float y = vc - normalized * half;
                    if (y - lastDrawnYUpper < minSpacing) continue;
                    string label = db == 0 ? " 0" : $"{db:F0}";
                    ds.DrawText(label, new Rect(0, y - 6, w - 3, 12), _axisTextColor, fmt);
                    lastDrawnYUpper = y;
                }
                if (vc - lastDrawnYUpper >= minSpacing)
                    ds.DrawText(" -\u221E", new Rect(0, vc - 6, w - 3, 12), _axisTextColor, fmt);

                // 下半部分 dB 标签（从 0dB 向底部，镜像）
                float lastDrawnYLower = float.PositiveInfinity;
                foreach (float db in DbLevels)
                {
                    float normalized = 1f - (db / DbFloor);
                    float y = vc + normalized * half;
                    if (lastDrawnYLower - y < minSpacing) continue;
                    // 跳过 0dB（已在上方绘制）
                    if (db == 0) { lastDrawnYLower = y; continue; }
                    string label = $"{db:F0}";
                    ds.DrawText(label, new Rect(0, y - 6, w - 3, 12), _axisTextColor, fmt);
                    lastDrawnYLower = y;
                }
            }
        }

        #endregion

        #region Wave Editor

        private bool _isSelecting;
        private float _editorPointerDownX;
        private int _editorPeakBlockSize;
        private long _editorPeakRangeStart;
        private long _editorPeakRangeEnd;
        private float[][]? _editorPeakArrays;
        private bool _editorUseFill;

        private bool _isMiddleButtonPanning;
        private float _editorPanStartX;
        private long _editorPanStartLeft;
        private long _editorPanStartRight;
        private long _hoverFrame = -1;

        private void EditorCanvasControl_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            if (AudioData == null || AudioData.Length < 1 || Channels < 1) return;

            GenerateEditorPeakArraysIfNeeded();

            var ds = args.DrawingSession;
            float w = (float)sender.ActualWidth, h = (float)sender.ActualHeight;
            if (w <= 0 || h <= 0) return;

            DrawEditorGrid(ds, w, h);

            float samplesPerPixel = w > 0 ? (float)(VisibleRightFrame - VisibleLeftFrame + 1) / w : 0;
            bool useFill = samplesPerPixel > 1.5f;

            if (_editorGeometryDirty || _editorPeakGeometry == null || useFill != _editorUseFill)
            {
                _editorPeakGeometry?.Dispose();
                _editorRmsGeometry?.Dispose();
                _editorUseFill = useFill;
                if (useFill)
                {
                    _editorPeakGeometry = BuildEditorFillGeometry(sender, w, h, false);
                    _editorRmsGeometry = BuildEditorFillGeometry(sender, w, h, true);
                }
                else
                {
                    _editorPeakGeometry = BuildEditorLineGeometry(sender, w, h);
                    _editorRmsGeometry = null;
                }
                _editorGeometryDirty = false;
            }

            if (_editorPeakGeometry != null)
            {
                if (_editorUseFill)
                {
                    ds.FillGeometry(_editorPeakGeometry, Color.FromArgb(70, 102, 205, 170));
                    if (_editorRmsGeometry != null)
                        ds.FillGeometry(_editorRmsGeometry, Color.FromArgb(200, 80, 190, 150));
                }
                else
                    ds.DrawGeometry(_editorPeakGeometry, Colors.MediumAquamarine, 1f);
            }

            DrawEditorOverlays(ds, w, h);
        }

        private void GenerateEditorPeakArraysIfNeeded()
        {
            if (AudioData == null || Channels <= 0) return;
            long visLen = VisibleRightFrame - VisibleLeftFrame + 1;
            long totalF = AudioData.Length / Channels;
            if (visLen <= 0 || totalF <= 0) return;

            float canvasW = (float)EditorCanvasControl.ActualWidth;
            if (canvasW <= 0) return;

            // Target ~1 peak per pixel, quantized to power-of-2 for stability
            int targetBlock = Math.Max(1, (int)Math.Ceiling((double)visLen / canvasW));
            int blockSize = 1;
            while (blockSize < targetBlock && blockSize < 8192) blockSize <<= 1;

            // Check if current cache covers the visible range with same block size
            if (_editorPeakArrays != null &&
                _editorPeakBlockSize == blockSize &&
                _editorPeakRangeStart <= VisibleLeftFrame &&
                _editorPeakRangeEnd >= VisibleRightFrame)
                return;

            // Generate for visible range + 100% margin for smooth panning
            long margin = visLen;
            long rangeStart = Math.Max(0, VisibleLeftFrame - margin);
            long rangeEnd = Math.Min(totalF - 1, VisibleRightFrame + margin);

            _editorPeakArrays = WaveDataHelper.GenerateRangePeakArrays(
                AudioData, Channels, blockSize, rangeStart, rangeEnd);
            _editorPeakBlockSize = blockSize;
            _editorPeakRangeStart = rangeStart;
            _editorPeakRangeEnd = rangeEnd;
            _editorGeometryDirty = true;
        }

        private void DrawEditorGrid(CanvasDrawingSession ds, float w, float h)
        {
            if (Channels < 1) return;
            float available = h - (Channels - 1) * ChannelSpacing;
            if (available <= 0) return;
            float chH = available / Channels;

            for (int ch = 0; ch < Channels; ch++)
            {
                float offY = ch * (chH + ChannelSpacing);
                float vc = offY + chH / 2;
                float half = chH / 2;

                // 声道分隔线
                if (ch > 0)
                {
                    float sepY = offY - ChannelSpacing / 2;
                    ds.DrawLine(0, sepY, w, sepY, _gridMajorColor);
                }

                ds.DrawLine(0, vc, w, vc, _centerLineColor, 1.5f);
                foreach (float db in DbLevels)
                {
                    if (db == 0) continue;
                    float normalized = 1f - (db / DbFloor); // log mapping
                    var lc = (db % 12 == 0) ? _gridMajorColor : _gridColor;
                    ds.DrawLine(0, vc - normalized * half, w, vc - normalized * half, lc);
                    ds.DrawLine(0, vc + normalized * half, w, vc + normalized * half, lc);
                }
            }

            if (SampleRate <= 0 || AudioData == null) return;
            long visLen = VisibleRightFrame - VisibleLeftFrame + 1;
            if (visLen <= 0) return;
            double visSec = (double)visLen / SampleRate;
            double major = NiceTickInterval(visSec, 10);
            double minor = major / 5;
            double startT = (double)VisibleLeftFrame / SampleRate;
            double endT = (double)VisibleRightFrame / SampleRate;
            float pps = w / (float)visSec;

            for (double t = Math.Ceiling(startT / minor) * minor; t <= endT; t += minor)
            {
                float x = (float)((t - startT) * pps);
                ds.DrawLine(x, 0, x, h, _gridColor);
            }
            for (double t = Math.Ceiling(startT / major) * major; t <= endT; t += major)
            {
                float x = (float)((t - startT) * pps);
                ds.DrawLine(x, 0, x, h, _gridMajorColor);
            }
        }

        private void DrawEditorOverlays(CanvasDrawingSession ds, float w, float h)
        {
            long visLen = VisibleRightFrame - VisibleLeftFrame + 1;
            if (visLen <= 0) return;
            float px = w / visLen;

            float sL = (SelectedLeftSample - VisibleLeftFrame) * px;
            float sR = (SelectedRightSample - VisibleLeftFrame) * px;
            if (sR < sL) (sL, sR) = (sR, sL);
            ds.DrawLine(sL, 0, sL, h, Colors.Orange);
            ds.DrawLine(sR, 0, sR, h, Colors.Orange);
            ds.FillRectangle(sL, 0, sR - sL, h, Color.FromArgb(100, 255, 165, 0));

            long playFrame = GetInterpolatedPlaybackFrame();
            float pX = (playFrame - VisibleLeftFrame) * px;
            ds.DrawLine(pX, 0, pX, h, Colors.Red);

            if (_hoverFrame >= 0)
            {
                float hX = (_hoverFrame - VisibleLeftFrame) * px;
                ds.DrawLine(hX, 0, hX, h, Color.FromArgb(80, 200, 200, 200));
            }
        }

        #region Editor Pointer Events

        private void OnEditorCanvasPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (AudioData == null || AudioData.Length < 1 || Channels < 1) return;
            var point = e.GetCurrentPoint(EditorCanvasControl);

            if (point.Properties.IsMiddleButtonPressed)
            {
                _isMiddleButtonPanning = true;
                _editorPanStartX = (float)point.Position.X;
                _editorPanStartLeft = VisibleLeftFrame;
                _editorPanStartRight = VisibleRightFrame;
                return;
            }

            if (point.Properties.IsLeftButtonPressed)
            {
                _isSelecting = true;
                _editorPointerDownX = (float)point.Position.X;
            }
        }

        private void OnEditorCanvasPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (AudioData == null || Channels <= 0) return;

            var point = e.GetCurrentPoint(EditorCanvasControl);
            UpdateHoverFrame(point.Position);

            if (_isMiddleButtonPanning)
            {
                BreakAutoFollow();
                float dx = (float)point.Position.X - _editorPanStartX;
                float cw = (float)EditorCanvasControl.ActualWidth;
                if (cw <= 0) return;
                long visLen = _editorPanStartRight - _editorPanStartLeft + 1;
                long delta = -(long)(dx * visLen / cw);
                long totalF = AudioData.Length / Channels;
                long newL = _editorPanStartLeft + delta;
                long newR = _editorPanStartRight + delta;
                if (newL < 0) { newL = 0; newR = Math.Min(visLen - 1, totalF - 1); }
                if (newR >= totalF) { newR = totalF - 1; newL = Math.Max(0, newR - visLen + 1); }
                VisibleLeftFrame = newL;
                VisibleRightFrame = newR;
                return;
            }

            if (!_isSelecting) return;

            float movedDist = Math.Abs((float)point.Position.X - _editorPointerDownX);
            if (movedDist > 5f)
            {
                float canvasW = (float)EditorCanvasControl.ActualWidth;
                long vl = VisibleRightFrame - VisibleLeftFrame + 1;
                if (canvasW <= 0 || vl <= 0) return;
                float x = (float)point.Position.X;
                float startX = Math.Min(_editorPointerDownX, x);
                float endX = Math.Max(_editorPointerDownX, x);
                float pxps = canvasW / vl;
                long ns = (long)(startX / pxps) + VisibleLeftFrame;
                long ne = (long)(endX / pxps) + VisibleLeftFrame;
                SelectedLeftSample = Math.Clamp(ns, VisibleLeftFrame, VisibleRightFrame);
                SelectedRightSample = Math.Clamp(ne, VisibleLeftFrame, VisibleRightFrame);
            }
        }

        private void OnEditorCanvasPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_isMiddleButtonPanning)
            {
                _isMiddleButtonPanning = false;
                return;
            }

            if (AudioData != null && Channels > 0)
            {
                float movedDist = Math.Abs((float)e.GetCurrentPoint(EditorCanvasControl).Position.X - _editorPointerDownX);
                if (movedDist <= 5f)
                {
                    float cw = (float)EditorCanvasControl.ActualWidth;
                    long vl = VisibleRightFrame - VisibleLeftFrame + 1;
                    if (cw > 0 && vl > 0)
                    {
                        float x = (float)e.GetCurrentPoint(EditorCanvasControl).Position.X;
                        long np = (long)(x / (cw / vl)) + VisibleLeftFrame;
                        np = Math.Clamp(np, VisibleLeftFrame, VisibleRightFrame);

                        // 有框选时：点击框选区域外仅取消框选，不移动播放位置
                        if (SelectedRightSample > SelectedLeftSample)
                        {
                            if (np < SelectedLeftSample || np > SelectedRightSample)
                            {
                                SelectedLeftSample = 0;
                                SelectedRightSample = 0;
                            }
                        }
                        else
                        {
                            // 无框选时，点击移动播放位置
                            PlaybackPositionSample = np;
                        }
                    }
                }
            }
            _isSelecting = false;
        }

        private void OnEditorCanvasPointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            if (AudioData == null || Channels <= 0) return;

            var point = e.GetCurrentPoint(EditorCanvasControl);
            float cw = (float)EditorCanvasControl.ActualWidth;
            if (cw <= 0) return;

            int delta = point.Properties.MouseWheelDelta;
            long totalF = AudioData.Length / Channels;
            long curVis = VisibleRightFrame - VisibleLeftFrame + 1;
            bool isShift = (e.KeyModifiers & Windows.System.VirtualKeyModifiers.Shift) != 0;

            if (isShift)
            {
                // Shift+滚轮 = 平移：打断追随
                BreakAutoFollow();
                long pan = Math.Max(1, curVis / 10);
                if (delta > 0) pan = -pan;
                long nL = VisibleLeftFrame + pan, nR = VisibleRightFrame + pan;
                if (nL < 0) { nL = 0; nR = Math.Min(curVis - 1, totalF - 1); }
                if (nR >= totalF) { nR = totalF - 1; nL = Math.Max(0, nR - curVis + 1); }
                VisibleLeftFrame = nL;
                VisibleRightFrame = nR;
            }
            else
            {
                float rel = Math.Clamp((float)point.Position.X / cw, 0f, 1f);
                float zf = delta > 0 ? 0.8f : 1.25f;
                long newVis = Math.Clamp((long)(curVis * zf), MinVisibleFrames, totalF);
                long mouseF = VisibleLeftFrame + (long)(rel * curVis);
                long nL = mouseF - (long)(rel * newVis);
                long nR = nL + newVis - 1;
                if (nL < 0) { nL = 0; nR = newVis - 1; }
                if (nR >= totalF) { nR = totalF - 1; nL = Math.Max(0, nR - newVis + 1); }
                VisibleLeftFrame = nL;
                VisibleRightFrame = nR;
            }

            e.Handled = true;
        }

        private void OnEditorCanvasPointerExited(object sender, PointerRoutedEventArgs e)
        {
            _hoverFrame = -1;
            EditorCanvasControl.Invalidate();
            UpdateStatusBar();
        }

        #endregion

        #endregion

        #region Time Axis

        private void TimeAxisCanvasControl_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            if (AudioData == null || SampleRate <= 0 || Channels <= 0) return;
            var ds = args.DrawingSession;
            float w = (float)sender.ActualWidth, h = (float)sender.ActualHeight;
            if (w <= 0 || h <= 0) return;

            long visLen = VisibleRightFrame - VisibleLeftFrame + 1;
            if (visLen <= 0) return;
            double visSec = (double)visLen / SampleRate;
            double startT = (double)VisibleLeftFrame / SampleRate;
            double endT = (double)VisibleRightFrame / SampleRate;
            float pps = w / (float)visSec;

            double major = NiceTickInterval(visSec, 10);
            double minor = major / 5;

            using var fmt = new CanvasTextFormat
            {
                FontSize = 10,
                FontFamily = "Segoe UI",
                HorizontalAlignment = CanvasHorizontalAlignment.Center,
                VerticalAlignment = CanvasVerticalAlignment.Top
            };

            for (double t = Math.Ceiling(startT / minor) * minor; t <= endT; t += minor)
            {
                float x = (float)((t - startT) * pps);
                ds.DrawLine(x, 0, x, h * 0.3f, _axisTextColor);
            }
            for (double t = Math.Ceiling(startT / major) * major; t <= endT; t += major)
            {
                float x = (float)((t - startT) * pps);
                ds.DrawLine(x, 0, x, h * 0.5f, _axisTextColor);
                ds.DrawText(FormatTime(t), new Rect(x - 40, h * 0.45f, 80, h * 0.55f), _axisTextColor, fmt);
            }
        }

        #endregion

        #region Status Bar

        private void UpdateHoverFrame(Point pos)
        {
            if (AudioData == null || Channels <= 0 || SampleRate <= 0) return;
            float cw = (float)EditorCanvasControl.ActualWidth;
            long visLen = VisibleRightFrame - VisibleLeftFrame + 1;
            if (cw <= 0 || visLen <= 0) return;

            long frame = (long)(pos.X / cw * visLen) + VisibleLeftFrame;
            long totalF = AudioData.Length / Channels;
            _hoverFrame = Math.Clamp(frame, 0, Math.Max(0, totalF - 1));
            EditorCanvasControl.Invalidate();
            UpdateStatusBar();
        }

        private void UpdateStatusBar()
        {
            if (AudioData == null || Channels <= 0 || SampleRate <= 0)
            {
                CursorInfoText.Text = "";
                return;
            }

            var sb = new StringBuilder();

            if (_hoverFrame >= 0)
            {
                double t = (double)_hoverFrame / SampleRate;
                sb.Append("Cursor: ").Append(FormatTime(t)).Append(" (S:").Append(_hoverFrame).Append(')');
            }

            double pt = (double)GetInterpolatedPlaybackFrame() / SampleRate;
            if (sb.Length > 0) sb.Append("  \u2502  ");
            sb.Append("Play: ").Append(FormatTime(pt));

            if (SelectedRightSample > SelectedLeftSample)
            {
                double s1 = (double)SelectedLeftSample / SampleRate;
                double s2 = (double)SelectedRightSample / SampleRate;
                sb.Append("  \u2502  Sel: ").Append(FormatTime(s1)).Append(" \u2014 ").Append(FormatTime(s2))
                  .Append(" (").Append($"{(s2 - s1):F3}").Append("s)");
            }

            CursorInfoText.Text = sb.ToString();
        }

        #endregion

        #region Geometry Builders

        private CanvasGeometry? BuildPreviewPeakGeometry(ICanvasResourceCreator creator, float[][] peakArrays,
            float canvasW, float canvasH)
        {
            if (peakArrays == null) return null;
            float available = canvasH - (Channels - 1) * ChannelSpacing;
            if (available <= 0) return null;
            float chH = available / Channels;
            int cwInt = (int)canvasW;
            if (cwInt <= 0) return null;
            int stride = WaveDataHelper.Stride;

            using var pb = new CanvasPathBuilder(creator);
            for (int ch = 0; ch < Channels; ch++)
            {
                float[] data = peakArrays[ch];
                if (data.Length < stride) continue;
                float offY = ch * (chH + ChannelSpacing);
                float vc = offY + chH / 2;
                float half = chH / 2;
                int totalBlocks = data.Length / stride;
                float bpp = (float)totalBlocks / cwInt;

                var topY = new float[cwInt];
                var botY = new float[cwInt];
                for (int x = 0; x < cwInt; x++)
                {
                    int s = (int)(x * bpp), e = Math.Min((int)((x + 1) * bpp), totalBlocks - 1);
                    float mn = float.MaxValue, mx = float.MinValue;
                    for (int i = s; i <= e; i++)
                    {
                        float lo = data[i * stride], hi = data[i * stride + 1];
                        if (lo < mn) mn = lo;
                        if (hi > mx) mx = hi;
                    }
                    if (mn == float.MaxValue) { mn = 0; mx = 0; }
                    topY[x] = AmplitudeToY(mx, vc, half);
                    botY[x] = AmplitudeToY(mn, vc, half);
                }

                pb.BeginFigure(new Vector2(0, topY[0]));
                for (int x = 1; x < cwInt; x++) pb.AddLine(new Vector2(x, topY[x]));
                for (int x = cwInt - 1; x >= 0; x--) pb.AddLine(new Vector2(x, botY[x]));
                pb.EndFigure(CanvasFigureLoop.Closed);
            }
            return CanvasGeometry.CreatePath(pb);
        }

        private CanvasGeometry? BuildPreviewRmsGeometry(ICanvasResourceCreator creator, float[][] peakArrays,
            float canvasW, float canvasH)
        {
            if (peakArrays == null) return null;
            float available = canvasH - (Channels - 1) * ChannelSpacing;
            if (available <= 0) return null;
            float chH = available / Channels;
            int cwInt = (int)canvasW;
            if (cwInt <= 0) return null;
            int stride = WaveDataHelper.Stride;

            using var pb = new CanvasPathBuilder(creator);
            for (int ch = 0; ch < Channels; ch++)
            {
                float[] data = peakArrays[ch];
                if (data.Length < stride) continue;
                float offY = ch * (chH + ChannelSpacing);
                float vc = offY + chH / 2;
                float half = chH / 2;
                int totalBlocks = data.Length / stride;
                float bpp = (float)totalBlocks / cwInt;

                var topY = new float[cwInt];
                var botY = new float[cwInt];
                for (int x = 0; x < cwInt; x++)
                {
                    int s = (int)(x * bpp), e = Math.Min((int)((x + 1) * bpp), totalBlocks - 1);
                    double sumSq = 0; int cnt = 0;
                    for (int i = s; i <= e; i++)
                    {
                        float rms = data[i * stride + 2];
                        sumSq += rms * rms;
                        cnt++;
                    }
                    float avgRms = cnt > 0 ? MathF.Sqrt((float)(sumSq / cnt)) : 0;
                    topY[x] = AmplitudeToY(avgRms, vc, half);
                    botY[x] = AmplitudeToY(-avgRms, vc, half);
                }

                pb.BeginFigure(new Vector2(0, topY[0]));
                for (int x = 1; x < cwInt; x++) pb.AddLine(new Vector2(x, topY[x]));
                for (int x = cwInt - 1; x >= 0; x--) pb.AddLine(new Vector2(x, botY[x]));
                pb.EndFigure(CanvasFigureLoop.Closed);
            }
            return CanvasGeometry.CreatePath(pb);
        }

        /// <summary>
        /// 编辑区 Fill 几何。useRms=false 画 Peak 包络，useRms=true 画 RMS 包络。
        /// </summary>
        private CanvasGeometry? BuildEditorFillGeometry(ICanvasResourceCreator creator, float canvasW, float canvasH, bool useRms)
        {
            if (_editorPeakArrays == null || AudioData == null || _editorPeakBlockSize <= 0) return null;
            float available = canvasH - (Channels - 1) * ChannelSpacing;
            if (available <= 0) return null;
            float chH = available / Channels;
            long visLen = VisibleRightFrame - VisibleLeftFrame + 1;
            if (visLen <= 1) return null;
            int cwInt = (int)canvasW;
            if (cwInt <= 0) return null;
            int stride = WaveDataHelper.Stride;

            float spp = (float)visLen / cwInt;
            using var pb = new CanvasPathBuilder(creator);
            for (int ch = 0; ch < Channels; ch++)
            {
                float[] data = _editorPeakArrays[ch];
                if (data.Length < stride) continue;
                float offY = ch * (chH + ChannelSpacing);
                float vc = offY + chH / 2;
                float half = chH / 2;
                int totalBlocks = data.Length / stride;

                var topY = new float[cwInt];
                var botY = new float[cwInt];
                for (int x = 0; x < cwInt; x++)
                {
                    long si0 = VisibleLeftFrame + (long)(x * spp);
                    long si1 = VisibleLeftFrame + (long)((x + 1) * spp);
                    long pi0 = (si0 - _editorPeakRangeStart) / _editorPeakBlockSize;
                    long pi1 = (si1 - _editorPeakRangeStart) / _editorPeakBlockSize;
                    pi0 = Math.Clamp(pi0, 0, totalBlocks - 1);
                    pi1 = Math.Clamp(pi1, 0, totalBlocks - 1);

                    if (useRms)
                    {
                        double sumSq = 0; int cnt = 0;
                        for (long p = pi0; p <= pi1; p++)
                        {
                            float rms = data[p * stride + 2];
                            sumSq += rms * rms;
                            cnt++;
                        }
                        float avgRms = cnt > 0 ? MathF.Sqrt((float)(sumSq / cnt)) : 0;
                        topY[x] = AmplitudeToY(avgRms, vc, half);
                        botY[x] = AmplitudeToY(-avgRms, vc, half);
                    }
                    else
                    {
                        float mn = float.MaxValue, mx = float.MinValue;
                        for (long p = pi0; p <= pi1; p++)
                        {
                            float lo = data[p * stride], hi = data[p * stride + 1];
                            if (lo < mn) mn = lo;
                            if (hi > mx) mx = hi;
                        }
                        if (mn == float.MaxValue) { mn = 0; mx = 0; }
                        topY[x] = AmplitudeToY(mx, vc, half);
                        botY[x] = AmplitudeToY(mn, vc, half);
                    }
                }

                pb.BeginFigure(new Vector2(0, topY[0]));
                for (int x = 1; x < cwInt; x++) pb.AddLine(new Vector2(x, topY[x]));
                for (int x = cwInt - 1; x >= 0; x--) pb.AddLine(new Vector2(x, botY[x]));
                pb.EndFigure(CanvasFigureLoop.Closed);
            }
            return CanvasGeometry.CreatePath(pb);
        }

        private CanvasGeometry? BuildEditorLineGeometry(ICanvasResourceCreator creator, float canvasW, float canvasH)
        {
            if (_editorPeakArrays == null || AudioData == null || _editorPeakBlockSize <= 0) return null;
            float available = canvasH - (Channels - 1) * ChannelSpacing;
            if (available <= 0) return null;
            float chH = available / Channels;
            long visLen = VisibleRightFrame - VisibleLeftFrame + 1;
            if (visLen <= 1) return null;
            int cwInt = (int)canvasW;
            if (cwInt <= 0) return null;
            int stride = WaveDataHelper.Stride;

            using var pb = new CanvasPathBuilder(creator);
            for (int ch = 0; ch < Channels; ch++)
            {
                float[] data = _editorPeakArrays[ch];
                if (data.Length < stride) continue;
                float offY = ch * (chH + ChannelSpacing);
                float vc = offY + chH / 2;
                float half = chH / 2;
                int totalBlocks = data.Length / stride;
                float spp = (float)visLen / cwInt;

                bool started = false;
                for (int x = 0; x < cwInt; x++)
                {
                    long si = VisibleLeftFrame + (long)(x * spp);
                    long pi = (si - _editorPeakRangeStart) / _editorPeakBlockSize;
                    pi = Math.Clamp(pi, 0, totalBlocks - 1);
                    float avg = (data[pi * stride] + data[pi * stride + 1]) / 2;
                    float y = AmplitudeToY(avg, vc, half);
                    if (!started) { pb.BeginFigure(new Vector2(x, y)); started = true; }
                    else pb.AddLine(new Vector2(x, y));
                }
                if (started) pb.EndFigure(CanvasFigureLoop.Open);
            }
            return CanvasGeometry.CreatePath(pb);
        }

        #endregion

        #region Helpers

        private static string FormatTime(double seconds)
        {
            if (seconds < 0) seconds = 0;
            int ts = (int)seconds;
            int ms = (int)((seconds - ts) * 1000);
            int h = ts / 3600, m = (ts % 3600) / 60, s = ts % 60;
            return h > 0 ? $"{h}:{m:D2}:{s:D2}.{ms:D3}" : $"{m}:{s:D2}.{ms:D3}";
        }

        private static double NiceTickInterval(double range, int maxTicks)
        {
            if (range <= 0) return 1;
            double raw = range / maxTicks;
            double mag = Math.Pow(10, Math.Floor(Math.Log10(raw)));
            double norm = raw / mag;
            double nice = norm <= 1 ? 1 : norm <= 2 ? 2 : norm <= 5 ? 5 : 10;
            return nice * mag;
        }

        #endregion
    }
}