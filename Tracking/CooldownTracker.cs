using System.Collections.Generic;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;
using LuminaAction = Lumina.Excel.Sheets.Action;

namespace BetterUI.Tracking;

public sealed class CooldownTracker
{
    private readonly IDataManager data;
    private readonly Dictionary<uint, uint[]> chainCache = new();

    public CooldownTracker(IDataManager data)
    {
        this.data = data;
    }

    public unsafe CooldownInfo Query(uint actionId, bool followCombo = false)
    {
        var sheet = data.GetExcelSheet<LuminaAction>();
        var name = string.Empty;
        ushort icon = 0;

        if (sheet is not null && sheet.TryGetRow(actionId, out var row))
        {
            name = row.Name.ExtractText();
            icon = row.Icon;
        }

        var manager = ActionManager.Instance();
        if (manager is null)
        {
            return new CooldownInfo(actionId, actionId, 0f, 0f, true, true, false, 0, 0, icon, name);
        }

        uint displayId = actionId;
        var comboAdvanced = false;

        if (followCombo && sheet is not null)
        {
            var chain = GetComboChain(actionId);
            var comboLast = manager->Combo.Action;
            if (comboLast != 0)
            {
                for (var i = 0; i < chain.Length - 1; i++)
                {
                    if (chain[i] == comboLast)
                    {
                        displayId = chain[i + 1];
                        comboAdvanced = true;
                        break;
                    }
                }
            }
        }

        var adjusted = manager->GetAdjustedActionId(displayId);
        var maxCharges = ActionManager.GetMaxCharges(adjusted, 0);

        if (sheet is not null && adjusted != actionId && sheet.TryGetRow(adjusted, out var adjRow))
        {
            name = adjRow.Name.ExtractText();
            icon = adjRow.Icon;
        }

        var usableStatus = manager->GetActionStatus(ActionType.Action, adjusted, 0xE000_0000u, false, false);
        var isUsable = usableStatus == 0;

        var group = manager->GetRecastGroup((int)ActionType.Action, adjusted);
        if (group < 0)
            return new CooldownInfo(actionId, adjusted, 0f, 0f, true, isUsable, comboAdvanced, maxCharges, maxCharges, icon, name);

        var detail = manager->GetRecastGroupDetail(group);
        if (detail == null || !detail->IsActive)
            return new CooldownInfo(actionId, adjusted, 0f, 0f, true, isUsable, comboAdvanced, maxCharges, maxCharges, icon, name);

        var total = detail->Total;
        var elapsed = detail->Elapsed;
        ushort currentCharges = maxCharges;
        if (maxCharges > 1 && total > 0f)
        {
            var perCharge = total / maxCharges;
            currentCharges = (ushort)System.Math.Clamp((int)(elapsed / perCharge), 0, maxCharges);
        }
        else
        {
            currentCharges = 0;
        }
        return new CooldownInfo(actionId, adjusted, total, elapsed, false, isUsable, comboAdvanced, maxCharges, currentCharges, icon, name);
    }

    private uint[] GetComboChain(uint baseActionId)
    {
        if (chainCache.TryGetValue(baseActionId, out var cached)) return cached;

        var result = new List<uint> { baseActionId };
        var sheet = data.GetExcelSheet<LuminaAction>();
        if (sheet is null || !sheet.TryGetRow(baseActionId, out var baseRow))
        {
            var arr = result.ToArray();
            chainCache[baseActionId] = arr;
            return arr;
        }

        var jobId = baseRow.ClassJob.RowId;
        var current = baseActionId;

        for (var depth = 0; depth < 8; depth++)
        {
            uint bestId = 0;
            byte bestLevel = byte.MaxValue;
            foreach (var candidate in sheet)
            {
                if (candidate.ActionCombo.RowId != current) continue;
                if (candidate.ClassJob.RowId != jobId) continue;
                if (candidate.Name.IsEmpty) continue;
                if (candidate.IsPvP) continue;
                if (candidate.ClassJobLevel < bestLevel)
                {
                    bestLevel = candidate.ClassJobLevel;
                    bestId = candidate.RowId;
                }
            }
            if (bestId == 0 || result.Contains(bestId)) break;
            result.Add(bestId);
            current = bestId;
        }

        var chain = result.ToArray();
        chainCache[baseActionId] = chain;
        return chain;
    }
}
