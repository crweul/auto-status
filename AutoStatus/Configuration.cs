using Dalamud.Configuration;
using Dalamud.Plugin;

namespace AutoStatus;

public sealed class Configuration : IPluginConfiguration
{
    public const uint UnsetStatusId = uint.MaxValue;

    public int Version { get; set; } = 0;

    public StatusKind StatusOnTabOut { get; set; } = StatusKind.Afk;

    public uint ActiveStatusId { get; set; } = UnsetStatusId;

    public uint InactiveStatusId { get; set; } = UnsetStatusId;

    public string LfpJobNameFocused { get; set; } = string.Empty;

    public string LfpJobNameUnfocused { get; set; } = string.Empty;

    public bool ShowAllStatuses { get; set; } = true;

    public bool DebugFocusLogs { get; set; } = false;

    public void Save(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.SavePluginConfig(this);
    }
}

public enum StatusKind
{
    Afk,
    Away,
    Busy
}
