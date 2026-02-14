namespace NowPlaying.Models;

/// <summary>
/// アプリケーションのユーザー設定を保持します。
/// </summary>
public class AppSettings
{
    /// <summary>曲が変わるたびにXへ自動ポストするか。デフォルトはオフ。</summary>
    public bool AutoPost { get; set; }

    /// <summary>一度でもXへのシェアに成功したか。自動ポストはこのフラグがtrueのときのみ有効。</summary>
    public bool HasSuccessfullyShared { get; set; }

    /// <summary>アルバムアートワークをポストするか。デフォルトはオフ。</summary>
    public bool PostAlbumArtwork { get; set; }

    /// <summary>手動ポスト時にアルバムアートワークをクリップボードへコピーするか。デフォルトはオフ。</summary>
    public bool CopyAlbumArtworkOnManualPost { get; set; }

    /// <summary>投稿後にシェアウィンドウを自動で閉じるか。デフォルトはオフ。</summary>
    public bool AutoCloseShareWindow { get; set; }

    /// <summary>X共有時にタイムアウトした場合、既定ブラウザーで開くか。デフォルトはオフ。</summary>
    public bool OpenBrowserOnShareTimeout { get; set; }
}
