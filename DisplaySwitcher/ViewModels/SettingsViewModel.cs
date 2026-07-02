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

    // Topology options for the combo box (null = "Don't change")
    public ObservableCollection<TopologyOption> TopologyOptions { get; } = new()
    {
        new TopologyOption(null, "Don't change"),
        new TopologyOption(TopologyMode.Extend, "Extend"),
        new TopologyOption(TopologyMode.Clone, "Clone"),
        new TopologyOption(TopologyMode.InternalOnly, "Internal only (show on 1)"),
        new TopologyOption(TopologyMode.ExternalOnly, "External only (show on 2)"),
    };

    [ObservableProperty]
    private TopologyOption? _selectedTopology;

    [ObservableProperty]
    private string _profileName = string.Empty;

    [ObservableProperty]
    private bool _autoStartEnabled;

    [ObservableProperty]
    private bool _isAddingProfile;

    /// <summary>Per-monitor entries in the add/edit panel.</summary>
    public ObservableCollection<MonitorSettingEntry> MonitorEntries { get; } = new();

    /// <summary>When non-null, the add panel is in "edit" mode for this item.</summary>
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
        Monitors.Clear();
        var monitors = _displayService.GetMonitors();
        foreach (var mon in monitors)
        {
            _scalingService.PopulateScaleInfo(mon);
            Monitors.Add(mon);
        }

        Profiles.Clear();
        foreach (var profile in _settingsService.Settings.Profiles)
            Profiles.Add(new ProfileItem(profile));

        var autoStartService = new AutoStartService();
        AutoStartEnabled = autoStartService.IsEnabled();
    }

    /// <summary>Get supported display modes for a monitor.</summary>
    public List<DisplayMode> GetSupportedModes(MonitorInfo monitor) =>
        _displayService.GetSupportedModes(monitor.DeviceName);

    /// <summary>Get supported DPI scales for a monitor.</summary>
    public int[] GetSupportedScales(MonitorInfo monitor) =>
        _scalingService.GetSupportedScales(monitor);

    [RelayCommand]
    private void StartAddProfile()
    {
        IsAddingProfile = true;
        ProfileName = string.Empty;
        SelectedTopology = TopologyOptions[0]; // "Don't change"
        MonitorEntries.Clear();
    }

    [RelayCommand]
    private void CancelAddProfile()
    {
        IsAddingProfile = false;
        EditingItem = null;
        MonitorEntries.Clear();
    }

    [RelayCommand]
    private void AddMonitorEntry()
    {
        if (Monitors.Count == 0) return;
        MonitorEntries.Add(new MonitorSettingEntry());
    }

    public void RemoveMonitorEntry(MonitorSettingEntry entry)
    {
        MonitorEntries.Remove(entry);
    }

    [RelayCommand]
    private void ConfirmAddProfile()
    {
        var profile = EditingItem?.Profile ?? new CompositeProfile();
        profile.Name = ProfileName;
        profile.Topology = SelectedTopology?.Mode;

        profile.MonitorSettings.Clear();
        foreach (var entry in MonitorEntries)
        {
            if (entry.SelectedMonitor == null) continue;

            var ms = new MonitorSetting
            {
                DeviceName = entry.SelectedMonitor.DeviceName,
                MonitorName = entry.SelectedMonitor.FriendlyName,
            };

            // Resolution — only if user selected something other than "Don't change"
            if (entry.SelectedMode != null)
            {
                ms.Width = entry.SelectedMode.Width;
                ms.Height = entry.SelectedMode.Height;
                ms.RefreshRate = entry.SelectedMode.RefreshRate;
                ms.BitsPerPixel = entry.SelectedMode.BitsPerPixel;
            }

            // DPI — only if user selected something other than "Don't change" (0)
            if (entry.SelectedScale > 0)
            {
                ms.ScalePercent = entry.SelectedScale;
            }

            profile.MonitorSettings.Add(ms);
        }

        if (EditingItem != null)
        {
            _settingsService.UpdateProfile(profile);
            EditingItem.Refresh();
            EditingItem = null;
        }
        else
        {
            _settingsService.AddProfile(profile);
            Profiles.Add(new ProfileItem(profile));
        }

        IsAddingProfile = false;
        MonitorEntries.Clear();
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

    /// <summary>Populate the edit panel from an existing profile.</summary>
    public void PopulateEditPanel(ProfileItem item)
    {
        EditingItem = item;
        var profile = item.Profile;

        ProfileName = profile.Name;
        SelectedTopology = TopologyOptions.FirstOrDefault(t => t.Mode == profile.Topology) ?? TopologyOptions[0];

        MonitorEntries.Clear();
        foreach (var ms in profile.MonitorSettings)
        {
            var entry = new MonitorSettingEntry
            {
                SelectedMonitor = Monitors.FirstOrDefault(m => m.DeviceName == ms.DeviceName)
                    ?? Monitors.FirstOrDefault(m => m.FriendlyName.Equals(ms.MonitorName, StringComparison.OrdinalIgnoreCase)),
            };

            // Defer mode/scale selection — will be set by the View after monitor combo populates
            entry.PendingWidth = ms.Width;
            entry.PendingHeight = ms.Height;
            entry.PendingRefreshRate = ms.RefreshRate;
            entry.PendingScale = ms.ScalePercent ?? 0;

            MonitorEntries.Add(entry);
        }
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

/// <summary>Wraps a TopologyMode? for combo box display.</summary>
public record TopologyOption(TopologyMode? Mode, string DisplayName)
{
    public override string ToString() => DisplayName;
}

/// <summary>
/// Observable wrapper around CompositeProfile for the settings UI.
/// </summary>
public partial class ProfileItem : ObservableObject
{
    public CompositeProfile Profile { get; }

    [ObservableProperty]
    private string _displayLabel = string.Empty;

    [ObservableProperty]
    private string _hotkeyDisplay = "None";

    public ProfileItem(CompositeProfile profile)
    {
        Profile = profile;
        DisplayLabel = profile.DisplayLabel;
        HotkeyDisplay = profile.HotkeyDisplayString;
    }

    public void RefreshHotkeyDisplay() => HotkeyDisplay = Profile.HotkeyDisplayString;
    public void Refresh()
    {
        DisplayLabel = Profile.DisplayLabel;
        HotkeyDisplay = Profile.HotkeyDisplayString;
    }
}

/// <summary>
/// A single monitor entry in the add/edit panel. Holds the user's selections
/// for one monitor row.
/// </summary>
public partial class MonitorSettingEntry : ObservableObject
{
    [ObservableProperty]
    private MonitorInfo? _selectedMonitor;

    /// <summary>Null = "Don't change resolution".</summary>
    [ObservableProperty]
    private DisplayMode? _selectedMode;

    /// <summary>0 = "Don't change DPI".</summary>
    [ObservableProperty]
    private int _selectedScale;

    // Used when loading an existing profile to defer selection until combos are populated
    public int? PendingWidth { get; set; }
    public int? PendingHeight { get; set; }
    public int? PendingRefreshRate { get; set; }
    public int PendingScale { get; set; }
}
