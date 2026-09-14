using System.Collections.Generic;

namespace AnoMech.Core.Combat;

// Shared by the four healers: role actions, Swiftcast and Lucid Dreaming. Heals, shields and
// mitigation show their status icons and cooldowns only; no HP numbers are simulated.
public abstract class HealerCombatBase : JobCombatBase
{
    private const ushort Swiftcast = 167, Surecast = 160, LucidDreaming = 1204;
    // ponytail: Lucid Dreaming's MP per tick is not in the client text ("效果量：55");
    // 550 per 3 s is the commonly cited value, pending the user's in-game check.
    private const int LucidMpPerTick = 550;
    private static readonly uint[] RoleActions = [7561, 7559, 7562, 7568, 7571, 16560];
    private IReadOnlyList<uint>? actions;
    private IReadOnlyList<ushort>? statusIds;
    private double lucidTick;

    protected HealerCombatBase(double gcdSeconds) : base(gcdSeconds) { }

    protected abstract IReadOnlyList<uint> JobActions { get; }
    protected abstract IReadOnlyList<ushort> JobStatusIds { get; }
    public sealed override IReadOnlyList<uint> Actions => actions ??= [.. JobActions, .. RoleActions];
    public sealed override IReadOnlyList<ushort> StatusIds => statusIds ??= [.. JobStatusIds, Swiftcast, Surecast, LucidDreaming];
    protected (int Group, double Recast, int Charges) Gcd => (GlobalCooldownGroup, GcdSeconds, 1);

    // Esuna and Rescue are cast on the player; party targets are not simulated.
    // Repose is the only role action with an enemy target; jobs never see it.
    public sealed override bool IsSelfAction(uint actionId)
        => actionId is 7561 or 7559 or 7562 or 7568 or 7571 || (actionId != 16560 && IsJobSelfAction(actionId));
    protected abstract bool IsJobSelfAction(uint actionId);
    public override bool IsGapCloser(uint actionId) => false;
    protected sealed override uint ComboFrom(uint actionId) => 0;

    public sealed override double CastTime(uint actionId)
    {
        var seconds = actionId == 16560 ? 2.5 : JobCastTime(actionId);
        return seconds > 0 && HasBuff(Swiftcast) ? 0 : seconds;
    }
    protected abstract double JobCastTime(uint actionId);

    protected sealed override int MpCost(uint actionId) => actionId switch
    {
        7568 => 400,
        16560 => 600,
        _ => JobMpCost(actionId),
    };
    protected abstract int JobMpCost(uint actionId);

    protected sealed override (int Group, double Recast, int Charges) TimingContract(uint actionId) => actionId switch
    {
        7561 => (44, 60, 1),
        7559 => (49, 120, 1),
        7562 => (45, 60, 1),
        7571 => (50, 120, 1),
        7568 or 16560 => Gcd,
        _ => JobTimingContract(actionId),
    };
    protected abstract (int Group, double Recast, int Charges) JobTimingContract(uint actionId);

    protected sealed override JobHit? Apply(uint actionId, bool hasTarget)
    {
        // Swiftcast is spent by the spell it made instant; a spell that was cast never had it.
        if (actionId == 16560 || JobCastTime(actionId) > 0) ConsumeBuff(Swiftcast);
        switch (actionId)
        {
            case 7561: Buff(Swiftcast, 10); return null;
            case 7559: Buff(Surecast, 6); return null;
            case 7562:
                Buff(LucidDreaming, 21);
                lucidTick = 0;
                return null;
            case 7568 or 7571: return null;
            case 16560: return new JobHit(actionId, false, false);
        }
        return ApplyJob(actionId, hasTarget);
    }
    protected abstract JobHit? ApplyJob(uint actionId, bool hasTarget);

    protected sealed override void AdvanceJob(double seconds)
    {
        if (HasBuff(LucidDreaming))
            for (lucidTick += seconds; lucidTick >= 3; lucidTick -= 3) GainMp(LucidMpPerTick);
        else
            lucidTick = 0;
        AdvanceHealer(seconds);
    }
    protected abstract void AdvanceHealer(double seconds);

    protected sealed override void ResetJob()
    {
        lucidTick = 0;
        ResetHealer();
    }
    protected abstract void ResetHealer();
}
