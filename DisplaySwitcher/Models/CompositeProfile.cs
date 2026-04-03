namespace DisplaySwitcher.Models;

using System.Text.Json.Serialization;

/// <summary>
/// A composite profile that can set topology, resolution, and/or DPI
/// across multiple monitors in a single action. All axes are independent:
/// - Topology is nullable (null = don't change)
/// - MonitorSettings entries have nullable fields (null = don't change that aspect)
/// </summary>
public sealed class CompositeProfile
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>User-friendly profile name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Display topology. Null = don't change topology.</summary>
    [JsonPropertyName("topology")]
    public TopologyMode? Topology { get; set; }

    /// <summary>Per-monitor settings. Empty list = no per-monitor changes (topology-only profile).</summary>
    [JsonPropertyName("monitorSettings")]
    public List<MonitorSetting> MonitorSettings { get; set; } = new();

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

    [JsonIgnore]
    public string DisplayLabel
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(Name))
                return Name;

            if (Topology.HasValue)
                parts.Add(Topology.Value.ToString());

            foreach (var ms in MonitorSettings)
            {
                if (ms.HasAnyChange)
                    parts.Add(ms.DisplaySummary);
            }

            return parts.Count > 0 ? string.Join(" | ", parts) : "Empty profile";
        }
    }
}
