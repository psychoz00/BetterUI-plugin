using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Lumina.Excel.Sheets;

namespace BetterCDs.Windows;

public sealed class StatusPicker
{
    private string search = string.Empty;

    public uint? Draw()
    {
        ImGui.SetNextItemWidth(240);
        ImGui.InputTextWithHint("##status-search", "Search by name or ID...", ref search, 64);

        var sheet = Plugin.DataManager.GetExcelSheet<Status>();
        if (sheet is null) return null;

        var trimmed = search.Trim();
        if (trimmed.Length == 0)
        {
            ImGui.TextDisabled("Type to search.");
            return null;
        }

        var searchLower = trimmed.ToLowerInvariant();
        uint? picked = null;

        ImGui.BeginChild("##status-results", new Vector2(0, 200), true);

        var shown = 0;
        const int limit = 200;
        foreach (var status in sheet)
        {
            if (shown >= limit) break;
            if (status.Name.IsEmpty) continue;
            if (status.Icon == 0) continue;

            var name = status.Name.ExtractText();
            var nameMatch = name.ToLowerInvariant().Contains(searchLower);
            var idMatch = status.RowId.ToString().Contains(trimmed);
            if (!nameMatch && !idMatch) continue;

            if (DrawRow(status, name)) picked = status.RowId;
            shown++;
        }

        if (shown == 0)
            ImGui.TextDisabled("No matches.");
        else if (shown >= limit)
            ImGui.TextDisabled($"Showing first {limit}. Refine search.");

        ImGui.EndChild();
        return picked;
    }

    private static bool DrawRow(Status status, string name)
    {
        ImGui.PushID((int)status.RowId);
        var iconSize = new Vector2(22, 22);
        var tex = Plugin.TryGetIcon(status.Icon);
        if (tex is not null)
            ImGui.Image(tex.Handle, iconSize);
        else
            ImGui.Dummy(iconSize);
        ImGui.SameLine();

        var clicked = ImGui.Selectable($"{name}  [{status.RowId}]", false, ImGuiSelectableFlags.None);
        ImGui.PopID();
        return clicked;
    }
}
