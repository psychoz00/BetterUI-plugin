using System;
using System.Numerics;

namespace BetterCDs.Profiles;

[Serializable]
public class BarSettings
{
    public Vector2 Position { get; set; } = new(400, 400);
    public bool Vertical { get; set; } = false;
    public string? AnchorToGroup { get; set; }
    public Vector2 AnchorOffset { get; set; } = Vector2.Zero;
}
