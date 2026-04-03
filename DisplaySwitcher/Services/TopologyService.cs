namespace DisplaySwitcher.Services;

using DisplaySwitcher.Interop;
using DisplaySwitcher.Models;

/// <summary>
/// Controls display topology (extend, clone, internal-only, external-only)
/// via the SetDisplayConfig API.
/// </summary>
public sealed class TopologyService
{
    /// <summary>
    /// Set the display topology. Uses SetDisplayConfig with SDC_APPLY and the
    /// appropriate topology flag.
    /// </summary>
    public TopologyResult SetTopology(TopologyMode mode)
    {
        uint flag = mode switch
        {
            TopologyMode.Extend => NativeMethods.SDC_TOPOLOGY_EXTEND,
            TopologyMode.Clone => NativeMethods.SDC_TOPOLOGY_CLONE,
            TopologyMode.InternalOnly => NativeMethods.SDC_TOPOLOGY_INTERNAL,
            TopologyMode.ExternalOnly => NativeMethods.SDC_TOPOLOGY_EXTERNAL,
            _ => 0
        };

        if (flag == 0)
            return new TopologyResult(false, "Unknown topology mode.");

        int result = NativeMethods.SetDisplayConfig(0, IntPtr.Zero, 0, IntPtr.Zero,
            NativeMethods.SDC_APPLY | flag);

        System.Diagnostics.Debug.WriteLine(
            $"[Topology] SetDisplayConfig({mode}) → result={result}");

        return result == 0
            ? new TopologyResult(true, $"Topology set to {mode}.")
            : new TopologyResult(false, $"SetDisplayConfig failed with error code {result}.");
    }
}

public record TopologyResult(bool Success, string Message);
