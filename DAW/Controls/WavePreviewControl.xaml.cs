using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
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
    public sealed partial class WavePreviewControl : UserControl
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
                typeof(WavePreviewControl),
                new PropertyMetadata(null, OnAudioDataChanged));

        // 可绑定的起始采样索引
        public int StartSampleIndex
        {
            get => (int)GetValue(StartSampleIndexProperty);
            set => SetValue(StartSampleIndexProperty, value);
        }

        public static readonly DependencyProperty StartSampleIndexProperty =
            DependencyProperty.Register(
                nameof(StartSampleIndex),
                typeof(int),
                typeof(WavePreviewControl),
                new PropertyMetadata(0));

        // 可绑定的结束采样索引
        public int EndSampleIndex
        {
            get => (int)GetValue(EndSampleIndexProperty);
            set => SetValue(EndSampleIndexProperty, value);
        }

        public static readonly DependencyProperty EndSampleIndexProperty =
            DependencyProperty.Register(
                nameof(EndSampleIndex),
                typeof(int),
                typeof(WavePreviewControl),
                new PropertyMetadata(0));

        private static void OnAudioDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WavePreviewControl control)
            {
                control.CanvasControl.Invalidate();
            }
        }

        #endregion

        #region Private Fields

        // 标记当前是否在拖拽
        private float _selectStartX;
        private float _selectEndX;
        private bool _isSelecting;

        #endregion

        public WavePreviewControl()
        {
            this.InitializeComponent();
        }

        private void CanvasControl_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            DrawWave(sender, args);
            DrawSelectedArea(sender, args);
        }

        private void DrawWave(CanvasControl sender, CanvasDrawEventArgs args)
        {
            if (AudioData == null || AudioData.Length < 2)
                return;

            // AudioData 假设存放的是 [min, max, min, max ...]
            // 每两个值为一对峰值
            var ds = args.DrawingSession;
            float width = (float)sender.ActualWidth;
            float height = (float)sender.ActualHeight;
            float middle = height / 2;
            if (width <= 0) return;

            int totalPairs = AudioData.Length / 2;
            float samplesPerPixel = (float)totalPairs / width;

            using var pathBuilder = new CanvasPathBuilder(ds);

            // 先收集在顶部 (max) 的点
            var topPoints = new List<Vector2>();
            // 再收集底部 (min) 的点
            var bottomPoints = new List<Vector2>();

            for (int x = 0; x < (int)width; x++)
            {
                int pairIndex = (int)(x * samplesPerPixel);
                if (pairIndex >= totalPairs) break;

                float minVal = AudioData[pairIndex * 2];
                float maxVal = AudioData[pairIndex * 2 + 1];

                float yMin = middle - (minVal * (height / 2));
                float yMax = middle - (maxVal * (height / 2));

                topPoints.Add(new Vector2(x, yMax));
                bottomPoints.Add(new Vector2(x, yMin));
            }

            // 在 PathBuilder 开始绘图
            // 1) 顶边，从左到右
            if (topPoints.Count > 0)
            {
                pathBuilder.BeginFigure(topPoints[0]);
                for (int i = 1; i < topPoints.Count; i++)
                {
                    pathBuilder.AddLine(topPoints[i]);
                }
                // 2) 从右到左走底边
                for (int i = bottomPoints.Count - 1; i >= 0; i--)
                {
                    pathBuilder.AddLine(bottomPoints[i]);
                }
                pathBuilder.EndFigure(CanvasFigureLoop.Closed);
            }

            // 构造几何并填充
            using var geometry = CanvasGeometry.CreatePath(pathBuilder);
            ds.FillGeometry(geometry, Colors.SkyBlue);

            // 可选：画一道外框线
            //ds.DrawGeometry(geometry, Microsoft.UI.Colors.White);
        }

        private void DrawSelectedArea(CanvasControl sender, CanvasDrawEventArgs args)
        {
            if (_isSelecting || IsValidSelection())
            {
                var ds = args.DrawingSession;
                float left = Math.Min(_selectStartX, _selectEndX);
                float right = Math.Max(_selectStartX, _selectEndX);
                float height = (float)sender.ActualHeight;

                ds.FillRectangle(left, 0, right - left, height, Color.FromArgb(60, 0, 120, 215));
                ds.DrawRectangle(left, 0, right - left, height, Color.FromArgb(255, 0, 120, 215));
            }
        }

        // 按下鼠标（或手指）时记录起点
        private void CanvasControl_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            float x = (float)e.GetCurrentPoint(CanvasControl).Position.X;
            _selectStartX = x;
            _selectEndX = x;
            _isSelecting = true;
            CanvasControl.Invalidate();
        }

        // 移动时更新终点
        private void CanvasControl_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_isSelecting) return;
            float x = (float)e.GetCurrentPoint(CanvasControl).Position.X;
            _selectEndX = x;
            CanvasControl.Invalidate();
        }

        // 抬起鼠标（或手指）时结束选择
        private void CanvasControl_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (!_isSelecting) return;
            _isSelecting = false;

            CalculateSampleRange();

            CanvasControl.Invalidate();
        }

        private bool IsValidSelection() => Math.Abs(_selectEndX - _selectStartX) > 2;
        private void CalculateSampleRange()
        {
            if (AudioData == null || AudioData.Length < 2) return;
            float canvasWidth = (float)CanvasControl.ActualWidth;
            if (canvasWidth <= 0) return;

            int totalPairs = AudioData.Length / 2;
            float samplesPerPixel = totalPairs / canvasWidth;
            float left = Math.Min(_selectStartX, _selectEndX);
            float right = Math.Max(_selectStartX, _selectEndX);

            int start = (int)(left * samplesPerPixel);
            int end = (int)(right * samplesPerPixel);
            // 确保在范围内并更新依赖属性
            StartSampleIndex = Math.Clamp(start, 0, totalPairs - 1);
            EndSampleIndex = Math.Clamp(end, 0, totalPairs - 1);
        }
    }
}
