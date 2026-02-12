using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace NowPlaying.Helpers;

/// <summary>
/// テキストが省略される場合、常に左へスクロールし右から出てくる。
/// 1周したら一旦停止し、数秒後に再度スクロール開始。
/// コンテンツは [Text][Text] の重複構造を想定。
/// </summary>
public static class MarqueeScrollBehavior
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(MarqueeScrollBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    /// <summary>1周後の停止時間（秒）</summary>
    public static readonly DependencyProperty PauseSecondsProperty =
        DependencyProperty.RegisterAttached(
            "PauseSeconds",
            typeof(double),
            typeof(MarqueeScrollBehavior),
            new PropertyMetadata(3.0));

    public static double GetPauseSeconds(DependencyObject obj) => (double)obj.GetValue(PauseSecondsProperty);
    public static void SetPauseSeconds(DependencyObject obj, double value) => obj.SetValue(PauseSecondsProperty, value);

    /// <summary>1周分のスクロールアニメーション時間（秒）</summary>
    public static readonly DependencyProperty ScrollDurationSecondsProperty =
        DependencyProperty.RegisterAttached(
            "ScrollDurationSeconds",
            typeof(double),
            typeof(MarqueeScrollBehavior),
            new PropertyMetadata(1.5));

    public static double GetScrollDurationSeconds(DependencyObject obj) => (double)obj.GetValue(ScrollDurationSecondsProperty);
    public static void SetScrollDurationSeconds(DependencyObject obj, double value) => obj.SetValue(ScrollDurationSecondsProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ScrollViewer scrollViewer)
            return;

        scrollViewer.Loaded -= OnScrollViewerLoaded;
        scrollViewer.Unloaded -= OnScrollViewerUnloaded;

        if ((bool)e.NewValue)
        {
            scrollViewer.Loaded += OnScrollViewerLoaded;
            scrollViewer.Unloaded += OnScrollViewerUnloaded;
            if (scrollViewer.IsLoaded)
                StartMarquee(scrollViewer);
        }
        else
        {
            StopMarquee(scrollViewer);
        }
    }

    private static void OnScrollViewerLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is ScrollViewer sv)
            StartMarquee(sv);
    }

    private static void OnScrollViewerUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is ScrollViewer sv)
            StopMarquee(sv);
    }

    private static readonly DependencyProperty MarqueeTimerProperty =
        DependencyProperty.RegisterAttached(
            "MarqueeTimer",
            typeof(DispatcherTimer),
            typeof(MarqueeScrollBehavior));

    private static void StartMarquee(ScrollViewer scrollViewer)
    {
        StopMarquee(scrollViewer);

        var pauseTimer = new DispatcherTimer(DispatcherPriority.Normal, scrollViewer.Dispatcher)
        {
            Interval = TimeSpan.FromSeconds(GetPauseSeconds(scrollViewer))
        };

        pauseTimer.Tick += (_, _) =>
        {
            var extentWidth = scrollViewer.ExtentWidth;
            var viewportWidth = scrollViewer.ViewportWidth;

            if (extentWidth <= viewportWidth) return;

            // [Text][Text] 構造なので 1周 = extentWidth/2
            var loopPoint = extentWidth / 2;
            pauseTimer.Stop();

            // 左方向のみスクロール（0 → loopPoint）
            AnimateScroll(scrollViewer, 0, loopPoint, () =>
            {
                scrollViewer.ScrollToHorizontalOffset(0);
                pauseTimer.Start();
            });
        };

        scrollViewer.SetValue(MarqueeTimerProperty, pauseTimer);
        pauseTimer.Start();
    }

    private static void AnimateScroll(ScrollViewer scrollViewer, double from, double to, Action onComplete)
    {
        var duration = GetScrollDurationSeconds(scrollViewer);
        var startTime = DateTime.Now;

        void Tick(object? s, EventArgs e)
        {
            var elapsed = (DateTime.Now - startTime).TotalSeconds;
            var progress = Math.Min(1.0, elapsed / duration);
            var eased = EaseInOutQuad(progress);
            var offset = from + (to - from) * eased;
            scrollViewer.ScrollToHorizontalOffset(offset);

            if (progress >= 1.0)
            {
                if (s is DispatcherTimer t)
                    t.Stop();
                onComplete();
            }
        }

        var animateTimer = new DispatcherTimer(DispatcherPriority.Render, scrollViewer.Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        animateTimer.Tick += Tick;
        animateTimer.Start();
    }

    private static double EaseInOutQuad(double t)
    {
        return t < 0.5 ? 2 * t * t : 1 - Math.Pow(-2 * t + 2, 2) / 2;
    }

    private static void StopMarquee(ScrollViewer scrollViewer)
    {
        if (scrollViewer.GetValue(MarqueeTimerProperty) is DispatcherTimer timer)
        {
            timer.Stop();
            scrollViewer.ClearValue(MarqueeTimerProperty);
        }
    }
}
