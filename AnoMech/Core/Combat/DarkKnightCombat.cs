using System;
using System.Collections.Generic;

namespace AnoMech.Core.Combat;

// Level-90 rotation, Blood gauge, MP and derived actions. Dark Arts and The
// Blackest Night are deliberately not simulated (user scope, 2026-09-14).
public sealed class DarkKnightCombat : JobCombatBase
{
    // ponytail: MP gain amounts are not in the TC client text ("恢復自身MP"
    // only); commonly cited values, pending the user's in-game check.
    private const int ComboMp = 600;
    private const int BloodWeaponMp = 600;
    private const int DeliriumMp = 200;
    private const int CarveMp = 600;
    private int blood;
    private double darksideRemaining;
    private double shadowRemaining;
    private int deliriumStacks;
    private double deliriumRemaining;
    private int bloodWeaponStacks;
    private double bloodWeaponRemaining;
    private double saltedEarthRemaining;

    public DarkKnightCombat(double gcdSeconds = 2.5) : base(gcdSeconds) { }

    public int Blood => blood;
    public double DarksideRemaining => darksideRemaining;
    public double ShadowRemaining => shadowRemaining;
    public int DeliriumStacks => deliriumStacks;
    public int BloodWeaponStacks => bloodWeaponStacks;
    public double SaltedEarthRemaining => saltedEarthRemaining;
    public override IReadOnlyList<uint> Actions { get; } =
        [3617, 3623, 3632, 3621, 16468, 3624, 7392, 7391, 16470, 16469, 25757, 3641, 3643, 3639, 25755, 7390, 16472, 36926];
    public override IReadOnlyList<ushort> StatusIds { get; } = [742, 1972, 749];

    protected override string JobDebugState
        => $"blood={blood} darkside={darksideRemaining:0.0} shadow={shadowRemaining:0.0} delirium={deliriumStacks}/{deliriumRemaining:0.0} bloodWeapon={bloodWeaponStacks}/{bloodWeaponRemaining:0.0} salt={saltedEarthRemaining:0.0} shadowstride={Timing.Remaining(8):0.0} shadowbringer={Timing.Remaining(23):0.0}";

    public override uint Adjust(uint actionId) => actionId switch
    {
        3625 => 7390,
        16466 => 16469,
        16467 => 16470,
        _ => actionId,
    };

    public override IEnumerable<JobStatus> Statuses()
    {
        yield return new(742, bloodWeaponStacks > 0 ? bloodWeaponRemaining : 0, (ushort)bloodWeaponStacks);
        yield return new(1972, deliriumStacks > 0 ? deliriumRemaining : 0, (ushort)deliriumStacks);
        yield return new(749, saltedEarthRemaining, 0);
    }

    public override bool IsSelfAction(uint actionId) => actionId is 3621 or 16468 or 7391 or 3639 or 25755 or 7390 or 16472;

    public override bool IsGapCloser(uint actionId) => actionId == 36926;

    protected override uint ComboFrom(uint actionId) => actionId switch
    {
        3623 => 3617,
        3632 => 3623,
        16468 => 3621,
        _ => 0,
    };

    protected override int MpCost(uint actionId) => actionId is 16470 or 16469 ? 3000 : 0;

    protected override bool JobCanUse(uint actionId, bool inCombat)
    {
        if (actionId == 25757 && darksideRemaining <= 0) return false;
        if (actionId == 25755 && saltedEarthRemaining <= 0) return false;
        if (actionId is 7392 or 7391 && blood < 50 && deliriumStacks == 0) return false;
        return true;
    }

    protected override JobHit? Apply(uint actionId, bool hasTarget)
    {
        switch (actionId)
        {
            case 7390:
                deliriumStacks = 3;
                deliriumRemaining = 15;
                bloodWeaponStacks = 3;
                bloodWeaponRemaining = 15;
                return null;
            case 16472:
                shadowRemaining = 20;
                return null;
            case 3639:
                saltedEarthRemaining = 15;
                return null;
            case 16470 or 16469:
                darksideRemaining = Math.Min(60, darksideRemaining + 30);
                break;
        }

        var isAoe = actionId is 3621 or 16468 or 7391 or 16469 or 25757 or 3641 or 25755;
        var freeSpender = false;
        if (actionId is 7392 or 7391)
        {
            freeSpender = deliriumStacks > 0;
            if (freeSpender) deliriumStacks--;
            else blood -= 50;
        }
        if (isAoe && !hasTarget)
        {
            if (actionId is 3621 or 16468) ClearCombo();
            return null;
        }

        if (freeSpender) GainMp(DeliriumMp);
        var weaponskillOrSpell = actionId is 3617 or 3623 or 3632 or 3621 or 16468 or 3624 or 7392 or 7391;
        if (weaponskillOrSpell && bloodWeaponStacks > 0)
        {
            bloodWeaponStacks--;
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
        deliriumRemaining = Decrease(deliriumRemaining, seconds);
        bloodWeaponRemaining = Decrease(bloodWeaponRemaining, seconds);
        saltedEarthRemaining = Decrease(saltedEarthRemaining, seconds);
        if (deliriumRemaining == 0) deliriumStacks = 0;
        if (bloodWeaponRemaining == 0) bloodWeaponStacks = 0;
    }

    protected override void ResetJob()
    {
        blood = 0;
        darksideRemaining = 0;
        shadowRemaining = 0;
        deliriumStacks = 0;
        deliriumRemaining = 0;
        bloodWeaponStacks = 0;
        bloodWeaponRemaining = 0;
        saltedEarthRemaining = 0;
    }

    private void GrantBlood(int amount) => blood = Math.Min(100, blood + amount);

    // Groups and durations from docs/client-data/DRK-90.md (TC client 2026.07.22).
    protected override (int Group, double Recast, int Charges) TimingContract(uint actionId)
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
            _ => (GlobalCooldownGroup, GcdSeconds, 1),
        };
}
