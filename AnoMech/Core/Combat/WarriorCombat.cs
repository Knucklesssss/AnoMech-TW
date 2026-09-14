using System;
using System.Collections.Generic;

namespace AnoMech.Core.Combat;

// Level-90 rotation, Beast Gauge, derived actions and castable defensive actions
// (status icons and cooldowns only). Rules from docs/client-data/WAR-90.md.
public sealed class WarriorCombat : TankCombatBase
{
    private const ushort InnerRelease = 1177, InnerStrength = 2663, NascentChaos = 1897, SurgingTempest = 2677, PrimalRendReady = 2624,
        Thrill = 87, Holmgang = 409, Vengeance = 89, Bloodwhetting = 2678, StemTheFlow = 2679, StemTheTide = 2680,
        Equilibrium = 2681, ShakeItOff = 1457, NascentFlash = 1857;
    private int beast;

    public WarriorCombat(double gcdSeconds = 2.5) : base(gcdSeconds, 48, 32066, 91, 2) { }

    public int Beast => beast;
    public int InnerReleaseStacks => BuffParam(InnerRelease);
    public double TempestRemaining => BuffRemaining(SurgingTempest);
    public bool Chaos => HasBuff(NascentChaos);
    public bool RendReady => HasBuff(PrimalRendReady);
    protected override IReadOnlyList<uint> JobActions { get; } =
        [31, 37, 42, 45, 41, 16462, 46, 3549, 3550, 16465, 16463, 25753, 7386, 7387, 25752, 52, 7389, 40, 43, 44, 3552, 7388, 25751, 16464];
    // PvE rows; the same-name CanStatusOff rows (1303, 3185, 1304, 3030, 3031, 1993) are PvP.
    protected override IReadOnlyList<ushort> JobStatusIds { get; } = [1177, 2663, 1897, 2677, 2624, 87, 409, 89, 2678, 2679, 2680, 2681, 1457, 1857];

    protected override string JobDebugState => $"beast={beast} infuriate={Timing.Remaining(20):0.0} onslaught={Timing.Remaining(8):0.0}";

    protected override uint AdjustJob(uint actionId)
    {
        actionId = actionId switch
        {
            49 => 3549,
            51 => 3550,
            38 => 7389,
            3551 => 25751,
            36923 => 44, // Damnation, level 92, stored on a level-100 hotbar
            _ => actionId,
        };
        if (HasBuff(NascentChaos) && beast >= 50)
            return actionId switch
            {
                3549 => 16465,
                3550 => 16463,
                _ => actionId,
            };
        return actionId;
    }

    // Holmgang and Nascent Flash are cast on the player; enemy and party targets are not simulated.
    protected override bool IsJobSelfAction(uint actionId)
        => actionId is 41 or 16462 or 3550 or 16463 or 25752 or 52 or 7389 or 40 or 43 or 44 or 3552 or 7388 or 25751 or 16464;

    public override bool IsGapCloser(uint actionId) => actionId is 25753 or 7386;

    protected override uint ComboFrom(uint actionId) => actionId switch
    {
        37 => 31,
        42 or 45 => 37,
        16462 => 41,
        _ => 0,
    };

    protected override bool JobHighlighted(uint actionId) => actionId switch
    {
        25753 => HasBuff(PrimalRendReady),
        16465 or 16463 => HasBuff(NascentChaos),
        3549 or 3550 => HasBuff(InnerRelease),
        _ => false,
    };

    protected override bool JobCanUse(uint actionId, bool inCombat)
    {
        if (actionId == 52 && !inCombat) return false;
        if (actionId == 25753 && !HasBuff(PrimalRendReady)) return false;
        if (actionId is 16465 or 16463 && (!HasBuff(NascentChaos) || beast < 50)) return false;
        if (actionId is 3549 or 3550 && beast < 50 && !HasBuff(InnerRelease)) return false;
        return true;
    }

    protected override JobHit? ApplyJob(uint actionId, bool hasTarget)
    {
        switch (actionId)
        {
            case 52:
                GrantBeast(50);
                Buff(NascentChaos, 30);
                return null;
            case 7389:
                Buff(InnerRelease, 15, 3);
                Buff(InnerStrength, 15);
                Buff(PrimalRendReady, 30);
                if (HasBuff(SurgingTempest)) ExtendBuff(SurgingTempest, 10, 60);
                return null;
            case 40: Buff(Thrill, 10); return null;
            case 43: Buff(Holmgang, 10); return null;
            case 44: Buff(Vengeance, 15); return null;
            case 3552: Buff(Equilibrium, 15); return null;
            case 16464: Buff(NascentFlash, 8); return null;
            case 25751:
                Buff(Bloodwhetting, 8);
                Buff(StemTheFlow, 4);
                Buff(StemTheTide, 20);
                return null;
            case 7388:
                ClearBuff(Thrill);
                ClearBuff(Vengeance);
                ClearBuff(Bloodwhetting);
                Buff(ShakeItOff, 30);
                return null;
        }

        var isAoe = actionId is 41 or 16462 or 3550 or 16463 or 25753 or 25752;
        if (actionId is 16465 or 16463)
        {
            beast -= 50;
            ClearBuff(NascentChaos);
        }
        else if (actionId is 3549 or 3550 && !ConsumeStack(InnerRelease))
        {
            beast -= 50;
        }
        if (actionId == 25753) ClearBuff(PrimalRendReady);
        if (isAoe && !hasTarget)
        {
            if (actionId is 41 or 16462) ClearCombo();
            return null;
        }

        switch (actionId)
        {
            case 31:
                SetCombo(31);
                break;
            case 37 when ComboAction == 31:
                GrantBeast(10);
                SetCombo(37);
                break;
            case 42 when ComboAction == 37:
                GrantBeast(20);
                ClearCombo();
                break;
            case 45 when ComboAction == 37:
                GrantBeast(10);
                ExtendBuff(SurgingTempest, 30, 60);
                ClearCombo();
                break;
            case 41:
                SetCombo(41);
                break;
            case 16462 when ComboAction == 41:
                GrantBeast(20);
                ExtendBuff(SurgingTempest, 30, 60);
                ClearCombo();
                break;
            case 37 or 42 or 45 or 16462:
                ClearCombo();
                break;
        }
        if (actionId is 3549 or 3550 or 16465 or 16463) Timing.Reduce(20, 5);
        return new JobHit(actionId, isAoe, actionId is 25753 or 7386);
    }

    protected override void AdvanceJob(double seconds) { }

    protected override void ResetJob() => beast = 0;

    private void GrantBeast(int amount) => beast = Math.Min(100, beast + amount);

    protected override (int Group, double Recast, int Charges) JobTimingContract(uint actionId)
        => actionId switch
        {
            52 => (20, 60, 2),
            7389 => (12, 60, 1),
            7386 => (8, 30, 3),
            7387 or 25752 => (9, 30, 1),
            40 => (16, 90, 1),
            43 => (25, 240, 1),
            44 => (22, 120, 1),
            3552 => (14, 60, 1),
            7388 => (15, 90, 1),
            25751 or 16464 => (7, 25, 1),
            _ => Gcd,
        };
}
