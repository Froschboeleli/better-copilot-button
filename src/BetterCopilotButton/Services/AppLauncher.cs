using System.Diagnostics;
using System.IO;
using BetterCopilotButton.Models;

namespace BetterCopilotButton.Services;

public static class AppLauncher
{
    public static LaunchPlan Resolve(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var preset = PresetCatalog.Get(settings.PresetId);

        if (preset.IsCustom)
        {
            var custom = settings.CustomPath.Trim();
            if (string.IsNullOrWhiteSpace(custom))
            {
                throw new InvalidOperationException("Choose an executable first. Use Browse to pick a .exe file.");
            }

            return new LaunchPlan(
                preset.DisplayName,
                custom,
                settings.CustomArguments.Trim(),
                LaunchKind.ExecutablePath,
                [Path.GetFileNameWithoutExtension(custom)],
                AlwaysLaunchNew: false);
        }

        if (preset.Id == "chatgpt")
        {
            var path = PresetCatalog.ResolveChatGptPath();
            if (path is null)
            {
                throw new InvalidOperationException(
                    "ChatGPT desktop was not found. Install it, or pick another app with Browse.");
            }

            return new LaunchPlan(
                preset.DisplayName,
                path,
                "",
                LaunchKind.ExecutablePath,
                preset.ProcessNames,
                AlwaysLaunchNew: false);
        }

        return new LaunchPlan(
            preset.DisplayName,
            preset.Target,
            preset.Arguments,
            preset.Kind,
            preset.ProcessNames,
            preset.AlwaysLaunchNew);
    }

    public static string DescribeTarget(AppSettings settings)
    {
        try
        {
            var plan = Resolve(settings);
            return plan.DisplayName;
        }
        catch (InvalidOperationException)
        {
            var preset = PresetCatalog.Get(settings.PresetId);
            return preset.DisplayName;
        }
    }

    public static void LaunchOrFocus(AppSettings settings)
    {
        var plan = Resolve(settings);
        LaunchOrFocus(plan);
    }

    public static void LaunchOrFocus(LaunchPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (!plan.AlwaysLaunchNew && TryFocus(plan.ProcessNames))
        {
            return;
        }

        var start = new ProcessStartInfo
        {
            FileName = plan.Target,
            Arguments = plan.Arguments,
            UseShellExecute = true
        };

        Process.Start(start);
    }

    private static bool TryFocus(IReadOnlyList<string> processNames)
    {
        foreach (var name in processNames)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            Process[] processes;
            try
            {
                processes = Process.GetProcessesByName(name);
            }
            catch (Exception)
            {
                continue;
            }

            foreach (var process in processes)
            {
                try
                {
                    var handle = process.MainWindowHandle;
                    if (handle == IntPtr.Zero || !NativeMethods.IsWindowVisible(handle))
                    {
                        continue;
                    }

                    FocusWindow(handle);
                    return true;
                }
                finally
                {
                    process.Dispose();
                }
            }
        }

        return false;
    }

    private static void FocusWindow(IntPtr handle)
    {
        if (NativeMethods.IsIconic(handle))
        {
            NativeMethods.ShowWindowAsync(handle, NativeMethods.SwRestore);
        }
        else
        {
            NativeMethods.ShowWindowAsync(handle, NativeMethods.SwShow);
        }

        var foreground = NativeMethods.GetForegroundWindow();
        var currentThread = NativeMethods.GetCurrentThreadId();
        var targetThread = NativeMethods.GetWindowThreadProcessId(handle, out _);
        var foregroundThread = NativeMethods.GetWindowThreadProcessId(foreground, out _);

        var attachedToTarget = false;
        var attachedToForeground = false;
        try
        {
            if (currentThread != targetThread)
            {
                attachedToTarget = NativeMethods.AttachThreadInput(currentThread, targetThread, true);
            }

            if (currentThread != foregroundThread && foregroundThread != targetThread)
            {
                attachedToForeground = NativeMethods.AttachThreadInput(currentThread, foregroundThread, true);
            }

            NativeMethods.BringWindowToTop(handle);
            NativeMethods.SetForegroundWindow(handle);
        }
        finally
        {
            if (attachedToTarget)
            {
                NativeMethods.AttachThreadInput(currentThread, targetThread, false);
            }

            if (attachedToForeground)
            {
                NativeMethods.AttachThreadInput(currentThread, foregroundThread, false);
            }
        }
    }
}

public sealed record LaunchPlan(
    string DisplayName,
    string Target,
    string Arguments,
    LaunchKind Kind,
    IReadOnlyList<string> ProcessNames,
    bool AlwaysLaunchNew);
