using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace NowPlaying.Helpers;

/// <summary>
/// 起動時はタイトル・曲情報を表示して開始。左へスクロール→完全に消える→余白→右から同時に表示→元の位置で停止、数秒後に再開。
/// タイトルと曲情報は文字数次第でそれぞれ独立して1周完了。右から表示されるタイミングは同期。
/// コンテンツは [Text][Gap][Text] の構造を想定。
/// </summary>
public static class MarqueeScrollGroupBehavior
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(MarqueeScrollGroupBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    /// <summary>1周後の停止時間（秒）- 起動時もこの時間待ってからスクロール開始</summary>
    public static readonly DependencyProperty PauseSecondsProperty =
        DependencyProperty.RegisterAttached(
            "PauseSeconds",
            typeof(double),
            typeof(MarqueeScrollGroupBehavior),
            new PropertyMetadata(3.0));

    public static double GetPauseSeconds(DependencyObject obj) => (double)obj.GetValue(PauseSecondsProperty);
    public static void SetPauseSeconds(DependencyObject obj, double value) => obj.SetValue(PauseSecondsProperty, value);

    /// <summary>右から表示されるまでの余白幅（ピクセル）</summary>
    public static readonly DependencyProperty GapWidthProperty =
        DependencyProperty.RegisterAttached(
            "GapWidth",
            typeof(double),
            typeof(MarqueeScrollGroupBehavior),
            new PropertyMetadata(120.0));

    public static double GetGapWidth(DependencyObject obj) => (double)obj.GetValue(GapWidthProperty);
    public static void SetGapWidth(DependencyObject obj, double value) => obj.SetValue(GapWidthProperty, value);

    /// <summary>スクロール速度（ピクセル/秒）</summary>
    public static readonly DependencyProperty ScrollSpeedProperty =
        DependencyProperty.RegisterAttached(
            "ScrollSpeed",
            typeof(double),
            typeof(MarqueeScrollGroupBehavior),
            new PropertyMetadata(40.0));

    public static double GetScrollSpeed(DependencyObject obj) => (double)obj.GetValue(ScrollSpeedProperty);
    public static void SetScrollSpeed(DependencyObject obj, double value) => obj.SetValue(ScrollSpeedProperty, value);

    /// <summary>スクロールが必要か（テキストがビューポートを超える場合true）</summary>
    public static readonly DependencyProperty NeedsScrollingProperty =
        DependencyProperty.RegisterAttached(
            "NeedsScrolling",
            typeof(bool),
            typeof(MarqueeScrollGroupBehavior),
            new PropertyMetadata(false));

    public static bool GetNeedsScrolling(DependencyObject obj) => (bool)obj.GetValue(NeedsScrollingProperty);
    public static void SetNeedsScrolling(DependencyObject obj, bool value) => obj.SetValue(NeedsScrollingProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element)
            return;

        element.Loaded -= OnElementLoaded;
        element.Unloaded -= OnElementUnloaded;

        if ((bool)e.NewValue)
        {
            element.Loaded += OnElementLoaded;
            element.Unloaded += OnElementUnloaded;
            if (element.IsLoaded)
                StartMarquee(element);
        }
        else
        {
            StopMarquee(element);
        }
    }

    private static void OnElementLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el)
            StartMarquee(el);
    }

    private static void OnElementUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el)
            StopMarquee(el);
    }

    private static readonly DependencyProperty MarqueeTimerProperty =
        DependencyProperty.RegisterAttached(
            "MarqueeTimer",
            typeof(DispatcherTimer),
            typeof(MarqueeScrollGroupBehavior));

    private static ScrollViewer[] GetScrollViewers(FrameworkElement parent)
    {
        var list = new List<ScrollViewer>();
        foreach (var child in GetVisualChildren(parent))
        {
            if (child is ScrollViewer sv)
                list.Add(sv);
        }
        return list.ToArray();
    }

    private static IEnumerable<DependencyObject> GetVisualChildren(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
        }
    }

    private static void StartMarquee(FrameworkElement element)
    {
        StopMarquee(element);

        var pauseSeconds = GetPauseSeconds(element);
        var gapWidth = GetGapWidth(element);
        var speed = GetScrollSpeed(element);

        // 各ScrollViewer: (IsPaused, Timestamp, LoopPoint, DelayStart)
        // DelayStart = 右から表示を同時にするため、短い方の開始を遅らせる秒数
        var states = new Dictionary<ScrollViewer, (bool IsPaused, double Timestamp, double LoopPoint, double DelayStart)>();

        void UpdateAll(object? s, EventArgs e)
        {
            var now = DateTime.Now.Ticks / (double)TimeSpan.TicksPerSecond;

            var allScrollViewers = GetScrollViewers(element).ToArray();

            var scrollViewers = allScrollViewers
                .Where(GetNeedsScrolling)
                .ToArray();

            var needsInit = scrollViewers.Where(sv => !states.ContainsKey(sv)).ToArray();
            var isScrolling = scrollViewers.Any(sv => states.TryGetValue(sv, out var st) && !st.IsPaused);

            if (!isScrolling)
            {
                // スクロール中でないときのみ NeedsScrolling を再評価（スクロール中の誤切替を防ぐ）
                foreach (var sv in allScrollViewers)
                {
                    var extentWidth = sv.ExtentWidth;
                    var viewportWidth = sv.ViewportWidth;
                    var isMarquee = GetNeedsScrolling(sv);
                    var textWidth = isMarquee
                        ? Math.Max(0, (extentWidth - gapWidth) / 2)
                        : extentWidth;
                    SetNeedsScrolling(sv, textWidth > viewportWidth);
                }

                scrollViewers = allScrollViewers.Where(GetNeedsScrolling).ToArray();
            }

            if (scrollViewers.Length == 0)
            {
                foreach (var sv in allScrollViewers.Where(sv => !GetNeedsScrolling(sv)))
                    states.Remove(sv);
                return;
            }

            if (needsInit.Length > 0)
            {
                var vpWidth = scrollViewers[0].ViewportWidth;
                var items = scrollViewers.Select(sv =>
                {
                    var loopPoint = (sv.ExtentWidth + gapWidth) / 2;
                    var distanceToEntry = Math.Max(0, loopPoint - vpWidth);
                    var timeToEntry = distanceToEntry / speed;
                    return (sv, loopPoint, timeToEntry);
                }).ToList();

                var maxTimeToEntry = items.Max(x => x.timeToEntry);
                foreach (var (sv, loopPoint, timeToEntry) in items)
                {
                    var delayStart = Math.Max(0, maxTimeToEntry - timeToEntry);
                    states[sv] = (true, now + pauseSeconds + delayStart, loopPoint, delayStart);
                }
                return;
            }

            foreach (var sv in allScrollViewers.Where(sv => !GetNeedsScrolling(sv)))
                states.Remove(sv);

            foreach (var sv in scrollViewers)
            {
                if (!states.TryGetValue(sv, out var state)) continue;

                if (state.IsPaused)
                {
                    if (now >= state.Timestamp)
                        states[sv] = (false, now, state.LoopPoint, state.DelayStart);
                }
                else
                {
                    var elapsed = now - state.Timestamp;
                    var distance = elapsed * speed;
                    var offset = Math.Min(state.LoopPoint, distance);
                    sv.ScrollToHorizontalOffset(offset);

                    if (distance >= state.LoopPoint)
                    {
                        sv.ScrollToHorizontalOffset(0);
                        states[sv] = (true, now + pauseSeconds, state.LoopPoint, state.DelayStart);
                    }
                }
            }
        }

        var updateTimer = new DispatcherTimer(DispatcherPriority.Render, element.Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        updateTimer.Tick += UpdateAll;
        element.SetValue(MarqueeTimerProperty, updateTimer);
        updateTimer.Start();
    }

    private static void StopMarquee(FrameworkElement element)
    {
        if (element.GetValue(MarqueeTimerProperty) is DispatcherTimer timer)
        {
            timer.Stop();
            element.ClearValue(MarqueeTimerProperty);
        }
    }
}
