using System;
using System.Collections.Generic;

namespace AnoMech.Core.Combat;

// Level-90 combos, Draconian Fire, Life of the Dragon and Firstminds' Focus. Rules from docs/client-data/DRG-90.md.
public sealed class DragoonCombat : MeleeCombatBase
{
    // PvE rows; the same-name CanStatusOff rows (2175, 1414, 4404) are PvP and 3258/3259/3265 are permanent display
    // rows. Power Surge is 2720: row 120 still carries the pre-Endwalker "next Jump" text.
    private const ushort LifeSurge = 116, PowerSurge = 2720, LanceCharge = 1864, BattleLitany = 786,
        DiveReady = 1243, DraconianFire = 1863, FirstmindsFocusStatus = 3178, EnhancedPiercingTalon = 1870,
        LifeOfTheDragon = 3177, NastrondReady = 3844;

    private int firstminds;

    public DragoonCombat(double gcdSeconds = 2.5) : base(gcdSeconds) { }

    public int FirstmindsFocus => firstminds;
    public double LifeOfTheDragonRemaining => BuffRemaining(LifeOfTheDragon);
    public bool DraconianFireReady => HasBuff(DraconianFire);

    protected override IReadOnlyList<uint> JobActions { get; } =
        [75, 78, 25771, 3554, 87, 25772, 3556, 86, 7397, 16477, 16479, 25770, 90,
         83, 85, 3557, 16478, 94, 96, 3555, 7400, 7399, 16480, 25773, 36951];
    protected override IReadOnlyList<ushort> JobStatusIds { get; } =
        [LifeSurge, PowerSurge, LanceCharge, BattleLitany, DiveReady, DraconianFire, FirstmindsFocusStatus,
         EnhancedPiercingTalon, LifeOfTheDragon, NastrondReady];
    protected override string JobDebugState
        => $"firstminds={firstminds} lotd={BuffRemaining(LifeOfTheDragon):0.0}s fire={(HasBuff(DraconianFire) ? "Y" : "-")}";

    public override uint Adjust(uint actionId) => actionId switch
    {
        84 => 25771,  // trait 437: 直刺 -> 蒼天刺
        88 => 25772,  // trait 437: 櫻花怒放 -> 櫻花繚亂
        92 => 16478,  // trait 275: 跳躍 -> 高跳
        75 => HasBuff(DraconianFire) ? 16479u : 75,  // trait 247
        86 => HasBuff(DraconianFire) ? 25770u : 86,  // trait 435
        _ => actionId,
    };

    public override bool IsGapCloser(uint actionId) => actionId is 96 or 16480 or 36951;
    protected override bool BlockedWhileBound(uint actionId) => actionId is 94 or 96 or 16480 or 36951;

    protected override uint ComboFrom(uint actionId) => actionId switch
    {
        78 or 87 => 75,
        25771 => 78,
        3554 => 25771,
        25772 => 87,
        3556 => 25772,
        7397 => 86,
        16477 => 7397,
        _ => 0,
    };

    protected override bool IsJobSelfAction(uint actionId) => actionId is 83 or 85 or 3557 or 94;

    protected override bool JobCanUse(uint actionId, bool inCombat) => actionId switch
    {
        16479 or 25770 => HasBuff(DraconianFire),
        7399 => HasBuff(DiveReady),
        7400 => HasBuff(NastrondReady),
        16480 => HasBuff(LifeOfTheDragon),
        25773 => firstminds >= 2,
        _ => true,
    };

    protected override bool JobHighlighted(uint actionId) => actionId switch
    {
        // Raiden Thrust and Draconian Fury continue the combos True Thrust and Doom Spike start.
        78 or 87 => ComboAction == 16479,
        7397 => ComboAction == 25770,
        16479 or 25770 => HasBuff(DraconianFire),
        7399 => HasBuff(DiveReady),
        7400 => HasBuff(NastrondReady),
        16480 => HasBuff(LifeOfTheDragon),
        25773 => firstminds >= 2,
        _ => false,
    };

    protected override JobHit? ApplyJob(uint actionId, bool hasTarget)
    {
        switch (actionId)
        {
            case 75 or 16479:
                if (actionId == 16479) GainFirstminds();
                SetCombo(actionId);
                return Weaponskill(actionId, false, hasTarget);
            case 78:
                Continue(ComboAction is 75 or 16479, 78);
                return Weaponskill(actionId, false, hasTarget);
            case 25771:
                Continue(ComboAction == 78, 25771);
                return Weaponskill(actionId, false, hasTarget);
            case 3554:
                Continue(ComboAction == 25771, 0);
                return Weaponskill(actionId, false, hasTarget);
            case 87:
                if (Continue(ComboAction is 75 or 16479, 87)) Buff(PowerSurge, 30);
                return Weaponskill(actionId, false, hasTarget);
            case 25772:
                Continue(ComboAction == 87, 25772);
                return Weaponskill(actionId, false, hasTarget);
            case 3556:
                Continue(ComboAction == 25772, 0);
                return Weaponskill(actionId, false, hasTarget);

            case 86 or 25770:
                if (actionId == 25770) GainFirstminds();
                SetCombo(actionId);
                return Weaponskill(actionId, true, hasTarget);
            case 7397:
                if (Continue(ComboAction is 86 or 25770, 7397)) Buff(PowerSurge, 30);
                return Weaponskill(actionId, true, hasTarget);
            case 16477:
                var torment = Continue(ComboAction == 7397, 0);
                var hit = Weaponskill(actionId, true, hasTarget);
                // Trait 435: only a successful combo grants Draconian Fire.
                if (torment) Buff(DraconianFire, 30);
                return hit;

            case 90:
                ClearBuff(EnhancedPiercingTalon);
                ClearCombo();
                return Weaponskill(actionId, false, hasTarget);

            case 83: Buff(LifeSurge, 5); return null;
            case 85: Buff(LanceCharge, 20); return null;
            case 3557: Buff(BattleLitany, 20); return null;
            case 16478: Buff(DiveReady, 15); return hasTarget ? new JobHit(actionId, false, false) : null;
            case 94:
                Buff(EnhancedPiercingTalon, 15);
                Move(JobMoveKind.Backward, 15);
                return null;
            case 96: return hasTarget ? new JobHit(actionId, true, true) : null;
            case 3555:
                // Trait 163 turns the granted Blood of the Dragon straight into Life of the Dragon.
                Buff(LifeOfTheDragon, 20);
                Buff(NastrondReady, 20);
                return hasTarget ? new JobHit(actionId, true, false) : null;
            case 7400:
                ClearBuff(NastrondReady);
                return hasTarget ? new JobHit(actionId, true, false) : null;
            case 7399:
                ClearBuff(DiveReady);
                return hasTarget ? new JobHit(actionId, false, false) : null;
            case 16480: return hasTarget ? new JobHit(actionId, true, true) : null;
            case 25773:
                firstminds = 0;
                return hasTarget ? new JobHit(actionId, true, false) : null;
            case 36951: return hasTarget ? new JobHit(actionId, false, true) : null;
        }
        return null;
    }

    // Life Surge is spent by any weaponskill. Draconian Fire is only lost to a melee one: trait 247 says
    // 近身攻擊戰技, which excludes the line AOEs and Piercing Talon.
    // ponytail: that reading comes from the trait text alone — confirm in game which skills drop 龍眼.
    private JobHit? Weaponskill(uint actionId, bool aoe, bool hasTarget)
    {
        ClearBuff(LifeSurge);
        if (actionId is 75 or 78 or 25771 or 3554 or 87 or 25772 or 3556 or 16479 or 25770) ClearBuff(DraconianFire);
        if (!hasTarget) return null;
        return new JobHit(actionId, aoe, false);
    }

    private bool Continue(bool success, uint next)
    {
        if (success && next != 0) SetCombo(next);
        else ClearCombo();
        return success;
    }

    private void GainFirstminds() => firstminds = Math.Min(2, firstminds + 1);

    protected override void AdvanceJob(double seconds) { }

    protected override void ResetJob() => firstminds = 0;

    protected override (int Group, double Recast, int Charges) JobTimingContract(uint actionId) => actionId switch
    {
        83 => (15, 40, 2),     // trait 438 (level 88): 2 charges
        85 => (10, 60, 1),
        3557 => (22, 120, 1),
        16478 => (8, 30, 1),
        94 => (6, 30, 1),
        96 => (21, 120, 1),
        3555 => (11, 60, 1),
        7400 => (4, 2, 1),
        7399 => (1, 1, 1),
        16480 => (7, 30, 1),
        25773 => (5, 10, 1),
        36951 => (20, 60, 2),  // trait 580 (level 84): 2 charges
        _ => Gcd,
    };
}
