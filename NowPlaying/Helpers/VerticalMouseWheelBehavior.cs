using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace NowPlaying.Helpers;

/// <summary>入れ子のスクロール領域でも、ホイールをページの縦スクロールに渡す。</summary>
public static class VerticalMouseWheelBehavior
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(VerticalMouseWheelBehavior),
        new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
    {
        if (obj is not ScrollViewer viewer)
            return;
        var handler = new MouseWheelEventHandler(OnPreviewMouseWheel);
        if ((bool)args.NewValue)
            viewer.AddHandler(Mouse.PreviewMouseWheelEvent, handler, handledEventsToo: true);
        else
            viewer.RemoveHandler(Mouse.PreviewMouseWheelEvent, handler);
    }

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs args)
    {
        if (args.Delta == 0 || SystemParameters.WheelScrollLines == 0)
            return;

        // NavigationView may provide the actual vertical viewport while this
        // page's ScrollViewer is measured to its content height.
        for (DependencyObject? current = (ScrollViewer)sender; current != null;
             current = VisualTreeHelper.GetParent(current))
        {
            if (current is not ScrollViewer viewer || viewer.ScrollableHeight <= 0 ||
                viewer.VerticalScrollBarVisibility == ScrollBarVisibility.Disabled)
                continue;

            var distance = SystemParameters.WheelScrollLines < 0
                ? viewer.ViewportHeight
                : SystemParameters.WheelScrollLines * 16.0;
            viewer.ScrollToVerticalOffset(viewer.VerticalOffset - args.Delta / 120.0 * distance);
            args.Handled = true;
            return;
        }
    }
}
