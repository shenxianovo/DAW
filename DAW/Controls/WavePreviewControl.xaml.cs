using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
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
        public float[]? AudioData
        {
            get => (float[]?)GetValue(AudioDataProperty);
            set => SetValue(AudioDataProperty, value);
        }

        public static readonly DependencyProperty AudioDataProperty =
            DependencyProperty.Register(nameof(AudioData), typeof(float[]), typeof(WavePreviewControl),
                new PropertyMetadata(null, OnAudioDataChanged));

        private static void OnAudioDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WavePreviewControl control)
            {
                control.CanvasControl.Invalidate(); // 重新触发绘制
            }
        }

        public WavePreviewControl()
        {
            this.InitializeComponent();
        }

        private void CanvasControl_Draw(CanvasControl sender, CanvasDrawEventArgs args)
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
            ds.FillGeometry(geometry, Microsoft.UI.Colors.SkyBlue);

            // 可选：画一道外框线
            //ds.DrawGeometry(geometry, Microsoft.UI.Colors.White);
        }

        private void DrawPeak(CanvasControl sender, CanvasDrawEventArgs args)
        {
            if (AudioData == null || AudioData.Length < 2)
                return;

            var ds = args.DrawingSession;
            float width = (float)sender.ActualWidth;
            float height = (float)sender.ActualHeight;
            float middle = height / 2;

            if (width <= 0) return;

            // 峰值数组有若干对 (minVal, maxVal)
            int totalPairs = AudioData.Length / 2;
            float samplesPerX = (float)totalPairs / width;

            for (int x = 0; x < (int)width; x++)
            {
                int pairIndex = (int)(x * samplesPerX);
                if (pairIndex >= totalPairs) break;

                float minVal = AudioData[pairIndex * 2];
                float maxVal = AudioData[pairIndex * 2 + 1];

                float yMin = middle - minVal * (height / 2);
                float yMax = middle - maxVal * (height / 2);

                // 这里用 SkyBlue 画竖线
                ds.DrawLine(x, yMin, x, yMax, Microsoft.UI.Colors.SkyBlue);
            }
        }

        private void DrawAverage(CanvasControl sender, CanvasDrawEventArgs args)
        {
            if (AudioData == null || AudioData.Length < 2)
                return;

            var ds = args.DrawingSession;
            float width = (float)sender.ActualWidth;
            float height = (float)sender.ActualHeight;
            float middle = height / 2;

            if (width <= 0)
                return;

            int totalPoints = AudioData.Length;
            // 计算每个采样点对应画布 X 坐标
            float xStep = width / (totalPoints - 1);

            // 准备第一个点
            Vector2 prevPoint = new Vector2(0, middle - (AudioData[0] * (height / 2)));

            // 从第 1 个采样点开始，依次连接到前一个点
            for (int i = 1; i < totalPoints; i++)
            {
                float x = i * xStep;
                float amplitudeScale = 10.0f; // 自行调整倍率
                float y = middle - (AudioData[i] * amplitudeScale * (height / 2));
                Vector2 currentPoint = new Vector2(x, y);

                ds.DrawLine(prevPoint, currentPoint, Colors.SkyBlue, 1f);

                prevPoint = currentPoint;
            }
        }
    }
}
