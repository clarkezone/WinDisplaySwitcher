namespace DisplaySwitcher.Services;

using DisplaySwitcher.Models;

/// <summary>
/// Orchestrates applying a composite profile with independent axes:
/// 1. Topology (optional) — only if profile.Topology is non-null
/// 2. Per-monitor resolution (optional) — only if MonitorSetting has Width/Height
/// 3. Per-monitor DPI (optional) — only if MonitorSetting has ScalePercent > 0
/// Each axis is fully independent and only fires if explicitly configured.
/// </summary>
public sealed class ProfileApplyService
{
    private readonly DisplayService _displayService;
    private readonly ScalingService _scalingService;
    private readonly TopologyService _topologyService;

    public event Action<string>? StatusChanged;

    public ProfileApplyService(DisplayService displayService, ScalingService scalingService, TopologyService topologyService)
    {
        _displayService = displayService;
        _scalingService = scalingService;
        _topologyService = topologyService;
    }

    public ApplyResult Apply(CompositeProfile profile)
    {
        var messages = new List<string>();
        bool anyFailure = false;

        // Step 1: Topology (only if set)
        if (profile.Topology.HasValue)
        {
            StatusChanged?.Invoke($"Switching topology to {profile.Topology.Value}…");
            var topoResult = _topologyService.SetTopology(profile.Topology.Value);
            System.Diagnostics.Debug.WriteLine($"[Apply] Topology: {topoResult.Message}");

            if (!topoResult.Success)
            {
                messages.Add($"Topology: {topoResult.Message}");
                anyFailure = true;
            }
            else
            {
                messages.Add($"Topology → {profile.Topology.Value}");
                // Brief delay to let OS settle after topology change
                Thread.Sleep(500);
            }
        }

        // Step 2: Per-monitor settings (only entries that have changes)
        if (profile.MonitorSettings.Count > 0)
        {
            // Re-enumerate monitors (topology change may have altered mappings)
            var monitors = _displayService.GetMonitors();

            foreach (var ms in profile.MonitorSettings)
            {
                if (!ms.HasAnyChange) continue;

                var monitor = FindMonitor(monitors, ms);
                if (monitor == null)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[Apply] Monitor not found: device='{ms.DeviceName}', name='{ms.MonitorName}', " +
                        $"available=[{string.Join(", ", monitors.Select(m => $"{m.DeviceName}={m.FriendlyName}"))}]");
                    messages.Add($"{ms.MonitorName}: not found");
                    anyFailure = true;
                    continue;
                }

                string deviceName = monitor.DeviceName;

                // Apply resolution if configured
                if (ms.HasResolution)
                {
                    StatusChanged?.Invoke($"Setting {monitor.FriendlyName} to {ms.Width}×{ms.Height}…");
                    var mode = new DisplayMode(
                        ms.Width!.Value, ms.Height!.Value,
                        ms.RefreshRate ?? 60,
                        0, // scale handled separately
                        ms.BitsPerPixel ?? 32);

                    var resResult = _displayService.ApplyResolution(deviceName, mode);
                    System.Diagnostics.Debug.WriteLine($"[Apply] Resolution on {deviceName}: {resResult}");

                    if (resResult != DisplayChangeResult.Success)
                    {
                        messages.Add($"{monitor.FriendlyName}: resolution failed ({resResult})");
                        anyFailure = true;
                    }
                    else
                    {
                        messages.Add($"{monitor.FriendlyName}: {ms.Width}×{ms.Height}@{ms.RefreshRate ?? 60}Hz");
                    }
                }

                // Apply DPI if configured
                if (ms.HasScale)
                {
                    StatusChanged?.Invoke($"Setting {monitor.FriendlyName} DPI to {ms.ScalePercent}%…");

                    // Re-fetch monitor info if resolution was just changed (LUID may shift)
                    if (ms.HasResolution)
                    {
                        monitors = _displayService.GetMonitors();
                        monitor = FindMonitor(monitors, ms);
                        if (monitor == null)
                        {
                            messages.Add($"{ms.MonitorName}: lost after resolution change");
                            anyFailure = true;
                            continue;
                        }
                    }

                    bool scaleOk = _scalingService.SetScale(monitor, ms.ScalePercent!.Value);
                    System.Diagnostics.Debug.WriteLine($"[Apply] Scale on {monitor.DeviceName} to {ms.ScalePercent}%: {(scaleOk ? "OK" : "FAILED")}");

                    if (!scaleOk)
                    {
                        messages.Add($"{monitor.FriendlyName}: DPI change failed");
                        anyFailure = true;
                    }
                    else
                    {
                        messages.Add($"{monitor.FriendlyName}: {ms.ScalePercent}%");
                    }
                }
            }
        }

        var summary = messages.Count > 0 ? string.Join("; ", messages) : "No changes applied.";
        StatusChanged?.Invoke(summary);
        return new ApplyResult(!anyFailure, summary);
    }

    /// <summary>
    /// Find a monitor matching a MonitorSetting. Tries exact DeviceName first,
    /// then falls back to FriendlyName match, then single-monitor fallback.
    /// </summary>
    private static MonitorInfo? FindMonitor(List<MonitorInfo> monitors, MonitorSetting setting)
    {
        // Exact device name match
        var monitor = monitors.FirstOrDefault(m => m.DeviceName == setting.DeviceName);
        if (monitor != null) return monitor;

        // Match by friendly name
        if (!string.IsNullOrEmpty(setting.MonitorName))
        {
            monitor = monitors.FirstOrDefault(m =>
                m.FriendlyName.Equals(setting.MonitorName, StringComparison.OrdinalIgnoreCase));
            if (monitor != null)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Apply] Remapped '{setting.DeviceName}' → '{monitor.DeviceName}' via friendly name '{setting.MonitorName}'");
                return monitor;
            }
        }

        // Single-monitor fallback
        if (monitors.Count == 1)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[Apply] Single-monitor fallback: '{setting.DeviceName}' → '{monitors[0].DeviceName}'");
            return monitors[0];
        }

        return null;
    }
}

public record ApplyResult(bool Success, string Message);
