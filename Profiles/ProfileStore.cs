using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace BetterCDs.Profiles;

public sealed class ProfileStore
{
    private readonly Configuration config;
    private readonly IDataManager data;

    public ProfileStore(Configuration config, IDataManager data)
    {
        this.config = config;
        this.data = data;
    }

    public JobProfileSet GetOrCreate(uint jobId)
    {
        if (!config.JobProfiles.TryGetValue(jobId, out var set))
        {
            set = new JobProfileSet();
            config.JobProfiles[jobId] = set;
        }

        if (set.Profiles.Count == 0)
        {
            set.Profiles.Add(new Profile
            {
                Name = "Default",
                JobId = jobId,
            });
        }

        if (set.ActiveProfileId is null || set.Profiles.All(p => p.Id != set.ActiveProfileId))
            set.ActiveProfileId = set.Profiles[0].Id;

        return set;
    }

    public Profile? GetActive(uint jobId)
    {
        var set = GetOrCreate(jobId);
        return set.Profiles.FirstOrDefault(p => p.Id == set.ActiveProfileId);
    }

    public Profile CreateEmpty(uint jobId, string? name = null)
    {
        var set = GetOrCreate(jobId);
        var profile = new Profile
        {
            Name = string.IsNullOrWhiteSpace(name) ? NextAutoName(jobId, set) : name!,
            JobId = jobId,
        };
        set.Profiles.Add(profile);
        set.ActiveProfileId = profile.Id;
        config.Save();
        return profile;
    }

    public Profile Duplicate(uint jobId, Profile source, string? name = null)
    {
        var set = GetOrCreate(jobId);
        var clone = source.Clone();
        clone.Name = string.IsNullOrWhiteSpace(name) ? NextAutoName(jobId, set) : name!;
        set.Profiles.Add(clone);
        set.ActiveProfileId = clone.Id;
        config.Save();
        return clone;
    }

    public bool Delete(uint jobId, Guid profileId)
    {
        var set = GetOrCreate(jobId);
        var target = set.Profiles.FirstOrDefault(p => p.Id == profileId);
        if (target is null) return false;

        set.Profiles.Remove(target);
        if (set.Profiles.Count == 0)
            set.ActiveProfileId = null;
        else if (set.ActiveProfileId == profileId)
            set.ActiveProfileId = set.Profiles[0].Id;
        config.Save();
        return true;
    }

    public void SetActive(uint jobId, Guid profileId)
    {
        var set = GetOrCreate(jobId);
        if (set.Profiles.Any(p => p.Id == profileId))
        {
            set.ActiveProfileId = profileId;
            config.Save();
        }
    }

    public Profile AddImported(Profile imported)
    {
        var set = GetOrCreate(imported.JobId);
        imported.Id = Guid.NewGuid();

        if (string.IsNullOrWhiteSpace(imported.Name) || set.Profiles.Any(p => p.Name == imported.Name))
            imported.Name = ResolveNameCollision(set, imported.Name, imported.JobId);

        set.Profiles.Add(imported);
        config.Save();
        return imported;
    }

    public string GetJobAbbreviation(uint jobId)
    {
        var sheet = data.GetExcelSheet<ClassJob>();
        if (sheet is null) return $"JOB{jobId}";
        if (!sheet.TryGetRow(jobId, out var row)) return $"JOB{jobId}";
        var abbrev = row.Abbreviation.ExtractText();
        return string.IsNullOrEmpty(abbrev) ? $"JOB{jobId}" : abbrev.ToUpperInvariant();
    }

    private string NextAutoName(uint jobId, JobProfileSet set)
    {
        var abbrev = GetJobAbbreviation(jobId);
        var pattern = new Regex($"^{Regex.Escape(abbrev)}(\\d+)$");
        var maxN = 0;
        foreach (var p in set.Profiles)
        {
            var m = pattern.Match(p.Name);
            if (m.Success && int.TryParse(m.Groups[1].Value, out var n) && n > maxN)
                maxN = n;
        }
        return $"{abbrev}{maxN + 1}";
    }

    private string ResolveNameCollision(JobProfileSet set, string desired, uint jobId)
    {
        if (string.IsNullOrWhiteSpace(desired))
            return NextAutoName(jobId, set);

        var taken = new HashSet<string>(set.Profiles.Select(p => p.Name), StringComparer.OrdinalIgnoreCase);
        if (!taken.Contains(desired)) return desired;

        for (var i = 2; i < 1000; i++)
        {
            var candidate = $"{desired} ({i})";
            if (!taken.Contains(candidate)) return candidate;
        }
        return desired + " " + Guid.NewGuid().ToString("N")[..6];
    }
}
