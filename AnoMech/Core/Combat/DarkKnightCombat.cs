using System;
using System.Collections.Generic;

namespace AnoMech.Core.Combat;

// Level-90 rotation, Blood gauge, MP, derived actions and castable defensive actions
// (status icons and cooldowns only). Dark Arts from a broken The Blackest Night is
// deliberately not simulated (user scope, 2026-09-14). Rules from docs/client-data/DRK-90.md.
public sealed class DarkKnightCombat : TankCombatBase
{
    // ponytail: MP gain amounts are not in the TC client text ("恢復自身MP"
    // only); commonly cited values, pending the user's in-game check.
    private const int ComboMp = 600;
    private const int BloodWeaponMp = 600;
    private const int DeliriumMp = 200;
    private const int CarveMp = 600;
    private const ushort BloodWeapon = 742, Delirium = 1972, SaltedEarth = 749;
    private int blood;
    private double darksideRemaining;
    private double shadowRemaining;

    public DarkKnightCombat(double gcdSeconds = 2.5) : base(gcdSeconds, 3629, 32067, 743, 2) { }

    public int Blood => blood;
    public double DarksideRemaining => darksideRemaining;
    public double ShadowRemaining => shadowRemaining;
    public int DeliriumStacks => BuffParam(Delirium);
    public int BloodWeaponStacks => BuffParam(BloodWeapon);
    public double SaltedEarthRemaining => BuffRemaining(SaltedEarth);
    public bool Grit => Stance;
    protected override IReadOnlyList<uint> JobActions { get; } =
        [3617, 3623, 3632, 3621, 16468, 3624, 7392, 7391, 16470, 16469, 25757, 3641, 3643, 3639, 25755, 7390, 16472, 36926,
         3636, 3634, 3638, 16471, 25754, 7393];
    // PvE rows; the same-name CanStatusOff rows (2171, 1308, 1996, 3036) are PvP.
    protected override IReadOnlyList<ushort> JobStatusIds { get; } = [742, 1972, 749, 747, 746, 810, 1894, 2682, 1178];

    protected override string JobDebugState
        => $"blood={blood} darkside={darksideRemaining:0.0} shadow={shadowRemaining:0.0} shadowstride={Timing.Remaining(8):0.0} shadowbringer={Timing.Remaining(23):0.0}";

    protected override uint AdjustJob(uint actionId) => actionId switch
    {
        3625 => 7390,
        16466 => 16469,
        16467 => 16470,
        3639 when HasBuff(SaltedEarth) => 25755,
        _ => actionId,
    };

    // The Blackest Night and Oblation are cast on the player; party targets are not simulated.
    protected override bool IsJobSelfAction(uint actionId)
        => actionId is 3621 or 16468 or 7391 or 3639 or 25755 or 7390 or 16472 or 3636 or 3634 or 3638 or 16471 or 25754 or 7393;

    public override bool IsGapCloser(uint actionId) => actionId == 36926;

    protected override uint ComboFrom(uint actionId) => actionId switch
    {
        3623 => 3617,
        3632 => 3623,
        16468 => 3621,
        _ => 0,
    };

    protected override int MpCost(uint actionId) => actionId is 16470 or 16469 or 7393 ? 3000 : 0;

    protected override bool JobCanUse(uint actionId, bool inCombat)
    {
        if (actionId == 25757 && darksideRemaining <= 0) return false;
        if (actionId == 25755 && !HasBuff(SaltedEarth)) return false;
        if (actionId is 7392 or 7391 && blood < 50 && !HasBuff(Delirium)) return false;
        return true;
    }

    protected override JobHit? ApplyJob(uint actionId, bool hasTarget)
    {
        switch (actionId)
        {
            case 7390:
                Buff(Delirium, 15, 3);
                Buff(BloodWeapon, 15, 3);
                return null;
            case 16472: shadowRemaining = 20; return null;
            case 3639: Buff(SaltedEarth, 15); return null;
            case 3636: Buff(747, 15); return null;
            case 3634: Buff(746, 10); return null;
            case 3638: Buff(810, 10); return null;
            case 16471: Buff(1894, 15); return null;
            case 25754: Buff(2682, 10); return null;
            case 7393: Buff(1178, 7); return null;
            case 16470 or 16469:
                darksideRemaining = Math.Min(60, darksideRemaining + 30);
                break;
        }

        var isAoe = actionId is 3621 or 16468 or 7391 or 16469 or 25757 or 3641 or 25755;
        var freeSpender = false;
        if (actionId is 7392 or 7391)
        {
            freeSpender = ConsumeStack(Delirium);
            if (!freeSpender) blood -= 50;
        }
        if (isAoe && !hasTarget)
        {
            if (actionId is 3621 or 16468) ClearCombo();
            return null;
        }

        if (freeSpender) GainMp(DeliriumMp);
        var weaponskillOrSpell = actionId is 3617 or 3623 or 3632 or 3621 or 16468 or 3624 or 7392 or 7391;
        if (weaponskillOrSpell && ConsumeStack(BloodWeapon))
        {
            GrantBlood(10);
            GainMp(BloodWeaponMp);
        }

        switch (actionId)
        {
            case 3617:
                SetCombo(3617);
                break;
            case 3623 when ComboAction == 3617:
                GainMp(ComboMp);
                SetCombo(3623);
                break;
            case 3632 when ComboAction == 3623:
                GrantBlood(20);
                ClearCombo();
                break;
            case 3621:
                SetCombo(3621);
                break;
            case 16468 when ComboAction == 3621:
                GrantBlood(20);
                GainMp(ComboMp);
                ClearCombo();
                break;
            case 3623 or 3632 or 16468:
                ClearCombo();
                break;
            case 3641 or 3643:
                GainMp(CarveMp);
                break;
            case 3624:
                Timing.Reduce(8, 5);
                break;
        }
        return new JobHit(actionId, isAoe, actionId == 36926);
    }

    protected override void AdvanceJob(double seconds)
    {
        darksideRemaining = Decrease(darksideRemaining, seconds);
        shadowRemaining = Decrease(shadowRemaining, seconds);
    }

    protected override void ResetJob()
    {
        blood = 0;
        darksideRemaining = 0;
        shadowRemaining = 0;
    }

    private void GrantBlood(int amount) => blood = Math.Min(100, blood + amount);

    protected override (int Group, double Recast, int Charges) JobTimingContract(uint actionId)
        => actionId switch
        {
            7390 => (11, 60, 1),
            16470 or 16469 => (1, 1, 1),
            25757 => (23, 60, 2),
            3641 or 3643 => (10, 60, 1),
            3639 => (16, 90, 1),
            25755 => (6, 20, 1),
            16472 => (22, 120, 1),
            36926 => (8, 30, 2),
            3636 => (21, 120, 1),
            3634 => (9, 60, 1),
            3638 => (25, 300, 1),
            16471 => (15, 90, 1),
            25754 => (19, 60, 2),
            7393 => (3, 15, 1),
            _ => Gcd,
        };
}
