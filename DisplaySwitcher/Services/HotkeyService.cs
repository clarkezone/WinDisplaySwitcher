namespace DisplaySwitcher.Services;

using DisplaySwitcher.Interop;
using DisplaySwitcher.Models;

/// <summary>
/// Registers and manages global hotkeys. Dispatches WM_HOTKEY to profile application.
/// </summary>
public sealed class HotkeyService : IDisposable
{
    private const int WM_HOTKEY = 0x0312;

    private readonly SettingsService _settingsService;
    private readonly ProfileApplyService _profileApplyService;
    private readonly Dictionary<int, ResolutionProfile> _registeredHotkeys = new();
    private IntPtr _hwnd;
    private int _nextId = 1;

    public HotkeyService(SettingsService settingsService, ProfileApplyService profileApplyService)
    {
        _settingsService = settingsService;
        _profileApplyService = profileApplyService;
    }

    /// <summary>
    /// Initialize hotkey listening using an existing message window HWND.
    /// Call this with the TrayIconService's Hwnd.
    /// </summary>
    public void Initialize(IntPtr hwnd)
    {
        _hwnd = hwnd;
        RegisterAll();
    }

    /// <summary>
    /// Register hotkeys for all profiles that have a hotkey assigned.
    /// </summary>
    public void RegisterAll()
    {
        UnregisterAll();

        foreach (var profile in _settingsService.Settings.Profiles)
        {
            if (!profile.HasHotkey) continue;

            int id = _nextId++;
            if (NativeMethods.RegisterHotKey(_hwnd, id, profile.HotkeyModifiers, profile.HotkeyVk))
            {
                _registeredHotkeys[id] = profile;
            }
        }
    }

    /// <summary>
    /// Unregister all currently registered hotkeys.
    /// </summary>
    public void UnregisterAll()
    {
        foreach (var id in _registeredHotkeys.Keys)
        {
            NativeMethods.UnregisterHotKey(_hwnd, id);
        }
        _registeredHotkeys.Clear();
    }

    /// <summary>
    /// Re-register hotkeys after settings change.
    /// </summary>
    public void Refresh()
    {
        RegisterAll();
    }

    /// <summary>
    /// Process a WM_HOTKEY message by hotkey ID. Returns true if handled.
    /// Call this from the message window's WndProc.
    /// </summary>
    public void ProcessHotkey(int hotkeyId)
    {
        if (_registeredHotkeys.TryGetValue(hotkeyId, out var profile))
        {
            _ = Task.Run(() => _profileApplyService.Apply(profile));
        }
    }

    public void Dispose()
    {
        UnregisterAll();
    }
}
