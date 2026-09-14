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
    int Mp { get; }
    uint CastingAction { get; }
    // Hotbar glow: the next combo step, or a proc/ready action.
    bool IsHighlighted(uint actionId);
    string DebugState { get; }
    // Contract of the listed action itself, without substitution: native recast
    // records are bound per listed action and must not borrow a replacement's timer.
    (int Group, double Recast, int Charges) GetBindingCooldown(uint actionId);
    // Stances survive entering a duty; read them from the client once at start.
    void Seed(Func<ushort, bool> hasStatus);
    // Client-adjusted recast (skill speed shortens weaponskills with their own timer).
    void OverrideRecast(uint actionId, double seconds);
    uint Adjust(uint actionId);
    bool Supports(uint actionId);
    // No target needed: self-centred AoEs and self buffs.
    bool IsSelfAction(uint actionId);
    bool IsGapCloser(uint actionId);
    (int Group, double Recast, int Charges) GetCooldown(uint actionId);
    bool CanUse(uint actionId, bool hasTarget, bool inRange, bool inCombat, bool alive = true, bool bound = false, bool checkTiming = true);
    // Instant actions only; cast-time actions go through BeginCast.
    JobHit? TryUse(uint actionId, bool hasTarget, bool inRange, bool inCombat, bool alive = true, bool bound = false);
    // Cast bar seconds for the resolved action right now; 0 = instant.
    double CastTime(uint actionId);
    // Recast and lock are paid at the start of a cast; effects and MP at the end.
    bool BeginCast(uint actionId, bool hasTarget, bool inRange, bool inCombat, bool alive = true, bool bound = false);
    bool CompleteCast(bool hasTarget, bool inRange, bool alive, out JobHit? hit);
    void CancelCast();
    // True when an active invulnerability swallows a death; may convert the status (Living Dead).
    bool SurviveLethal();
    void AutoAttackHit();
    // Remaining <= 0 means the status must be absent; +Infinity is permanent.
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
        new(19, 90, 9, gcd => new PaladinCombat(gcd)),
        new(21, 90, 31, gcd => new WarriorCombat(gcd)),
        new(32, 90, 3617, gcd => new DarkKnightCombat(gcd)),
        new(37, 90, 16137, gcd => new GunbreakerCombat(gcd)),
        new(24, 90, 25859, gcd => new WhiteMageCombat(gcd)),
        new(28, 90, 25865, gcd => new ScholarCombat(gcd)),
        new(33, 90, 25871, gcd => new AstrologianCombat(gcd)),
        new(40, 90, 24312, gcd => new SageCombat(gcd)),
        new(23, 90, 16495, gcd => new BardCombat(gcd)),
        new(31, 90, 7411, gcd => new MachinistCombat(gcd)),
        new(38, 90, 15989, gcd => new DancerCombat(gcd)),
        new(25, 90, 156, gcd => new BlackMageCombat(gcd)),
        new(27, 90, 3579, gcd => new SummonerCombat(gcd)),
        new(35, 90, 37004, gcd => new RedMageCombat(gcd)),
        new(42, 90, 34650, gcd => new PictomancerCombat(gcd)),
    ];

    public static JobCombatEntry? Find(byte classJob, byte level)
        => Entries.FirstOrDefault(e => e.ClassJob == classJob && e.Level == level);
}
