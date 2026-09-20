using System;
using System.Collections.Generic;

namespace AnoMech.Core.Combat;

// Level-90 Soul, Shroud, Enshrouded and the Hell's Ingress gate. Rules from docs/client-data/RPR-90.md.
public sealed class ReaperCombat : MeleeCombatBase
{
    // PvE rows; the same-name 28xx rows are PvP. Enhanced Harpe is 2845, whose text carries the Ingress
    // cooldown clause that 2859 lacks.
    private const ushort SoulReaver = 2587, EnhancedGibbet = 2588, EnhancedGallows = 2589,
        EnhancedVoidReaping = 2590, EnhancedCrossReaping = 2591, ImmortalSacrifice = 2592, Enshrouded = 2593,
        Soulsow = 2594, Threshold = 2595, EnhancedHarpe = 2845, IdealHost = 3905;

    private const int MaxSoul = 100, MaxShroud = 100, MaxLemure = 5, MaxVoid = 5;

    private int soul;
    private int shroud;
    private int lemure;
    private int voidShroud;
    // Trait 382 replaces whichever of Ingress/Egress was not used, so remember which one that is.
    private uint gateReturn;

    public ReaperCombat(double gcdSeconds = 2.5) : base(gcdSeconds) { }

    public int Soul => soul;
    public int Shroud => shroud;
    public int LemureShroud => lemure;
    public int VoidShroud => voidShroud;
    public double EnshroudedRemaining => BuffRemaining(Enshrouded);

    protected override IReadOnlyList<uint> JobActions { get; } =
        [24373, 24374, 24375, 24376, 24377, 24378, 24379, 24380, 24381,
         24382, 24383, 24384, 24385, 24386, 24387, 24388, 24395, 24396, 24397, 24398,
         24389, 24390, 24391, 24392, 24393, 24394, 24399, 24400,
         24401, 24402, 24403, 24404, 24405];
    protected override IReadOnlyList<ushort> JobStatusIds { get; } =
        [SoulReaver, EnhancedGibbet, EnhancedGallows, EnhancedVoidReaping, EnhancedCrossReaping,
         ImmortalSacrifice, Enshrouded, Soulsow, Threshold, EnhancedHarpe, IdealHost];
    protected override string JobDebugState
        => $"soul={soul} shroud={shroud} lemure={lemure} void={voidShroud} gate={gateReturn}";

    public override uint Adjust(uint actionId) => actionId switch
    {
        24382 => HasBuff(Enshrouded) ? 24395u : 24382,
        24383 => HasBuff(Enshrouded) ? 24396u : 24383,
        24384 => HasBuff(Enshrouded) ? 24397u : 24384,
        24389 => voidShroud >= 2 ? 24399u : HasBuff(EnhancedGibbet) ? 24390u : HasBuff(EnhancedGallows) ? 24391u : 24389,
        24392 => voidShroud >= 2 ? 24400u : 24392,
        24387 => HasBuff(Soulsow) ? 24388u : 24387,
        24401 or 24402 when actionId == gateReturn && HasBuff(Threshold) => 24403,
        _ => actionId,
    };

    public override double CastTime(uint actionId) => actionId switch
    {
        24386 => HasBuff(EnhancedHarpe) ? 0 : 1.3,
        // Soulsow is instant out of combat; the session passes combat state to CanUse, not here, so the
        // full cast is the safe default and Harvest Moon itself is instant.
        24387 => 5,
        24398 => 1.3,
        _ => 0,
    };

    public override bool IsGapCloser(uint actionId) => false;
    protected override bool BlockedWhileBound(uint actionId) => actionId is 24401 or 24402 or 24403;
    // The two Soul weaponskills carry their own cooldown and still spend the GCD.
    protected override bool AlsoUsesGcd(uint actionId) => actionId is 24380 or 24381;
    protected override double AlsoUsesGcdSeconds(uint actionId) => GcdSeconds;

    protected override uint ComboFrom(uint actionId) => actionId switch
    {
        24374 => 24373,
        24375 => 24374,
        24377 => 24376,
        _ => 0,
    };

    protected override bool IsJobSelfAction(uint actionId)
        => actionId is 24376 or 24377 or 24379 or 24381 or 24387 or 24394 or 24401 or 24402 or 24403 or 24404 or 24405;

    protected override bool JobCanUse(uint actionId, bool inCombat) => actionId switch
    {
        // Enshrouded locks out the avatar abilities and the plain combo weaponskills.
        24389 or 24390 or 24391 or 24392 or 24393 when HasBuff(Enshrouded) => false,
        24389 or 24390 or 24391 or 24392 or 24393 => soul >= 50,
        24382 or 24383 or 24384 => HasBuff(SoulReaver),
        24394 => HasBuff(IdealHost) || shroud >= 50,
        24395 or 24396 or 24397 => lemure >= 1,
        24398 => lemure >= 1,
        24399 or 24400 => voidShroud >= 2,
        24385 => HasBuff(ImmortalSacrifice),
        24388 => HasBuff(Soulsow),
        24403 => HasBuff(Threshold),
        _ => true,
    };

    protected override bool JobHighlighted(uint actionId) => actionId switch
    {
        24382 or 24383 or 24384 => HasBuff(SoulReaver),
        24389 or 24390 or 24391 or 24392 => !HasBuff(Enshrouded) && soul >= 50,
        24393 => !HasBuff(Enshrouded) && soul >= 50,
        24394 => HasBuff(IdealHost) || shroud >= 50,
        24395 or 24396 or 24397 or 24398 => lemure >= 1,
        24399 or 24400 => voidShroud >= 2,
        24385 => HasBuff(ImmortalSacrifice),
        24388 => HasBuff(Soulsow),
        24403 => HasBuff(Threshold),
        _ => false,
    };

    protected override JobHit? ApplyJob(uint actionId, bool hasTarget)
    {
        // Trait 381: Soul Reaver only survives until the next weaponskill or spell, and the three that
        // spend it are handled in their own cases.
        if (IsGcd(actionId) && actionId is not (24382 or 24383 or 24384)) ClearBuff(SoulReaver);

        switch (actionId)
        {
            case 24373: GainSoul(10); SetCombo(24373); return Strike(actionId, false, hasTarget);
            case 24374:
                if (Continue(ComboAction == 24373, 24374)) GainSoul(10);
                return Strike(actionId, false, hasTarget);
            case 24375:
                if (Continue(ComboAction == 24374, 0)) GainSoul(10);
                return Strike(actionId, false, hasTarget);
            case 24376: GainSoul(10); SetCombo(24376); return Strike(actionId, true, hasTarget);
            case 24377:
                if (Continue(ComboAction == 24376, 0)) GainSoul(10);
                return Strike(actionId, true, hasTarget);
            case 24378: ClearCombo(); return Strike(actionId, false, hasTarget);
            case 24379: ClearCombo(); return Strike(actionId, true, hasTarget);
            case 24380: GainSoul(50); ClearCombo(); return Strike(actionId, false, hasTarget);
            case 24381: GainSoul(50); ClearCombo(); return Strike(actionId, true, hasTarget);

            // Trait 384: each Soul Reaver spender banks ten Shroud.
            case 24382:
                ConsumeStack(SoulReaver);
                Buff(EnhancedGallows, 60);
                ClearBuff(EnhancedGibbet);
                GainShroud(10);
                ClearCombo();
                return Strike(actionId, false, hasTarget);
            case 24383:
                ConsumeStack(SoulReaver);
                Buff(EnhancedGibbet, 60);
                ClearBuff(EnhancedGallows);
                GainShroud(10);
                ClearCombo();
                return Strike(actionId, false, hasTarget);
            case 24384:
                ConsumeStack(SoulReaver);
                GainShroud(10);
                ClearCombo();
                return Strike(actionId, true, hasTarget);

            case 24385:
                ClearBuff(ImmortalSacrifice);
                Buff(IdealHost, 30);
                ClearCombo();
                return Strike(actionId, true, hasTarget);
            case 24386:
                ClearBuff(EnhancedHarpe);
                GainSoul(10);
                ClearCombo();
                return Strike(actionId, false, hasTarget);
            case 24387: Buff(Soulsow, 0); ClearCombo(); return null;
            case 24388:
                ClearBuff(Soulsow);
                GainSoul(10);
                ClearCombo();
                return Strike(actionId, true, hasTarget);

            case 24395:
                SpendLemure();
                Buff(EnhancedCrossReaping, 30);
                ClearBuff(EnhancedVoidReaping);
                ClearCombo();
                return Strike(actionId, false, hasTarget);
            case 24396:
                SpendLemure();
                Buff(EnhancedVoidReaping, 30);
                ClearBuff(EnhancedCrossReaping);
                ClearCombo();
                return Strike(actionId, false, hasTarget);
            case 24397:
                SpendLemure();
                ClearCombo();
                return Strike(actionId, true, hasTarget);
            case 24398:
                lemure = 0;
                voidShroud = 0;
                ClearBuff(Enshrouded);
                ClearCombo();
                return Strike(actionId, true, hasTarget);

            case 24389 or 24390 or 24391 or 24392:
                soul -= 50;
                Buff(SoulReaver, 30, 1);
                return hasTarget ? new JobHit(actionId, actionId is 24392, false) : null;
            case 24393:
                soul -= 50;
                Buff(SoulReaver, 30, 2);
                return hasTarget ? new JobHit(actionId, true, false) : null;
            case 24394:
                if (!ConsumeBuff(IdealHost)) shroud -= 50;
                Buff(Enshrouded, 30);
                lemure = MaxLemure;
                voidShroud = 0;
                return null;
            case 24399 or 24400:
                voidShroud -= 2;
                return hasTarget ? new JobHit(actionId, actionId == 24400, false) : null;

            case 24401 or 24402:
                Move(actionId == 24401 ? JobMoveKind.Forward : JobMoveKind.Backward, 15, marksReturn: true);
                Buff(EnhancedHarpe, 10);
                Buff(Threshold, 10);
                gateReturn = actionId == 24401 ? 24402u : 24401u;
                return null;
            case 24403:
                ClearBuff(Threshold);
                gateReturn = 0;
                Move(JobMoveKind.ReturnPoint);
                return null;
            case 24404: return null;
            case 24405:
                // Solo, the Sacrifice ring only ever feeds the caster.
                Buff(ImmortalSacrifice, 30, 1);
                return null;
        }
        return null;
    }

    private static bool IsGcd(uint actionId)
        => actionId is 24373 or 24374 or 24375 or 24376 or 24377 or 24378 or 24379 or 24380 or 24381
            or 24382 or 24383 or 24384 or 24385 or 24386 or 24387 or 24388 or 24395 or 24396 or 24397 or 24398;

    private void SpendLemure()
    {
        lemure = Math.Max(0, lemure - 1);
        voidShroud = Math.Min(MaxVoid, voidShroud + 1);
    }

    private static JobHit? Strike(uint actionId, bool aoe, bool hasTarget)
        => hasTarget ? new JobHit(actionId, aoe, false) : null;

    private bool Continue(bool success, uint next)
    {
        if (success && next != 0) SetCombo(next);
        else ClearCombo();
        return success;
    }

    private void GainSoul(int amount) => soul = Math.Min(MaxSoul, soul + amount);
    private void GainShroud(int amount) => shroud = Math.Min(MaxShroud, shroud + amount);

    protected override void AdvanceJob(double seconds)
    {
        // Enshrouded ending strands whatever is left of the shroud stacks.
        if (!HasBuff(Enshrouded) && (lemure > 0 || voidShroud > 0)) { lemure = 0; voidShroud = 0; }
    }

    protected override void ResetJob()
    {
        soul = 0;
        shroud = 0;
        lemure = 0;
        voidShroud = 0;
        gateReturn = 0;
    }

    protected override (int Group, double Recast, int Charges) JobTimingContract(uint actionId) => actionId switch
    {
        24380 or 24381 => (9, 30, 2),  // trait 383 (level 78): 2 charges, shared between them
        24389 or 24390 or 24391 or 24392 or 24399 or 24400 => (1, 1, 1),
        24393 => (13, 60, 1),
        24394 => (4, 5, 1),
        24401 or 24402 => (5, 20, 1),
        24403 => (2, 1, 1),
        24404 => (7, 30, 1),
        24405 => (22, 120, 1),
        _ => Gcd,
    };
}
