namespace DisplaySwitcher.Services;

using DisplaySwitcher.Interop;
using DisplaySwitcher.Models;
using System.Runtime.InteropServices;

/// <summary>
/// Manages the system tray icon and context menu using Win32 APIs.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private const int WM_TRAYICON = 0x8000; // WM_APP
    private const int WM_COMMAND = 0x0111;
    private const uint MF_STRING = 0x0000;
    private const uint MF_SEPARATOR = 0x0800;
    private const uint MF_POPUP = 0x0010;
    private const uint TPM_BOTTOMALIGN = 0x0020;
    private const uint TPM_LEFTALIGN = 0x0000;
    private const uint TPM_RIGHTBUTTON = 0x0002;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_RBUTTONUP = 0x0205;
    private const int WM_DESTROY = 0x0002;

    private const int MENU_ID_SETTINGS = 9000;
    private const int MENU_ID_EXIT = 9001;
    private const int MENU_ID_PROFILE_BASE = 1000;

    private IntPtr _hwnd;
    private IntPtr _hIcon;
    private bool _iconAdded;
    private NativeMethods.WndProc? _wndProcDelegate; // prevent GC
    private readonly SettingsService _settingsService;
    private readonly ProfileApplyService _profileApplyService;
    private List<ResolutionProfile> _menuProfiles = new();

    public event Action? SettingsRequested;
    public event Action? ExitRequested;

    /// <summary>
    /// Optional callback for WM_HOTKEY messages — set by App to bridge to HotkeyService.
    /// Signature: (int hotkeyId) => void
    /// </summary>
    public Action<int>? HotkeyHandler { get; set; }

    public IntPtr Hwnd => _hwnd;

    public TrayIconService(SettingsService settingsService, ProfileApplyService profileApplyService)
    {
        _settingsService = settingsService;
        _profileApplyService = profileApplyService;
    }

    public void Initialize()
    {
        _wndProcDelegate = WndProc;

        _hwnd = NativeMethods.CreateMessageWindow("DisplaySwitcherMsg", _wndProcDelegate);
        if (_hwnd == IntPtr.Zero)
            throw new InvalidOperationException("Failed to create message window.");

        _hIcon = LoadTrayIcon();
        AddTrayIcon();
    }

    public void RefreshMenu()
    {
        // Menu is built on-demand when right-clicked
    }

    private void AddTrayIcon()
    {
        var nid = NativeMethods.CreateNotifyIconData(_hwnd, _hIcon, "DisplaySwitcher");
        NativeMethods.Shell_NotifyIconW(NativeMethods.NIM_ADD, ref nid);
        _iconAdded = true;
    }

    private void RemoveTrayIcon()
    {
        if (!_iconAdded) return;

        var nid = NativeMethods.CreateNotifyIconData(_hwnd, IntPtr.Zero, string.Empty);
        NativeMethods.Shell_NotifyIconW(NativeMethods.NIM_DELETE, ref nid);
        _iconAdded = false;
    }

    private void ShowContextMenu()
    {
        IntPtr hMenu = NativeMethods.CreatePopupMenu();
        if (hMenu == IntPtr.Zero) return;

        try
        {
            _menuProfiles = _settingsService.Settings.Profiles;

            // Group profiles by monitor
            var grouped = _menuProfiles
                .Select((p, i) => (Profile: p, Index: i))
                .GroupBy(x => x.Profile.MonitorName);

            foreach (var group in grouped)
            {
                // Add monitor header as sub-menu or separator with label
                NativeMethods.AppendMenuW(hMenu, MF_SEPARATOR, 0, null);

                foreach (var item in group)
                {
                    string label = item.Profile.DisplayLabel;
                    if (item.Profile.HasHotkey)
                        label += $"  ({item.Profile.HotkeyDisplayString})";

                    NativeMethods.AppendMenuW(hMenu, MF_STRING,
                        (uint)(MENU_ID_PROFILE_BASE + item.Index), label);
                }
            }

            NativeMethods.AppendMenuW(hMenu, MF_SEPARATOR, 0, null);
            NativeMethods.AppendMenuW(hMenu, MF_STRING, MENU_ID_SETTINGS, "Settings…");
            NativeMethods.AppendMenuW(hMenu, MF_STRING, MENU_ID_EXIT, "Exit");

            // Required: set foreground so menu dismisses properly
            NativeMethods.SetForegroundWindow(_hwnd);

            NativeMethods.GetCursorPos(out var pt);
            NativeMethods.TrackPopupMenuEx(hMenu, TPM_LEFTALIGN | TPM_BOTTOMALIGN | TPM_RIGHTBUTTON,
                pt.x, pt.y, _hwnd, IntPtr.Zero);

            // Post empty message to force menu to close (Win32 quirk)
            NativeMethods.PostMessageW(_hwnd, 0, IntPtr.Zero, IntPtr.Zero);
        }
        finally
        {
            NativeMethods.DestroyMenu(hMenu);
        }
    }

    private IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case WM_TRAYICON:
                int mouseMsg = (int)(lParam & 0xFFFF);
                if (mouseMsg == WM_RBUTTONUP || mouseMsg == WM_LBUTTONUP)
                {
                    ShowContextMenu();
                }
                return IntPtr.Zero;

            case WM_COMMAND:
                int menuId = (int)(wParam & 0xFFFF);
                HandleMenuCommand(menuId);
                return IntPtr.Zero;

            case NativeMethods.WM_HOTKEY:
                HotkeyHandler?.Invoke((int)wParam);
                return IntPtr.Zero;
        }

        return NativeMethods.DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    private void HandleMenuCommand(int menuId)
    {
        if (menuId == MENU_ID_SETTINGS)
        {
            SettingsRequested?.Invoke();
        }
        else if (menuId == MENU_ID_EXIT)
        {
            ExitRequested?.Invoke();
        }
        else if (menuId >= MENU_ID_PROFILE_BASE)
        {
            int idx = menuId - MENU_ID_PROFILE_BASE;
            if (idx >= 0 && idx < _menuProfiles.Count)
            {
                var profile = _menuProfiles[idx];
                _ = Task.Run(() => _profileApplyService.Apply(profile));
            }
        }
    }

    private IntPtr LoadTrayIcon()
    {
        // Try to load from Assets folder first
        string icoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "tray.ico");
        if (File.Exists(icoPath))
        {
            IntPtr hIcon = NativeMethods.LoadImageW(IntPtr.Zero, icoPath,
                1 /*IMAGE_ICON*/, 16, 16, 0x0010 /*LR_LOADFROMFILE*/);
            if (hIcon != IntPtr.Zero) return hIcon;
        }

        // Fallback to a stock system icon
        return NativeMethods.LoadIconW(IntPtr.Zero, new IntPtr(32516)); // IDI_INFORMATION
    }

    public void Dispose()
    {
        RemoveTrayIcon();
        if (_hwnd != IntPtr.Zero)
        {
            NativeMethods.DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
        }
    }
}
