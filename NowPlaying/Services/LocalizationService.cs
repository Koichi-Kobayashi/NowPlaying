using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Xml.Linq;

namespace NowPlaying.Services;

/// <summary>
/// Provides localization services for the application.
/// Loads strings from .resw files based on the current culture.
/// </summary>
public class LocalizationService : INotifyPropertyChanged
{
    private static LocalizationService? _instance;
    private static readonly object _lock = new();
    private static readonly string SettingsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NowPlaying",
        "language.json");

    private readonly Dictionary<string, string> _strings = [];
    private CultureInfo _currentCulture;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? LanguageChanged;

    /// <summary>
    /// Gets the singleton instance of the LocalizationService.
    /// </summary>
    public static LocalizationService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new LocalizationService();
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// Gets or sets the current culture for localization.
    /// </summary>
    public CultureInfo CurrentCulture
    {
        get => _currentCulture;
        set
        {
            if (_currentCulture.Name != value.Name)
            {
                _currentCulture = value;
                LoadStrings();
                SaveLanguageSetting();
                OnPropertyChanged(nameof(CurrentCulture));
                OnPropertyChanged(string.Empty);
                LanguageChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>
    /// Gets the list of supported cultures.
    /// </summary>
    public IReadOnlyList<CultureInfo> SupportedCultures { get; }

    private LocalizationService()
    {
        SupportedCultures = DetectSupportedCultures();

        var savedCulture = LoadLanguageSetting();

        if (savedCulture != null && SupportedCultures.Any(c => c.Name.Equals(savedCulture.Name, StringComparison.OrdinalIgnoreCase)))
        {
            _currentCulture = savedCulture;
        }
        else
        {
            _currentCulture = CultureInfo.CurrentUICulture;

            if (!SupportedCultures.Any(c => c.Name.Equals(_currentCulture.Name, StringComparison.OrdinalIgnoreCase)))
            {
                _currentCulture = SupportedCultures.FirstOrDefault(c => c.Name.StartsWith("en", StringComparison.OrdinalIgnoreCase))
                                  ?? SupportedCultures.FirstOrDefault()
                                  ?? CultureInfo.InvariantCulture;
            }
        }

        LoadStrings();
    }

    /// <summary>
    /// Sets the current culture by culture name.
    /// </summary>
    public void SetCulture(string cultureName)
    {
        try
        {
            var culture = CultureInfo.GetCultureInfo(cultureName);
            if (SupportedCultures.Any(c => c.Name.Equals(cultureName, StringComparison.OrdinalIgnoreCase)))
            {
                CurrentCulture = culture;
            }
        }
        catch
        {
            // Invalid culture name, ignore
        }
    }

    /// <summary>
    /// Gets the display name for a culture in the current language.
    /// </summary>
    public string GetCultureDisplayName(CultureInfo culture)
    {
        var key = $"Language_{culture.Name}";
        var displayName = GetString(key);
        return displayName != key ? displayName : culture.NativeName;
    }

    private CultureInfo? LoadLanguageSetting()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<LanguageSettings>(json);
                if (!string.IsNullOrEmpty(settings?.CultureName))
                {
                    return CultureInfo.GetCultureInfo(settings.CultureName);
                }
            }
        }
        catch
        {
            // Ignore errors
        }
        return null;
    }

    private void SaveLanguageSetting()
    {
        try
        {
            var directory = Path.GetDirectoryName(SettingsFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var settings = new LanguageSettings { CultureName = _currentCulture.Name };
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
        }
        catch
        {
            // Ignore errors
        }
    }

    private class LanguageSettings
    {
        public string CultureName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Gets a localized string by key.
    /// </summary>
    public string GetString(string key)
    {
        return _strings.TryGetValue(key, out var value) ? value : key;
    }

    /// <summary>
    /// Gets a localized string by key with format arguments.
    /// </summary>
    public string GetString(string key, params object[] args)
    {
        var format = GetString(key);
        try
        {
            return string.Format(CultureInfo.CurrentCulture, format, args);
        }
        catch
        {
            return format;
        }
    }

    /// <summary>
    /// Indexer to get localized string by key.
    /// </summary>
    public string this[string key] => GetString(key);

    private IReadOnlyList<CultureInfo> DetectSupportedCultures()
    {
        var cultures = new List<CultureInfo>();
        var stringsFolder = GetStringsFolder();

        if (Directory.Exists(stringsFolder))
        {
            foreach (var dir in Directory.GetDirectories(stringsFolder))
            {
                var cultureName = Path.GetFileName(dir);
                try
                {
                    var culture = CultureInfo.GetCultureInfo(cultureName);
                    cultures.Add(culture);
                }
                catch
                {
                    // Invalid culture name, skip
                }
            }
        }

        if (cultures.Count == 0)
        {
            cultures.Add(CultureInfo.GetCultureInfo("en-US"));
        }

        return cultures;
    }

    private void LoadStrings()
    {
        _strings.Clear();

        var loaded = TryLoadResourceFile(_currentCulture.Name);

        if (!loaded && !string.IsNullOrEmpty(_currentCulture.Parent?.Name))
        {
            loaded = TryLoadResourceFile(_currentCulture.Parent.Name);
        }

        if (!loaded)
        {
            TryLoadResourceFile("en-US");
        }
    }

    private bool TryLoadResourceFile(string cultureName)
    {
        var filePath = GetResourceFilePath(cultureName);

        if (!File.Exists(filePath))
            return false;

        try
        {
            var doc = XDocument.Load(filePath);
            var dataElements = doc.Descendants("data");

            foreach (var data in dataElements)
            {
                var name = data.Attribute("name")?.Value;
                var value = data.Element("value")?.Value;

                if (!string.IsNullOrEmpty(name) && value != null)
                {
                    _strings[name] = value;
                }
            }

            return _strings.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    private static string GetStringsFolder()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        return Path.Combine(baseDir, "Strings");
    }

    private static string GetResourceFilePath(string cultureName)
    {
        return Path.Combine(GetStringsFolder(), cultureName, "Resources.resw");
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Static helper class for easy access to localized strings.
/// </summary>
public static class Loc
{
    /// <summary>
    /// Gets a localized string by key.
    /// </summary>
    public static string Get(string key) => LocalizationService.Instance.GetString(key);

    /// <summary>
    /// Gets a localized string by key with format arguments.
    /// </summary>
    public static string Get(string key, params object[] args) => LocalizationService.Instance.GetString(key, args);
}
