namespace DisplaySwitcher.Interop;

using System.Runtime.InteropServices;

/// <summary>
/// P/Invoke declarations for display topology control via SetDisplayConfig.
/// </summary>
internal static partial class NativeMethods
{
    // SetDisplayConfig topology flags
    public const uint SDC_TOPOLOGY_INTERNAL = 0x00000001;
    public const uint SDC_TOPOLOGY_CLONE    = 0x00000002;
    public const uint SDC_TOPOLOGY_EXTEND   = 0x00000004;
    public const uint SDC_TOPOLOGY_EXTERNAL = 0x00000008;
    public const uint SDC_APPLY             = 0x00000080;

    [DllImport("user32.dll")]
    public static extern int SetDisplayConfig(
        uint numPathArrayElements,
        IntPtr pathArray,
        uint numModeInfoArrayElements,
        IntPtr modeInfoArray,
        uint flags);
}
