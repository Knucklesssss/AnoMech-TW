using System;
using System.Collections.Generic;

namespace AnoMech.Core.Combat;

// Level-90 rotation, Oath Gauge, MP, casts, derived actions and castable defensive
// actions (status icons and cooldowns only). Rules from docs/client-data/PLD-90.md.
public sealed class PaladinCombat : TankCombatBase
{
    // ponytail: MP gain amounts are not in the TC client text ("恢復自身MP"
    // only); commonly cited values, pending the user's in-game check.
    private const int RiotBladeMp = 1000;
    private const int ProminenceMp = 1000;
    private const int ExpiacionMp = 500;
    private const int AtonementMp = 400;
    private const double SpellCast = 1.5;
    private const ushort FightOrFlight = 76, GoringBladeReady = 3847, Requiescat = 1368, ConfiteorReady = 3019, AtonementReady = 1902,
        SupplicationReady = 3827, SepulchreReady = 3828, DivineMight = 2673, Sentinel = 74, Bulwark = 77, HallowedGround = 82,
        Cover = 80, HolySheltron = 2674, KnightsResolve = 2675, KnightsBenediction = 2676, Intervention = 1174,
        DivineVeil = 1362, PassageOfArms = 1175;
    private int oath;

    public PaladinCombat(double gcdSeconds = 2.5) : base(gcdSeconds, 28, 32065, 79, 1) { }

    public int Oath => oath;
    public int RequiescatStacks => BuffParam(Requiescat);
    protected override IReadOnlyList<uint> JobActions { get; } =
        [9, 15, 3539, 7381, 16457, 16, 24, 3538, 7383, 7384, 16458, 3541, 16459, 25748, 25749, 25750, 16460, 36918, 36919,
         23, 25747, 16461, 20, 17, 22, 30, 27, 25746, 7382, 3540, 7385];
    // PvE rows; the same-name CanStatusOff rows (1369, 3028, 2015, 4281, 4282, 1302, 2020, 3188, 3026) are PvP.
    protected override IReadOnlyList<ushort> JobStatusIds { get; } =
        [76, 3847, 1368, 3019, 1902, 3827, 3828, 2673, 74, 77, 82, 80, 2674, 2675, 2676, 1174, 1362, 1175];

    protected override string JobDebugState => $"oath={oath} intervene={Timing.Remaining(10):0.0}";

    // Substitutions from the client's ReplaceAction sheet (技能變換設定).
    protected override uint AdjustJob(uint actionId) => actionId switch
    {
        21 => 3539,
        3542 => 25746,
        29 => 25747,
        20 when HasBuff(GoringBladeReady) => 3538,
        16459 when ComboAction == 16459 => 25748,
        16459 when ComboAction == 25748 => 25749,
        16459 when ComboAction == 25749 => 25750,
        16460 when HasBuff(SepulchreReady) => 36919,
        16460 when HasBuff(SupplicationReady) => 36918,
        _ => actionId,
    };

    // Clemency, Cover and Intervention are cast on the player; party targets are not simulated.
    protected override bool IsJobSelfAction(uint actionId)
        => actionId is 7381 or 16457 or 16458 or 3541 or 23 or 20 or 17 or 22 or 30 or 27 or 25746 or 7382 or 3540 or 7385;

    public override bool IsGapCloser(uint actionId) => actionId == 16461;

    protected override uint ComboFrom(uint actionId) => actionId switch
    {
        15 => 9,
        3539 => 15,
        16457 => 7381,
        25748 => 16459,
        25749 => 25748,
        25750 => 25749,
        _ => 0,
    };

    protected override bool JobHighlighted(uint actionId) => actionId switch
    {
        3538 => HasBuff(GoringBladeReady),
        16459 => HasBuff(ConfiteorReady),
        16460 => HasBuff(AtonementReady),
        36918 => HasBuff(SupplicationReady),
        36919 => HasBuff(SepulchreReady),
        7384 or 16458 => HasBuff(DivineMight) || HasBuff(Requiescat),
        _ => false,
    };

    // Trait 207 halves every spell's sheet MP cost.
    protected override int MpCost(uint actionId) => actionId switch
    {
        7384 or 16458 or 16459 or 25748 or 25749 or 25750 => 1000,
        3541 => 2000,
        _ => 0,
    };

    public override double CastTime(uint actionId) => actionId switch
    {
        7384 or 16458 => HasBuff(DivineMight) || HasBuff(Requiescat) ? 0 : SpellCast,
        3541 => HasBuff(Requiescat) ? 0 : SpellCast,
        _ => 0,
    };

    public override void AutoAttackHit() => oath = Math.Min(100, oath + 5);

    protected override bool JobCanUse(uint actionId, bool inCombat) => actionId switch
    {
        3538 => HasBuff(GoringBladeReady),
        16459 => HasBuff(ConfiteorReady),
        25748 => ComboAction == 16459,
        25749 => ComboAction == 25748,
        25750 => ComboAction == 25749,
        16460 => HasBuff(AtonementReady),
        36918 => HasBuff(SupplicationReady),
        36919 => HasBuff(SepulchreReady),
        27 or 25746 or 7382 => oath >= 50,
        _ => true,
    };

    protected override JobHit? ApplyJob(uint actionId, bool hasTarget)
    {
        switch (actionId)
        {
            case 20:
                Buff(FightOrFlight, 20);
                Buff(GoringBladeReady, 30);
                return null;
            case 17: Buff(Sentinel, 15); return null;
            case 22: Buff(Bulwark, 10); return null;
            case 30: Buff(HallowedGround, 10); return null;
            case 3540: Buff(DivineVeil, 30); return null;
            case 7385: Buff(PassageOfArms, 18); return null;
            case 27:
                oath -= 50;
                Buff(Cover, 12);
                return null;
            case 25746:
                oath -= 50;
                Buff(HolySheltron, 8);
                Buff(KnightsResolve, 4);
                Buff(KnightsBenediction, 12);
                return null;
            case 7382:
                oath -= 50;
                Buff(Intervention, 8);
                Buff(KnightsResolve, 4);
                Buff(KnightsBenediction, 12);
                return null;
            case 3541:
                ConsumeStack(Requiescat);
                return null;
            case 7383:
                Buff(Requiescat, 30, 4);
                Buff(ConfiteorReady, 30);
                return new JobHit(actionId, false, false);
        }

        var isAoe = actionId is 7381 or 16457 or 16458 or 23 or 16459 or 25748 or 25749 or 25750 or 25747;
        if (actionId is 7384 or 16458 && !ConsumeBuff(DivineMight)) ConsumeStack(Requiescat);
        if (actionId is 16459 or 25748 or 25749 or 25750) ConsumeStack(Requiescat);
        if (actionId == 16459) ConsumeBuff(ConfiteorReady);
        if (actionId == 3538) ConsumeBuff(GoringBladeReady);
        if (isAoe && !hasTarget)
        {
            if (actionId is 7381 or 16457) ClearCombo();
            return null;
        }

        switch (actionId)
        {
            case 9:
                SetCombo(9);
                break;
            case 15 when ComboAction == 9:
                GainMp(RiotBladeMp);
                SetCombo(15);
                break;
            case 3539 when ComboAction == 15:
                Buff(AtonementReady, 30);
                Buff(DivineMight, 30);
                ClearCombo();
                break;
            case 7381:
                SetCombo(7381);
                break;
            case 16457 when ComboAction == 7381:
                GainMp(ProminenceMp);
                Buff(DivineMight, 30);
                ClearCombo();
                break;
            case 15 or 3539 or 16457:
                ClearCombo();
                break;
            case 16459:
                SetCombo(16459);
                break;
            case 25748:
                SetCombo(25748);
                break;
            case 25749:
                SetCombo(25749);
                break;
            case 25750:
                ClearCombo();
                break;
            case 16460:
                ConsumeBuff(AtonementReady);
                Buff(SupplicationReady, 30);
                GainMp(AtonementMp);
                break;
            case 36918:
                ConsumeBuff(SupplicationReady);
                Buff(SepulchreReady, 30);
                GainMp(AtonementMp);
                break;
            case 36919:
                ConsumeBuff(SepulchreReady);
                GainMp(AtonementMp);
                break;
            case 25747:
                GainMp(ExpiacionMp);
                break;
        }
        return new JobHit(actionId, isAoe, actionId == 16461);
    }

    protected override void AdvanceJob(double seconds) { }

    protected override void ResetJob() => oath = 0;

    protected override (int Group, double Recast, int Charges) JobTimingContract(uint actionId)
        => actionId switch
        {
            20 => (11, 60, 1),
            7383 => (12, 60, 1),
            23 => (7, 30, 1),
            25747 => (8, 30, 1),
            16461 => (10, 30, 2),
            17 => (20, 120, 1),
            22 => (16, 90, 1),
            30 => (25, 420, 1),
            27 => (21, 120, 1),
            25746 => (3, 5, 1),
            7382 => (5, 10, 1),
            3540 => (15, 90, 1),
            7385 => (22, 120, 1),
            _ => Gcd,
        };
}
