using System.Collections.Generic;

namespace AnoMech.Core.Combat;

// Role actions shared by Bard, Machinist and Dancer. No casts and no MP costs.
public abstract class RangedCombatBase : JobCombatBase
{
    protected const ushort ArmsLength = 1209, Peloton = 1199;
    private static readonly uint[] RoleActions = [7541, 7548, 7551, 7553, 7554, 7557];
    private IReadOnlyList<uint>? actions;
    private IReadOnlyList<ushort>? statusIds;

    protected RangedCombatBase(double gcdSeconds) : base(gcdSeconds) { }

    protected abstract IReadOnlyList<uint> JobActions { get; }
    protected abstract IReadOnlyList<ushort> JobStatusIds { get; }
    public sealed override IReadOnlyList<uint> Actions => actions ??= [.. JobActions, .. RoleActions];
    public sealed override IReadOnlyList<ushort> StatusIds => statusIds ??= [.. JobStatusIds, ArmsLength, Peloton];
    protected (int Group, double Recast, int Charges) Gcd => (GlobalCooldownGroup, GcdSeconds, 1);
    protected double Scaled(double seconds) => seconds * GcdSeconds / 2.5;

    // Second Wind, Arm's Length and Peloton are cast on the player; the grazes need an enemy.
    public sealed override bool IsSelfAction(uint actionId)
        => actionId is 7541 or 7548 or 7557 || (actionId is not (7551 or 7553 or 7554) && IsJobSelfAction(actionId));
    protected abstract bool IsJobSelfAction(uint actionId);

    // ponytail: Peloton is refused in combat but not removed when combat starts.
    protected sealed override bool JobCanUse(uint actionId, bool inCombat)
        => actionId == 7557 ? !inCombat : RangedCanUse(actionId, inCombat);
    protected virtual bool RangedCanUse(uint actionId, bool inCombat) => true;

    protected sealed override (int Group, double Recast, int Charges) TimingContract(uint actionId) => actionId switch
    {
        7541 => (50, 120, 1),
        7548 => (49, 120, 1),
        7551 => (44, 30, 1),
        7553 => (42, 30, 1),
        7554 => (43, 30, 1),
        7557 => (41, 5, 1),
        _ => JobTimingContract(actionId),
    };
    protected abstract (int Group, double Recast, int Charges) JobTimingContract(uint actionId);

    protected sealed override JobHit? Apply(uint actionId, bool hasTarget)
    {
        switch (actionId)
        {
            case 7548: Buff(ArmsLength, 6); return null;
            case 7557: Buff(Peloton, 30); return null;
            case 7541 or 7551 or 7553 or 7554: return null;
        }
        return ApplyJob(actionId, hasTarget);
    }
    protected abstract JobHit? ApplyJob(uint actionId, bool hasTarget);
}
