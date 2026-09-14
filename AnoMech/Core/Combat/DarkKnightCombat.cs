using System;
using System.Collections.Generic;

namespace AnoMech.Core.Combat;

// Level-90 rotation, Blood gauge, MP, derived actions, and castable defensive and
// role actions (status icon and cooldown only, no mitigation numbers). Dark Arts
// from a broken The Blackest Night is deliberately not simulated (user scope, 2026-09-14).
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
    private double rampartRemaining;
    private double shadowWallRemaining;
    private double darkMindRemaining;
    private double livingDeadRemaining;
    private double missionaryRemaining;
    private double oblationRemaining;
    private double blackestNightRemaining;
    private double armsLengthRemaining;
    private bool grit;

    public DarkKnightCombat(double gcdSeconds = 2.5) : base(gcdSeconds) { }

    public int Blood => blood;
    public double DarksideRemaining => darksideRemaining;
    public double ShadowRemaining => shadowRemaining;
    public int DeliriumStacks => deliriumStacks;
    public int BloodWeaponStacks => bloodWeaponStacks;
    public double SaltedEarthRemaining => saltedEarthRemaining;
    public bool Grit => grit;
    public override IReadOnlyList<uint> Actions { get; } =
        [3617, 3623, 3632, 3621, 16468, 3624, 7392, 7391, 16470, 16469, 25757, 3641, 3643, 3639, 25755, 7390, 16472, 36926,
         3629, 32067, 7531, 3636, 3634, 3638, 16471, 25754, 7393, 7548, 7535, 7533, 7537, 7538, 7540];
    // PvE rows; the same-name rows flagged CanStatusOff (1978, 2171, 1308, 1984, 1996, 3036) are PvP.
    public override IReadOnlyList<ushort> StatusIds { get; } = [742, 1972, 749, 743, 1191, 747, 746, 810, 1894, 2682, 1178, 1209];

    protected override string JobDebugState
        => $"blood={blood} darkside={darksideRemaining:0.0} shadow={shadowRemaining:0.0} delirium={deliriumStacks}/{deliriumRemaining:0.0} bloodWeapon={bloodWeaponStacks}/{bloodWeaponRemaining:0.0} salt={saltedEarthRemaining:0.0} grit={grit} shadowstride={Timing.Remaining(8):0.0} shadowbringer={Timing.Remaining(23):0.0}";

    public override uint Adjust(uint actionId) => actionId switch
    {
        3625 => 7390,
        16466 => 16469,
        16467 => 16470,
        3629 when grit => 32067,
        3639 when saltedEarthRemaining > 0 => 25755,
        _ => actionId,
    };

    public override void Seed(Func<ushort, bool> hasStatus) => grit = hasStatus(743);

    public override IEnumerable<JobStatus> Statuses()
    {
        yield return new(742, bloodWeaponStacks > 0 ? bloodWeaponRemaining : 0, (ushort)bloodWeaponStacks);
        yield return new(1972, deliriumStacks > 0 ? deliriumRemaining : 0, (ushort)deliriumStacks);
        yield return new(749, saltedEarthRemaining, 0);
        yield return new(743, grit ? double.PositiveInfinity : 0, 0);
        yield return new(1191, rampartRemaining, 0);
        yield return new(747, shadowWallRemaining, 0);
        yield return new(746, darkMindRemaining, 0);
        yield return new(810, livingDeadRemaining, 0);
        yield return new(1894, missionaryRemaining, 0);
        yield return new(2682, oblationRemaining, 0);
        yield return new(1178, blackestNightRemaining, 0);
        yield return new(1209, armsLengthRemaining, 0);
    }

    // The Blackest Night, Oblation and Shirk are cast on the player; party targets are not simulated.
    public override bool IsSelfAction(uint actionId) => actionId is 3621 or 16468 or 7391 or 3639 or 25755 or 7390 or 16472
        or 3629 or 32067 or 7531 or 3636 or 3634 or 3638 or 16471 or 25754 or 7393 or 7548 or 7535 or 7537;

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
            case 3629 or 32067:
                grit = actionId == 3629;
                return null;
            case 7531: rampartRemaining = 20; return null;
            case 3636: shadowWallRemaining = 15; return null;
            case 3634: darkMindRemaining = 10; return null;
            case 3638: livingDeadRemaining = 10; return null;
            case 16471: missionaryRemaining = 15; return null;
            case 25754: oblationRemaining = 10; return null;
            case 7393: blackestNightRemaining = 7; return null;
            case 7548: armsLengthRemaining = 6; return null;
            case 7535 or 7537: return null;
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
        rampartRemaining = Decrease(rampartRemaining, seconds);
        shadowWallRemaining = Decrease(shadowWallRemaining, seconds);
        darkMindRemaining = Decrease(darkMindRemaining, seconds);
        livingDeadRemaining = Decrease(livingDeadRemaining, seconds);
        missionaryRemaining = Decrease(missionaryRemaining, seconds);
        oblationRemaining = Decrease(oblationRemaining, seconds);
        blackestNightRemaining = Decrease(blackestNightRemaining, seconds);
        armsLengthRemaining = Decrease(armsLengthRemaining, seconds);
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
        rampartRemaining = 0;
        shadowWallRemaining = 0;
        darkMindRemaining = 0;
        livingDeadRemaining = 0;
        missionaryRemaining = 0;
        oblationRemaining = 0;
        blackestNightRemaining = 0;
        armsLengthRemaining = 0;
        // Grit is a stance: it survives entering and leaving a duty.
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
            3629 => (2, 2, 1),
            32067 => (1002, 1, 1), // shares native group 2 with Grit but has its own 1 s recast
            7531 => (47, 90, 1),
            3636 => (21, 120, 1),
            3634 => (9, 60, 1),
            3638 => (25, 300, 1),
            16471 => (15, 90, 1),
            25754 => (19, 60, 2),
            7393 => (3, 15, 1),
            7548 => (49, 120, 1),
            7535 => (45, 60, 1),
            7533 => (43, 30, 1),
            7537 => (50, 120, 1),
            7538 => (44, 30, 1),
            7540 => (42, 25, 1),
            _ => (GlobalCooldownGroup, GcdSeconds, 1),
        };
}
