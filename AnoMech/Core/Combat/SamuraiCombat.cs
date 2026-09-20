using System;
using System.Collections.Generic;

namespace AnoMech.Core.Combat;

// Level-90 combos, Sen, Kenki, Meditation and Tsubame-gaeshi. Rules from docs/client-data/SAM-90.md.
public sealed class SamuraiCombat : MeleeCombatBase
{
    // Meikyo Shisui is 1233, the three-stack combo row; 1320 is the cleanse-immunity row of the same name.
    private const ushort Meditate = 1231, ThirdEye = 1232, MeikyoShisui = 1233, Fugetsu = 1298, Fuka = 1299,
        OgiNamikiriReady = 2959, TsubameFiveSwords = 3852, TsubameMidare = 4216, Tengentsu = 3853;

    private const int Setsu = 1, Getsu = 2, Ka = 4;
    private const int MaxKenki = 100, MaxMeditation = 3;

    // 0 = nothing armed, otherwise the action Tsubame-gaeshi repeats.
    private uint kaeshi;
    private int sen;
    private int kenki;
    private int meditation;

    public SamuraiCombat(double gcdSeconds = 2.5) : base(gcdSeconds) { }

    public int Kenki => kenki;
    public int Meditation => meditation;
    public int Sen => sen;
    public uint Kaeshi => kaeshi;

    protected override IReadOnlyList<uint> JobActions { get; } =
        [7477, 7478, 7479, 7480, 7481, 7482, 25780, 7484, 7485, 7486,
         7867, 7489, 7488, 7487, 16483, 16485, 16486, 25782, 25781,
         7490, 7491, 7492, 7493, 7495, 7496, 7497, 36962, 7499, 16481, 16482, 16487];
    protected override IReadOnlyList<ushort> JobStatusIds { get; } =
        [Meditate, ThirdEye, MeikyoShisui, Fugetsu, Fuka, OgiNamikiriReady, TsubameFiveSwords, TsubameMidare, Tengentsu];
    protected override string JobDebugState
        => $"kenki={kenki} sen={sen} meditation={meditation} kaeshi={kaeshi}";

    public override uint Adjust(uint actionId) => actionId switch
    {
        7483 => 25780,  // trait 519: 風雅 -> 風光
        7498 => 36962,  // trait 589: 心眼 -> 天眼通
        7867 => SenCount() switch { 1 => 7489u, 2 => 7488u, 3 => 7487u, _ => 7867 },
        16483 => kaeshi != 0 ? kaeshi : 16483,
        _ => actionId,
    };

    public override double CastTime(uint actionId) => actionId switch
    {
        // Trait 277 shortens every iaijutsu, and Ogi Namikiri shares that cast.
        7489 or 7488 or 7487 => 1.3,
        25781 => 1.8,
        _ => 0,
    };

    public override bool IsGapCloser(uint actionId) => false;
    protected override bool BlockedWhileBound(uint actionId) => actionId == 7493;

    protected override uint ComboFrom(uint actionId) => actionId switch
    {
        7478 or 7479 or 7480 => 7477,
        7481 => 7478,
        7482 => 7479,
        7484 or 7485 => 25780,
        _ => 0,
    };

    protected override bool IsJobSelfAction(uint actionId)
        => actionId is 25780 or 7484 or 7485 or 7488 or 16485 or 7491 or 7495 or 7497 or 36962 or 7499 or 16482 or 7867;

    protected override bool JobCanUse(uint actionId, bool inCombat) => actionId switch
    {
        7867 => SenCount() > 0,
        7489 => SenCount() >= 1,
        7488 => SenCount() >= 2,
        7487 => SenCount() >= 3,
        16483 or 16485 or 16486 or 25782 => kaeshi != 0,
        25781 => HasBuff(OgiNamikiriReady),
        7495 => SenCount() > 0,
        16487 => meditation >= MaxMeditation,
        16482 => inCombat,
        7490 or 7491 or 7496 or 16481 => kenki >= 25,
        7492 or 7493 => kenki >= 10,
        _ => true,
    };

    protected override bool JobHighlighted(uint actionId) => actionId switch
    {
        7867 or 7489 or 7488 or 7487 => SenCount() > 0,
        16483 or 16485 or 16486 or 25782 => kaeshi != 0,
        25781 => HasBuff(OgiNamikiriReady),
        16487 => meditation >= MaxMeditation,
        // Meikyo Shisui satisfies every weaponskill's combo requirement.
        7478 or 7479 or 7480 or 7481 or 7482 or 7484 or 7485 => HasBuff(MeikyoShisui),
        _ => false,
    };

    protected override JobHit? ApplyJob(uint actionId, bool hasTarget)
    {
        // Meditate drops the moment anything else is used.
        if (actionId != 7497) ClearBuff(Meditate);

        switch (actionId)
        {
            case 7477: SetCombo(7477); return Weaponskill(actionId, false, hasTarget);
            case 7478:
                Combo(ComboAction == 7477, 7478);
                return Weaponskill(actionId, false, hasTarget);
            case 7479:
                Combo(ComboAction == 7477, 7479);
                return Weaponskill(actionId, false, hasTarget);
            case 7480:
                if (Combo(ComboAction == 7477, 0)) { GainSen(Setsu); GainKenki(15); }
                return Weaponskill(actionId, false, hasTarget);
            case 7481:
                if (Combo(ComboAction == 7478, 0)) { GainSen(Getsu); GainKenki(10); }
                if (HasBuff(MeikyoShisui)) Buff(Fugetsu, 40);
                return Weaponskill(actionId, false, hasTarget);
            case 7482:
                if (Combo(ComboAction == 7479, 0)) { GainSen(Ka); GainKenki(10); }
                if (HasBuff(MeikyoShisui)) Buff(Fuka, 40);
                return Weaponskill(actionId, false, hasTarget);
            case 25780: SetCombo(25780); return Weaponskill(actionId, true, hasTarget);
            case 7484:
                if (Combo(ComboAction == 25780, 0)) { Buff(Fugetsu, 40); GainSen(Getsu); GainKenki(10); }
                return Weaponskill(actionId, true, hasTarget);
            case 7485:
                if (Combo(ComboAction == 25780, 0)) { Buff(Fuka, 40); GainSen(Ka); GainKenki(10); }
                return Weaponskill(actionId, true, hasTarget);
            case 7486: GainKenki(10); return Weaponskill(actionId, false, hasTarget);

            // Iaijutsu spends every Sen and banks Meditation. Trait 277 arms Tsubame-gaeshi for all but Higanbana.
            case 7489:
                sen = 0;
                GainMeditation();
                ClearCombo();
                return Iaijutsu(actionId, false, hasTarget);
            case 7488:
                sen = 0;
                GainMeditation();
                Buff(TsubameFiveSwords, 30);
                kaeshi = 16485;
                ClearCombo();
                return Iaijutsu(actionId, true, hasTarget);
            case 7487:
                sen = 0;
                GainMeditation();
                Buff(TsubameMidare, 30);
                kaeshi = 16486;
                ClearCombo();
                return Iaijutsu(actionId, false, hasTarget);
            case 16485 or 16486 or 25782:
                ClearKaeshi();
                ClearCombo();
                return Iaijutsu(actionId, actionId == 16485, hasTarget);
            case 25781:
                ClearBuff(OgiNamikiriReady);
                // The client gates 回返斬浪 by having used Ogi Namikiri, not by a documented status row.
                kaeshi = 25782;
                ClearCombo();
                return Iaijutsu(actionId, true, hasTarget);

            case 7490 or 7491 or 7496 or 16481:
                kenki -= 25;
                return hasTarget ? new JobHit(actionId, actionId is 7491 or 7496, false) : null;
            case 7492:
                kenki -= 10;
                return hasTarget ? new JobHit(actionId, false, false) : null;
            case 7493:
                kenki -= 10;
                Move(JobMoveKind.Backward, 10);
                return hasTarget ? new JobHit(actionId, false, false) : null;
            case 7495:
                GainKenki(10 * SenCount());
                sen = 0;
                return null;
            case 7497: Buff(Meditate, 15); return null;
            case 36962: Buff(ThirdEye, 15); Buff(Tengentsu, 4); return null;
            case 7499: Buff(MeikyoShisui, 20, 3); return null;
            case 16482:
                GainKenki(50);
                Buff(OgiNamikiriReady, 30);
                return null;
            case 16487:
                meditation = 0;
                return hasTarget ? new JobHit(actionId, true, false) : null;
        }
        return null;
    }

    // Trait 208: every weaponskill but an iaijutsu adds five Kenki. Meikyo Shisui covers one weaponskill's
    // combo requirement and is spent by it.
    private JobHit? Weaponskill(uint actionId, bool aoe, bool hasTarget)
    {
        GainKenki(5);
        ConsumeStack(MeikyoShisui);
        if (!hasTarget) return null;
        return new JobHit(actionId, aoe, false);
    }

    private JobHit? Iaijutsu(uint actionId, bool aoe, bool hasTarget)
        => !hasTarget ? null : new JobHit(actionId, aoe, false);

    // Meikyo Shisui satisfies a combo requirement outright, so a "failed" combo still counts under it.
    private bool Combo(bool success, uint next)
    {
        success = success || HasBuff(MeikyoShisui);
        if (success && next != 0) SetCombo(next);
        else ClearCombo();
        return success;
    }

    private void ClearKaeshi()
    {
        kaeshi = 0;
        ClearBuff(TsubameFiveSwords);
        ClearBuff(TsubameMidare);
    }

    private void GainSen(int flag) => sen |= flag;
    private int SenCount() => (sen & Setsu) / Setsu + ((sen & Getsu) >> 1) + ((sen & Ka) >> 2);
    private void GainKenki(int amount) => kenki = Math.Min(MaxKenki, kenki + amount);
    private void GainMeditation() => meditation = Math.Min(MaxMeditation, meditation + 1);

    protected override void AdvanceJob(double seconds)
    {
        // ponytail: Meditate's per-second Kenki and Meditation rate is not in the client text — one of each
        // per second until the user calibrates it in game.
        if (!HasBuff(Meditate)) return;
        meditateCarry += seconds;
        while (meditateCarry >= 1)
        {
            meditateCarry -= 1;
            GainKenki(10);
            GainMeditation();
        }
    }

    private double meditateCarry;

    protected override void ResetJob()
    {
        kaeshi = 0;
        sen = 0;
        kenki = 0;
        meditation = 0;
        meditateCarry = 0;
    }

    protected override (int Group, double Recast, int Charges) JobTimingContract(uint actionId) => actionId switch
    {
        7490 => (2, 1, 1),
        7491 => (1, 1, 1),
        7492 => (5, 5, 1),
        7493 => (6, 10, 1),
        7495 => (4, 5, 1),
        7496 => (22, 120, 1),
        7497 => (13, 60, 1),
        36962 => (7, 15, 1),
        7499 => (19, 55, 2),  // trait 443 (level 76): 2 charges
        16481 => (22, 120, 1),
        16482 => (20, 120, 1),
        16487 => (8, 15, 1),
        _ => Gcd,
    };

    protected override bool AlsoUsesGcd(uint actionId) => actionId == 7497;
    protected override double AlsoUsesGcdSeconds(uint actionId) => GcdSeconds;
}
