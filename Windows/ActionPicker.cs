using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Lumina.Excel.Sheets;
using LuminaAction = Lumina.Excel.Sheets.Action;

namespace BetterCDs.Windows;

public sealed class ActionPicker
{
    private string search = string.Empty;
    private bool jobFilter = true;

    public uint? Draw(uint jobId, HashSet<uint> excludeIds)
    {
        ImGui.SetNextItemWidth(240);
        ImGui.InputTextWithHint("##picker-search", "Search by name or ID...", ref search, 64);
        ImGui.SameLine();
        ImGui.Checkbox("Job only", ref jobFilter);

        var actionSheet = Plugin.DataManager.GetExcelSheet<LuminaAction>();
        if (actionSheet is null) return null;

        var trimmed = search.Trim();
        var searchActive = trimmed.Length > 0;
        if (!searchActive && !jobFilter)
        {
            ImGui.TextDisabled("Type to search all actions.");
            return null;
        }

        var acceptedJobs = BuildJobFilter(jobId);
        var searchLower = trimmed.ToLowerInvariant();

        uint? picked = null;

        ImGui.BeginChild("##picker-results", new Vector2(0, 200), true);

        var shown = 0;
        const int limit = 200;
        foreach (var action in actionSheet)
        {
            if (shown >= limit) break;
            if (!PassesBaseFilter(action)) continue;
            if (jobFilter && !acceptedJobs.Contains(action.ClassJob.RowId)) continue;

            var name = action.Name.ExtractText();
            if (searchActive)
            {
                var nameMatch = name.ToLowerInvariant().Contains(searchLower);
                var idMatch = action.RowId.ToString().Contains(trimmed);
                if (!nameMatch && !idMatch) continue;
            }

            var already = excludeIds.Contains(action.RowId);
            if (DrawRow(action, name, already)) picked = action.RowId;
            shown++;
        }

        if (shown == 0)
            ImGui.TextDisabled("No matches.");
        else if (shown >= limit)
            ImGui.TextDisabled($"Showing first {limit}. Refine search to see more.");

        ImGui.EndChild();

        return picked;
    }

    private static HashSet<uint> BuildJobFilter(uint jobId)
    {
        var set = new HashSet<uint> { jobId };
        if (jobId == 0) return set;

        var jobSheet = Plugin.DataManager.GetExcelSheet<ClassJob>();
        if (jobSheet is not null && jobSheet.TryGetRow(jobId, out var job))
        {
            var parent = job.ClassJobParent.RowId;
            if (parent != 0 && parent != jobId) set.Add(parent);
        }
        return set;
    }

    private static bool PassesBaseFilter(LuminaAction action)
    {
        if (action.Name.IsEmpty) return false;
        if (action.CooldownGroup == 0) return false;
        if (action.IsPvP) return false;
        if (action.ClassJobLevel == 0) return false;
        return true;
    }

    private static bool DrawRow(LuminaAction action, string name, bool alreadyTracked)
    {
        ImGui.PushID((int)action.RowId);

        var iconSize = new Vector2(22, 22);
        var tex = Plugin.TryGetIcon(action.Icon);
        if (tex is not null)
            ImGui.Image(tex.Handle, iconSize);
        else
            ImGui.Dummy(iconSize);
        ImGui.SameLine();

        var label = alreadyTracked
            ? $"{name}  [{action.RowId}]  — added"
            : $"{name}  [{action.RowId}]";

        var clicked = false;
        using (new DisabledScope(alreadyTracked))
        {
            if (ImGui.Selectable(label, false, ImGuiSelectableFlags.None))
                clicked = true;
        }

        ImGui.PopID();
        return clicked && !alreadyTracked;
    }
}
