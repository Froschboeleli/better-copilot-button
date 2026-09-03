using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace BetterCopilotButton.Services;

public static class InstallService
{
    public const string ProductName = "Better Copilot Button";
    private const string UninstallKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\BetterCopilotButton";

    public static string InstallDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BetterCopilotButton");

    public static string InstalledExePath => Path.Combine(InstallDirectory, "BetterCopilotButton.exe");

    public static string StartMenuShortcutPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            "Programs",
            ProductName + ".lnk");

    public static string? CurrentExePath => Environment.ProcessPath;

    public static bool IsRunningFromInstallFolder()
    {
        var current = CurrentExePath;
        if (string.IsNullOrWhiteSpace(current))
        {
            return false;
        }

        return PathsEqual(Path.GetDirectoryName(current), InstallDirectory);
    }

    public static void InstallForCurrentUser()
    {
        var source = CurrentExePath
            ?? throw new InvalidOperationException("Could not find the running executable.");

        Directory.CreateDirectory(InstallDirectory);
        if (!PathsEqual(source, InstalledExePath))
        {
            File.Copy(source, InstalledExePath, overwrite: true);
        }

        CreateShortcut(StartMenuShortcutPath, InstalledExePath);
        WriteUninstallKey(InstalledExePath);
    }

    public static void Uninstall(bool deleteSettings)
    {
        StartupRegistration.Remove();
        DeleteUninstallKey();
        TryDelete(StartMenuShortcutPath);

        if (deleteSettings)
        {
            SettingsStore.Delete();
        }

        var current = CurrentExePath;
        if (current is not null && PathsEqual(Path.GetDirectoryName(current), InstallDirectory))
        {
            ScheduleFolderDelete(InstallDirectory);
            return;
        }

        TryDeleteDirectory(InstallDirectory);
    }

    private static void WriteUninstallKey(string exePath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(UninstallKeyPath, writable: true)
            ?? throw new InvalidOperationException("Could not write the uninstall entry.");

        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
        key.SetValue("DisplayName", ProductName);
        key.SetValue("DisplayVersion", version);
        key.SetValue("Publisher", "Better Copilot Button");
        key.SetValue("InstallLocation", InstallDirectory);
        key.SetValue("DisplayIcon", exePath);
        key.SetValue("UninstallString", Quote(exePath) + " --uninstall");
        key.SetValue("QuietUninstallString", Quote(exePath) + " --uninstall");
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        try
        {
            key.SetValue("EstimatedSize", new FileInfo(exePath).Length / 1024, RegistryValueKind.DWord);
        }
        catch (IOException)
        {
            // Size is optional.
        }
    }

    private static void DeleteUninstallKey()
    {
        using var parent = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall", writable: true);
        parent?.DeleteSubKeyTree("BetterCopilotButton", throwOnMissingSubKey: false);
    }

    private static void CreateShortcut(string shortcutPath, string targetPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath)!);
        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("WScript.Shell is not available.");
        var shell = Activator.CreateInstance(shellType)
            ?? throw new InvalidOperationException("Could not create WScript.Shell.");
        var shortcut = shellType.InvokeMember(
            "CreateShortcut",
            BindingFlags.InvokeMethod,
            binder: null,
            target: shell,
            args: [shortcutPath])
            ?? throw new InvalidOperationException("Could not create the Start menu shortcut.");

        var shortcutType = shortcut.GetType();
        SetComProperty(shortcutType, shortcut, "TargetPath", targetPath);
        SetComProperty(shortcutType, shortcut, "WorkingDirectory", Path.GetDirectoryName(targetPath) ?? "");
        SetComProperty(shortcutType, shortcut, "WindowStyle", 1);
        SetComProperty(shortcutType, shortcut, "Description", "Replace the Copilot key with any app you choose.");
        SetComProperty(shortcutType, shortcut, "IconLocation", targetPath + ",0");
        shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, binder: null, target: shortcut, args: null);
    }

    private static void SetComProperty(Type type, object target, string name, object value)
    {
        type.InvokeMember(name, BindingFlags.SetProperty, binder: null, target: target, args: [value]);
    }

    private static void ScheduleFolderDelete(string folder)
    {
        var cmd = $"/C timeout /t 2 /nobreak > NUL & rmdir /s /q \"{folder}\"";
        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = cmd,
            CreateNoWindow = true,
            UseShellExecute = false
        });
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Delete failed: " + ex.Message);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Directory delete failed: " + ex.Message);
        }
    }

    private static bool PathsEqual(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
        {
            return false;
        }

        return string.Equals(
            Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string Quote(string path) =>
        path.Contains(' ', StringComparison.Ordinal) ? "\"" + path + "\"" : path;
}
