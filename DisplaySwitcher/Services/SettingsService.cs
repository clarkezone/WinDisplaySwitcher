namespace DisplaySwitcher.Services;

using DisplaySwitcher.Models;
using System.IO;
using System.Text.Json;

/// <summary>
/// Loads and saves application settings from JSON in %APPDATA%\DisplaySwitcher.
/// </summary>
public sealed class SettingsService
{
    private static readonly string SettingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DisplaySwitcher");
    private static readonly string SettingsFile = Path.Combine(SettingsDir, "settings.json");
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly object _lock = new();
    private AppSettings _cached = null!;

    public AppSettings Settings => _cached;

    public SettingsService()
    {
        _cached = Load();
    }

    public AppSettings Load()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    var json = File.ReadAllText(SettingsFile);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                    if (settings != null)
                    {
                        _cached = settings;
                        return settings;
                    }
                }
            }
            catch
            {
                // Corrupt file — fall through to defaults
            }

            _cached = AppSettings.CreateDefault();
            Save(_cached);
            return _cached;
        }
    }

    public void Save(AppSettings? settings = null)
    {
        lock (_lock)
        {
            settings ??= _cached;
            _cached = settings;

            Directory.CreateDirectory(SettingsDir);
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsFile, json);
        }
    }

    public void AddProfile(ResolutionProfile profile)
    {
        _cached.Profiles.Add(profile);
        Save();
    }

    public void RemoveProfile(string profileId)
    {
        _cached.Profiles.RemoveAll(p => p.Id == profileId);
        Save();
    }

    public void UpdateProfile(ResolutionProfile profile)
    {
        var idx = _cached.Profiles.FindIndex(p => p.Id == profile.Id);
        if (idx >= 0)
        {
            _cached.Profiles[idx] = profile;
            Save();
        }
    }
}
