using System;
using System.Collections.Generic;

namespace AnoMech.Core.Combat;

// Shared by the four tanks: role actions and the enmity stance toggle.
// Status icons and cooldowns only; no enmity, mitigation or healing numbers.
public abstract class TankCombatBase : JobCombatBase
{
    private const ushort Rampart = 1191;
    private const ushort ArmsLength = 1209;
    private static readonly uint[] RoleActions = [7531, 7533, 7535, 7537, 7538, 7540, 7548];
    private readonly uint stanceAction;
    private readonly uint releaseAction;
    private readonly ushort stanceStatus;
    private readonly int stanceGroup;
    private IReadOnlyList<uint>? actions;
    private IReadOnlyList<ushort>? statusIds;

    protected TankCombatBase(double gcdSeconds, uint stanceAction, uint releaseAction, ushort stanceStatus, int stanceGroup) : base(gcdSeconds)
    {
        this.stanceAction = stanceAction;
        this.releaseAction = releaseAction;
        this.stanceStatus = stanceStatus;
        this.stanceGroup = stanceGroup;
    }

    public bool Stance => HasBuff(stanceStatus);
    protected abstract IReadOnlyList<uint> JobActions { get; }
    protected abstract IReadOnlyList<ushort> JobStatusIds { get; }
    public sealed override IReadOnlyList<uint> Actions => actions ??= [.. JobActions, stanceAction, releaseAction, .. RoleActions];
    public sealed override IReadOnlyList<ushort> StatusIds => statusIds ??= [.. JobStatusIds, stanceStatus, Rampart, ArmsLength];
    protected (int Group, double Recast, int Charges) Gcd => (GlobalCooldownGroup, GcdSeconds, 1);

    public sealed override uint Adjust(uint actionId) => actionId == stanceAction && Stance ? releaseAction : AdjustJob(actionId);
    protected virtual uint AdjustJob(uint actionId) => actionId;

    public sealed override void Seed(Func<ushort, bool> hasStatus)
    {
        if (hasStatus(stanceStatus)) Buff(stanceStatus, double.PositiveInfinity);
    }

    // Shirk is cast on the player; party targets are not simulated.
    public sealed override bool IsSelfAction(uint actionId)
        => actionId == stanceAction || actionId == releaseAction || actionId is 7531 or 7535 or 7537 or 7548 || IsJobSelfAction(actionId);
    protected abstract bool IsJobSelfAction(uint actionId);

    protected sealed override (int Group, double Recast, int Charges) TimingContract(uint actionId)
    {
        if (actionId == stanceAction) return (stanceGroup, 2, 1);
        // Release shares the stance's native group but has its own 1 s recast.
        if (actionId == releaseAction) return (1000 + stanceGroup, 1, 1);
        return actionId switch
        {
            7531 => (47, 90, 1),
            7533 => (43, 30, 1),
            7535 => (45, 60, 1),
            7537 => (50, 120, 1),
            7538 => (44, 30, 1),
            7540 => (42, 25, 1),
            7548 => (49, 120, 1),
            _ => JobTimingContract(actionId),
        };
    }
    protected abstract (int Group, double Recast, int Charges) JobTimingContract(uint actionId);

    protected sealed override JobHit? Apply(uint actionId, bool hasTarget)
    {
        if (actionId == stanceAction) { Buff(stanceStatus, double.PositiveInfinity); return null; }
        if (actionId == releaseAction) { ClearBuff(stanceStatus); return null; }
        switch (actionId)
        {
            case 7531: Buff(Rampart, 20); return null;
            case 7548: Buff(ArmsLength, 6); return null;
            case 7535 or 7537: return null;
            case 7533 or 7538 or 7540: return new JobHit(actionId, false, false);
        }
        return ApplyJob(actionId, hasTarget);
    }
    protected abstract JobHit? ApplyJob(uint actionId, bool hasTarget);
}
