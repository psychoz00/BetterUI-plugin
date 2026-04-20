using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using BetterUI.Profiles;
using BetterUI.Tracking;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Plugin.Services;

namespace BetterUI.Windows;

public sealed class BarRenderer : IDisposable
{
    private readonly Plugin plugin;
    private readonly CooldownTracker tracker;
    private readonly ITextureProvider textureProvider;
    private readonly IClientState clientState;

    private readonly Dictionary<string, Vector2> resolvedPositions = new();
    private readonly Dictionary<string, Vector2> appliedPositions = new();
    private readonly Dictionary<string, Vector2> groupSizes = new();

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
        var screenCenter = ImGui.GetIO().DisplaySize * 0.5f;

        if (string.IsNullOrEmpty(settings.AnchorToGroup))
            return screenCenter + settings.Position;

        if (!visited.Add(groupName))
            return screenCenter + settings.Position;

        Vector2 anchorPos;
        if (resolvedPositions.TryGetValue(settings.AnchorToGroup, out var cached))
            anchorPos = cached;
        else if (plugin.Configuration.Bars.TryGetValue(settings.AnchorToGroup, out var anchorSettings))
            anchorPos = ResolvePosition(settings.AnchorToGroup, anchorSettings, visited);
        else
            return screenCenter + settings.Position;

        var anchorSize = groupSizes.GetValueOrDefault(settings.AnchorToGroup, Vector2.Zero);
        var selfSize = groupSizes.GetValueOrDefault(groupName, Vector2.Zero);
        var directional = ComputeDirectionalOffset(settings.AnchorDirection, anchorSize, selfSize);
        return anchorPos + directional + settings.AnchorOffset;
    }

    private static Vector2 ComputeDirectionalOffset(AnchorSide side, Vector2 anchorSize, Vector2 selfSize)
    {
        const float gap = 4f;
        return side switch
        {
            AnchorSide.Up    => new Vector2(0, -(anchorSize.Y + selfSize.Y) * 0.5f - gap),
            AnchorSide.Down  => new Vector2(0,  (anchorSize.Y + selfSize.Y) * 0.5f + gap),
            AnchorSide.Left  => new Vector2(-(anchorSize.X + selfSize.X) * 0.5f - gap, 0),
            AnchorSide.Right => new Vector2( (anchorSize.X + selfSize.X) * 0.5f + gap, 0),
            _ => Vector2.Zero,
        };
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
        if (!anchored && !locked &&
            (!appliedPositions.TryGetValue(groupName, out var applied) || applied != settings.Position))
        {
            cond = ImGuiCond.Always;
        }
        var pivot = new Vector2(0.5f, 0.5f);
        ImGui.SetNextWindowPos(position, cond, pivot);

        if (ImGui.Begin($"BetterUI##bar_{groupName}", flags))
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

            groupSizes[groupName] = ImGui.GetWindowSize();

            if (!locked && !anchored)
            {
                var topLeft = ImGui.GetWindowPos();
                var windowSize = ImGui.GetWindowSize();
                var screenCenter = ImGui.GetIO().DisplaySize * 0.5f;
                var relative = topLeft + windowSize * 0.5f - screenCenter;
                if (System.Math.Abs(relative.X - settings.Position.X) > 0.5f ||
                    System.Math.Abs(relative.Y - settings.Position.Y) > 0.5f)
                {
                    settings.Position = relative;
                    plugin.Configuration.Save();
                }
                appliedPositions[groupName] = settings.Position;
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

        DrawEclipseVignette(drawList, cursor, size);

        if (!info.IsReady && info.TotalRecast > 0)
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
                DrawCooldownText(drawList, cursor, size, text);
            }
        }

        var buffGlow = tracked.GlowBuffId is { } buffId && buffId != 0 && HasActiveBuff(buffId);
        var chainGlow = (tracked.DimWhenNotUsable && info.IsUsable)
                        || (tracked.FollowComboChain && info.ComboAdvanced);
        if (buffGlow || chainGlow)
        {
            var baseTint = new Vector4(1f, 0.72f, 0.18f, 1f);
            DrawProcGlow(drawList, cursor, size, baseTint);
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

    private static void DrawEclipseVignette(ImDrawListPtr drawList, Vector2 cursor, Vector2 size)
    {
        const uint dark = 0x70000000;
        const uint medium = 0x30000000;
        const uint light = 0x00000000;

        var center = cursor + size * 0.5f;
        var topMid = new Vector2(center.X, cursor.Y);
        var rightMid = new Vector2(cursor.X + size.X, center.Y);
        var bottomMid = new Vector2(center.X, cursor.Y + size.Y);
        var leftMid = new Vector2(cursor.X, center.Y);
        var tl = cursor;
        var tr = new Vector2(cursor.X + size.X, cursor.Y);
        var br = cursor + size;
        var bl = new Vector2(cursor.X, cursor.Y + size.Y);

        drawList.AddRectFilledMultiColor(tl, center, dark, medium, light, medium);
        drawList.AddRectFilledMultiColor(topMid, rightMid, medium, dark, medium, light);
        drawList.AddRectFilledMultiColor(center, br, light, medium, dark, medium);
        drawList.AddRectFilledMultiColor(leftMid, bottomMid, medium, light, medium, dark);
    }

    private static void DrawCooldownText(ImDrawListPtr drawList, Vector2 cursor, Vector2 size, string text)
    {
        var font = ImGui.GetFont();
        var fontSize = MathF.Max(12f, size.Y * 0.4f);
        var scale = fontSize / ImGui.GetFontSize();
        var textSize = ImGui.CalcTextSize(text) * scale;
        var textPos = new Vector2(
            cursor.X + (size.X - textSize.X) * 0.5f,
            cursor.Y + (size.Y - textSize.Y) * 0.5f);

        const uint shadowColor = 0xFF000000;
        const float strokeOffset = 1.5f;
        for (var dx = -1; dx <= 1; dx++)
        for (var dy = -1; dy <= 1; dy++)
        {
            if (dx == 0 && dy == 0) continue;
            drawList.AddText(font, fontSize, textPos + new Vector2(dx * strokeOffset, dy * strokeOffset), shadowColor, text);
        }
        drawList.AddText(font, fontSize, textPos, 0xFFFFFFFF, text);
    }

    private static void DrawProcGlow(ImDrawListPtr drawList, Vector2 cursor, Vector2 size, Vector4 baseTint)
    {
        const float inset = 2f;
        const float desiredPeriod = 14f;
        const float dashRatio = 0.55f;
        const float revolutionSeconds = 5f;
        const float coreThickness = 2.5f;
        const float haloThickness = 5f;

        var tl = cursor + new Vector2(inset, inset);
        var w = MathF.Max(1f, size.X - inset * 2f);
        var h = MathF.Max(1f, size.Y - inset * 2f);
        var perimeter = 2f * (w + h);

        var numDashes = System.Math.Max(4, (int)MathF.Round(perimeter / desiredPeriod));
        var period = perimeter / numDashes;
        var dashLen = period * dashRatio;

        var time = (float)ImGui.GetTime();
        var phase = (time * perimeter / revolutionSeconds) % period;

        var pulse = (MathF.Sin(time * 4f) + 1f) * 0.5f;
        var alpha = 0.7f + pulse * 0.3f;

        var coreColor = ImGui.GetColorU32(new Vector4(baseTint.X, baseTint.Y, baseTint.Z, alpha));
        var haloColor = ImGui.GetColorU32(new Vector4(baseTint.X, baseTint.Y, baseTint.Z, alpha * 0.35f));

        for (var i = 0; i < numDashes; i++)
        {
            var start = (i * period + phase) % perimeter;
            DrawPerimeterDash(drawList, tl, w, h, perimeter, start, dashLen, haloColor, haloThickness);
            DrawPerimeterDash(drawList, tl, w, h, perimeter, start, dashLen, coreColor, coreThickness);
        }
    }

    private static void DrawPerimeterDash(ImDrawListPtr drawList, Vector2 tl, float w, float h, float perimeter,
        float startP, float dashLen, uint color, float thickness)
    {
        var cur = ((startP % perimeter) + perimeter) % perimeter;
        var remaining = dashLen;

        while (remaining > 0.01f)
        {
            float distToCorner;
            if (cur < w) distToCorner = w - cur;
            else if (cur < w + h) distToCorner = w + h - cur;
            else if (cur < 2f * w + h) distToCorner = 2f * w + h - cur;
            else distToCorner = perimeter - cur;

            var segLen = MathF.Min(remaining, distToCorner);
            var a = PerimeterPoint(cur, tl, w, h);
            var b = PerimeterPoint(cur + segLen, tl, w, h);
            drawList.AddLine(a, b, color, thickness);

            remaining -= segLen;
            cur += segLen;
            if (cur >= perimeter) cur -= perimeter;
        }
    }

    private static Vector2 PerimeterPoint(float p, Vector2 tl, float w, float h)
    {
        if (p < w) return new Vector2(tl.X + p, tl.Y);
        p -= w;
        if (p < h) return new Vector2(tl.X + w, tl.Y + p);
        p -= h;
        if (p < w) return new Vector2(tl.X + w - p, tl.Y + h);
        p -= w;
        return new Vector2(tl.X, tl.Y + h - p);
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
