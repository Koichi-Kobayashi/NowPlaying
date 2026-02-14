using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using Wpf.Ui.Controls;

namespace NowPlaying.Views.Windows;

public partial class ShareToXWindow : FluentWindow
{
    private readonly string _url;
    private readonly bool _hasAlbumArtwork;
    private readonly bool _autoClose;
    private readonly bool _autoSubmitPost;

    private const int VK_CONTROL = 0x11;
    private const int VK_V = 0x56;
    private const int KEYEVENTF_KEYUP = 0x02;

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

    public ShareToXWindow(
        string url,
        bool hasAlbumArtwork = false,
        bool autoClose = false,
        bool autoSubmitPost = false)
    {
        _url = url;
        _hasAlbumArtwork = hasAlbumArtwork;
        _autoClose = autoClose;
        _autoSubmitPost = autoSubmitPost;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        var udf = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NowPlaying",
            "WebView2");
        var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(
            browserExecutableFolder: null,
            userDataFolder: udf);
        await WebView.EnsureCoreWebView2Async(env);

        WebView.Source = new Uri(_url);
        WebView.NavigationCompleted += OnNavigationCompleted;
    }

    private async void OnNavigationCompleted(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
    {
        if (sender is not Microsoft.Web.WebView2.Wpf.WebView2 wv)
            return;

        wv.NavigationCompleted -= OnNavigationCompleted;

        try
        {
            await Task.Delay(2500);

            var coreWebView2 = wv.CoreWebView2;
            if (coreWebView2 == null) return;

            if (_hasAlbumArtwork)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    wv.Focus();
                    Activate();
                });
                await Task.Delay(300);

                var focusScript = @"
                    (function() {
                        var sel = document.querySelector('[data-testid=""tweetTextarea_0""]') ||
                                 document.querySelector('[data-testid=""tweetTextarea""]') ||
                                 document.querySelector('div[contenteditable=""true""][role=""textbox""]') ||
                                 document.querySelector('div[contenteditable=""true""]');
                        if (sel) {
                            sel.focus();
                            sel.click();
                            return true;
                        }
                        return false;
                    })();
                ";
                await coreWebView2.ExecuteScriptAsync(focusScript);
                await Task.Delay(300);

                await Dispatcher.InvokeAsync(() =>
                {
                    Activate();
                    wv.Focus();
                    SendPasteKeys();
                });
                await Task.Delay(1500);
            }

            if (_autoSubmitPost)
            {
                var clickPostScript = @"
                    (function() {
                        var btn = document.querySelector('button[data-testid=""tweetButton""]');
                        if (btn) {
                            btn.click();
                            return true;
                        }
                        return false;
                    })();
                ";
                var clickResult = await coreWebView2.ExecuteScriptAsync(clickPostScript);
                await Task.Delay(2000);

                // 「自動で閉じる」がオンで、投稿ボタンのクリックに成功した場合のみ閉じる
                // ログインページ（URLに login 含む）の場合は閉じない
                var postSucceeded = clickResult?.Trim().Trim('"') == "true";
                var currentUrl = coreWebView2.Source?.ToString() ?? "";
                var isLoginPage = currentUrl.Contains("login", StringComparison.OrdinalIgnoreCase);
                if (_autoClose && postSucceeded && !isLoginPage)
                    await Dispatcher.InvokeAsync(Close);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Share automation error: {ex.Message}");
        }
    }

    private void SendPasteKeys()
    {
        keybd_event((byte)VK_CONTROL, 0, 0, 0);
        keybd_event((byte)VK_V, 0, 0, 0);
        keybd_event((byte)VK_V, 0, KEYEVENTF_KEYUP, 0);
        keybd_event((byte)VK_CONTROL, 0, KEYEVENTF_KEYUP, 0);
    }
}
