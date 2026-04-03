namespace DisplaySwitcher.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Per-monitor settings within a composite profile.
/// All value fields are nullable — null means "don't change this aspect".
/// </summary>
public sealed class MonitorSetting
{
    /// <summary>GDI device name, e.g. \\.\DISPLAY1</summary>
    [JsonPropertyName("deviceName")]
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>Friendly monitor name for UI display and fallback matching.</summary>
    [JsonPropertyName("monitorName")]
    public string MonitorName { get; set; } = string.Empty;

    /// <summary>Resolution width. Null = don't change resolution.</summary>
    [JsonPropertyName("width")]
    public int? Width { get; set; }

    /// <summary>Resolution height. Null = don't change resolution.</summary>
    [JsonPropertyName("height")]
    public int? Height { get; set; }

    /// <summary>Refresh rate in Hz. Null = don't change.</summary>
    [JsonPropertyName("refreshRate")]
    public int? RefreshRate { get; set; }

    /// <summary>Bits per pixel. Null = don't change.</summary>
    [JsonPropertyName("bitsPerPixel")]
    public int? BitsPerPixel { get; set; }

    /// <summary>DPI scale percentage (100, 125, 150, …). Null or 0 = don't change DPI.</summary>
    [JsonPropertyName("scalePercent")]
    public int? ScalePercent { get; set; }

    /// <summary>True if this entry has a resolution to apply.</summary>
    [JsonIgnore]
    public bool HasResolution => Width.HasValue && Height.HasValue;

    /// <summary>True if this entry has a DPI scale to apply.</summary>
    [JsonIgnore]
    public bool HasScale => ScalePercent.HasValue && ScalePercent.Value > 0;

    /// <summary>True if this entry has anything to apply at all.</summary>
    [JsonIgnore]
    public bool HasAnyChange => HasResolution || HasScale;

    [JsonIgnore]
    public string DisplaySummary
    {
        get
        {
            var parts = new List<string>();
            if (HasResolution)
            {
                var res = $"{Width}×{Height}";
                if (RefreshRate.HasValue) res += $"@{RefreshRate}Hz";
                parts.Add(res);
            }
            if (HasScale) parts.Add($"{ScalePercent}%");
            if (parts.Count == 0) parts.Add("No change");
            return $"{MonitorName}: {string.Join(" · ", parts)}";
        }
    }
}
