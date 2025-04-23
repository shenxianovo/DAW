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
            if (AudioData == null || AudioData.Length == 0)
                return;

            var ds = args.DrawingSession;
            float width = (float)sender.ActualWidth;
            float height = (float)sender.ActualHeight;
            float middle = height / 2;

            // 如果宽度为 0 或长度不足，直接返回
            if (width <= 0)
                return;

            // 每个像素对应多少个样本
            float samplesPerPixel = (float)AudioData.Length / width;

            // 分段绘制波形最值
            for (int x = 0; x < (int)width; x++)
            {
                // 当前像素对应的样本范围
                int startIndex = (int)(x * samplesPerPixel);
                int endIndex = (int)((x + 1) * samplesPerPixel);
                if (endIndex >= AudioData.Length)
                    endIndex = AudioData.Length - 1;
                if (startIndex >= endIndex)
                    continue;

                // 找到该区段的最小值和最大值
                float minVal = float.MaxValue;
                float maxVal = float.MinValue;
                for (int i = startIndex; i <= endIndex; i++)
                {
                    float sample = AudioData[i];
                    if (sample < minVal) minVal = sample;
                    if (sample > maxVal) maxVal = sample;
                }

                // 将 min/max 映射到画布坐标系
                float yMax = middle - (maxVal * (height / 2));
                float yMin = middle - (minVal * (height / 2));

                // 在 x 像素上画一条从 yMin 到 yMax 的竖线
                ds.DrawLine(x, yMin, x, yMax, Colors.SkyBlue);
            }
        }
    }
}
