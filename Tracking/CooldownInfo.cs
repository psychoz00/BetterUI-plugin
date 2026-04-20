namespace BetterUI.Tracking;

public readonly record struct CooldownInfo(
    uint ActionId,
    uint AdjustedActionId,
    float TotalRecast,
    float Elapsed,
    bool IsReady,
    bool IsUsable,
    bool ComboAdvanced,
    ushort MaxCharges,
    ushort CurrentCharges,
    ushort IconId,
    string ActionName
)
{
    public float Remaining => IsReady ? 0f : System.MathF.Max(0f, TotalRecast - Elapsed);
    public float FillFraction => TotalRecast <= 0 ? 1f : System.Math.Clamp(Elapsed / TotalRecast, 0f, 1f);
}
