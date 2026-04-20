using System;
using System.Numerics;

namespace BetterUI.Profiles;

public enum AnchorSide
{
    None,
    Up,
    Down,
    Left,
    Right,
}

[Serializable]
public class BarSettings
{
    public Vector2 Position { get; set; } = new(400, 400);
    public bool Vertical { get; set; } = false;
    public string? AnchorToGroup { get; set; }
    public Vector2 AnchorOffset { get; set; } = Vector2.Zero;
    public AnchorSide AnchorDirection { get; set; } = AnchorSide.Up;
}
