using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using BetterCDs.Profiles;
using BetterCDs.Tracking;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Plugin.Services;

namespace BetterCDs.Windows;

public sealed class BarRenderer : IDisposable
{
    private readonly Plugin plugin;
    private readonly CooldownTracker tracker;
    private readonly ITextureProvider textureProvider;
    private readonly IClientState clientState;

    private readonly Dictionary<string, Vector2> resolvedPositions = new();

    public BarRenderer(Plugin plugin, CooldownTracker tracker, ITextureProvider textureProvider, IClientState clientState)
    {
        this.plugin = plugin;
        this.tracker = tracker;
        this.textureProvider = textureProvider;
        this.clientState = clientState;
    }

    public void Dispose() { }

    public void Draw()
    {
        if (!plugin.Configuration.ShowOverlay) return;
        if (clientState.LocalPlayer is null) return;

        var job = clientState.LocalPlayer.ClassJob.RowId;
        if (job == 0) return;

        var profile = plugin.ProfileStore.GetActive(job);
        if (profile is null || profile.Tracked.Count == 0) return;

        var byGroup = profile.Tracked
            .GroupBy(t => t.GroupName)
            .ToDictionary(g => g.Key, g => g.OrderBy(t => t.Order).ToList());

        resolvedPositions.Clear();
        foreach (var groupName in profile.Groups)
        {
            if (!byGroup.TryGetValue(groupName, out var actions) || actions.Count == 0) continue;
            var settings = GetOrCreateBarSettings(groupName);
            var pos = ResolvePosition(groupName, settings, new HashSet<string>());
            resolvedPositions[groupName] = pos;
            DrawBar(groupName, actions, settings, pos);
        }
    }

    private BarSettings GetOrCreateBarSettings(string groupName)
    {
        if (!plugin.Configuration.Bars.TryGetValue(groupName, out var settings))
        {
            settings = new BarSettings { Position = plugin.Configuration.OverlayPosition };
            plugin.Configuration.Bars[groupName] = settings;
            plugin.Configuration.Save();
        }
        return settings;
    }

    private Vector2 ResolvePosition(string groupName, BarSettings settings, HashSet<string> visited)
    {
        if (string.IsNullOrEmpty(settings.AnchorToGroup))
            return settings.Position;

        if (!visited.Add(groupName))
            return settings.Position;

        if (resolvedPositions.TryGetValue(settings.AnchorToGroup, out var anchorPos))
            return anchorPos + settings.AnchorOffset;

        if (plugin.Configuration.Bars.TryGetValue(settings.AnchorToGroup, out var anchorSettings))
            return ResolvePosition(settings.AnchorToGroup, anchorSettings, visited) + settings.AnchorOffset;

        return settings.Position;
    }

    private void DrawBar(string groupName, List<TrackedAction> actions, BarSettings settings, Vector2 position)
    {
        var locked = plugin.Configuration.OverlayLocked;
        var anchored = !string.IsNullOrEmpty(settings.AnchorToGroup);

        var flags = ImGuiWindowFlags.NoScrollbar
                    | ImGuiWindowFlags.NoScrollWithMouse
                    | ImGuiWindowFlags.NoTitleBar
                    | ImGuiWindowFlags.NoCollapse
                    | ImGuiWindowFlags.AlwaysAutoResize
                    | ImGuiWindowFlags.NoFocusOnAppearing
                    | ImGuiWindowFlags.NoSavedSettings;

        if (locked)
            flags |= ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoBackground;
        if (anchored)
            flags |= ImGuiWindowFlags.NoMove;

        var cond = (locked || anchored) ? ImGuiCond.Always : ImGuiCond.FirstUseEver;
        var pivot = new Vector2(0.5f, 0.5f);
        ImGui.SetNextWindowPos(position, cond, pivot);

        if (ImGui.Begin($"BetterCDs##bar_{groupName}", flags))
        {
            for (var i = 0; i < actions.Count; i++)
            {
                if (i > 0)
                {
                    if (settings.Vertical)
                        ImGui.Dummy(new Vector2(0, plugin.Configuration.OverlayIconSpacing));
                    else
                        ImGui.SameLine(0, plugin.Configuration.OverlayIconSpacing);
                }
                DrawAction(actions[i]);
            }

            if (!locked && !anchored)
            {
                var topLeft = ImGui.GetWindowPos();
                var windowSize = ImGui.GetWindowSize();
                var center = topLeft + windowSize * 0.5f;
                if (System.Math.Abs(center.X - settings.Position.X) > 0.5f ||
                    System.Math.Abs(center.Y - settings.Position.Y) > 0.5f)
                {
                    settings.Position = center;
                    plugin.Configuration.Save();
                }
            }
        }
        ImGui.End();
    }

    private void DrawAction(TrackedAction tracked)
    {
        var info = tracker.Query(tracked.ActionId, tracked.FollowComboChain);
        var size = new Vector2(plugin.Configuration.OverlayIconSize);
        var cursor = ImGui.GetCursorScreenPos();

        var tex = Plugin.TryGetIcon(info.IconId);
        var dim = tracked.DimWhenNotUsable && !info.IsUsable;

        if (tex is not null)
        {
            var tint = dim
                ? new Vector4(0.35f, 0.35f, 0.35f, 1f)
                : new Vector4(1f, 1f, 1f, 1f);
            ImGui.Image(tex.Handle, size, Vector2.Zero, Vector2.One, tint, Vector4.Zero);
        }
        else
        {
            ImGui.Dummy(size);
        }

        var drawList = ImGui.GetWindowDrawList();

        var suppressRecast = tracked.DimWhenNotUsable || tracked.FollowComboChain;
        if (!info.IsReady && info.TotalRecast > 0 && !suppressRecast)
        {
            var fill = info.FillFraction;
            var center = new Vector2(cursor.X + size.X * 0.5f, cursor.Y + size.Y * 0.5f);
            var radius = size.X;
            var startAngle = -MathF.PI / 2f + fill * MathF.PI * 2f;
            var endAngle = -MathF.PI / 2f + MathF.PI * 2f;

            drawList.PushClipRect(cursor, cursor + size, true);
            drawList.PathLineTo(center);
            drawList.PathArcTo(center, radius, startAngle, endAngle, 64);
            drawList.PathFillConvex(0xA0000000);
            drawList.PopClipRect();

            if (tracked.ShowTimer)
            {
                var remaining = info.Remaining;
                var text = remaining >= 10f ? $"{(int)MathF.Ceiling(remaining)}" : $"{remaining:0.0}";
                var textSize = ImGui.CalcTextSize(text);
                var textPos = new Vector2(
                    cursor.X + (size.X - textSize.X) * 0.5f,
                    cursor.Y + (size.Y - textSize.Y) * 0.5f);
                drawList.AddText(textPos + new Vector2(1, 1), 0xFF000000, text);
                drawList.AddText(textPos, 0xFFFFFFFF, text);
            }
        }

        var buffGlow = tracked.GlowBuffId is { } buffId && buffId != 0 && HasActiveBuff(buffId);
        var chainGlow = (tracked.DimWhenNotUsable && info.IsUsable)
                        || (tracked.FollowComboChain && info.ComboAdvanced);
        if (buffGlow || chainGlow)
        {
            var pulse = (MathF.Sin((float)ImGui.GetTime() * 4f) + 1f) * 0.5f;
            var alpha = 0.6f + pulse * 0.4f;
            var tint = buffGlow
                ? new Vector4(1f, 0.85f, 0.2f, alpha)
                : new Vector4(0.4f, 0.9f, 1f, alpha);
            var color = ImGui.GetColorU32(tint);
            var pad = new Vector2(2, 2);
            drawList.AddRect(cursor - pad, cursor + size + pad, color, 2f, ImDrawFlags.None, 3f);
        }

        if (info.MaxCharges > 1)
        {
            var chargeText = info.CurrentCharges.ToString();
            var font = ImGui.GetFont();
            var fontSize = size.Y * 0.25f;
            var textPos = new Vector2(
                cursor.X + 2,
                cursor.Y + size.Y - fontSize - 2);
            drawList.AddText(font, fontSize, textPos + new Vector2(1, 1), 0xFF000000, chargeText);
            drawList.AddText(font, fontSize, textPos, 0xFFFFFFFF, chargeText);
        }

        if (ImGui.IsItemHovered() && !string.IsNullOrEmpty(info.ActionName))
            ImGui.SetTooltip(info.ActionName);
    }

    private bool HasActiveBuff(uint statusId)
    {
        var player = clientState.LocalPlayer;
        if (player is null) return false;
        foreach (var s in player.StatusList)
            if (s.StatusId == statusId) return true;
        return false;
    }
}
