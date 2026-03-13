namespace DisplaySwitcher.Models;

using System.Text.Json.Serialization;

/// <summary>Root settings model persisted to JSON.</summary>
public sealed class AppSettings
{
    [JsonPropertyName("profiles")]
    public List<ResolutionProfile> Profiles { get; set; } = new();

    [JsonPropertyName("autoStart")]
    public bool AutoStart { get; set; }

    /// <summary>Create default settings with common resolutions on primary display.</summary>
    public static AppSettings CreateDefault()
    {
        return new AppSettings
        {
            AutoStart = false,
            Profiles = new List<ResolutionProfile>
            {
                new()
                {
                    DeviceName = @"\\.\DISPLAY1",
                    MonitorName = "Primary",
                    Mode = new DisplayMode(1920, 1080, 60, 100)
                },
                new()
                {
                    DeviceName = @"\\.\DISPLAY1",
                    MonitorName = "Primary",
                    Mode = new DisplayMode(3840, 2160, 60, 150)
                }
            }
        };
    }
}
