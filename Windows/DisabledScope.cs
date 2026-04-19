using System;
using Dalamud.Bindings.ImGui;

namespace BetterCDs.Windows;

public readonly struct DisabledScope : IDisposable
{
    private readonly bool active;
    public DisabledScope(bool disabled)
    {
        active = disabled;
        if (active) ImGui.BeginDisabled();
    }
    public void Dispose()
    {
        if (active) ImGui.EndDisabled();
    }
}
