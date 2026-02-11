using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using ImGui = Dalamud.Bindings.ImGui.ImGui;

namespace AutoStatus;

public sealed class SettingsWindow : Window
{
    private readonly Configuration configuration;
    private readonly Plugin plugin;

    public SettingsWindow(Plugin plugin, Configuration configuration)
        : base("Auto-Status Settings")
    {
        this.plugin = plugin;
        this.configuration = configuration;

        Flags = Dalamud.Bindings.ImGui.ImGuiWindowFlags.AlwaysAutoResize;
    }

    public override void Draw()
    {
        var activeIndex = plugin.GetStatusIndex(configuration.ActiveStatusId);
        var inactiveIndex = plugin.GetStatusIndex(configuration.InactiveStatusId);
        var labels = plugin.StatusLabels;

        if (ImGui.Combo("Status while focused", ref activeIndex, labels, labels.Length))
        {
            configuration.ActiveStatusId = plugin.GetStatusId(activeIndex);
            configuration.Save(Plugin.PluginInterface);
        }

        if (configuration.ActiveStatusId == Plugin.LookingForPartyStatusId)
        {
            var jobName = configuration.LfpJobNameFocused;
            if (ImGui.InputTextWithHint("LFP job (focused)", "DRK, WHM, GNB...", ref jobName, 32))
            {
                configuration.LfpJobNameFocused = jobName.Trim();
                configuration.Save(Plugin.PluginInterface);
            }
        }

        if (ImGui.Combo("Status while unfocused", ref inactiveIndex, labels, labels.Length))
        {
            configuration.InactiveStatusId = plugin.GetStatusId(inactiveIndex);
            configuration.Save(Plugin.PluginInterface);
        }

        if (configuration.InactiveStatusId == Plugin.LookingForPartyStatusId)
        {
            var jobName = configuration.LfpJobNameUnfocused;
            if (ImGui.InputTextWithHint("LFP job (unfocused)", "DRK, WHM, GNB...", ref jobName, 32))
            {
                configuration.LfpJobNameUnfocused = jobName.Trim();
                configuration.Save(Plugin.PluginInterface);
            }
        }

        ImGui.Separator();

        var showAllStatuses = configuration.ShowAllStatuses;
        if (ImGui.Checkbox("Show all statuses", ref showAllStatuses))
        {
            configuration.ShowAllStatuses = showAllStatuses;
            configuration.Save(Plugin.PluginInterface);
            plugin.ReloadStatusOptions();
        }

        var debugFocus = configuration.DebugFocusLogs;
        if (ImGui.Checkbox("Debug", ref debugFocus))
        {
            configuration.DebugFocusLogs = debugFocus;
            configuration.Save(Plugin.PluginInterface);
        }

        if (configuration.DebugFocusLogs && ImGui.Button("Test unfocused status"))
        {
            plugin.ApplyStatus(isFocused: false, isManual: true);
        }

        if (configuration.DebugFocusLogs)
        {
            ImGui.SameLine();

            if (ImGui.Button("Test focused status"))
            {
                plugin.ApplyStatus(isFocused: true, isManual: true);
            }
        }

        ImGui.Separator();

        var kofiColor = new System.Numerics.Vector4(1.0f, 0.3686f, 0.3569f, 1.0f);
        var textColor = new System.Numerics.Vector4(1.0f, 1.0f, 1.0f, 1.0f);

        ImGui.PushStyleColor(Dalamud.Bindings.ImGui.ImGuiCol.Button, kofiColor);
        ImGui.PushStyleColor(Dalamud.Bindings.ImGui.ImGuiCol.ButtonHovered, kofiColor);
        ImGui.PushStyleColor(Dalamud.Bindings.ImGui.ImGuiCol.ButtonActive, kofiColor);
        ImGui.PushStyleColor(Dalamud.Bindings.ImGui.ImGuiCol.Text, textColor);

        if (ImGui.Button("Support on Ko-fi"))
        {
            Util.OpenLink("https://ko-fi.com/kylo");
        }

        ImGui.PopStyleColor(4);
    }
}
