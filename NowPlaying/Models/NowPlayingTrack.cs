using System.Windows.Media;

namespace NowPlaying.Models;

/// <summary>
/// 再生中の曲情報
/// </summary>
public class NowPlayingTrack
{
    public string Title { get; init; } = string.Empty;
    public string Artist { get; init; } = string.Empty;
    public string AlbumTitle { get; init; } = string.Empty;
    public ImageSource? AlbumArtwork { get; init; }
    public bool IsPlaying { get; init; }

    public bool IsEmpty => string.IsNullOrWhiteSpace(Title) && string.IsNullOrWhiteSpace(Artist);

    /// <summary>アルバム名の表示行を表示するか</summary>
    public bool ShowAlbumLine => !IsEmpty && !string.IsNullOrWhiteSpace(AlbumTitle);

    public string DisplayText => IsEmpty
        ? "再生中の曲がありません"
        : $"{(string.IsNullOrWhiteSpace(Artist) ? "" : Artist + " - ")}{Title}";
}
