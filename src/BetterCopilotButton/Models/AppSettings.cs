namespace BetterCopilotButton.Models;

public sealed class AppSettings
{
    public const string DefaultPresetId = "calculator";

    public bool RemapEnabled { get; set; }

    public bool StartAtLogin { get; set; }

    public string PresetId { get; set; } = DefaultPresetId;

    public string CustomPath { get; set; } = "";

    public string CustomArguments { get; set; } = "";
}
