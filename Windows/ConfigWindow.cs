using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using BetterUI.Profiles;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Windowing;
using Lumina.Excel.Sheets;
using LuminaAction = Lumina.Excel.Sheets.Action;

namespace BetterUI.Windows;

public sealed class ConfigWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private readonly ActionPicker picker = new();
    private readonly StatusPicker statusPicker = new();
    private uint selectedJobId;
    private string importBuffer = string.Empty;
    private string importError = string.Empty;
    private string newGroupBuffer = string.Empty;

    public ConfigWindow(Plugin plugin)
        : base("BetterUI — Settings##Config", ImGuiWindowFlags.NoCollapse)
    {
        this.plugin = plugin;
        Size = new Vector2(680, 560);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public void Dispose() { }

    public override void OnOpen()
    {
        if (selectedJobId == 0)
            selectedJobId = Plugin.ClientState.LocalPlayer?.ClassJob.RowId ?? 1;
    }

    public override void Draw()
    {
        if (ImGui.BeginTabBar("##bcd-tabs"))
        {
            if (ImGui.BeginTabItem("General"))
            {
                DrawGeneralTab();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Profile"))
            {
                DrawProfileTab();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Actions & Groups"))
            {
                DrawActionsTab();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Bars"))
            {
                DrawBarsTab();
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
    }

    private void DrawGeneralTab()
    {
        var showOverlay = plugin.Configuration.ShowOverlay;
        if (ImGui.Checkbox("Show overlay", ref showOverlay))
        {
            plugin.Configuration.ShowOverlay = showOverlay;
            plugin.Configuration.Save();
        }

        ImGui.SameLine();
        var locked = plugin.Configuration.OverlayLocked;
        if (ImGui.Checkbox("Lock bars", ref locked))
        {
            plugin.Configuration.OverlayLocked = locked;
            plugin.Configuration.Save();
        }

        ImGui.Spacing();
        ImGui.TextDisabled("Icon appearance");

        var size = plugin.Configuration.OverlayIconSize;
        if (ImGui.SliderFloat("Icon size", ref size, 24f, 96f, "%.0f px"))
        {
            plugin.Configuration.OverlayIconSize = size;
            plugin.Configuration.Save();
        }

        var spacing = plugin.Configuration.OverlayIconSpacing;
        if (ImGui.SliderFloat("Icon spacing", ref spacing, 0f, 20f, "%.0f px"))
        {
            plugin.Configuration.OverlayIconSpacing = spacing;
            plugin.Configuration.Save();
        }
    }

    private void DrawProfileTab()
    {
        DrawJobSelector();
        ImGui.Separator();
        DrawProfileToolbar();
    }

    private void DrawActionsTab()
    {
        if (selectedJobId == 0)
        {
            ImGui.TextDisabled("Pick a job on the Profile tab.");
            return;
        }
        var active = plugin.ProfileStore.GetActive(selectedJobId);
        if (active is null)
        {
            ImGui.TextDisabled("No active profile.");
            return;
        }

        DrawGroupsEditor(active);
        ImGui.Separator();
        DrawTrackedTable(active);
    }

    private void DrawBarsTab()
    {
        if (selectedJobId == 0)
        {
            ImGui.TextDisabled("Pick a job on the Profile tab.");
            return;
        }
        var active = plugin.ProfileStore.GetActive(selectedJobId);
        if (active is null)
        {
            ImGui.TextDisabled("No active profile.");
            return;
        }

        ImGui.TextWrapped("Each group is rendered as a separate bar. You can position a bar freely, or anchor it to another bar so they move together.");
        ImGui.Spacing();

        foreach (var group in active.Groups)
        {
            ImGui.PushID($"bar_{group}");
            DrawBarRow(active, group);
            ImGui.PopID();
            ImGui.Separator();
        }
    }

    private void DrawBarRow(Profile active, string group)
    {
        var settings = GetOrCreateBarSettings(group);

        ImGui.Text(group);

        var pos = settings.Position;
        ImGui.SetNextItemWidth(200);
        if (ImGui.DragFloat2("Position (center)", ref pos, 1f))
        {
            settings.Position = pos;
            plugin.Configuration.Save();
        }

        ImGui.SameLine();
        var vertical = settings.Vertical;
        if (ImGui.Checkbox("Vertical", ref vertical))
        {
            settings.Vertical = vertical;
            plugin.Configuration.Save();
        }

        var anchored = !string.IsNullOrEmpty(settings.AnchorToGroup);
        var anchorLabel = anchored ? settings.AnchorToGroup! : "(none)";
        ImGui.SetNextItemWidth(160);
        if (ImGui.BeginCombo("Anchor to", anchorLabel))
        {
            if (ImGui.Selectable("(none)", !anchored))
            {
                settings.AnchorToGroup = null;
                plugin.Configuration.Save();
            }
            foreach (var other in active.Groups)
            {
                if (other == group) continue;
                if (CreatesCycle(group, other)) continue;
                if (ImGui.Selectable(other, settings.AnchorToGroup == other))
                {
                    settings.AnchorToGroup = other;
                    plugin.Configuration.Save();
                }
            }
            ImGui.EndCombo();
        }

        if (anchored)
        {
            var offset = settings.AnchorOffset;
            ImGui.SetNextItemWidth(200);
            if (ImGui.DragFloat2("Offset", ref offset, 1f))
            {
                settings.AnchorOffset = offset;
                plugin.Configuration.Save();
            }
        }
    }

    private bool CreatesCycle(string source, string target)
    {
        var visited = new HashSet<string> { source };
        var current = target;
        while (current is not null && plugin.Configuration.Bars.TryGetValue(current, out var s))
        {
            if (!visited.Add(current)) return true;
            if (s.AnchorToGroup == source) return true;
            current = s.AnchorToGroup;
        }
        return false;
    }

    private BarSettings GetOrCreateBarSettings(string group)
    {
        if (!plugin.Configuration.Bars.TryGetValue(group, out var settings))
        {
            settings = new BarSettings { Position = plugin.Configuration.OverlayPosition };
            plugin.Configuration.Bars[group] = settings;
            plugin.Configuration.Save();
        }
        return settings;
    }

    private void DrawJobSelector()
    {
        var jobSheet = Plugin.DataManager.GetExcelSheet<ClassJob>();
        if (jobSheet is null) return;

        var currentAbbrev = plugin.ProfileStore.GetJobAbbreviation(selectedJobId);
        ImGui.Text("Job:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(200);
        if (ImGui.BeginCombo("##job", currentAbbrev))
        {
            foreach (var job in jobSheet.Where(j => j.RowId > 0 && !j.Abbreviation.IsEmpty).OrderBy(j => j.RowId))
            {
                var label = $"{job.Abbreviation.ExtractText()} — {job.Name.ExtractText()}";
                if (ImGui.Selectable(label, job.RowId == selectedJobId))
                    selectedJobId = job.RowId;
            }
            ImGui.EndCombo();
        }

        ImGui.SameLine();
        if (ImGui.Button("Use current job"))
            selectedJobId = Plugin.ClientState.LocalPlayer?.ClassJob.RowId ?? selectedJobId;
    }

    private void DrawProfileToolbar()
    {
        if (selectedJobId == 0) return;
        var set = plugin.ProfileStore.GetOrCreate(selectedJobId);
        var active = set.Profiles.FirstOrDefault(p => p.Id == set.ActiveProfileId);

        ImGui.Text("Profile:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(220);
        if (ImGui.BeginCombo("##profile", active?.Name ?? "(none)"))
        {
            foreach (var p in set.Profiles)
            {
                if (ImGui.Selectable(p.Name, p.Id == set.ActiveProfileId))
                    plugin.ProfileStore.SetActive(selectedJobId, p.Id);
            }
            ImGui.EndCombo();
        }

        ImGui.SameLine();
        if (ImGui.Button("New"))
            plugin.ProfileStore.CreateEmpty(selectedJobId);

        ImGui.SameLine();
        if (ImGui.Button("Duplicate") && active is not null)
            plugin.ProfileStore.Duplicate(selectedJobId, active);

        ImGui.SameLine();
        if (ImGui.Button("Delete") && active is not null)
            plugin.ProfileStore.Delete(selectedJobId, active.Id);

        if (active is not null)
        {
            var name = active.Name;
            ImGui.SetNextItemWidth(220);
            if (ImGui.InputText("Name", ref name, 64))
            {
                active.Name = name;
                plugin.Configuration.Save();
            }
        }

        ImGui.Spacing();
        ImGui.TextDisabled("Share");

        if (ImGui.Button("Export to clipboard") && active is not null)
        {
            ImGui.SetClipboardText(ProfileShareCodec.Export(active));
            Plugin.ChatGui.Print("[BetterUI] Profile copied to clipboard.");
        }

        ImGui.SameLine();
        if (ImGui.Button("Import from clipboard"))
            TryImport(ImGui.GetClipboardText() ?? string.Empty);

        ImGui.SetNextItemWidth(-120);
        ImGui.InputText("##import", ref importBuffer, 4096);
        ImGui.SameLine();
        if (ImGui.Button("Import string"))
            TryImport(importBuffer);

        if (!string.IsNullOrEmpty(importError))
            ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), importError);
    }

    private void TryImport(string raw)
    {
        if (ProfileShareCodec.TryImport(raw, out var imported, out var err) && imported is not null)
        {
            var stored = plugin.ProfileStore.AddImported(imported);
            plugin.ProfileStore.SetActive(stored.JobId, stored.Id);
            if (stored.JobId != selectedJobId) selectedJobId = stored.JobId;
            importError = string.Empty;
            importBuffer = string.Empty;
            Plugin.ChatGui.Print($"[BetterUI] Imported \"{stored.Name}\".");
        }
        else
        {
            importError = err;
        }
    }

    private void DrawTrackedTable(Profile active)
    {
        ImGui.Text($"Tracked actions ({active.Tracked.Count}):");

        if (ImGui.BeginTable("##actions", 8,
            ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY,
            new Vector2(0, 260)))
        {
            ImGui.TableSetupColumn("Icon", ImGuiTableColumnFlags.WidthFixed, 36);
            ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Group", ImGuiTableColumnFlags.WidthFixed, 120);
            ImGui.TableSetupColumn("Order", ImGuiTableColumnFlags.WidthFixed, 70);
            ImGui.TableSetupColumn("Glow", ImGuiTableColumnFlags.WidthFixed, 130);
            ImGui.TableSetupColumn("Dim", ImGuiTableColumnFlags.WidthFixed, 42);
            ImGui.TableSetupColumn("Combo", ImGuiTableColumnFlags.WidthFixed, 54);
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableHeadersRow();

            var actionSheet = Plugin.DataManager.GetExcelSheet<LuminaAction>();
            TrackedAction? toRemove = null;

            var ordered = active.Tracked.OrderBy(t => t.Order).ToList();
            for (var i = 0; i < ordered.Count; i++)
            {
                var tracked = ordered[i];
                ImGui.PushID((int)tracked.ActionId + i * 100000);

                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                if (actionSheet is not null && actionSheet.TryGetRow(tracked.ActionId, out var row))
                {
                    var tex = Plugin.TryGetIcon(row.Icon);
                    if (tex is not null)
                        ImGui.Image(tex.Handle, new Vector2(28, 28));
                }

                ImGui.TableNextColumn();
                var name = actionSheet is not null && actionSheet.TryGetRow(tracked.ActionId, out var nameRow)
                    ? nameRow.Name.ExtractText()
                    : $"#{tracked.ActionId}";
                ImGui.TextUnformatted($"{name}  [{tracked.ActionId}]");

                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(-1);
                if (ImGui.BeginCombo("##grp", tracked.GroupName))
                {
                    foreach (var g in active.Groups)
                        if (ImGui.Selectable(g, g == tracked.GroupName))
                        {
                            tracked.GroupName = g;
                            plugin.Configuration.Save();
                        }
                    ImGui.EndCombo();
                }

                ImGui.TableNextColumn();
                if (ImGui.SmallButton("▲") && i > 0)
                {
                    var prev = ordered[i - 1];
                    (tracked.Order, prev.Order) = (prev.Order, tracked.Order);
                    plugin.Configuration.Save();
                }
                ImGui.SameLine();
                if (ImGui.SmallButton("▼") && i < ordered.Count - 1)
                {
                    var next = ordered[i + 1];
                    (tracked.Order, next.Order) = (next.Order, tracked.Order);
                    plugin.Configuration.Save();
                }

                ImGui.TableNextColumn();
                DrawGlowCell(tracked);

                ImGui.TableNextColumn();
                var dim = tracked.DimWhenNotUsable;
                if (ImGui.Checkbox("##dim", ref dim))
                {
                    tracked.DimWhenNotUsable = dim;
                    plugin.Configuration.Save();
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Dim icon when the action is not usable (resource/status missing). Glow border while usable. Good for proc-gated chains like Attonement.");

                ImGui.TableNextColumn();
                var combo = tracked.FollowComboChain;
                if (ImGui.Checkbox("##combo", ref combo))
                {
                    tracked.FollowComboChain = combo;
                    plugin.Configuration.Save();
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Follow combo progression: icon swaps to the next step as the combo advances. Glow only while past the first step. Good for basic combos like Fast Blade → Riot Blade → Royal Authority.");

                ImGui.TableNextColumn();
                if (ImGui.SmallButton("Remove"))
                    toRemove = tracked;

                ImGui.PopID();
            }

            if (toRemove is not null)
            {
                active.Tracked.Remove(toRemove);
                plugin.Configuration.Save();
            }

            ImGui.EndTable();
        }

        ImGui.Spacing();
        ImGui.TextUnformatted("Add action:");

        var existing = new HashSet<uint>(active.Tracked.Select(t => t.ActionId));
        if (picker.Draw(selectedJobId, existing) is { } pickedId)
        {
            var order = active.Tracked.Count == 0 ? 0 : active.Tracked.Max(t => t.Order) + 1;
            active.Tracked.Add(new TrackedAction
            {
                ActionId = pickedId,
                Order = order,
                GroupName = active.Groups.FirstOrDefault() ?? "Essential",
            });
            plugin.Configuration.Save();
        }
    }

    private void DrawGlowCell(TrackedAction tracked)
    {
        var statusSheet = Plugin.DataManager.GetExcelSheet<Status>();

        if (tracked.GlowBuffId is { } buffId && buffId != 0)
        {
            if (statusSheet is not null && statusSheet.TryGetRow(buffId, out var statusRow))
            {
                var tex = Plugin.TryGetIcon(statusRow.Icon);
                if (tex is not null)
                {
                    ImGui.Image(tex.Handle, new Vector2(20, 20));
                    ImGui.SameLine();
                }
            }
            if (ImGui.SmallButton("Edit"))
                ImGui.OpenPopup("##glow-popup");
            ImGui.SameLine();
            if (ImGui.SmallButton("×"))
            {
                tracked.GlowBuffId = null;
                plugin.Configuration.Save();
            }
        }
        else
        {
            if (ImGui.SmallButton("Set glow"))
                ImGui.OpenPopup("##glow-popup");
        }

        if (ImGui.BeginPopup("##glow-popup"))
        {
            if (statusPicker.Draw() is { } pickedId)
            {
                tracked.GlowBuffId = pickedId;
                plugin.Configuration.Save();
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }
    }

    private void DrawGroupsEditor(Profile active)
    {
        ImGui.Text($"Groups ({active.Groups.Count}):");

        string? toRemove = null;
        for (var i = 0; i < active.Groups.Count; i++)
        {
            ImGui.PushID($"group_{i}");
            var original = active.Groups[i];
            var buffer = original;
            ImGui.SetNextItemWidth(160);
            if (ImGui.InputText("##name", ref buffer, 32, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                var trimmed = buffer.Trim();
                if (!string.IsNullOrEmpty(trimmed) && trimmed != original && !active.Groups.Contains(trimmed))
                {
                    RenameGroup(active, original, trimmed);
                    active.Groups[i] = trimmed;
                    plugin.Configuration.Save();
                }
            }
            ImGui.SameLine();
            if (ImGui.SmallButton("▲") && i > 0)
            {
                (active.Groups[i], active.Groups[i - 1]) = (active.Groups[i - 1], active.Groups[i]);
                plugin.Configuration.Save();
            }
            ImGui.SameLine();
            if (ImGui.SmallButton("▼") && i < active.Groups.Count - 1)
            {
                (active.Groups[i], active.Groups[i + 1]) = (active.Groups[i + 1], active.Groups[i]);
                plugin.Configuration.Save();
            }
            ImGui.SameLine();
            using (new DisabledScope(active.Groups.Count <= 1))
            {
                if (ImGui.SmallButton("Remove"))
                    toRemove = original;
            }
            ImGui.PopID();
        }

        if (toRemove is not null)
        {
            var replacement = active.Groups.FirstOrDefault(g => g != toRemove) ?? "Essential";
            foreach (var t in active.Tracked)
                if (t.GroupName == toRemove) t.GroupName = replacement;
            active.Groups.Remove(toRemove);
            RemoveBarSettings(toRemove);
            plugin.Configuration.Save();
        }

        ImGui.SetNextItemWidth(160);
        ImGui.InputTextWithHint("##newgroup", "New group name", ref newGroupBuffer, 32);
        ImGui.SameLine();
        if (ImGui.Button("Add group"))
        {
            var trimmed = newGroupBuffer.Trim();
            if (!string.IsNullOrEmpty(trimmed) && !active.Groups.Contains(trimmed))
            {
                active.Groups.Add(trimmed);
                newGroupBuffer = string.Empty;
                plugin.Configuration.Save();
            }
        }
    }

    private void RenameGroup(Profile active, string oldName, string newName)
    {
        foreach (var t in active.Tracked)
            if (t.GroupName == oldName) t.GroupName = newName;

        if (plugin.Configuration.Bars.Remove(oldName, out var settings))
            plugin.Configuration.Bars[newName] = settings;

        foreach (var s in plugin.Configuration.Bars.Values)
            if (s.AnchorToGroup == oldName) s.AnchorToGroup = newName;
    }

    private void RemoveBarSettings(string group)
    {
        plugin.Configuration.Bars.Remove(group);
        foreach (var s in plugin.Configuration.Bars.Values)
            if (s.AnchorToGroup == group)
            {
                s.AnchorToGroup = null;
                s.AnchorOffset = Vector2.Zero;
            }
    }
}
