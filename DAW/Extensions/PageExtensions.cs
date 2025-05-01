using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;

namespace DAW.Extensions;

public static class PageExtensions
{
    // 设置期望宽度
    public static void SetDesiredWidth(this Page page, double width)
    {
        page.SetValue(DesiredWidthProperty, width);
    }

    // 获取期望宽度
    public static double GetDesiredWidth(this Page page)
    {
        return (double)page.GetValue(DesiredWidthProperty);
    }

    // 设置期望高度
    public static void SetDesiredHeight(this Page page, double height)
    {
        page.SetValue(DesiredHeightProperty, height);
    }

    // 获取期望高度
    public static double GetDesiredHeight(this Page page)
    {
        return (double)page.GetValue(DesiredHeightProperty);
    }

    // 附加属性：期望宽度
    public static readonly DependencyProperty DesiredWidthProperty =
        DependencyProperty.RegisterAttached(
            "DesiredWidth",
            typeof(double),
            typeof(PageExtensions),
            new PropertyMetadata(0.0)
        );

    // 附加属性：期望高度
    public static readonly DependencyProperty DesiredHeightProperty =
        DependencyProperty.RegisterAttached(
            "DesiredHeight",
            typeof(double),
            typeof(PageExtensions),
            new PropertyMetadata(0.0)
        );
}
