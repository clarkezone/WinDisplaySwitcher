namespace DisplaySwitcher.Services;

using DisplaySwitcher.Interop;
using DisplaySwitcher.Models;
using System.Runtime.InteropServices;

/// <summary>
/// Enumerates connected monitors and their supported resolution modes.
/// Applies resolution changes via ChangeDisplaySettingsEx.
/// </summary>
public sealed class DisplayService
{
    /// <summary>
    /// Get all connected monitors with adapter/source IDs for DPI scaling queries.
    /// </summary>
    public List<MonitorInfo> GetMonitors()
    {
        var monitors = new List<MonitorInfo>();

        // First, enumerate GDI devices
        var gdiDevices = new Dictionary<string, string>(); // deviceName → friendlyName
        var dd = new NativeMethods.DISPLAY_DEVICE();
        dd.cb = Marshal.SizeOf<NativeMethods.DISPLAY_DEVICE>();

        for (uint i = 0; NativeMethods.EnumDisplayDevicesW(null, i, ref dd, 0); i++)
        {
            if ((dd.StateFlags & NativeMethods.DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) == 0)
            {
                dd = new NativeMethods.DISPLAY_DEVICE();
                dd.cb = Marshal.SizeOf<NativeMethods.DISPLAY_DEVICE>();
                continue;
            }

            string deviceName = dd.DeviceName;
            string friendlyName = dd.DeviceString;

            // Try to get monitor-specific name
            var mon = new NativeMethods.DISPLAY_DEVICE();
            mon.cb = Marshal.SizeOf<NativeMethods.DISPLAY_DEVICE>();
            if (NativeMethods.EnumDisplayDevicesW(deviceName, 0, ref mon, 0))
            {
                if (!string.IsNullOrWhiteSpace(mon.DeviceString))
                    friendlyName = mon.DeviceString;
            }

            gdiDevices[deviceName] = friendlyName;

            dd = new NativeMethods.DISPLAY_DEVICE();
            dd.cb = Marshal.SizeOf<NativeMethods.DISPLAY_DEVICE>();
        }

        // Now query DisplayConfig for LUID + sourceId mapping
        if (NativeMethods.GetDisplayConfigBufferSizes(
                NativeMethods.QDC_ONLY_ACTIVE_PATHS,
                out uint pathCount, out uint modeCount) == 0)
        {
            var paths = new NativeMethods.DISPLAYCONFIG_PATH_INFO[pathCount];
            var modes = new NativeMethods.DISPLAYCONFIG_MODE_INFO[modeCount];

            if (NativeMethods.QueryDisplayConfig(
                    NativeMethods.QDC_ONLY_ACTIVE_PATHS,
                    ref pathCount, paths,
                    ref modeCount, modes, IntPtr.Zero) == 0)
            {
                foreach (var path in paths.Take((int)pathCount))
                {
                    // Get GDI device name for this source
                    var sourceName = new NativeMethods.DISPLAYCONFIG_SOURCE_DEVICE_NAME();
                    sourceName.header.type = NativeMethods.DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME;
                    sourceName.header.size = Marshal.SizeOf<NativeMethods.DISPLAYCONFIG_SOURCE_DEVICE_NAME>();
                    sourceName.header.adapterId = path.sourceInfo.adapterId;
                    sourceName.header.id = path.sourceInfo.id;

                    if (NativeMethods.DisplayConfigGetDeviceInfo(ref sourceName) != 0)
                        continue;

                    string gdiName = sourceName.viewGdiDeviceName;
                    if (!gdiDevices.TryGetValue(gdiName, out string? friendly))
                        continue;

                    // Try to get a better friendly name from target
                    var targetName = new NativeMethods.DISPLAYCONFIG_TARGET_DEVICE_NAME();
                    targetName.header.type = NativeMethods.DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME;
                    targetName.header.size = Marshal.SizeOf<NativeMethods.DISPLAYCONFIG_TARGET_DEVICE_NAME>();
                    targetName.header.adapterId = path.targetInfo.adapterId;
                    targetName.header.id = path.targetInfo.id;

                    if (NativeMethods.DisplayConfigGetDeviceInfo(ref targetName) == 0
                        && !string.IsNullOrWhiteSpace(targetName.monitorFriendlyDeviceName))
                    {
                        friendly = targetName.monitorFriendlyDeviceName;
                    }

                    // Skip if we already have this device name (duplicate active paths)
                    if (monitors.Any(m => m.DeviceName == gdiName))
                        continue;

                    monitors.Add(new MonitorInfo
                    {
                        DeviceName = gdiName,
                        FriendlyName = friendly,
                        AdapterLuidValue = path.sourceInfo.adapterId.ToInt64(),
                        SourceId = path.sourceInfo.id,
                    });
                }
            }
        }

        // Fallback: if DisplayConfig failed, use GDI-only data (no scaling support)
        foreach (var kvp in gdiDevices)
        {
            if (!monitors.Any(m => m.DeviceName == kvp.Key))
            {
                monitors.Add(new MonitorInfo
                {
                    DeviceName = kvp.Key,
                    FriendlyName = kvp.Value,
                });
            }
        }

        return monitors;
    }

    /// <summary>
    /// Get all supported display modes for a given monitor.
    /// </summary>
    public List<DisplayMode> GetSupportedModes(string deviceName)
    {
        var modes = new List<DisplayMode>();
        var devMode = new NativeMethods.DEVMODE();
        devMode.dmSize = (short)Marshal.SizeOf<NativeMethods.DEVMODE>();

        for (uint i = 0; NativeMethods.EnumDisplaySettingsExW(deviceName, i, ref devMode, 0); i++)
        {
            var mode = new DisplayMode(
                devMode.dmPelsWidth, devMode.dmPelsHeight,
                devMode.dmDisplayFrequency, 0, devMode.dmBitsPerPel);

            // Deduplicate (ignore bits-per-pixel variants for the same resolution)
            if (!modes.Any(m => m.Width == mode.Width && m.Height == mode.Height && m.RefreshRate == mode.RefreshRate))
                modes.Add(mode);

            devMode = new NativeMethods.DEVMODE();
            devMode.dmSize = (short)Marshal.SizeOf<NativeMethods.DEVMODE>();
        }

        return modes.OrderByDescending(m => m.Width)
                     .ThenByDescending(m => m.Height)
                     .ThenByDescending(m => m.RefreshRate)
                     .ToList();
    }

    /// <summary>
    /// Get the current display mode for a monitor.
    /// </summary>
    public DisplayMode? GetCurrentMode(string deviceName)
    {
        var devMode = new NativeMethods.DEVMODE();
        devMode.dmSize = (short)Marshal.SizeOf<NativeMethods.DEVMODE>();

        if (!NativeMethods.EnumDisplaySettingsExW(deviceName, NativeMethods.ENUM_CURRENT_SETTINGS, ref devMode, 0))
            return null;

        return new DisplayMode(
            devMode.dmPelsWidth, devMode.dmPelsHeight,
            devMode.dmDisplayFrequency, 0, devMode.dmBitsPerPel);
    }

    /// <summary>
    /// Get the screen bounds (position and size in virtual screen coordinates) for a monitor.
    /// </summary>
    public (int X, int Y, int Width, int Height)? GetMonitorBounds(string deviceName)
    {
        var devMode = new NativeMethods.DEVMODE();
        devMode.dmSize = (short)Marshal.SizeOf<NativeMethods.DEVMODE>();

        if (!NativeMethods.EnumDisplaySettingsExW(deviceName, NativeMethods.ENUM_CURRENT_SETTINGS, ref devMode, 0))
            return null;

        return (devMode.dmPositionX, devMode.dmPositionY, devMode.dmPelsWidth, devMode.dmPelsHeight);
    }

    /// <summary>
    /// Apply a resolution (width, height, refresh rate) to the specified monitor.
    /// Does NOT change DPI scaling — use ScalingService for that.
    /// </summary>
    public DisplayChangeResult ApplyResolution(string deviceName, DisplayMode mode)
    {
        var devMode = new NativeMethods.DEVMODE();
        devMode.dmSize = (short)Marshal.SizeOf<NativeMethods.DEVMODE>();
        devMode.dmPelsWidth = mode.Width;
        devMode.dmPelsHeight = mode.Height;
        devMode.dmDisplayFrequency = mode.RefreshRate;
        devMode.dmBitsPerPel = mode.BitsPerPixel;
        devMode.dmFields = NativeMethods.DM_PELSWIDTH | NativeMethods.DM_PELSHEIGHT
                         | NativeMethods.DM_DISPLAYFREQUENCY | NativeMethods.DM_BITSPERPEL;

        int result = NativeMethods.ChangeDisplaySettingsExW(
            deviceName, ref devMode, IntPtr.Zero,
            NativeMethods.CDS_UPDATEREGISTRY | NativeMethods.CDS_NORESET,
            IntPtr.Zero);

        if (result == NativeMethods.DISP_CHANGE_SUCCESSFUL)
        {
            // Apply the change (CDS_NORESET defers it until this call)
            NativeMethods.ChangeDisplaySettingsExW(null, IntPtr.Zero, IntPtr.Zero, 0, IntPtr.Zero);
            return DisplayChangeResult.Success;
        }

        return result switch
        {
            NativeMethods.DISP_CHANGE_BADMODE => DisplayChangeResult.UnsupportedMode,
            NativeMethods.DISP_CHANGE_FAILED => DisplayChangeResult.Failed,
            NativeMethods.DISP_CHANGE_RESTART => DisplayChangeResult.RestartRequired,
            _ => DisplayChangeResult.Failed,
        };
    }
}

public enum DisplayChangeResult
{
    Success,
    UnsupportedMode,
    Failed,
    RestartRequired,
}
