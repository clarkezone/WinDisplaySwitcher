namespace DisplaySwitcher.Views;

using DisplaySwitcher.Models;
using DisplaySwitcher.Services;
using DisplaySwitcher.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

public sealed partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;
    private ProfileItem? _hotkeyTarget;
    private bool _capturingHotkey;

    public SettingsWindow(
        SettingsService settingsService,
        DisplayService displayService,
        ScalingService scalingService,
        HotkeyService hotkeyService,
        TrayIconService trayIconService)
    {
        this.InitializeComponent();

        // Set a smaller default window size
        if (AppWindow != null)
        {
            AppWindow.Resize(new Windows.Graphics.SizeInt32(560, 480));
        }

        _viewModel = new SettingsViewModel(
            settingsService, displayService, scalingService, hotkeyService, trayIconService);

        ProfileList.ItemsSource = _viewModel.Profiles;
        MonitorCombo.ItemsSource = _viewModel.Monitors;
        AutoStartToggle.IsOn = _viewModel.AutoStartEnabled;

        AddProfileBtn.Click += OnAddProfileClick;
        MonitorCombo.SelectionChanged += OnMonitorSelectionChanged;
        ConfirmAddBtn.Click += OnConfirmAddClick;
        CancelAddBtn.Click += OnCancelAddClick;
        AutoStartToggle.Toggled += OnAutoStartToggled;

        if (this.Content is UIElement rootElement)
        {
            rootElement.PreviewKeyDown += OnPreviewKeyDown;
        }
    }

    private void OnAddProfileClick(object sender, RoutedEventArgs e)
    {
        _viewModel.EditingItem = null;
        AddPanel.Visibility = Visibility.Visible;
        ConfirmAddBtn.Content = "Add";
        _viewModel.StartAddProfileCommand.Execute(null);
        MonitorCombo.ItemsSource = _viewModel.Monitors;
        if (_viewModel.SelectedMonitor != null)
            MonitorCombo.SelectedItem = _viewModel.SelectedMonitor;
    }

    private void OnEditClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not ProfileItem item) return;

        _viewModel.EditingItem = item;
        AddPanel.Visibility = Visibility.Visible;
        ConfirmAddBtn.Content = "Save";
        _viewModel.StartAddProfileCommand.Execute(null);
        MonitorCombo.ItemsSource = _viewModel.Monitors;

        // Pre-select the monitor
        foreach (var mon in _viewModel.Monitors)
        {
            if (mon.DeviceName == item.Profile.DeviceName)
            {
                MonitorCombo.SelectedItem = mon;
                break;
            }
        }

        // Pre-select resolution (mode list populated by monitor selection)
        foreach (var mode in _viewModel.AvailableModes)
        {
            if (mode.ResolutionEquals(item.Profile.Mode))
            {
                ResolutionCombo.SelectedItem = mode;
                break;
            }
        }

        // Pre-select scale
        ScaleCombo.SelectedItem = item.Profile.Mode.ScalePercent;
    }

    private void OnMonitorSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MonitorCombo.SelectedItem is MonitorInfo monitor)
        {
            _viewModel.SelectedMonitor = monitor;
            ResolutionCombo.ItemsSource = _viewModel.AvailableModes;
            ScaleCombo.ItemsSource = _viewModel.AvailableScales;

            if (_viewModel.SelectedMode != null)
                ResolutionCombo.SelectedItem = _viewModel.SelectedMode;
            if (_viewModel.AvailableScales.Count > 0)
                ScaleCombo.SelectedItem = _viewModel.SelectedScale;
        }
    }

    private void OnConfirmAddClick(object sender, RoutedEventArgs e)
    {
        if (ResolutionCombo.SelectedItem is DisplayMode mode)
            _viewModel.SelectedMode = mode;
        if (ScaleCombo.SelectedItem is int scale)
            _viewModel.SelectedScale = scale;

        _viewModel.ConfirmAddProfileCommand.Execute(null);
        AddPanel.Visibility = Visibility.Collapsed;
        ConfirmAddBtn.Content = "Add";
    }

    private void OnCancelAddClick(object sender, RoutedEventArgs e)
    {
        _viewModel.EditingItem = null;
        _viewModel.CancelAddProfileCommand.Execute(null);
        AddPanel.Visibility = Visibility.Collapsed;
        ConfirmAddBtn.Content = "Add";
    }

    private void OnRemoveClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ProfileItem item)
        {
            _viewModel.RemoveProfileCommand.Execute(item);
        }
    }

    private void OnSetHotkeyClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ProfileItem item)
        {
            _hotkeyTarget = item;
            _capturingHotkey = true;
            btn.Content = "Press keys…";
        }
    }

    private void OnClearHotkeyClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ProfileItem item)
        {
            _viewModel.ClearHotkey(item);
        }
    }

    private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!_capturingHotkey || _hotkeyTarget == null) return;

        var key = e.Key;

        if (key == Windows.System.VirtualKey.Control ||
            key == Windows.System.VirtualKey.Shift ||
            key == Windows.System.VirtualKey.Menu ||
            key == Windows.System.VirtualKey.LeftWindows ||
            key == Windows.System.VirtualKey.RightWindows)
        {
            return;
        }

        int modifiers = 0;
        var state = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control);
        if (state.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
            modifiers |= HotkeyHelper.MOD_CONTROL;

        state = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Menu);
        if (state.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
            modifiers |= HotkeyHelper.MOD_ALT;

        state = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift);
        if (state.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
            modifiers |= HotkeyHelper.MOD_SHIFT;

        _viewModel.UpdateHotkey(_hotkeyTarget, modifiers, (int)key);
        _capturingHotkey = false;
        _hotkeyTarget = null;

        e.Handled = true;
    }

    private void OnAutoStartToggled(object sender, RoutedEventArgs e)
    {
        _viewModel.AutoStartEnabled = AutoStartToggle.IsOn;
        _viewModel.ToggleAutoStartCommand.Execute(null);
    }
}
