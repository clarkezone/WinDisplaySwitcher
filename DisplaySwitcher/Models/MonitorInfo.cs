namespace DisplaySwitcher.Models;

/// <summary>
/// Metadata about a connected monitor, including identifiers needed for DPI scaling queries.
/// </summary>
public sealed class MonitorInfo
{
    /// <summary>GDI device name, e.g. \\.\DISPLAY1</summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>User-friendly monitor name (from DISPLAY_DEVICE.DeviceString or DisplayConfig target name).</summary>
    public string FriendlyName { get; set; } = string.Empty;

    /// <summary>Adapter LUID — needed for DisplayConfig DPI calls.</summary>
    public long AdapterLuidValue { get; set; }

    /// <summary>Source ID within the adapter — needed for DisplayConfig DPI calls.</summary>
    public uint SourceId { get; set; }

    /// <summary>Minimum supported DPI scale percentage for this monitor.</summary>
    public int MinScalePercent { get; set; } = 100;

    /// <summary>Maximum supported DPI scale percentage for this monitor.</summary>
    public int MaxScalePercent { get; set; } = 500;

    /// <summary>Recommended (default) DPI scale percentage for this monitor.</summary>
    public int RecommendedScalePercent { get; set; } = 100;

    /// <summary>All supported scale percentages for this monitor (between min and max).</summary>
    public int[] SupportedScales { get; set; } = [];

    public override string ToString() => $"{FriendlyName} ({DeviceName})";
}
