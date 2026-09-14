using System.Collections.Generic;

namespace AnoMech.Core.Combat;

// Role actions shared by Black Mage, Summoner, Red Mage and Pictomancer.
public abstract class CasterCombatBase : MagicCombatBase
{
    protected CasterCombatBase(double gcdSeconds) : base(gcdSeconds) { }

    protected sealed override IReadOnlyList<uint> RoleActions { get; } = [7561, 7559, 7562, 7560, 25880];

    // Addle and Sleep need an enemy; the other role actions are cast on the player.
    public sealed override bool IsSelfAction(uint actionId)
        => actionId is 7561 or 7559 or 7562 || (actionId is not (7560 or 25880) && IsJobSelfAction(actionId));
    protected abstract bool IsJobSelfAction(uint actionId);

    protected sealed override double SpellCastTime(uint actionId) => actionId == 25880 ? 2.5 : JobCastTime(actionId);
    protected abstract double JobCastTime(uint actionId);

    protected sealed override int MpCost(uint actionId) => actionId == 25880 ? 800 : JobMpCost(actionId);
    protected abstract int JobMpCost(uint actionId);

    protected sealed override (int Group, double Recast, int Charges) TimingContract(uint actionId) => actionId switch
    {
        7561 => (44, 60, 1),
        7559 => (49, 120, 1),
        7562 => (45, 60, 1),
        7560 => (47, 90, 1),
        25880 => Gcd,
        _ => JobTimingContract(actionId),
    };
    protected abstract (int Group, double Recast, int Charges) JobTimingContract(uint actionId);

    protected sealed override JobHit? ApplySpell(uint actionId, bool hasTarget, bool hardcast)
        => actionId is 7560 or 25880 ? null : ApplyJob(actionId, hasTarget, hardcast);
    protected abstract JobHit? ApplyJob(uint actionId, bool hasTarget, bool hardcast);

    protected sealed override void AdvanceMagic(double seconds) => AdvanceCaster(seconds);
    protected abstract void AdvanceCaster(double seconds);

    protected sealed override void ResetMagic() => ResetCaster();
    protected abstract void ResetCaster();
}
