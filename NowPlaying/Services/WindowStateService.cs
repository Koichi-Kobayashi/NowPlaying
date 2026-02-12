using System.IO;
using System.Text.Json;
using NowPlaying.Models;

namespace NowPlaying.Services;

/// <summary>
/// ウィンドウの状態を永続化するサービスです。
/// </summary>
public class WindowStateService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _filePath;

    public WindowStateService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var folder = Path.Combine(appData, "NowPlaying");
        Directory.CreateDirectory(folder);
        _filePath = Path.Combine(folder, "windowstate.json");
    }

    /// <summary>
    /// 保存されたウィンドウ状態を読み込みます。存在しない場合は null を返します。
    /// </summary>
    public Models.WindowState? Load()
    {
        try
        {
            if (!File.Exists(_filePath))
                return null;

            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<Models.WindowState>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// ウィンドウ状態を保存します。
    /// </summary>
    public void Save(Models.WindowState state)
    {
        try
        {
            var json = JsonSerializer.Serialize(state, JsonOptions);
            File.WriteAllText(_filePath, json);
        }
        catch
        {
            // 保存失敗時は無視
        }
    }
}
