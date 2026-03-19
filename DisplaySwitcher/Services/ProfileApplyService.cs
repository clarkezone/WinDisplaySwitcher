namespace DisplaySwitcher.Services;

using DisplaySwitcher.Models;

/// <summary>
/// Orchestrates applying a resolution profile with smart-apply logic:
/// - If only scale differs from current state → change scale only (no flicker)
/// - If resolution differs → change resolution first, then scale
/// - If everything matches → no-op
/// </summary>
public sealed class ProfileApplyService
{
    private readonly DisplayService _displayService;
    private readonly ScalingService _scalingService;

    public event Action<string>? StatusChanged;

    public ProfileApplyService(DisplayService displayService, ScalingService scalingService)
    {
        _displayService = displayService;
        _scalingService = scalingService;
    }

    public ApplyResult Apply(ResolutionProfile profile)
    {
        var monitors = _displayService.GetMonitors();
        var monitor = FindMonitor(monitors, profile);
        if (monitor == null)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[Apply] Monitor not found: profile.DeviceName='{profile.DeviceName}', " +
                $"profile.MonitorName='{profile.MonitorName}', available=[{string.Join(", ", monitors.Select(m => $"{m.DeviceName}={m.FriendlyName}"))}]");
            return new ApplyResult(false, $"Monitor '{profile.DeviceName}' not found.");
        }

        // Use the actual device name (may differ from profile if device was re-mapped)
        string deviceName = monitor.DeviceName;

        var currentMode = _displayService.GetCurrentMode(deviceName);
        if (currentMode == null)
            return new ApplyResult(false, "Could not read current display mode.");

        int currentScale = _scalingService.GetCurrentScale(monitor);

        var targetMode = profile.Mode;
        bool resolutionMatch = currentMode.Width == targetMode.Width
                            && currentMode.Height == targetMode.Height
                            && currentMode.RefreshRate == targetMode.RefreshRate;
        bool scaleMatch = targetMode.ScalePercent <= 0 || currentScale == targetMode.ScalePercent;

        System.Diagnostics.Debug.WriteLine(
            $"[Apply] device={deviceName} current={currentMode} scale={currentScale}% → target={targetMode} " +
            $"resMatch={resolutionMatch} scaleMatch={scaleMatch}");

        if (resolutionMatch && scaleMatch)
        {
            StatusChanged?.Invoke($"Already at {targetMode}");
            return new ApplyResult(true, "Already at requested settings.");
        }

        // Apply resolution if needed
        if (!resolutionMatch)
        {
            StatusChanged?.Invoke($"Switching resolution to {targetMode.Width}×{targetMode.Height}@{targetMode.RefreshRate}Hz…");
            var resResult = _displayService.ApplyResolution(deviceName, targetMode);
            System.Diagnostics.Debug.WriteLine($"[Apply] Resolution change result: {resResult}");
            if (resResult != DisplayChangeResult.Success)
                return new ApplyResult(false, $"Resolution change failed: {resResult}");
        }

        // Apply scale if needed
        if (!scaleMatch && targetMode.ScalePercent > 0)
        {
            StatusChanged?.Invoke($"Setting scale to {targetMode.ScalePercent}%…");

            // Re-fetch monitor info (LUID may change after resolution switch)
            if (!resolutionMatch)
            {
                monitors = _displayService.GetMonitors();
                monitor = FindMonitor(monitors, profile);
                if (monitor == null)
                    return new ApplyResult(true, "Resolution changed but could not find monitor for scale change.");
                deviceName = monitor.DeviceName;
            }

            bool scaleOk = _scalingService.SetScale(monitor, targetMode.ScalePercent);
            System.Diagnostics.Debug.WriteLine($"[Apply] Scale set to {targetMode.ScalePercent}%: {(scaleOk ? "OK" : "FAILED")}");
            if (!scaleOk)
                return new ApplyResult(!resolutionMatch,
                    resolutionMatch ? "Scale change failed." : "Resolution changed but scale change failed.");
        }

        StatusChanged?.Invoke($"Applied: {targetMode}");
        return new ApplyResult(true, $"Applied: {targetMode}");
    }

    /// <summary>
    /// Find a monitor matching a profile. Tries exact DeviceName first, then
    /// falls back to FriendlyName match, then single-monitor fallback.
    /// This handles Windows reassigning device names (e.g. DISPLAY1→DISPLAY2)
    /// after reboots or monitor reconnections.
    /// </summary>
    private static MonitorInfo? FindMonitor(List<MonitorInfo> monitors, ResolutionProfile profile)
    {
        // Exact device name match
        var monitor = monitors.FirstOrDefault(m => m.DeviceName == profile.DeviceName);
        if (monitor != null) return monitor;

        // Match by friendly name (e.g. "BMD HDMI")
        if (!string.IsNullOrEmpty(profile.MonitorName))
        {
            monitor = monitors.FirstOrDefault(m =>
                m.FriendlyName.Equals(profile.MonitorName, StringComparison.OrdinalIgnoreCase));
            if (monitor != null)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Apply] Remapped '{profile.DeviceName}' → '{monitor.DeviceName}' via friendly name '{profile.MonitorName}'");
                return monitor;
            }
        }

        // Single-monitor fallback
        if (monitors.Count == 1)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[Apply] Single-monitor fallback: '{profile.DeviceName}' → '{monitors[0].DeviceName}'");
            return monitors[0];
        }

        return null;
    }
}

public record ApplyResult(bool Success, string Message);
