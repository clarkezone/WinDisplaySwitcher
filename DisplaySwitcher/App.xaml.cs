namespace DisplaySwitcher;

using DisplaySwitcher.Interop;
using DisplaySwitcher.Services;
using DisplaySwitcher.Views;
using Microsoft.UI.Xaml;

/// <summary>
/// Application lifecycle: initializes services, tray icon, hotkeys.
/// No main window is shown — the tray icon is the primary UI.
/// </summary>
public partial class App : Application
{
    private SettingsService _settingsService = null!;
    private DisplayService _displayService = null!;
    private ScalingService _scalingService = null!;
    private ProfileApplyService _profileApplyService = null!;
    private TrayIconService _trayIconService = null!;
    private HotkeyService _hotkeyService = null!;
    private SettingsWindow? _settingsWindow;

    public App()
    {
        this.InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Initialize services (constructor calls Load automatically)
        _settingsService = new SettingsService();

        _displayService = new DisplayService();
        _scalingService = new ScalingService();
        _profileApplyService = new ProfileApplyService(_displayService, _scalingService);

        _trayIconService = new TrayIconService(_settingsService, _profileApplyService);
        _trayIconService.Initialize();

        _hotkeyService = new HotkeyService(_settingsService, _profileApplyService);
        _hotkeyService.Initialize(_trayIconService.Hwnd);

        // Wire up events
        _trayIconService.SettingsRequested += OnSettingsRequested;
        _trayIconService.ExitRequested += OnExitRequested;

        // Inject hotkey handling into the message loop
        InjectHotkeyHandling();
    }

    private void OnSettingsRequested()
    {
        if (_settingsWindow != null)
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(
            _settingsService, _displayService, _scalingService, _hotkeyService, _trayIconService);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Activate();
    }

    private void OnExitRequested()
    {
        _hotkeyService.Dispose();
        _trayIconService.Dispose();
        Exit();
    }

    /// <summary>
    /// Subclass the tray message window to intercept WM_HOTKEY before default dispatch.
    /// We do this by replacing the WndProc on the hidden message window.
    /// Since TrayIconService already owns the WndProc, we inject via a wrapper approach:
    /// HotkeyService.ProcessHotkey is called from TrayIconService's WndProc.
    /// </summary>
    private void InjectHotkeyHandling()
    {
        // The TrayIconService's WndProc doesn't know about hotkeys directly.
        // We handle this by modifying TrayIconService to also check for WM_HOTKEY,
        // or by subclassing. For simplicity, we'll add a hook via the shared HWND.
        // The hotkey WM_HOTKEY messages will be dispatched to the message window.
        // Since TrayIconService's WndProc calls DefWindowProc for unhandled messages,
        // and WM_HOTKEY is only sent to the registered hwnd, we need to hook it.
        // 
        // Solution: We update TrayIconService to accept an optional WM_HOTKEY handler.
        _trayIconService.HotkeyHandler = _hotkeyService.ProcessHotkey;
    }
}
