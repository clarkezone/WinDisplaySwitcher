namespace DisplaySwitcher.Services;

using Microsoft.Win32;

/// <summary>
/// Manages auto-start with Windows via HKCU Run registry key.
/// </summary>
public sealed class AutoStartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "DisplaySwitcher";

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
        return key?.GetValue(AppName) != null;
    }

    public void Enable()
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath)) return;

        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
        key?.SetValue(AppName, $"\"{exePath}\"");
    }

    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
        key?.DeleteValue(AppName, false);
    }

    public void SetEnabled(bool enabled)
    {
        if (enabled) Enable();
        else Disable();
    }
}
