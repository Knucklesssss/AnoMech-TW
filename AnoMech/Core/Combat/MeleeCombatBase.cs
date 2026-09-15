using System.Collections.Generic;

namespace AnoMech.Core.Combat;

// Role actions shared by Monk, Dragoon, Ninja, Samurai, Reaper and Viper. No casts and no MP costs.
public abstract class MeleeCombatBase : JobCombatBase
{
    protected const ushort Bloodbath = 84, TrueNorth = 1250, ArmsLength = 1209;
    private static readonly uint[] RoleActions = [7541, 7542, 7546, 7548, 7549, 7863];
    private IReadOnlyList<uint>? actions;
    private IReadOnlyList<ushort>? statusIds;

    protected MeleeCombatBase(double gcdSeconds) : base(gcdSeconds) { }

    protected abstract IReadOnlyList<uint> JobActions { get; }
    protected abstract IReadOnlyList<ushort> JobStatusIds { get; }
    public sealed override IReadOnlyList<uint> Actions => actions ??= [.. JobActions, .. RoleActions];
    public sealed override IReadOnlyList<ushort> StatusIds => statusIds ??= [.. JobStatusIds, Bloodbath, TrueNorth, ArmsLength];
    protected (int Group, double Recast, int Charges) Gcd => (GlobalCooldownGroup, GcdSeconds, 1);
    // A sheet GCD recast (e.g. Viper's 3 s coils) shortened by the same skill speed as the 2.5 s GCD.
    protected double Scaled(double seconds) => seconds * GcdSeconds / 2.5;

    // Feint and Leg Sweep need an enemy; the other role actions are cast on the player.
    public sealed override bool IsSelfAction(uint actionId)
        => actionId is 7541 or 7542 or 7546 or 7548 || (actionId is not (7549 or 7863) && IsJobSelfAction(actionId));
    protected abstract bool IsJobSelfAction(uint actionId);

    protected sealed override (int Group, double Recast, int Charges) TimingContract(uint actionId) => actionId switch
    {
        7541 => (50, 120, 1),
        7542 => (47, 90, 1),
        7546 => (46, 45, 2),
        7548 => (49, 120, 1),
        7549 => (48, 90, 1),
        7863 => (44, 40, 1),
        _ => JobTimingContract(actionId),
    };
    protected abstract (int Group, double Recast, int Charges) JobTimingContract(uint actionId);

    protected sealed override JobHit? Apply(uint actionId, bool hasTarget)
    {
        switch (actionId)
        {
            case 7542: Buff(Bloodbath, 20); return null;
            case 7546: Buff(TrueNorth, 10); return null;
            case 7548: Buff(ArmsLength, 6); return null;
            case 7541 or 7549 or 7863: return null;
        }
        return ApplyJob(actionId, hasTarget);
    }
    protected abstract JobHit? ApplyJob(uint actionId, bool hasTarget);
}
