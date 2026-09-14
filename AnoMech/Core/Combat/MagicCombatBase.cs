using System.Collections.Generic;

namespace AnoMech.Core.Combat;

// Swiftcast, Surecast and Lucid Dreaming, shared by healers and casters.
public abstract class MagicCombatBase : JobCombatBase
{
    protected const ushort Swiftcast = 167, Surecast = 160, LucidDreaming = 1204;
    // ponytail: Lucid Dreaming's MP per tick is not in the client text ("效果量：55");
    // 550 per 3 s is the commonly cited value, pending the user's in-game check.
    private const int LucidMpPerTick = 550;
    private IReadOnlyList<uint>? actions;
    private IReadOnlyList<ushort>? statusIds;
    private double lucidTick;

    protected MagicCombatBase(double gcdSeconds) : base(gcdSeconds) { }

    protected abstract IReadOnlyList<uint> JobActions { get; }
    protected abstract IReadOnlyList<ushort> JobStatusIds { get; }
    protected abstract IReadOnlyList<uint> RoleActions { get; }
    public sealed override IReadOnlyList<uint> Actions => actions ??= [.. JobActions, .. RoleActions];
    public sealed override IReadOnlyList<ushort> StatusIds => statusIds ??= [.. JobStatusIds, Swiftcast, Surecast, LucidDreaming];
    protected (int Group, double Recast, int Charges) Gcd => (GlobalCooldownGroup, GcdSeconds, 1);
    protected double Scaled(double seconds) => seconds * GcdSeconds / 2.5;

    // Cast time before Swiftcast and job instant effects.
    protected abstract double SpellCastTime(uint actionId);
    // A job effect (Triplecast, Dualcast, Acceleration) that makes this cast instant.
    protected virtual bool JobInstant(uint actionId) => false;
    protected virtual void SpendJobInstant(uint actionId) { }

    public sealed override double CastTime(uint actionId)
    {
        var seconds = SpellCastTime(actionId);
        return seconds > 0 && (HasBuff(Swiftcast) || JobInstant(actionId)) ? 0 : seconds;
    }

    protected sealed override JobHit? Apply(uint actionId, bool hasTarget)
    {
        var seconds = SpellCastTime(actionId);
        var hardcast = seconds > 0 && !HasBuff(Swiftcast) && !JobInstant(actionId);
        // Swiftcast is spent first by the spell it made instant; a job instant only without it.
        if (seconds > 0 && !ConsumeBuff(Swiftcast) && !hardcast) SpendJobInstant(actionId);
        switch (actionId)
        {
            case 7561: Buff(Swiftcast, 10); return null;
            case 7559: Buff(Surecast, 6); return null;
            case 7562:
                Buff(LucidDreaming, 21);
                lucidTick = 0;
                return null;
        }
        return ApplySpell(actionId, hasTarget, hardcast);
    }
    protected abstract JobHit? ApplySpell(uint actionId, bool hasTarget, bool hardcast);

    protected sealed override void AdvanceJob(double seconds)
    {
        if (HasBuff(LucidDreaming))
            for (lucidTick += seconds; lucidTick >= 3; lucidTick -= 3) GainMp(LucidMpPerTick);
        else
            lucidTick = 0;
        AdvanceMagic(seconds);
    }
    protected abstract void AdvanceMagic(double seconds);

    protected sealed override void ResetJob()
    {
        lucidTick = 0;
        ResetMagic();
    }
    protected abstract void ResetMagic();
}
