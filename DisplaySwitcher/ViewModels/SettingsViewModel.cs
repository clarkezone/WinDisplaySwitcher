namespace DisplaySwitcher.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DisplaySwitcher.Models;
using DisplaySwitcher.Services;
using System.Collections.ObjectModel;

public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    private readonly DisplayService _displayService;
    private readonly ScalingService _scalingService;
    private readonly HotkeyService _hotkeyService;
    private readonly TrayIconService _trayIconService;

    public ObservableCollection<ProfileItem> Profiles { get; } = new();
    public ObservableCollection<MonitorInfo> Monitors { get; } = new();
    public ObservableCollection<DisplayMode> AvailableModes { get; } = new();
    public ObservableCollection<int> AvailableScales { get; } = new();

    [ObservableProperty]
    private MonitorInfo? _selectedMonitor;

    [ObservableProperty]
    private DisplayMode? _selectedMode;

    [ObservableProperty]
    private int _selectedScale = 100;

    [ObservableProperty]
    private bool _autoStartEnabled;

    [ObservableProperty]
    private bool _isAddingProfile;

    /// <summary>
    /// When non-null, the add panel is in "edit" mode for this item.
    /// </summary>
    public ProfileItem? EditingItem { get; set; }

    public SettingsViewModel(
        SettingsService settingsService,
        DisplayService displayService,
        ScalingService scalingService,
        HotkeyService hotkeyService,
        TrayIconService trayIconService)
    {
        _settingsService = settingsService;
        _displayService = displayService;
        _scalingService = scalingService;
        _hotkeyService = hotkeyService;
        _trayIconService = trayIconService;

        LoadData();
    }

    private void LoadData()
    {
        // Load monitors
        Monitors.Clear();
        var monitors = _displayService.GetMonitors();
        foreach (var mon in monitors)
        {
            _scalingService.PopulateScaleInfo(mon);
            Monitors.Add(mon);
        }

        // Load existing profiles
        Profiles.Clear();
        foreach (var profile in _settingsService.Settings.Profiles)
        {
            Profiles.Add(new ProfileItem(profile));
        }

        // Auto-start
        var autoStartService = new AutoStartService();
        AutoStartEnabled = autoStartService.IsEnabled();
    }

    partial void OnSelectedMonitorChanged(MonitorInfo? value)
    {
        AvailableModes.Clear();
        AvailableScales.Clear();

        if (value == null) return;

        var modes = _displayService.GetSupportedModes(value.DeviceName);
        foreach (var mode in modes)
            AvailableModes.Add(mode);

        if (AvailableModes.Count > 0)
            SelectedMode = AvailableModes[0];

        var scales = _scalingService.GetSupportedScales(value);
        foreach (var scale in scales)
            AvailableScales.Add(scale);

        if (AvailableScales.Count > 0)
            SelectedScale = value.RecommendedScalePercent;
    }

    [RelayCommand]
    private void StartAddProfile()
    {
        IsAddingProfile = true;
        if (Monitors.Count > 0 && SelectedMonitor == null)
            SelectedMonitor = Monitors[0];
    }

    [RelayCommand]
    private void CancelAddProfile()
    {
        IsAddingProfile = false;
    }

    [RelayCommand]
    private void ConfirmAddProfile()
    {
        if (SelectedMonitor == null || SelectedMode == null) return;

        var newMode = new DisplayMode(
            SelectedMode.Width, SelectedMode.Height,
            SelectedMode.RefreshRate, SelectedScale,
            SelectedMode.BitsPerPixel);

        if (EditingItem != null)
        {
            // Update the existing profile in-place (preserves hotkey assignment)
            EditingItem.Profile.DeviceName = SelectedMonitor.DeviceName;
            EditingItem.Profile.MonitorName = SelectedMonitor.FriendlyName;
            EditingItem.Profile.Mode = newMode;
            EditingItem.DisplayLabel = EditingItem.Profile.DisplayLabel;
            EditingItem = null;
        }
        else
        {
            var profile = new ResolutionProfile
            {
                DeviceName = SelectedMonitor.DeviceName,
                MonitorName = SelectedMonitor.FriendlyName,
                Mode = newMode
            };

            _settingsService.AddProfile(profile);
            Profiles.Add(new ProfileItem(profile));
        }

        IsAddingProfile = false;
        SaveAndRefresh();
    }

    [RelayCommand]
    private void RemoveProfile(ProfileItem? item)
    {
        if (item == null) return;

        _settingsService.RemoveProfile(item.Profile.Id);
        Profiles.Remove(item);

        SaveAndRefresh();
    }

    public void UpdateHotkey(ProfileItem item, int modifiers, int vk)
    {
        item.Profile.HotkeyModifiers = modifiers;
        item.Profile.HotkeyVk = vk;
        item.RefreshHotkeyDisplay();

        SaveAndRefresh();
    }

    public void ClearHotkey(ProfileItem item)
    {
        item.Profile.HotkeyModifiers = 0;
        item.Profile.HotkeyVk = 0;
        item.RefreshHotkeyDisplay();

        SaveAndRefresh();
    }

    [RelayCommand]
    private void ToggleAutoStart()
    {
        var autoStartService = new AutoStartService();
        if (AutoStartEnabled)
            autoStartService.Enable();
        else
            autoStartService.Disable();
    }

    private void SaveAndRefresh()
    {
        _settingsService.Save();
        _hotkeyService.Refresh();
        _trayIconService.RefreshMenu();
    }
}

/// <summary>
/// Observable wrapper around ResolutionProfile for the settings UI.
/// </summary>
public partial class ProfileItem : ObservableObject
{
    public ResolutionProfile Profile { get; }

    [ObservableProperty]
    private string _displayLabel = string.Empty;

    [ObservableProperty]
    private string _hotkeyDisplay = "None";

    public ProfileItem(ResolutionProfile profile)
    {
        Profile = profile;
        DisplayLabel = profile.DisplayLabel;
        HotkeyDisplay = profile.HotkeyDisplayString;
    }

    public void RefreshHotkeyDisplay()
    {
        HotkeyDisplay = Profile.HotkeyDisplayString;
    }
}
