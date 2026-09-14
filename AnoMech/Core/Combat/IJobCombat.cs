using System;
using System.Collections.Generic;
using System.Linq;

namespace AnoMech.Core.Combat;

public interface IJobCombat
{
    IReadOnlyList<uint> Actions { get; }
    IReadOnlyList<ushort> StatusIds { get; }
    CombatTiming Timing { get; }
    uint ComboAction { get; }
    double ComboRemaining { get; }
    uint Adjust(uint actionId);
    bool Supports(uint actionId);
    // No target needed: self-centred AoEs and self buffs.
    bool IsSelfAction(uint actionId);
    bool IsGapCloser(uint actionId);
    (int Group, double Recast, int Charges) GetCooldown(uint actionId);
    bool CanUse(uint actionId, bool hasTarget, bool inRange, bool inCombat, bool alive = true, bool bound = false, bool checkTiming = true);
    JobHit? TryUse(uint actionId, bool hasTarget, bool inRange, bool inCombat, bool alive = true, bool bound = false);
    // Remaining <= 0 means the status must be absent.
    IEnumerable<JobStatus> Statuses();
    void Advance(double seconds);
    void Reset();
}

public readonly record struct JobHit(uint ActionId, bool IsAoe, bool GapCloser);

public readonly record struct JobStatus(ushort Id, double Remaining, ushort Param);

// GcdProbeAction lives here, not on the rules: the session reads the GCD
// duration from the client before it can construct the rules.
public sealed record JobCombatEntry(byte ClassJob, byte Level, uint GcdProbeAction, Func<double, IJobCombat> CreateRules);

public static class JobCombatRegistry
{
    public static IReadOnlyList<JobCombatEntry> Entries { get; } =
    [
        new(21, 90, 31, gcd => new WarriorCombat(gcd)),
        new(32, 90, 3617, gcd => new DarkKnightCombat(gcd)),
    ];

    public static JobCombatEntry? Find(byte classJob, byte level)
        => Entries.FirstOrDefault(e => e.ClassJob == classJob && e.Level == level);
}
