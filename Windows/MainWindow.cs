using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace BetterCDs.Windows;

public sealed class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;

    public MainWindow(Plugin plugin)
        : base("BetterCDs##Main", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse)
    {
        this.plugin = plugin;
        Size = new Vector2(360, 0);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public void Dispose() { }

    public override void Draw()
    {
        var job = Plugin.ClientState.LocalPlayer?.ClassJob.RowId ?? 0;
        var jobLabel = job == 0 ? "—" : plugin.ProfileStore.GetJobAbbreviation(job);
        var active = job == 0 ? null : plugin.ProfileStore.GetActive(job);

        ImGui.Text($"Current job: {jobLabel}");
        ImGui.Text($"Active profile: {active?.Name ?? "(none)"}");
        ImGui.Text($"Overlay: {(plugin.Configuration.ShowOverlay ? "on" : "off")} / {(plugin.Configuration.OverlayLocked ? "locked" : "unlocked")}");

        ImGui.Spacing();
        if (ImGui.Button("Open settings"))
            plugin.ToggleConfigUi();

        ImGui.SameLine();
        if (ImGui.Button(plugin.Configuration.OverlayLocked ? "Unlock overlay" : "Lock overlay"))
        {
            plugin.Configuration.OverlayLocked = !plugin.Configuration.OverlayLocked;
            plugin.Configuration.Save();
        }
    }
}
