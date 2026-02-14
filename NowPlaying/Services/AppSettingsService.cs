using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using NowPlaying.Models;

namespace NowPlaying.Services;

/// <summary>
/// アプリケーション設定の永続化を行うサービスです。
/// </summary>
public partial class AppSettingsService : ObservableObject
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _filePath;

    [ObservableProperty]
    private bool _autoPost;

    [ObservableProperty]
    private bool _hasSuccessfullyShared;

    [ObservableProperty]
    private bool _postAlbumArtwork;

    [ObservableProperty]
    private bool _copyAlbumArtworkOnManualPost;

    [ObservableProperty]
    private bool _autoCloseShareWindow;

    partial void OnAutoPostChanged(bool value) => Save();
    partial void OnHasSuccessfullySharedChanged(bool value) => Save();
    partial void OnPostAlbumArtworkChanged(bool value) => Save();
    partial void OnCopyAlbumArtworkOnManualPostChanged(bool value) => Save();
    partial void OnAutoCloseShareWindowChanged(bool value) => Save();

    /// <summary>
    /// Xへのシェアが成功したことを記録します。ウィンドウを閉じたときに呼び出されます。
    /// </summary>
    public void MarkShareSucceeded()
    {
        if (!HasSuccessfullyShared)
        {
            HasSuccessfullyShared = true;
        }
    }

    public AppSettingsService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var folder = Path.Combine(appData, "NowPlaying");
        Directory.CreateDirectory(folder);
        _filePath = Path.Combine(folder, "appsettings.json");
        Load();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_filePath))
                return;

            var json = File.ReadAllText(_filePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            if (settings != null)
            {
                AutoPost = settings.AutoPost;
                HasSuccessfullyShared = settings.HasSuccessfullyShared;
                PostAlbumArtwork = settings.PostAlbumArtwork;
                CopyAlbumArtworkOnManualPost = settings.CopyAlbumArtworkOnManualPost;
                AutoCloseShareWindow = settings.AutoCloseShareWindow;
            }
        }
        catch
        {
            // 読み込み失敗時はデフォルト値を維持
        }
    }

    /// <summary>
    /// 現在の設定を保存します。
    /// </summary>
    public void Save()
    {
        try
        {
            var settings = new AppSettings
            {
                AutoPost = AutoPost,
                HasSuccessfullyShared = HasSuccessfullyShared,
                PostAlbumArtwork = PostAlbumArtwork,
                CopyAlbumArtworkOnManualPost = CopyAlbumArtworkOnManualPost,
                AutoCloseShareWindow = AutoCloseShareWindow
            };
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(_filePath, json);
        }
        catch
        {
            // 保存失敗時は無視
        }
    }
}
