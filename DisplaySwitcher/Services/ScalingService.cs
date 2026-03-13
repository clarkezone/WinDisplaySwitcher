namespace DisplaySwitcher.Services;

using DisplaySwitcher.Interop;
using DisplaySwitcher.Models;
using System.Runtime.InteropServices;

/// <summary>
/// Gets and sets per-monitor DPI scaling using the undocumented DisplayConfig DPI APIs.
/// </summary>
public sealed class ScalingService
{
    private static readonly int[] ScaleTable = NativeMethods.ScalePercentages;

    /// <summary>
    /// Get the current DPI scale percentage for a monitor.
    /// Returns 0 if the query fails.
    /// </summary>
    public int GetCurrentScale(MonitorInfo monitor)
    {
        var dpiInfo = GetDpiScaleInfo(monitor);
        if (dpiInfo == null) return 0;

        int recommendedIndex = FindRecommendedIndex(dpiInfo.Value);
        int currentIndex = recommendedIndex + dpiInfo.Value.curScaleRel;

        if (currentIndex >= 0 && currentIndex < ScaleTable.Length)
            return ScaleTable[currentIndex];

        return 0;
    }

    /// <summary>
    /// Get all supported DPI scale percentages for a monitor.
    /// </summary>
    public int[] GetSupportedScales(MonitorInfo monitor)
    {
        var dpiInfo = GetDpiScaleInfo(monitor);
        if (dpiInfo == null) return [100];

        int recommendedIndex = FindRecommendedIndex(dpiInfo.Value);
        int minIndex = recommendedIndex + dpiInfo.Value.minScaleRel;
        int maxIndex = recommendedIndex + dpiInfo.Value.maxScaleRel;

        minIndex = Math.Max(0, minIndex);
        maxIndex = Math.Min(ScaleTable.Length - 1, maxIndex);

        var scales = new List<int>();
        for (int i = minIndex; i <= maxIndex; i++)
            scales.Add(ScaleTable[i]);

        return scales.ToArray();
    }

    /// <summary>
    /// Set the DPI scale for a monitor.
    /// </summary>
    public bool SetScale(MonitorInfo monitor, int scalePercent)
    {
        var dpiInfo = GetDpiScaleInfo(monitor);
        if (dpiInfo == null) return false;

        int recommendedIndex = FindRecommendedIndex(dpiInfo.Value);
        int targetIndex = Array.IndexOf(ScaleTable, scalePercent);
        if (targetIndex < 0) return false;

        int relativeStep = targetIndex - recommendedIndex;

        // Validate within monitor's supported range
        if (relativeStep < dpiInfo.Value.minScaleRel || relativeStep > dpiInfo.Value.maxScaleRel)
            return false;

        var setPacket = new NativeMethods.DISPLAYCONFIG_SOURCE_DPI_SCALE_SET();
        setPacket.header.type = NativeMethods.DISPLAYCONFIG_DEVICE_INFO_SET_DPI_SCALE;
        setPacket.header.size = Marshal.SizeOf<NativeMethods.DISPLAYCONFIG_SOURCE_DPI_SCALE_SET>();
        setPacket.header.adapterId = NativeMethods.LUID.FromInt64(monitor.AdapterLuidValue);
        setPacket.header.id = monitor.SourceId;
        setPacket.scaleRel = relativeStep;

        int result = NativeMethods.DisplayConfigSetDeviceInfo(ref setPacket);
        return result == 0;
    }

    /// <summary>
    /// Populate a MonitorInfo's scale-related fields (min, max, recommended, supported list).
    /// </summary>
    public void PopulateScaleInfo(MonitorInfo monitor)
    {
        var dpiInfo = GetDpiScaleInfo(monitor);
        if (dpiInfo == null)
        {
            monitor.SupportedScales = [100];
            monitor.MinScalePercent = 100;
            monitor.MaxScalePercent = 100;
            monitor.RecommendedScalePercent = 100;
            return;
        }

        int recommendedIndex = FindRecommendedIndex(dpiInfo.Value);
        monitor.RecommendedScalePercent = ScaleTable[Math.Clamp(recommendedIndex, 0, ScaleTable.Length - 1)];

        int minIndex = Math.Max(0, recommendedIndex + dpiInfo.Value.minScaleRel);
        int maxIndex = Math.Min(ScaleTable.Length - 1, recommendedIndex + dpiInfo.Value.maxScaleRel);

        monitor.MinScalePercent = ScaleTable[minIndex];
        monitor.MaxScalePercent = ScaleTable[maxIndex];

        var scales = new List<int>();
        for (int i = minIndex; i <= maxIndex; i++)
            scales.Add(ScaleTable[i]);
        monitor.SupportedScales = scales.ToArray();
    }

    private NativeMethods.DISPLAYCONFIG_SOURCE_DPI_SCALE_GET? GetDpiScaleInfo(MonitorInfo monitor)
    {
        var getPacket = new NativeMethods.DISPLAYCONFIG_SOURCE_DPI_SCALE_GET();
        getPacket.header.type = NativeMethods.DISPLAYCONFIG_DEVICE_INFO_GET_DPI_SCALE;
        getPacket.header.size = Marshal.SizeOf<NativeMethods.DISPLAYCONFIG_SOURCE_DPI_SCALE_GET>();
        getPacket.header.adapterId = NativeMethods.LUID.FromInt64(monitor.AdapterLuidValue);
        getPacket.header.id = monitor.SourceId;

        int result = NativeMethods.DisplayConfigGetDeviceInfo(ref getPacket);
        if (result != 0) return null;

        return getPacket;
    }

    /// <summary>
    /// Determine the recommended DPI index. The recommended is at relative step 0.
    /// We find it by: recommendedIndex = -minScaleRel (since min is 0 or negative,
    /// and min percentage index = recommendedIndex + minScaleRel).
    /// Actually, the minScaleRel is typically negative, meaning the recommended index
    /// is at position |minScaleRel| from the absolute minimum.
    ///
    /// For a 4K display: minScaleRel=-2, maxScaleRel=5, recommended=150% (index 2).
    /// recommendedIndex = 0 - minScaleRel = 0 - (-2) = 2 → ScaleTable[2] = 150%. Correct.
    ///
    /// For a 1080p display: minScaleRel=0, maxScaleRel=3, recommended=100% (index 0).
    /// recommendedIndex = 0 - 0 = 0 → ScaleTable[0] = 100%. Correct.
    /// </summary>
    private static int FindRecommendedIndex(NativeMethods.DISPLAYCONFIG_SOURCE_DPI_SCALE_GET dpiInfo)
    {
        // The minimum absolute index in the table, when minScaleRel <= 0:
        // absoluteMinIndex = recommendedIndex + minScaleRel
        // The absolute minimum should be >= 0, so:
        // recommendedIndex = absoluteMinIndex - minScaleRel
        // If we assume absoluteMinIndex can be 0 (100%), that gives: recommendedIndex = -minScaleRel
        // But this isn't always true. More reliable: iterate to find where curScaleRel=0 maps.
        // Since we can't query recommended directly, we use: recommendedIndex = -minScaleRel
        // because Windows always allows scaling down to 100% (index 0) as minimum.
        return -dpiInfo.minScaleRel;
    }
}
