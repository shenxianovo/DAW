//using Microsoft.Graphics.Canvas.UI.Xaml;
//using Microsoft.UI.Xaml.Input;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace DAW.Controls
//{
//    internal class Class1
//    {
//        #region Events

//        private void CanvasControl_Draw(CanvasControl sender, CanvasDrawEventArgs args)
//        {
//            DrawWave(sender, args);
//            DrawSelectedArea(sender, args);
//        }

//        // 按下鼠标（或手指）时记录起点
//        private void CanvasControl_PointerPressed(object sender, PointerRoutedEventArgs e)
//        {
//            float x = (float)e.GetCurrentPoint(PreviewCanvasControl).Position.X;
//            _selectStartX = x;
//            _selectEndX = x;
//            _isSelecting = true;
//            PreviewCanvasControl.Invalidate();
//        }

//        // 移动时更新终点
//        private void CanvasControl_PointerMoved(object sender, PointerRoutedEventArgs e)
//        {
//            if (!_isSelecting) return;
//            float x = (float)e.GetCurrentPoint(PreviewCanvasControl).Position.X;
//            _selectEndX = x;
//            PreviewCanvasControl.Invalidate();
//        }

//        // 抬起鼠标（或手指）时结束选择
//        private void CanvasControl_PointerReleased(object sender, PointerRoutedEventArgs e)
//        {
//            if (!_isSelecting) return;
//            _isSelecting = false;

//            CalculateSampleRange();

//            PreviewCanvasControl.Invalidate();
//        }

//        #endregion
//    }
//}
