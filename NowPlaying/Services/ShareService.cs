using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows;
using System.Windows.Media.Imaging;
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
                _ = SetShareDataAsync(args.Request.Data, track, deferral);
            }

            interop.ShowShareUIForWindow(hwnd);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Share error: {ex.Message}");
            FallbackToXUrl(track);
        }
    }

    private static async Task SetShareDataAsync(DataPackage data, NowPlayingTrack track, DataRequestDeferral deferral)
    {
        try
        {
            var text = BuildShareText(track);
            data.Properties.Title = "Now Playing";
            data.SetText(text);

            if (track.AlbumArtwork is BitmapSource bitmap)
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
    public void ShareViaWebView2(NowPlayingTrack track)
    {
        // アルバムアートをクリップボードにコピー
        if (track.AlbumArtwork is BitmapSource bitmap)
        {
            try
            {
                System.Windows.Clipboard.SetImage(bitmap);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Clipboard copy error: {ex.Message}");
            }
        }

        var url = BuildXIntentUrl(track);
        var hasAlbumArtwork = track.AlbumArtwork != null;
        var window = new Views.Windows.ShareToXWindow(url, hasAlbumArtwork);
        window.Closed += (_, _) => _appSettingsService.MarkShareSucceeded();
        window.Show();
    }

    /// <summary>
    /// Xの投稿画面を外部ブラウザで開く
    /// </summary>
    public void FallbackToXUrl(NowPlayingTrack track)
    {
        var url = BuildXIntentUrl(track);

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
        if (track.IsEmpty) return "現在再生中の曲がありません #NowPlaying";
        return $"Now Playing: {track.Title} - {track.Artist} #NowPlaying";
    }

    private static string BuildXIntentUrl(NowPlayingTrack track)
    {
        var text = BuildShareText(track);
        var encoded = Uri.EscapeDataString(text);
        return $"https://x.com/intent/tweet?text={encoded}";
    }
}
