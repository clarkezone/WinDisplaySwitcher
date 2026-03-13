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
        var monitor = monitors.FirstOrDefault(m => m.DeviceName == profile.DeviceName);
        if (monitor == null)
            return new ApplyResult(false, $"Monitor '{profile.DeviceName}' not found.");

        // Get current state
        var currentMode = _displayService.GetCurrentMode(profile.DeviceName);
        if (currentMode == null)
            return new ApplyResult(false, "Could not read current display mode.");

        int currentScale = _scalingService.GetCurrentScale(monitor);

        var targetMode = profile.Mode;
        bool resolutionMatch = currentMode.Width == targetMode.Width
                            && currentMode.Height == targetMode.Height
                            && currentMode.RefreshRate == targetMode.RefreshRate;
        bool scaleMatch = targetMode.ScalePercent <= 0 || currentScale == targetMode.ScalePercent;

        if (resolutionMatch && scaleMatch)
        {
            StatusChanged?.Invoke($"Already at {targetMode}");
            return new ApplyResult(true, "Already at requested settings.");
        }

        // Apply resolution if needed
        if (!resolutionMatch)
        {
            StatusChanged?.Invoke($"Switching resolution to {targetMode.Width}×{targetMode.Height}@{targetMode.RefreshRate}Hz…");
            var resResult = _displayService.ApplyResolution(profile.DeviceName, targetMode);
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
                monitor = monitors.FirstOrDefault(m => m.DeviceName == profile.DeviceName);
                if (monitor == null)
                    return new ApplyResult(true, "Resolution changed but could not find monitor for scale change.");
            }

            bool scaleOk = _scalingService.SetScale(monitor, targetMode.ScalePercent);
            if (!scaleOk)
                return new ApplyResult(!resolutionMatch,
                    resolutionMatch ? "Scale change failed." : "Resolution changed but scale change failed.");
        }

        StatusChanged?.Invoke($"Applied: {targetMode}");
        return new ApplyResult(true, $"Applied: {targetMode}");
    }
}

public record ApplyResult(bool Success, string Message);
