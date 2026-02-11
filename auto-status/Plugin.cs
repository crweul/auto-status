using System;
using System.Collections.Generic;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using Lumina.Excel.Sheets;

namespace AutoStatus;

public sealed class Plugin : IDalamudPlugin
{
    public const uint LookingForPartyStatusId = 23;

    private static readonly HashSet<uint> CuratedStatusIds =
    [
        47,
        23,
        21,
        29,
        30,
        28,
        27,
        12,
        22,
        44,
        17
    ];

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;

    private readonly WindowSystem windowSystem = new("AutoStatus");
    private readonly SettingsWindow settingsWindow;
    private readonly Configuration configuration;
    private OnlineStatusOption[] statusOptions = Array.Empty<OnlineStatusOption>();
    private string[] statusLabels = Array.Empty<string>();

    private const string CommandName = "/autostatus";
    private const string CommandAlias = "/aus";

    private bool? wasFocused;
    private bool? pendingFocus;
    private DateTime pendingFocusSince;

    public Plugin()
    {
        configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        ReloadStatusOptions();
        EnsureDefaults();
        EnsureStatusIdsValid();

        settingsWindow = new SettingsWindow(this, configuration);
        windowSystem.AddWindow(settingsWindow);

        PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleConfigUi;

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open the Auto-status settings window."
        });
        CommandManager.AddHandler(CommandAlias, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open the Auto-status settings window."
        });

        Framework.Update += OnFrameworkUpdate;
    }

    public void Dispose()
    {
        Framework.Update -= OnFrameworkUpdate;

        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleConfigUi;

        windowSystem.RemoveAllWindows();

        CommandManager.RemoveHandler(CommandName);
        CommandManager.RemoveHandler(CommandAlias);
    }

    public string[] StatusLabels => statusLabels;

    public int GetStatusIndex(uint statusId)
    {
        for (var i = 0; i < statusOptions.Length; i++)
        {
            if (statusOptions[i].Id == statusId)
            {
                return i;
            }
        }

        return 0;
    }

    public uint GetStatusId(int index)
    {
        if (index < 0 || index >= statusOptions.Length)
        {
            return Configuration.UnsetStatusId;
        }

        return statusOptions[index].Id;
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (!ClientState.IsLoggedIn)
        {
            wasFocused = null;
            pendingFocus = null;
            return;
        }

        var isFocused = Util.ApplicationIsActivated();
        if (wasFocused is null)
        {
            wasFocused = isFocused;
            pendingFocus = null;
            return;
        }

        if (isFocused == wasFocused)
        {
            pendingFocus = null;
            return;
        }

        if (pendingFocus != isFocused)
        {
            pendingFocus = isFocused;
            pendingFocusSince = DateTime.UtcNow;
            return;
        }

        if (DateTime.UtcNow - pendingFocusSince < TimeSpan.FromSeconds(2))
        {
            return;
        }

        wasFocused = isFocused;
        pendingFocus = null;
        ApplyStatus(isFocused, isManual: false);
    }

    private void ToggleConfigUi()
    {
        settingsWindow.Toggle();
    }

    private void OnCommand(string command, string args)
    {
        ToggleConfigUi();
    }

    public void ApplyStatus(bool isFocused, bool isManual)
    {
        var statusId = isFocused ? configuration.ActiveStatusId : configuration.InactiveStatusId;
        if (statusId == Configuration.UnsetStatusId)
        {
            return;
        }

        var lfpJobName = isFocused
            ? configuration.LfpJobNameFocused
            : configuration.LfpJobNameUnfocused;

        void Execute()
        {
            var ok = statusId == LookingForPartyStatusId
                ? TrySetLookingForPartyStatus(lfpJobName)
                : TrySetOnlineStatus(statusId);

            if (!ok && statusId == LookingForPartyStatusId)
            {
                ok = TrySetOnlineStatus(statusId);
            }

            if (!ok)
            {
                Log.Warning("Auto-status failed to set online status: {StatusId}", statusId);
                if (configuration.DebugFocusLogs || isManual)
                {
                    ChatGui.PrintError($"Auto-status: failed to set status {statusId}");
                }
            }

            if (configuration.DebugFocusLogs || isManual)
            {
                ChatGui.Print($"Auto-status: set status {statusId} (focused={isFocused}, ok={ok})");
            }
        }

        if (Framework.IsInFrameworkUpdateThread)
        {
            Execute();
            return;
        }

        _ = Framework.Run(Execute);
    }

    private bool TrySetLookingForPartyStatus(string jobName)
    {
        if (string.IsNullOrWhiteSpace(jobName))
        {
            return false;
        }

        var command = $"/lfp {jobName.Trim()}";
        return CommandManager.ProcessCommand(command);
    }

    public void ReloadStatusOptions()
    {
        statusOptions = LoadStatusOptions();
        statusLabels = Array.ConvertAll(statusOptions, option => option.Label);
        EnsureStatusIdsValid();
    }

    private OnlineStatusOption[] LoadStatusOptions()
    {
        var options = new List<OnlineStatusOption>();

        var sheet = DataManager.GetExcelSheet<OnlineStatus>();
        if (sheet == null)
        {
            return options.ToArray();
        }

        foreach (var row in sheet)
        {
            var name = row.Name.ToString();
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if (!configuration.ShowAllStatuses && !CuratedStatusIds.Contains(row.RowId))
            {
                continue;
            }

            options.Add(new OnlineStatusOption(row.RowId, $"{name} (ID {row.RowId})"));
        }

        return options.ToArray();
    }

    private void EnsureDefaults()
    {
        if (configuration.Version >= 1)
        {
            return;
        }

        configuration.ActiveStatusId = Configuration.UnsetStatusId;
        configuration.InactiveStatusId = FindLegacyStatusId(configuration.StatusOnTabOut);
        configuration.Version = 1;
        configuration.Save(PluginInterface);
    }

    private void EnsureStatusIdsValid()
    {
        var fallback = GetFallbackStatusId();
        if (fallback == Configuration.UnsetStatusId)
        {
            return;
        }

        var changed = false;

        if (!IsStatusIdValid(configuration.ActiveStatusId))
        {
            configuration.ActiveStatusId = fallback;
            changed = true;
        }

        if (!IsStatusIdValid(configuration.InactiveStatusId))
        {
            configuration.InactiveStatusId = fallback;
            changed = true;
        }

        if (changed)
        {
            configuration.Save(PluginInterface);
        }
    }

    private uint GetFallbackStatusId()
    {
        return statusOptions.Length > 0 ? statusOptions[0].Id : Configuration.UnsetStatusId;
    }

    private bool IsStatusIdValid(uint statusId)
    {
        for (var i = 0; i < statusOptions.Length; i++)
        {
            if (statusOptions[i].Id == statusId)
            {
                return true;
            }
        }

        return false;
    }

    private uint FindLegacyStatusId(StatusKind kind)
    {
        return kind switch
        {
            StatusKind.Busy => FindStatusIdByTokens("busy"),
            StatusKind.Away => FindStatusIdByTokens("away"),
            _ => FindStatusIdByTokens("afk")
        };
    }

    private uint FindStatusIdByTokens(params string[] tokens)
    {
        foreach (var option in statusOptions)
        {
            foreach (var token in tokens)
            {
                if (option.Label.Contains(token, StringComparison.OrdinalIgnoreCase))
                {
                    return option.Id;
                }
            }
        }

        return Configuration.UnsetStatusId;
    }


    private unsafe bool TrySetOnlineStatus(uint statusId)
    {
        var infoModule = InfoModule.Instance();
        if (infoModule == null)
        {
            return false;
        }

        var proxy = infoModule->GetInfoProxyById(InfoProxyId.Detail);
        if (proxy == null)
        {
            return false;
        }

        var detailProxy = (InfoProxyDetail*)proxy;
        detailProxy->SendOnlineStatusUpdate(statusId);
        return true;
    }
}

internal readonly record struct OnlineStatusOption(
    uint Id,
    string Label);