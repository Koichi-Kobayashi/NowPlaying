using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;
using System.Windows.Media.Imaging;

namespace NowPlaying.Services;

/// <summary>
/// Chromeの再生タイトルと表示中のURLが一致する場合だけ、YouTubeのサムネイルを取得する。
/// ブラウザへの入力やタブの切り替えは行わない。
/// </summary>
internal sealed class YouTubeArtworkService
{
    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(4),
        MaxResponseContentBufferSize = 8 * 1024 * 1024
    };
    private readonly HttpClient _httpClient;
    private readonly Func<string, Task<string?>> _resolveVideoId;
    private readonly Dictionary<string, BitmapSource> _images = new(StringComparer.Ordinal);
    private string? _cacheKey;
    private Task<BitmapSource?>? _cachedRequest;
    private DateTimeOffset _retryAfter;
    private Task<string?>? _browserLookup;

    public YouTubeArtworkService() : this(SharedHttpClient, null) { }

    internal YouTubeArtworkService(HttpClient httpClient, Func<string, Task<string?>>? resolveVideoId)
    {
        _httpClient = httpClient;
        _resolveVideoId = resolveVideoId ?? FindVideoIdAsync;
    }

    public Task<BitmapSource?> GetArtworkAsync(string sourceAppId, string title, string artist)
    {
        if (!sourceAppId.Contains("chrome", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(title))
            return Task.FromResult<BitmapSource?>(null);

        var key = $"{sourceAppId}\n{title}\n{artist}";
        if (_cacheKey == key && _cachedRequest != null &&
            (!_cachedRequest.IsCompleted || DateTimeOffset.UtcNow < _retryAfter))
            return _cachedRequest;

        _cacheKey = key;
        _cachedRequest = LoadAndCacheArtworkAsync(key, title);
        return _cachedRequest;
    }

    private async Task<BitmapSource?> LoadAndCacheArtworkAsync(string key, string title)
    {
        var artwork = await LoadArtworkAsync(title);
        if (_cacheKey == key)
        {
            // During navigation, the media-session title can change before the
            // browser title/URL. Retry those transient failures on the next poll.
            _retryAfter = DateTimeOffset.UtcNow.AddSeconds(artwork == null ? 3 : 30);
        }
        return artwork;
    }

    private async Task<BitmapSource?> LoadArtworkAsync(string title)
    {
        try
        {
            var videoId = await _resolveVideoId(title);
            if (videoId == null)
                return null;
            if (_images.TryGetValue(videoId, out var cached))
                return cached;

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            foreach (var size in new[] { "maxresdefault", "sddefault", "hqdefault" })
            {
                using var response = await _httpClient.GetAsync(
                    $"https://i.ytimg.com/vi/{videoId}/{size}.jpg", timeout.Token);
                if (!response.IsSuccessStatusCode)
                    continue;

                using var stream = new MemoryStream(await response.Content.ReadAsByteArrayAsync(timeout.Token));
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();

                // Missing variants can return a 120px placeholder with HTTP 200.
                if (bitmap.PixelWidth >= 480)
                {
                    if (_images.Count >= 8)
                        _images.Remove(_images.Keys.First());
                    _images[videoId] = bitmap;
                    return bitmap;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"YouTube artwork unavailable: {ex.Message}");
        }
        return null;
    }

    private async Task<string?> FindVideoIdAsync(string title)
    {
        // UI Automation can stall inside another process. Keep at most one lookup
        // running, and never wait for it on the WPF dispatcher.
        if (_browserLookup is { IsCompleted: false })
            return null;
        _browserLookup = Task.Run(() => FindVideoId(title));
        return await _browserLookup.WaitAsync(TimeSpan.FromSeconds(2));
    }

    private static string? FindVideoId(string title)
    {
        var browserProcessIds = new HashSet<int>();
        foreach (var process in Process.GetProcessesByName("chrome"))
        {
            using (process)
                browserProcessIds.Add(process.Id);
        }

        var matchingWindows = new List<nint>();
        EnumWindows((handle, _) =>
        {
            GetWindowThreadProcessId(handle, out var processId);
            if (!browserProcessIds.Contains((int)processId))
                return true;
            var text = new StringBuilder(2048);
            GetWindowText(handle, text, text.Capacity);
            var windowTitle = text.ToString();
            if (MatchesWindowTitle(windowTitle, title))
                matchingWindows.Add(handle);
            return true;
        }, 0);

        var videoIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var handle in matchingWindows)
        {
            try
            {
                var videoId = ReadVideoIdFromBrowser(AutomationElement.FromHandle(handle));
                var currentTitle = new StringBuilder(2048);
                GetWindowText(handle, currentTitle, currentTitle.Capacity);
                if (videoId != null && MatchesWindowTitle(currentTitle.ToString(), title))
                    videoIds.Add(videoId);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"YouTube URL unavailable: {ex.Message}");
            }
        }
        // Identical titles on different videos are ambiguous; retain the OS image.
        return videoIds.Count == 1 ? videoIds.Single() : null;
    }

    internal static bool MatchesWindowTitle(string windowTitle, string mediaTitle)
    {
        if (string.IsNullOrWhiteSpace(mediaTitle))
            return false;

        var expected = mediaTitle.Trim() + " - YouTube";
        bool Matches(string candidate) => candidate.Equals(expected, StringComparison.Ordinal) ||
            candidate.StartsWith(expected + " - ", StringComparison.Ordinal);

        windowTitle = windowTitle.Trim();
        // Try the complete title first: a video may itself start with "(43)".
        if (Matches(windowTitle))
            return true;

        // YouTube adds an unread-notification count to the browser tab title,
        // but not to the Windows media-session title.
        var closingBracket = windowTitle.IndexOf(')');
        if (windowTitle.StartsWith('(') && closingBracket > 1 &&
            closingBracket + 1 < windowTitle.Length && char.IsWhiteSpace(windowTitle[closingBracket + 1]) &&
            windowTitle.AsSpan(1, closingBracket - 1).IndexOfAnyExceptInRange('0', '9') < 0)
            return Matches(windowTitle[(closingBracket + 1)..].TrimStart());

        return false;
    }

    private static string? ReadVideoIdFromBrowser(AutomationElement window)
    {
        var walker = TreeWalker.ControlViewWalker;
        var pending = new Queue<AutomationElement>();
        pending.Enqueue(window);
        for (var visited = 0; pending.Count > 0 && visited < 300; visited++)
        {
            var element = pending.Dequeue();
            var controlType = element.Current.ControlType;
            // Inspect browser chrome only, never text boxes in web page content.
            if (controlType == ControlType.Document)
                continue;
            if (controlType == ControlType.Edit && !element.Current.HasKeyboardFocus &&
                element.TryGetCurrentPattern(ValuePattern.Pattern, out var pattern))
            {
                var videoId = ParseVideoId(((ValuePattern)pattern).Current.Value);
                if (videoId != null)
                    return videoId;
            }
            for (var child = walker.GetFirstChild(element); child != null; child = walker.GetNextSibling(child))
            {
                if (pending.Count >= 300)
                    break;
                pending.Enqueue(child);
            }
        }
        return null;
    }

    internal static string? ParseVideoId(string address)
    {
        address = address.Trim();
        if (!address.Contains("://", StringComparison.Ordinal))
            address = "https://" + address;
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            return null;

        string? id = null;
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (uri.Host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase) && segments.Length == 1)
            id = segments[0];
        else if (uri.Host.Equals("youtube.com", StringComparison.OrdinalIgnoreCase) ||
                 uri.Host.EndsWith(".youtube.com", StringComparison.OrdinalIgnoreCase))
        {
            if (uri.AbsolutePath == "/watch")
            {
                foreach (var pair in uri.Query.TrimStart('?').Split('&'))
                {
                    var parts = pair.Split('=', 2);
                    if (parts.Length == 2 && parts[0] == "v")
                    {
                        id = Uri.UnescapeDataString(parts[1]);
                        break;
                    }
                }
            }
            else if (segments.Length == 2 && segments[0] is "shorts" or "live" or "embed")
                id = segments[1];
        }
        return id is { Length: 11 } && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_') ? id : null;
    }

    private delegate bool EnumWindowsCallback(nint handle, nint parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsCallback callback, nint parameter);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(nint handle, StringBuilder text, int count);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint handle, out uint processId);
}
