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

    /// <summary>投稿後にシェアウィンドウを自動で閉じるか。デフォルトはオフ。</summary>
    public bool AutoCloseShareWindow { get; set; }
}
