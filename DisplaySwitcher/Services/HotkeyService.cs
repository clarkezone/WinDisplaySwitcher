namespace DisplaySwitcher.Services;

using DisplaySwitcher.Interop;
using DisplaySwitcher.Models;
using System.Runtime.InteropServices;

/// <summary>
/// Registers and manages global hotkeys on a dedicated background thread
/// with its own Win32 message pump, ensuring WM_HOTKEY messages are
/// always dispatched regardless of WinUI 3's message loop behavior.
/// </summary>
public sealed class HotkeyService : IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly ProfileApplyService _profileApplyService;
    private readonly Dictionary<int, ResolutionProfile> _registeredHotkeys = new();
    private IntPtr _hwnd;
    private int _nextId = 1;
    private Thread? _thread;
    private volatile bool _initialized;
    private readonly ManualResetEventSlim _readyEvent = new(false);
    private NativeMethods.WndProc? _wndProcDelegate; // prevent GC

    public HotkeyService(SettingsService settingsService, ProfileApplyService profileApplyService)
    {
        _settingsService = settingsService;
        _profileApplyService = profileApplyService;
    }

    /// <summary>
    /// Start the hotkey background thread and register current hotkeys.
    /// </summary>
    public void Initialize()
    {
        _thread = new Thread(MessageLoop) { IsBackground = true, Name = "HotkeyThread" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        _readyEvent.Wait(5000);
    }

    private void MessageLoop()
    {
        _wndProcDelegate = WndProc;
        _hwnd = NativeMethods.CreateMessageWindow("DisplaySwitcherHotkey", _wndProcDelegate);
        _initialized = _hwnd != IntPtr.Zero;

        if (_initialized)
            RegisterAllInternal();

        _readyEvent.Set();

        if (!_initialized) return;

        // Standard Win32 message pump — dispatches WM_HOTKEY and custom messages
        while (NativeMethods.GetMessageInt(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            NativeMethods.TranslateMessage(ref msg);
            NativeMethods.DispatchMessageW(ref msg);
        }

        // After WM_QUIT, clean up the window
        NativeMethods.DestroyWindow(_hwnd);
        _hwnd = IntPtr.Zero;
    }

    private IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case (uint)NativeMethods.WM_HOTKEY:
                ProcessHotkey((int)wParam);
                return IntPtr.Zero;

            case NativeMethods.WM_USER_REFRESH_HOTKEYS:
                RegisterAllInternal();
                return IntPtr.Zero;

            case NativeMethods.WM_USER_QUIT_HOTKEYS:
                UnregisterAllInternal();
                NativeMethods.PostQuitMessage(0);
                return IntPtr.Zero;
        }

        return NativeMethods.DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    /// <summary>
    /// Re-register hotkeys after settings change. Thread-safe: posts a message
    /// to the hotkey thread so registration runs on the correct thread.
    /// </summary>
    public void Refresh()
    {
        if (_initialized && _hwnd != IntPtr.Zero)
            NativeMethods.PostMessageW(_hwnd, NativeMethods.WM_USER_REFRESH_HOTKEYS, IntPtr.Zero, IntPtr.Zero);
    }

    // Called only on the hotkey thread
    private void RegisterAllInternal()
    {
        UnregisterAllInternal();

        foreach (var profile in _settingsService.Settings.Profiles)
        {
            if (!profile.HasHotkey) continue;

            int id = _nextId++;
            // MOD_NOREPEAT prevents auto-repeat from firing repeatedly
            int mods = profile.HotkeyModifiers | NativeMethods.MOD_NOREPEAT;
            if (NativeMethods.RegisterHotKey(_hwnd, id, mods, profile.HotkeyVk))
            {
                _registeredHotkeys[id] = profile;
                System.Diagnostics.Debug.WriteLine(
                    $"[Hotkey] Registered id={id} mods=0x{mods:X} vk=0x{profile.HotkeyVk:X} ({profile.HotkeyDisplayString})");
            }
            else
            {
                int err = Marshal.GetLastWin32Error();
                System.Diagnostics.Debug.WriteLine(
                    $"[Hotkey] FAILED to register {profile.HotkeyDisplayString} — error {err}");
            }
        }
    }

    // Called only on the hotkey thread
    private void UnregisterAllInternal()
    {
        foreach (var id in _registeredHotkeys.Keys)
        {
            NativeMethods.UnregisterHotKey(_hwnd, id);
        }
        _registeredHotkeys.Clear();
    }

    private void ProcessHotkey(int hotkeyId)
    {
        if (_registeredHotkeys.TryGetValue(hotkeyId, out var profile))
        {
            System.Diagnostics.Debug.WriteLine($"[Hotkey] Fired: {profile.HotkeyDisplayString} → applying");
            _ = Task.Run(() => _profileApplyService.Apply(profile));
        }
    }

    public void Dispose()
    {
        if (_initialized && _hwnd != IntPtr.Zero)
        {
            NativeMethods.PostMessageW(_hwnd, NativeMethods.WM_USER_QUIT_HOTKEYS, IntPtr.Zero, IntPtr.Zero);
        }
        _thread?.Join(3000);
        _readyEvent.Dispose();
    }
}
