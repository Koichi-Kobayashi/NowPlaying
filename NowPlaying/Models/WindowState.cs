namespace NowPlaying.Models;

/// <summary>
/// ウィンドウのサイズ、位置、表示モードを保持します。
/// </summary>
public class WindowState
{
    /// <summary>ウィンドウの幅</summary>
    public double Width { get; set; }

    /// <summary>ウィンドウの高さ</summary>
    public double Height { get; set; }

    /// <summary>ウィンドウの左端の位置</summary>
    public double Left { get; set; }

    /// <summary>ウィンドウの上端の位置</summary>
    public double Top { get; set; }

    /// <summary>true=ミニプレーヤー、false=ダッシュボード</summary>
    public bool IsMiniPlayer { get; set; }

    /// <summary>true=最前面で表示、false=通常表示</summary>
    public bool Topmost { get; set; }
}
