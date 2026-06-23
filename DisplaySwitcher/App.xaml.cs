namespace DisplaySwitcher;

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
    private TopologyService _topologyService = null!;
    private ProfileApplyService _profileApplyService = null!;
    private TrayIconService _trayIconService = null!;
    private HotkeyService _hotkeyService = null!;
    private SettingsWindow? _settingsWindow;
    private Window? _lifetimeWindow; // hidden window to keep app alive

    public App()
    {
        this.InitializeComponent();
        this.UnhandledException += OnUnhandledException;
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        var logPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DisplaySwitcher", "crash.log");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath)!);
        System.IO.File.AppendAllText(logPath,
            $"[{DateTime.Now:O}] {e.Exception?.GetType().Name}: {e.Message}\n{e.Exception?.StackTrace}\n\n");
        e.Handled = false;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Create a hidden window to keep the app alive (WinUI exits when last window closes)
        _lifetimeWindow = new Window();
        _lifetimeWindow.AppWindow.Hide();

        _settingsService = new SettingsService();

        _displayService = new DisplayService();
        _scalingService = new ScalingService();
        _topologyService = new TopologyService();
        _profileApplyService = new ProfileApplyService(_displayService, _scalingService, _topologyService);

        _trayIconService = new TrayIconService(_settingsService, _profileApplyService);
        _trayIconService.Initialize();

        // HotkeyService runs its own message pump thread — no HWND parameter needed
        _hotkeyService = new HotkeyService(_settingsService, _profileApplyService);
        _hotkeyService.Initialize();

        _trayIconService.SettingsRequested += OnSettingsRequested;
        _trayIconService.ExitRequested += OnExitRequested;
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
        _lifetimeWindow?.Close();
        Exit();
    }
}
