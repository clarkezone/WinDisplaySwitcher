namespace DisplaySwitcher.Models;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>Root settings model persisted to JSON.</summary>
public sealed class AppSettings
{
    [JsonPropertyName("profiles")]
    public List<CompositeProfile> Profiles { get; set; } = new();

    [JsonPropertyName("autoStart")]
    public bool AutoStart { get; set; }

    /// <summary>Create default settings with common profiles.</summary>
    public static AppSettings CreateDefault()
    {
        return new AppSettings
        {
            AutoStart = false,
            Profiles = new List<CompositeProfile>
            {
                new()
                {
                    Name = "Primary 1080p 100%",
                    MonitorSettings = new List<MonitorSetting>
                    {
                        new()
                        {
                            DeviceName = @"\\.\DISPLAY1",
                            MonitorName = "Primary",
                            Width = 1920, Height = 1080,
                            RefreshRate = 60, BitsPerPixel = 32,
                            ScalePercent = 100,
                        }
                    }
                },
                new()
                {
                    Name = "Primary 4K 150%",
                    MonitorSettings = new List<MonitorSetting>
                    {
                        new()
                        {
                            DeviceName = @"\\.\DISPLAY1",
                            MonitorName = "Primary",
                            Width = 3840, Height = 2160,
                            RefreshRate = 60, BitsPerPixel = 32,
                            ScalePercent = 150,
                        }
                    }
                }
            }
        };
    }

    /// <summary>
    /// Attempt to migrate a legacy settings JSON (which used ResolutionProfile with
    /// DeviceName/Mode at root level) to the new CompositeProfile format.
    /// Returns null if the JSON is already in the new format or cannot be migrated.
    /// </summary>
    public static AppSettings? MigrateFromLegacy(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("profiles", out var profilesArray)
                || profilesArray.ValueKind != JsonValueKind.Array
                || profilesArray.GetArrayLength() == 0)
                return null;

            // Check if first profile has "deviceName" at root level (legacy) vs "monitorSettings" (new)
            var first = profilesArray[0];
            if (!first.TryGetProperty("deviceName", out _))
                return null; // Already new format or unknown

            var settings = new AppSettings();
            if (root.TryGetProperty("autoStart", out var autoStart))
                settings.AutoStart = autoStart.GetBoolean();

            foreach (var legacy in profilesArray.EnumerateArray())
            {
                var composite = new CompositeProfile();

                if (legacy.TryGetProperty("id", out var id))
                    composite.Id = id.GetString() ?? composite.Id;

                if (legacy.TryGetProperty("hotkeyVk", out var hkVk))
                    composite.HotkeyVk = hkVk.GetInt32();
                if (legacy.TryGetProperty("hotkeyModifiers", out var hkMod))
                    composite.HotkeyModifiers = hkMod.GetInt32();

                var ms = new MonitorSetting();
                if (legacy.TryGetProperty("deviceName", out var dn))
                    ms.DeviceName = dn.GetString() ?? string.Empty;
                if (legacy.TryGetProperty("monitorName", out var mn))
                    ms.MonitorName = mn.GetString() ?? string.Empty;

                if (legacy.TryGetProperty("mode", out var mode))
                {
                    if (mode.TryGetProperty("width", out var w)) ms.Width = w.GetInt32();
                    if (mode.TryGetProperty("height", out var h)) ms.Height = h.GetInt32();
                    if (mode.TryGetProperty("refreshRate", out var rr)) ms.RefreshRate = rr.GetInt32();
                    if (mode.TryGetProperty("bitsPerPixel", out var bpp)) ms.BitsPerPixel = bpp.GetInt32();
                    if (mode.TryGetProperty("scalePercent", out var sp))
                    {
                        int scale = sp.GetInt32();
                        ms.ScalePercent = scale > 0 ? scale : null;
                    }
                }

                // Generate a name from the legacy display label
                composite.Name = $"{ms.MonitorName}: {ms.Width}×{ms.Height}";
                if (ms.ScalePercent.HasValue) composite.Name += $" {ms.ScalePercent}%";

                composite.MonitorSettings.Add(ms);
                settings.Profiles.Add(composite);
            }

            return settings;
        }
        catch
        {
            return null;
        }
    }
}
