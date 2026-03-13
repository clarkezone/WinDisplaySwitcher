namespace DisplaySwitcher.Interop;

using System.Runtime.InteropServices;

/// <summary>P/Invoke declarations for display enumeration and mode switching.</summary>
internal static partial class NativeMethods
{
    public const int ENUM_CURRENT_SETTINGS = -1;
    public const int ENUM_REGISTRY_SETTINGS = -2;

    public const int DISP_CHANGE_SUCCESSFUL = 0;
    public const int DISP_CHANGE_RESTART = 1;
    public const int DISP_CHANGE_BADMODE = -2;
    public const int DISP_CHANGE_FAILED = -1;

    public const int CDS_UPDATEREGISTRY = 0x00000001;
    public const int CDS_TEST = 0x00000002;
    public const int CDS_SET_PRIMARY = 0x00000010;
    public const int CDS_RESET = 0x40000000;

    public const int DM_BITSPERPEL = 0x00040000;
    public const int DM_PELSWIDTH = 0x00080000;
    public const int DM_PELSHEIGHT = 0x00100000;
    public const int DM_DISPLAYFREQUENCY = 0x00400000;

    public const int DISPLAY_DEVICE_ACTIVE = 0x00000001;
    public const int DISPLAY_DEVICE_ATTACHED_TO_DESKTOP = 0x00000001;
    public const int DISPLAY_DEVICE_PRIMARY_DEVICE = 0x00000004;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;
        public short dmSpecVersion;
        public short dmDriverVersion;
        public short dmSize;
        public short dmDriverExtra;
        public int dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public int dmDisplayOrientation;
        public int dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel;
        public int dmPelsWidth;
        public int dmPelsHeight;
        public int dmDisplayFlags;
        public int dmDisplayFrequency;
        public int dmICMMethod;
        public int dmICMIntent;
        public int dmMediaType;
        public int dmDitherType;
        public int dmReserved1;
        public int dmReserved2;
        public int dmPanningWidth;
        public int dmPanningHeight;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct DISPLAY_DEVICE
    {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;
        public int StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }

    [DllImport("user32.dll", EntryPoint = "EnumDisplayDevicesW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumDisplayDevices(
        string? lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

    [DllImport("user32.dll", EntryPoint = "EnumDisplaySettingsExW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumDisplaySettingsEx(
        string lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode, uint dwFlags);

    [DllImport("user32.dll", EntryPoint = "ChangeDisplaySettingsExW", CharSet = CharSet.Unicode)]
    public static extern int ChangeDisplaySettingsEx(
        string lpszDeviceName, ref DEVMODE lpDevMode, IntPtr hwnd, int dwflags, IntPtr lParam);

    // Overload for null DEVMODE (used to apply deferred changes)
    [DllImport("user32.dll", EntryPoint = "ChangeDisplaySettingsExW", CharSet = CharSet.Unicode)]
    public static extern int ChangeDisplaySettingsExApply(
        string? lpszDeviceName, IntPtr lpDevMode, IntPtr hwnd, int dwflags, IntPtr lParam);

    public const int CDS_NORESET = 0x10000000;

    // Convenience aliases used by services
    public static bool EnumDisplayDevicesW(string? lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags) =>
        EnumDisplayDevices(lpDevice, iDevNum, ref lpDisplayDevice, dwFlags);
    public static bool EnumDisplaySettingsExW(string lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode, uint dwFlags) =>
        EnumDisplaySettingsEx(lpszDeviceName, iModeNum, ref lpDevMode, dwFlags);
    public static bool EnumDisplaySettingsExW(string lpszDeviceName, uint iModeNum, ref DEVMODE lpDevMode, uint dwFlags) =>
        EnumDisplaySettingsEx(lpszDeviceName, (int)iModeNum, ref lpDevMode, dwFlags);
    public static int ChangeDisplaySettingsExW(string deviceName, ref DEVMODE devMode, IntPtr hwnd, int flags, IntPtr lParam) =>
        ChangeDisplaySettingsEx(deviceName, ref devMode, hwnd, flags, lParam);
    public static int ChangeDisplaySettingsExW(string? deviceName, IntPtr devMode, IntPtr hwnd, int flags, IntPtr lParam) =>
        ChangeDisplaySettingsExApply(deviceName, devMode, hwnd, flags, lParam);

    /// <summary>Initializes a DEVMODE struct with correct size fields.</summary>
    public static DEVMODE CreateDevMode()
    {
        var dm = new DEVMODE();
        dm.dmSize = (short)Marshal.SizeOf<DEVMODE>();
        dm.dmDeviceName = string.Empty;
        dm.dmFormName = string.Empty;
        return dm;
    }

    /// <summary>Initializes a DISPLAY_DEVICE struct with correct cb size.</summary>
    public static DISPLAY_DEVICE CreateDisplayDevice()
    {
        var dd = new DISPLAY_DEVICE();
        dd.cb = Marshal.SizeOf<DISPLAY_DEVICE>();
        dd.DeviceName = string.Empty;
        dd.DeviceString = string.Empty;
        dd.DeviceID = string.Empty;
        dd.DeviceKey = string.Empty;
        return dd;
    }
}
