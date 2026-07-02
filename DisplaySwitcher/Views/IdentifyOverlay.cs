namespace DisplaySwitcher.Views;

using DisplaySwitcher.Models;
using DisplaySwitcher.Services;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Runtime.InteropServices;
using Windows.Graphics;
using WinRT.Interop;

/// <summary>
/// Shows a brief identification overlay on each monitor (number + name), auto-closes after 2s.
/// </summary>
public static class IdentifyOverlay
{
    private static readonly List<Window> _activeWindows = new();

    public static void Show(DisplayService displayService)
    {
        // Close any existing overlays
        Close();

        var monitors = displayService.GetMonitors();
        int index = 1;

        foreach (var monitor in monitors)
        {
            var bounds = displayService.GetMonitorBounds(monitor.DeviceName);
            if (bounds == null) continue;

            var (x, y, width, height) = bounds.Value;
            CreateOverlayWindow(index, monitor, x, y, width, height);
            index++;
        }

        // Auto-close after 2 seconds
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (s, e) =>
        {
            timer.Stop();
            Close();
        };
        timer.Start();
    }

    private static void CreateOverlayWindow(int number, MonitorInfo monitor, int x, int y, int width, int height)
    {
        var window = new Window();
        window.SystemBackdrop = null;

        // Create content
        var grid = new Grid
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(220, 30, 30, 30)),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        var stack = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 8,
        };

        stack.Children.Add(new TextBlock
        {
            Text = number.ToString(),
            FontSize = 160,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = new SolidColorBrush(Colors.White),
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        stack.Children.Add(new TextBlock
        {
            Text = monitor.FriendlyName,
            FontSize = 40,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Colors.White),
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        stack.Children.Add(new TextBlock
        {
            Text = monitor.DeviceName,
            FontSize = 36,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(230, 200, 200, 200)),
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        grid.Children.Add(stack);
        window.Content = grid;

        // Position and size the window on the target monitor
        var appWindow = window.AppWindow;
        var presenter = appWindow.Presenter as OverlappedPresenter;
        if (presenter != null)
        {
            presenter.IsResizable = false;
            presenter.IsMinimizable = false;
            presenter.IsMaximizable = false;
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsAlwaysOnTop = true;
        }

        // Size: centered box on the monitor
        int overlayWidth = Math.Min(700, width * 2 / 3);
        int overlayHeight = Math.Min(500, height * 2 / 3);
        int overlayX = x + (width - overlayWidth) / 2;
        int overlayY = y + (height - overlayHeight) / 2;

        appWindow.MoveAndResize(new RectInt32(overlayX, overlayY, overlayWidth, overlayHeight));

        window.Activate();
        _activeWindows.Add(window);
    }

    public static void Close()
    {
        foreach (var w in _activeWindows)
        {
            try { w.Close(); } catch { }
        }
        _activeWindows.Clear();
    }
}
