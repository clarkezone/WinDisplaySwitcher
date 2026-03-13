namespace DisplaySwitcher.Interop;

using System.Runtime.InteropServices;

/// <summary>P/Invoke declarations for global hotkey registration.</summary>
internal static partial class NativeMethods
{
    public const int WM_HOTKEY = 0x0312;

    // Custom message IDs for cross-thread communication via PostThreadMessage
    public const uint WM_USER_REFRESH_HOTKEYS = 0x0401;
    public const uint WM_USER_QUIT_HOTKEYS = 0x0402;

    // Modifier flags (matches Models.HotkeyHelper constants)
    public const int MOD_ALT = 0x0001;
    public const int MOD_CONTROL = 0x0002;
    public const int MOD_SHIFT = 0x0004;
    public const int MOD_WIN = 0x0008;
    public const int MOD_NOREPEAT = 0x4000;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    /// <summary>
    /// GetMessage with int return for correct WM_QUIT handling:
    /// returns 0 for WM_QUIT, -1 for error, positive for messages.
    /// </summary>
    [DllImport("user32.dll", EntryPoint = "GetMessageW")]
    public static extern int GetMessageInt(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PostThreadMessage(uint idThread, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    public static extern uint GetCurrentThreadId();
}
