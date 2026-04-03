namespace DisplaySwitcher.Services;

using DisplaySwitcher.Interop;
using DisplaySwitcher.Models;
using System.Runtime.InteropServices;

/// <summary>
/// Registers and manages global hotkeys on a dedicated background thread
/// with its own Win32 message pump.
///
/// Uses RegisterHotKey with HWND=NULL so WM_HOTKEY is posted to the
/// thread's message queue. The GetMessage loop handles messages directly
/// without DispatchMessage — matching the proven pattern from WindowsStack.
/// Cross-thread communication uses PostThreadMessage.
/// </summary>
public sealed class HotkeyService : IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly ProfileApplyService _profileApplyService;
    private readonly Dictionary<int, CompositeProfile> _registeredHotkeys = new();
    private int _nextId = 1;
    private Thread? _thread;
    private uint _threadId;
    private volatile bool _initialized;
    private readonly ManualResetEventSlim _readyEvent = new(false);

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
        _thread.Start();
        _readyEvent.Wait(5000);
    }

    private void MessageLoop()
    {
        // Capture this thread's ID for PostThreadMessage from other threads
        _threadId = NativeMethods.GetCurrentThreadId();

        // Register all current hotkeys (must be done on this thread)
        RegisterAllInternal();
        _initialized = true;
        _readyEvent.Set();

        // Message pump — handle messages directly, no DispatchMessage needed.
        // RegisterHotKey(NULL) posts WM_HOTKEY to the thread queue.
        // PostThreadMessage posts custom messages to the thread queue.
        // GetMessage retrieves both.
        while (NativeMethods.GetMessageInt(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            if (msg.message == (uint)NativeMethods.WM_HOTKEY)
            {
                ProcessHotkey((int)msg.wParam);
            }
            else if (msg.message == NativeMethods.WM_USER_REFRESH_HOTKEYS)
            {
                RegisterAllInternal();
            }
            else if (msg.message == NativeMethods.WM_USER_QUIT_HOTKEYS)
            {
                UnregisterAllInternal();
                NativeMethods.PostQuitMessage(0);
            }
        }
    }

    /// <summary>
    /// Re-register hotkeys after settings change. Thread-safe: posts a message
    /// to the hotkey thread so registration runs on the correct thread.
    /// </summary>
    public void Refresh()
    {
        if (_initialized && _threadId != 0)
            NativeMethods.PostThreadMessage(_threadId, NativeMethods.WM_USER_REFRESH_HOTKEYS, IntPtr.Zero, IntPtr.Zero);
    }

    // Called only on the hotkey thread
    private void RegisterAllInternal()
    {
        UnregisterAllInternal();

        foreach (var profile in _settingsService.Settings.Profiles)
        {
            if (!profile.HasHotkey) continue;

            int id = _nextId++;
            int mods = profile.HotkeyModifiers | NativeMethods.MOD_NOREPEAT;
            // Register with HWND=NULL — WM_HOTKEY posted to this thread's queue
            if (NativeMethods.RegisterHotKey(IntPtr.Zero, id, mods, profile.HotkeyVk))
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
            NativeMethods.UnregisterHotKey(IntPtr.Zero, id);
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
        if (_initialized && _threadId != 0)
        {
            NativeMethods.PostThreadMessage(_threadId, NativeMethods.WM_USER_QUIT_HOTKEYS, IntPtr.Zero, IntPtr.Zero);
        }
        _thread?.Join(3000);
        _readyEvent.Dispose();
    }
}
