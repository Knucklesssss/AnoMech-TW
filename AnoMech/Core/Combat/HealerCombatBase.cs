using System.Collections.Generic;

namespace AnoMech.Core.Combat;

// Shared by the four healers: role actions on top of Swiftcast and Lucid Dreaming. Heals, shields and
// mitigation show their status icons and cooldowns only; no HP numbers are simulated.
public abstract class HealerCombatBase : MagicCombatBase
{
    protected HealerCombatBase(double gcdSeconds) : base(gcdSeconds) { }

    protected sealed override IReadOnlyList<uint> RoleActions { get; } = [7561, 7559, 7562, 7568, 7571, 16560];

    // Esuna and Rescue are cast on the player; party targets are not simulated.
    // Repose is the only role action with an enemy target; jobs never see it.
    public sealed override bool IsSelfAction(uint actionId)
        => actionId is 7561 or 7559 or 7562 or 7568 or 7571 || (actionId != 16560 && IsJobSelfAction(actionId));
    protected abstract bool IsJobSelfAction(uint actionId);
    public override bool IsGapCloser(uint actionId) => false;
    protected sealed override uint ComboFrom(uint actionId) => 0;

    protected sealed override double SpellCastTime(uint actionId) => actionId == 16560 ? 2.5 : JobCastTime(actionId);
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

    protected sealed override JobHit? ApplySpell(uint actionId, bool hasTarget, bool hardcast) => actionId switch
    {
        7568 or 7571 => null,
        16560 => new JobHit(actionId, false, false),
        _ => ApplyJob(actionId, hasTarget),
    };
    protected abstract JobHit? ApplyJob(uint actionId, bool hasTarget);

    protected sealed override void AdvanceMagic(double seconds) => AdvanceHealer(seconds);
    protected abstract void AdvanceHealer(double seconds);

    protected sealed override void ResetMagic() => ResetHealer();
    protected abstract void ResetHealer();
}
