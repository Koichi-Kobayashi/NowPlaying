using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows.Interop;
using NowPlaying.Models;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;

namespace NowPlaying.Services;

/// <summary>
/// X（Twitter）等への共有を行うサービス
/// </summary>
public class ShareService
{
    private readonly AppSettingsService _appSettingsService;

    public ShareService(AppSettingsService appSettingsService)
    {
        _appSettingsService = appSettingsService;
    }

    private static readonly Guid IDataTransferManagerInteropGuid = new("3A3DCD6C-3EAB-43DC-BCDE-45671CE800C8");
    private static readonly Guid DataTransferManagerGuid = new(0xa5caee9b, 0x8708, 0x49d1, 0x8d, 0x36, 0x67, 0xd2, 0x5a, 0x8d, 0xa0, 0x0c);

    [DllImport("api-ms-win-core-winrt-l1-1-0", CharSet = CharSet.Unicode, ExactSpelling = true, PreserveSig = true)]
    private static extern int RoGetActivationFactory([MarshalAs(UnmanagedType.HString)] string activatableClassId, [In] ref Guid iid, out IntPtr factory);

    [ComImport]
    [Guid("3A3DCD6C-3EAB-43DC-BCDE-45671CE800C8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDataTransferManagerInterop
    {
        DataTransferManager GetForWindow([In] IntPtr appWindow, [In] ref Guid riid);
        void ShowShareUIForWindow(IntPtr appWindow);
    }

    /// <summary>
    /// Windowsの共有チャームを表示（Xアプリ等に画像+テキストを共有）
    /// </summary>
    public void ShareViaWindows(NowPlayingTrack track, Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            FallbackToXUrl(track);
            return;
        }

        try
        {
            var interopIid = IDataTransferManagerInteropGuid;
            var hr = RoGetActivationFactory("Windows.ApplicationModel.DataTransfer.DataTransferManager", ref interopIid, out var factoryPtr);
            if (hr != 0 || factoryPtr == IntPtr.Zero)
            {
                FallbackToXUrl(track);
                return;
            }

            var interop = (IDataTransferManagerInterop)Marshal.GetObjectForIUnknown(factoryPtr);
            Marshal.Release(factoryPtr);

            var dtmIid = DataTransferManagerGuid;
            var dtm = interop.GetForWindow(hwnd, ref dtmIid);

            dtm.DataRequested += OnDataRequested;

            void OnDataRequested(DataTransferManager sender, DataRequestedEventArgs args)
            {
                dtm.DataRequested -= OnDataRequested;

                var deferral = args.Request.GetDeferral();
                _ = SetShareDataAsync(args.Request.Data, track, deferral, _appSettingsService.PostAlbumArtwork);
            }

            interop.ShowShareUIForWindow(hwnd);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Share error: {ex.Message}");
            FallbackToXUrl(track);
        }
    }

    private static async Task SetShareDataAsync(DataPackage data, NowPlayingTrack track, DataRequestDeferral deferral, bool postAlbumArtwork)
    {
        try
        {
            var text = BuildShareText(track);
            data.Properties.Title = "Now Playing";
            data.SetText(text);

            if (postAlbumArtwork && track.AlbumArtwork is BitmapSource bitmap)
            {
                var streamRef = await CreateStreamReferenceFromBitmapAsync(bitmap);
                if (streamRef != null)
                {
                    data.SetBitmap(streamRef);
                }
            }
        }
        finally
        {
            deferral.Complete();
        }
    }

    private static async Task<RandomAccessStreamReference?> CreateStreamReferenceFromBitmapAsync(BitmapSource bitmap)
    {
        try
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            await using var ms = new MemoryStream();
            encoder.Save(ms);
            var bytes = ms.ToArray();

            var stream = new InMemoryRandomAccessStream();
            await stream.WriteAsync(bytes.AsBuffer());
            stream.Seek(0);

            return RandomAccessStreamReference.CreateFromStream(stream);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Create stream ref error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// WebView2でXの投稿画面を開く（アルバムアートはクリップボードにコピーされ、Ctrl+Vで貼り付け可能）
    /// </summary>
    /// <param name="track">共有するトラック</param>
    /// <param name="isAutoPost">自動ポストによる呼び出しの場合true。ウィンドウを閉じるのは自動ポスト時のみ。</param>
    public void ShareViaWebView2(NowPlayingTrack track, bool isAutoPost = false)
    {
        var shouldCopyAlbumArtwork = isAutoPost
            ? _appSettingsService.PostAlbumArtwork
            : _appSettingsService.CopyAlbumArtworkOnManualPost;

        var copiedAlbumArtwork = false;

        // アルバムアートをクリップボードにコピー（設定がオンの場合のみ）
        if (shouldCopyAlbumArtwork)
        {
            copiedAlbumArtwork = TryCopyAlbumArtworkToClipboard(track.AlbumArtwork);
        }

        var url = BuildXIntentUrl(track);

        if (_appSettingsService.OpenBrowserOnShareTimeout)
        {
            OpenUrlInExternalBrowser(url);
            _appSettingsService.MarkShareSucceeded();
            return;
        }

        var hasAlbumArtwork = copiedAlbumArtwork;
        var autoClose = isAutoPost && _appSettingsService.AutoCloseShareWindow;
        var window = new Views.Windows.ShareToXWindow(
            url,
            hasAlbumArtwork,
            autoClose,
            autoSubmitPost: isAutoPost,
            openBrowserOnTimeout: _appSettingsService.OpenBrowserOnShareTimeout);
        window.Closed += (_, _) => _appSettingsService.MarkShareSucceeded();
        window.Show();
    }

    private static bool TryCopyAlbumArtworkToClipboard(ImageSource? albumArtwork)
    {
        if (albumArtwork == null)
            return false;

        var bitmap = NormalizeForClipboard(albumArtwork);
        if (bitmap == null)
        {
            System.Diagnostics.Debug.WriteLine("Clipboard copy skipped: album artwork cannot be converted to BitmapSource.");
            return false;
        }

        bool CopyAction()
        {
            for (var attempt = 1; attempt <= 4; attempt++)
            {
                try
                {
                    var dataObject = new DataObject();
                    dataObject.SetImage(bitmap);
                    System.Windows.Clipboard.SetDataObject(dataObject, true);
                    if (System.Windows.Clipboard.ContainsImage())
                    {
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Clipboard copy error (attempt {attempt}): {ex.Message}");
                }

                if (attempt < 4)
                {
                    Thread.Sleep(150);
                }
            }

            return false;
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
            return CopyAction();

        return dispatcher.Invoke(CopyAction);
    }

    private static BitmapSource? NormalizeForClipboard(ImageSource source)
    {
        if (source is not BitmapSource bitmapSource)
            return null;

        try
        {
            // クリップボード互換性のため、一度PNGに再エンコードして正規化する。
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmapSource));

            using var ms = new MemoryStream();
            encoder.Save(ms);
            ms.Position = 0;

            var normalized = new BitmapImage();
            normalized.BeginInit();
            normalized.CacheOption = BitmapCacheOption.OnLoad;
            normalized.StreamSource = ms;
            normalized.EndInit();
            normalized.Freeze();

            return normalized;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Normalize bitmap error: {ex.Message}");
            return bitmapSource;
        }
    }

    /// <summary>
    /// Xの投稿画面を外部ブラウザで開く
    /// </summary>
    public void FallbackToXUrl(NowPlayingTrack track)
    {
        var url = BuildXIntentUrl(track);
        OpenUrlInExternalBrowser(url);
    }

    private static void OpenUrlInExternalBrowser(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Open URL error: {ex.Message}");
        }
    }

    private static string BuildShareText(NowPlayingTrack track)
    {
        if (track.IsEmpty) return NowPlaying.Services.Loc.Get("Share_NoTrack");
        var main = $"Now Playing: {track.Title} - {track.Artist}";
        if (!string.IsNullOrWhiteSpace(track.AlbumTitle))
            main += $" - {track.AlbumTitle}";
        var culture = LocalizationService.Instance.CurrentCulture;
        var timestamp = DateTime.Now.ToString("g", culture);
        return $"{main} #NowPlaying ({timestamp})";
    }

    private static string BuildXIntentUrl(NowPlayingTrack track)
    {
        var text = BuildShareText(track);
        var encoded = Uri.EscapeDataString(text);
        var url = $"https://x.com/intent/tweet?text={encoded}";
        System.Diagnostics.Debug.WriteLine($"X intent URL: {url}");
        return url;
    }
}
