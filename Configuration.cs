using System;
using System.Collections.Generic;
using System.Numerics;
using BetterCDs.Profiles;
using Dalamud.Configuration;

namespace BetterCDs;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 2;

    public bool ShowOverlay { get; set; } = true;
    public bool OverlayLocked { get; set; } = false;
    public Vector2 OverlayPosition { get; set; } = new(200, 200);
    public float OverlayIconSize { get; set; } = 48f;
    public float OverlayIconSpacing { get; set; } = 4f;

    public Dictionary<uint, JobProfileSet> JobProfiles { get; set; } = new();

    public Dictionary<string, BarSettings> Bars { get; set; } = new();

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
