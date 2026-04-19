using System;

namespace BetterCDs.Profiles;

[Serializable]
public class TrackedAction
{
    public uint ActionId { get; set; }
    public string GroupName { get; set; } = "Essential";
    public int Order { get; set; }
    public bool ShowTimer { get; set; } = true;
    public uint? GlowBuffId { get; set; }
    public bool DimWhenNotUsable { get; set; } = false;
    public bool FollowComboChain { get; set; } = false;
}
