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

        if (AppWindow != null)
            AppWindow.Resize(new Windows.Graphics.SizeInt32(640, 600));

        _viewModel = new SettingsViewModel(
            settingsService, displayService, scalingService, hotkeyService, trayIconService);

        ProfileList.ItemsSource = _viewModel.Profiles;
        AutoStartToggle.IsOn = _viewModel.AutoStartEnabled;

        // Populate topology combo
        TopologyCombo.ItemsSource = _viewModel.TopologyOptions;
        TopologyCombo.SelectedIndex = 0;

        AddProfileBtn.Click += OnAddProfileClick;
        ConfirmAddBtn.Click += OnConfirmAddClick;
        CancelAddBtn.Click += OnCancelAddClick;
        AutoStartToggle.Toggled += OnAutoStartToggled;

        if (this.Content is UIElement rootElement)
            rootElement.PreviewKeyDown += OnPreviewKeyDown;
    }

    private void OnAddProfileClick(object sender, RoutedEventArgs e)
    {
        _viewModel.StartAddProfileCommand.Execute(null);
        AddPanel.Visibility = Visibility.Visible;
        ConfirmAddBtn.Content = "Add";
        ProfileNameBox.Text = string.Empty;
        TopologyCombo.SelectedIndex = 0;
        MonitorEntriesPanel.Children.Clear();
    }

    private void OnEditClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not ProfileItem item) return;

        _viewModel.PopulateEditPanel(item);
        AddPanel.Visibility = Visibility.Visible;
        ConfirmAddBtn.Content = "Save";
        ProfileNameBox.Text = _viewModel.ProfileName;
        TopologyCombo.SelectedItem = _viewModel.SelectedTopology;

        // Rebuild monitor entry UI rows
        MonitorEntriesPanel.Children.Clear();
        foreach (var entry in _viewModel.MonitorEntries)
            AddMonitorEntryRow(entry);
    }

    private void OnAddMonitorEntry(object sender, RoutedEventArgs e)
    {
        _viewModel.AddMonitorEntryCommand.Execute(null);
        if (_viewModel.MonitorEntries.Count > 0)
            AddMonitorEntryRow(_viewModel.MonitorEntries[^1]);
    }

    private void AddMonitorEntryRow(MonitorSettingEntry entry)
    {
        var panel = new StackPanel { Spacing = 4, Padding = new Thickness(8), BorderThickness = new Thickness(1), BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray), CornerRadius = new CornerRadius(4) };

        // Monitor picker
        var monitorCombo = new ComboBox { Header = "Monitor", HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var mon in _viewModel.Monitors)
            monitorCombo.Items.Add(mon);
        if (entry.SelectedMonitor != null)
            monitorCombo.SelectedItem = entry.SelectedMonitor;

        // Resolution combo — "Don't change" + available modes
        var resCombo = new ComboBox { Header = "Resolution", HorizontalAlignment = HorizontalAlignment.Stretch };
        resCombo.Items.Add("Don't change");
        resCombo.SelectedIndex = 0;

        // Scale combo — "Don't change" + available scales
        var scaleCombo = new ComboBox { Header = "Scale %", HorizontalAlignment = HorizontalAlignment.Stretch };
        scaleCombo.Items.Add("Don't change");
        scaleCombo.SelectedIndex = 0;

        // When monitor changes, repopulate resolution/scale combos
        monitorCombo.SelectionChanged += (s, args) =>
        {
            if (monitorCombo.SelectedItem is not MonitorInfo mon) return;
            entry.SelectedMonitor = mon;

            resCombo.Items.Clear();
            resCombo.Items.Add("Don't change");
            foreach (var mode in _viewModel.GetSupportedModes(mon))
                resCombo.Items.Add(mode);
            resCombo.SelectedIndex = 0;

            scaleCombo.Items.Clear();
            scaleCombo.Items.Add("Don't change");
            foreach (var scale in _viewModel.GetSupportedScales(mon))
                scaleCombo.Items.Add(scale);
            scaleCombo.SelectedIndex = 0;

            // Apply pending selections if editing
            ApplyPendingSelections(entry, resCombo, scaleCombo);
        };

        resCombo.SelectionChanged += (s, args) =>
        {
            entry.SelectedMode = resCombo.SelectedItem is DisplayMode m ? m : null;
        };

        scaleCombo.SelectionChanged += (s, args) =>
        {
            entry.SelectedScale = scaleCombo.SelectedItem is int sc ? sc : 0;
        };

        // Remove button
        var removeBtn = new Button { Content = "Remove Monitor" };
        removeBtn.Click += (s, args) =>
        {
            _viewModel.RemoveMonitorEntry(entry);
            MonitorEntriesPanel.Children.Remove(panel);
        };

        panel.Children.Add(monitorCombo);
        panel.Children.Add(resCombo);
        panel.Children.Add(scaleCombo);
        panel.Children.Add(removeBtn);
        MonitorEntriesPanel.Children.Add(panel);

        // If monitor was pre-selected (editing), trigger population
        if (entry.SelectedMonitor != null)
        {
            monitorCombo.SelectedItem = entry.SelectedMonitor;
        }
    }

    private void ApplyPendingSelections(MonitorSettingEntry entry, ComboBox resCombo, ComboBox scaleCombo)
    {
        if (entry.PendingWidth != null && entry.PendingHeight != null)
        {
            foreach (var item in resCombo.Items)
            {
                if (item is DisplayMode mode &&
                    mode.Width == entry.PendingWidth &&
                    mode.Height == entry.PendingHeight &&
                    (entry.PendingRefreshRate == null || mode.RefreshRate == entry.PendingRefreshRate))
                {
                    resCombo.SelectedItem = mode;
                    break;
                }
            }
            entry.PendingWidth = null;
            entry.PendingHeight = null;
            entry.PendingRefreshRate = null;
        }

        if (entry.PendingScale > 0)
        {
            foreach (var item in scaleCombo.Items)
            {
                if (item is int scale && scale == entry.PendingScale)
                {
                    scaleCombo.SelectedItem = scale;
                    break;
                }
            }
            entry.PendingScale = 0;
        }
    }

    private void OnConfirmAddClick(object sender, RoutedEventArgs e)
    {
        _viewModel.ProfileName = ProfileNameBox.Text;
        _viewModel.SelectedTopology = TopologyCombo.SelectedItem as TopologyOption;
        _viewModel.ConfirmAddProfileCommand.Execute(null);
        AddPanel.Visibility = Visibility.Collapsed;
        MonitorEntriesPanel.Children.Clear();
    }

    private void OnCancelAddClick(object sender, RoutedEventArgs e)
    {
        _viewModel.CancelAddProfileCommand.Execute(null);
        AddPanel.Visibility = Visibility.Collapsed;
        MonitorEntriesPanel.Children.Clear();
    }

    private void OnRemoveClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ProfileItem item)
            _viewModel.RemoveProfileCommand.Execute(item);
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
            _viewModel.ClearHotkey(item);
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
            return;

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
