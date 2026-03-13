namespace DisplaySwitcher.Interop;

using System.Runtime.InteropServices;

/// <summary>P/Invoke declarations for system tray icon and context menus.</summary>
internal static partial class NativeMethods
{
    // Shell_NotifyIcon messages
    public const int NIM_ADD = 0x00000000;
    public const int NIM_MODIFY = 0x00000001;
    public const int NIM_DELETE = 0x00000002;
    public const int NIM_SETVERSION = 0x00000004;

    // NOTIFYICONDATA flags
    public const int NIF_MESSAGE = 0x00000001;
    public const int NIF_ICON = 0x00000002;
    public const int NIF_TIP = 0x00000004;
    public const int NIF_INFO = 0x00000010;
    public const int NIF_SHOWTIP = 0x00000080;

    public const int NOTIFYICON_VERSION_4 = 4;

    // Tray callback messages
    public const int WM_TRAYICON = 0x8000; // WM_APP
    public const int WM_COMMAND = 0x0111;
    public const int WM_DESTROY = 0x0002;

    // Mouse messages (sent as lParam in WM_TRAYICON with version 4)
    public const int WM_LBUTTONUP = 0x0202;
    public const int WM_RBUTTONUP = 0x0205;
    public const int WM_CONTEXTMENU = 0x007B;
    public const int NIN_SELECT = 0x0400;
    public const int NIN_KEYSELECT = 0x0401;

    // Menu flags
    public const int MF_STRING = 0x00000000;
    public const int MF_SEPARATOR = 0x00000800;
    public const int MF_POPUP = 0x00000010;
    public const int MF_CHECKED = 0x00000008;
    public const int MF_UNCHECKED = 0x00000000;
    public const int MF_GRAYED = 0x00000001;

    public const int TPM_LEFTALIGN = 0x0000;
    public const int TPM_RIGHTBUTTON = 0x0002;
    public const int TPM_RETURNCMD = 0x0100;
    public const int TPM_NONOTIFY = 0x0080;

    public const int IMAGE_ICON = 1;
    public const int LR_LOADFROMFILE = 0x00000010;
    public const int LR_DEFAULTSIZE = 0x00000040;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public int dwState;
        public int dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public int dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WNDCLASSEX
    {
        public int cbSize;
        public int style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? lpszMenuName;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }

    public delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("user32.dll", EntryPoint = "RegisterClassExW", CharSet = CharSet.Unicode)]
    public static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

    [DllImport("user32.dll", EntryPoint = "CreateWindowExW", CharSet = CharSet.Unicode)]
    public static extern IntPtr CreateWindowEx(
        int dwExStyle, string lpClassName, string lpWindowName, int dwStyle,
        int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll", EntryPoint = "DefWindowProcW")]
    public static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", EntryPoint = "AppendMenuW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool AppendMenu(IntPtr hMenu, int uFlags, IntPtr uIDNewItem, string? lpNewItem);

    [DllImport("user32.dll")]
    public static extern int TrackPopupMenuEx(
        IntPtr hMenu, uint fuFlags, int x, int y, IntPtr hwnd, IntPtr lptpm);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll", EntryPoint = "LoadImageW", CharSet = CharSet.Unicode)]
    public static extern IntPtr LoadImage(
        IntPtr hInst, string name, int type, int cx, int cy, int fuLoad);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", CharSet = CharSet.Unicode)]
    public static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    public static extern void PostQuitMessage(int nExitCode);

    [DllImport("user32.dll", EntryPoint = "PostMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PostMessageW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", EntryPoint = "GetMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetMessageW(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll", EntryPoint = "DispatchMessageW")]
    public static extern IntPtr DispatchMessageW(ref MSG lpMsg);

    [DllImport("user32.dll", EntryPoint = "LoadIconW")]
    public static extern IntPtr LoadIconW(IntPtr hInstance, IntPtr lpIconName);

    // Convenience aliases matching the names used by services
    public static bool Shell_NotifyIconW(int dwMessage, ref NOTIFYICONDATA lpData) => Shell_NotifyIcon(dwMessage, ref lpData);
    public static bool AppendMenuW(IntPtr hMenu, uint uFlags, uint uIDNewItem, string? lpNewItem) =>
        AppendMenu(hMenu, (int)uFlags, (IntPtr)uIDNewItem, lpNewItem);
    public static IntPtr LoadImageW(IntPtr hInst, string name, int type, int cx, int cy, int fuLoad) =>
        LoadImage(hInst, name, type, cx, cy, fuLoad);
    public static IntPtr DefWindowProcW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam) =>
        DefWindowProc(hWnd, msg, wParam, lParam);

    /// <summary>Creates a hidden message-only window for tray icon and hotkey callbacks.</summary>
    public static IntPtr CreateMessageWindow(string className, WndProc wndProc)
    {
        IntPtr hInstance = GetModuleHandle(null);

        var wc = new WNDCLASSEX
        {
            cbSize = Marshal.SizeOf<WNDCLASSEX>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(wndProc),
            hInstance = hInstance,
            lpszClassName = className
        };

        ushort atom = RegisterClassEx(ref wc);
        if (atom == 0) return IntPtr.Zero;

        // HWND_MESSAGE (-3) as parent creates a message-only window
        IntPtr hwnd = CreateWindowEx(0, className, className, 0,
            0, 0, 0, 0, new IntPtr(-3), IntPtr.Zero, hInstance, IntPtr.Zero);

        return hwnd;
    }

    /// <summary>Creates and initializes a NOTIFYICONDATA struct.</summary>
    public static NOTIFYICONDATA CreateNotifyIconData(IntPtr hwnd, IntPtr hIcon, string tip)
    {
        var nid = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = hwnd,
            uID = 1,
            uFlags = (uint)(NIF_MESSAGE | NIF_ICON | NIF_TIP | NIF_SHOWTIP),
            uCallbackMessage = (uint)WM_TRAYICON,
            hIcon = hIcon,
            szTip = tip,
            szInfo = string.Empty,
            szInfoTitle = string.Empty,
            uVersion = NOTIFYICON_VERSION_4
        };
        return nid;
    }
}
