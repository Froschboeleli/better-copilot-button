using BetterCopilotButton.Models;

namespace BetterCopilotButton.Services;

public static class PresetCatalog
{
    public static IReadOnlyList<AppPreset> All { get; } =
    [
        new AppPreset
        {
            Id = "calculator",
            DisplayName = "Calculator",
            Description = "Windows Calculator (default)",
            Kind = LaunchKind.ShellCommand,
            Target = "calc.exe",
            ProcessNames = ["calc", "CalculatorApp", "Calculator"],
        },
        new AppPreset
        {
            Id = "notepad",
            DisplayName = "Notepad",
            Description = "Classic Notepad",
            Kind = LaunchKind.ShellCommand,
            Target = "notepad.exe",
            ProcessNames = ["notepad", "Notepad"],
        },
        new AppPreset
        {
            Id = "terminal",
            DisplayName = "Windows Terminal",
            Description = "wt.exe if installed",
            Kind = LaunchKind.ShellCommand,
            Target = "wt.exe",
            ProcessNames = ["WindowsTerminal", "wt"],
        },
        new AppPreset
        {
            Id = "explorer",
            DisplayName = "File Explorer",
            Description = "Opens a new Explorer window",
            Kind = LaunchKind.ShellCommand,
            Target = "explorer.exe",
            AlwaysLaunchNew = true,
        },
        new AppPreset
        {
            Id = "settings",
            DisplayName = "Settings",
            Description = "Windows Settings",
            Kind = LaunchKind.Protocol,
            Target = "ms-settings:",
            AlwaysLaunchNew = true,
            ProcessNames = ["SystemSettings"],
        },
        new AppPreset
        {
            Id = "chatgpt",
            DisplayName = "ChatGPT",
            Description = "Optional, if the desktop app is installed",
            Kind = LaunchKind.ExecutablePath,
            Optional = true,
            ProcessNames = ["ChatGPT", "chatgpt"],
        },
        new AppPreset
        {
            Id = "custom",
            DisplayName = "Custom app",
            Description = "Browse for any .exe",
            Kind = LaunchKind.ExecutablePath,
            IsCustom = true,
        },
    ];

    public static AppPreset Get(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return All[0];
        }

        return All.FirstOrDefault(p => p.Id == id) ?? All[0];
    }

    public static string? ResolveChatGptPath()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var candidates = new[]
        {
            Path.Combine(local, "Programs", "ChatGPT", "ChatGPT.exe"),
            Path.Combine(local, "Microsoft", "WindowsApps", "chatgpt.exe"),
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        var programs = Path.Combine(local, "Programs", "ChatGPT");
        if (Directory.Exists(programs))
        {
            var found = Directory.GetFiles(programs, "ChatGPT.exe", SearchOption.AllDirectories);
            if (found.Length > 0)
            {
                return found[0];
            }
        }

        return null;
    }
}
