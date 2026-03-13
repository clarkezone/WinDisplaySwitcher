namespace DisplaySwitcher.Models;

using System.Text.Json.Serialization;

/// <summary>
/// A saved resolution profile: a display mode bound to a specific monitor,
/// with an optional global hotkey.
/// </summary>
public sealed class ResolutionProfile
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>Display device name (e.g. \\.\DISPLAY1).</summary>
    [JsonPropertyName("deviceName")]
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>Friendly monitor name for UI display.</summary>
    [JsonPropertyName("monitorName")]
    public string MonitorName { get; set; } = string.Empty;

    [JsonPropertyName("mode")]
    public DisplayMode Mode { get; set; } = new();

    /// <summary>Virtual-key code for the hotkey (0 = none).</summary>
    [JsonPropertyName("hotkeyVk")]
    public int HotkeyVk { get; set; }

    /// <summary>Modifier flags (MOD_ALT=1, MOD_CONTROL=2, MOD_SHIFT=4, MOD_WIN=8).</summary>
    [JsonPropertyName("hotkeyModifiers")]
    public int HotkeyModifiers { get; set; }

    [JsonIgnore]
    public bool HasHotkey => HotkeyVk != 0;

    [JsonIgnore]
    public string HotkeyDisplayString => HasHotkey
        ? HotkeyHelper.ToString(HotkeyModifiers, HotkeyVk)
        : "None";

    public string DisplayLabel => $"{MonitorName}: {Mode}";
}

/// <summary>Utility to convert modifier+vk to a readable string.</summary>
public static class HotkeyHelper
{
    public const int MOD_ALT = 0x0001;
    public const int MOD_CONTROL = 0x0002;
    public const int MOD_SHIFT = 0x0004;
    public const int MOD_WIN = 0x0008;

    public static string ToString(int modifiers, int vk)
    {
        var parts = new List<string>();
        if ((modifiers & MOD_CONTROL) != 0) parts.Add("Ctrl");
        if ((modifiers & MOD_ALT) != 0) parts.Add("Alt");
        if ((modifiers & MOD_SHIFT) != 0) parts.Add("Shift");
        if ((modifiers & MOD_WIN) != 0) parts.Add("Win");

        var keyName = ((Windows.System.VirtualKey)vk).ToString();
        parts.Add(keyName);
        return string.Join("+", parts);
    }
}
