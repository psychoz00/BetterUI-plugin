using System;
using System.Collections.Generic;

namespace BetterUI.Profiles;

[Serializable]
public class Profile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public uint JobId { get; set; }
    public int FormatVersion { get; set; } = 1;
    public List<string> Groups { get; set; } = new() { "Essential", "Utility", "Defensive" };
    public List<TrackedAction> Tracked { get; set; } = new();

    public Profile Clone()
    {
        return new Profile
        {
            Id = Guid.NewGuid(),
            Name = Name,
            JobId = JobId,
            FormatVersion = FormatVersion,
            Groups = new List<string>(Groups),
            Tracked = Tracked.ConvertAll(t => new TrackedAction
            {
                ActionId = t.ActionId,
                GroupName = t.GroupName,
                Order = t.Order,
                ShowTimer = t.ShowTimer,
                GlowBuffId = t.GlowBuffId,
            }),
        };
    }
}
