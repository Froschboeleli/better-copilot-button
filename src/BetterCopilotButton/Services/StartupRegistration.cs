using Microsoft.Win32;

namespace BetterCopilotButton.Services;

public static class StartupRegistration
{
    public const string RunValueName = "BetterCopilotButton";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static void SetEnabled(bool enabled, string exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath))
        {
            throw new ArgumentException("Executable path is required.", nameof(exePath));
        }

        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException("Could not open the current-user Run key.");

        if (enabled)
        {
            key.SetValue(RunValueName, Quote(exePath) + " --tray");
        }
        else
        {
            key.DeleteValue(RunValueName, throwOnMissingValue: false);
        }
    }

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(RunValueName) is string value && !string.IsNullOrWhiteSpace(value);
    }

    public static void Remove()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(RunValueName, throwOnMissingValue: false);
    }

    private static string Quote(string path) =>
        path.Contains(' ', StringComparison.Ordinal) ? "\"" + path + "\"" : path;
}
