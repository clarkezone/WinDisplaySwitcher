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

        _viewModel = new SettingsViewModel(
            settingsService, displayService, scalingService, hotkeyService, trayIconService);

        ProfileList.ItemsSource = _viewModel.Profiles;
        MonitorCombo.ItemsSource = _viewModel.Monitors;
        AutoStartToggle.IsOn = _viewModel.AutoStartEnabled;

        // Wire up events in code-behind (top-level elements)
        AddProfileBtn.Click += OnAddProfileClick;
        MonitorCombo.SelectionChanged += OnMonitorSelectionChanged;
        ConfirmAddBtn.Click += OnConfirmAddClick;
        CancelAddBtn.Click += OnCancelAddClick;
        AutoStartToggle.Toggled += OnAutoStartToggled;

        // Enable hotkey capture on the window
        if (this.Content is UIElement rootElement)
        {
            rootElement.PreviewKeyDown += OnPreviewKeyDown;
        }
    }

    private void OnAddProfileClick(object sender, RoutedEventArgs e)
    {
        AddPanel.Visibility = Visibility.Visible;
        _viewModel.StartAddProfileCommand.Execute(null);
        MonitorCombo.ItemsSource = _viewModel.Monitors;
        if (_viewModel.SelectedMonitor != null)
            MonitorCombo.SelectedItem = _viewModel.SelectedMonitor;
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
    }

    private void OnCancelAddClick(object sender, RoutedEventArgs e)
    {
        _viewModel.CancelAddProfileCommand.Execute(null);
        AddPanel.Visibility = Visibility.Collapsed;
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

        // Skip modifier-only keys
        if (key == Windows.System.VirtualKey.Control ||
            key == Windows.System.VirtualKey.Shift ||
            key == Windows.System.VirtualKey.Menu ||
            key == Windows.System.VirtualKey.LeftWindows ||
            key == Windows.System.VirtualKey.RightWindows)
        {
            return;
        }

        // Build modifiers
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
