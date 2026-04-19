using System;
using System.Collections.Generic;

namespace BetterCDs.Profiles;

[Serializable]
public class JobProfileSet
{
    public List<Profile> Profiles { get; set; } = new();
    public Guid? ActiveProfileId { get; set; }
}
