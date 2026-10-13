using System;
using Dalamud.Configuration;
using Dalamud.Plugin;

namespace AutoStatus;

public sealed class Configuration : IPluginConfiguration
{
    public const uint UnsetStatusId = uint.MaxValue;

    public const float MinUnfocusedDelaySeconds = 0f;

    public const float MaxUnfocusedDelaySeconds = 600f;

    public const float DefaultUnfocusedDelaySeconds = 2f;

    public int Version { get; set; } = 0;

    public StatusKind StatusOnTabOut { get; set; } = StatusKind.Afk;

    public uint ActiveStatusId { get; set; } = UnsetStatusId;

    public uint InactiveStatusId { get; set; } = UnsetStatusId;

    public float UnfocusedDelaySeconds { get; set; } = DefaultUnfocusedDelaySeconds;

    public string LfpJobNameFocused { get; set; } = string.Empty;

    public string LfpJobNameUnfocused { get; set; } = string.Empty;

    public bool ShowAllStatuses { get; set; } = false;

    public bool DebugFocusLogs { get; set; } = false;

    public void Save(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.SavePluginConfig(this);
    }

    public void ClampUnfocusedDelay()
    {
        if (float.IsNaN(UnfocusedDelaySeconds))
        {
            UnfocusedDelaySeconds = DefaultUnfocusedDelaySeconds;
            return;
        }

        UnfocusedDelaySeconds = Math.Clamp(UnfocusedDelaySeconds, MinUnfocusedDelaySeconds, MaxUnfocusedDelaySeconds);
    }
}

public enum StatusKind
{
    Afk,
    Away,
    Busy
}
