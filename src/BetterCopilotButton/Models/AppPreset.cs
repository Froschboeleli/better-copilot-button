namespace BetterCopilotButton.Models;

public sealed class AppPreset
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public required string Description { get; init; }

    public required LaunchKind Kind { get; init; }

    public string Target { get; init; } = "";

    public string Arguments { get; init; } = "";

    public IReadOnlyList<string> ProcessNames { get; init; } = [];

    public bool AlwaysLaunchNew { get; init; }

    public bool IsCustom { get; init; }

    public bool Optional { get; init; }
}

public enum LaunchKind
{
    ShellCommand,
    ExecutablePath,
    Protocol
}
