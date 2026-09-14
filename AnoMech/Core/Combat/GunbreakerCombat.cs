using System;
using System.Collections.Generic;

namespace AnoMech.Core.Combat;

// Level-90 rotation, Powder Gauge, Gnashing Fang chain, Continuation and castable
// defensive actions (status icons and cooldowns only). Rules from docs/client-data/GNB-90.md.
public sealed class GunbreakerCombat : TankCombatBase
{
    // ponytail: the Gnashing Fang chain window is not in the client text; 30 s like
    // a normal combo, pending the user's in-game check.
    private const double ChainSeconds = 30;
    private const ushort NoMercy = 1831, ReadyToBreak = 3886, ReadyToRip = 1842, ReadyToTear = 1843, ReadyToGouge = 1844,
        ReadyToBlast = 2686, BrutalShell = 1898, Camouflage = 1832, Nebula = 1834, Aurora = 1835, Superbolide = 1836,
        HeartOfLight = 1839, HeartOfCorundum = 2683, ClarityOfCorundum = 2684, CatharsisOfCorundum = 2685;
    private int cartridges;
    private int gnashStep;
    private double gnashRemaining;

    public GunbreakerCombat(double gcdSeconds = 2.5) : base(gcdSeconds, 16142, 32068, 1833, 2) { }

    public int Cartridges => cartridges;
    public int GnashStep => gnashStep;
    protected override IReadOnlyList<uint> JobActions { get; } =
        [16137, 16139, 16145, 16141, 16149, 16143, 16138, 16153, 16159, 16165, 16146, 16147, 16150, 16155, 16156, 16157, 16158,
         25759, 16162, 16163, 25760, 16164, 36934, 16140, 16148, 16151, 16152, 16160, 25758];
    // PvE rows; the same-name CanStatusOff rows (3042, 3051, 2065, 4295, 4296, 1997, 2002, 2003, 2004) are PvP.
    protected override IReadOnlyList<ushort> JobStatusIds { get; } =
        [1831, 3886, 1842, 1843, 1844, 2686, 1898, 1832, 1834, 1835, 1836, 1839, 2683, 2684, 2685];

    protected override string JobDebugState => $"cartridges={cartridges} gnash={gnashStep}/{gnashRemaining:0.0} gnashFang={Timing.Remaining(8):0.0}";

    // Substitutions from the client's ReplaceAction sheet (技能變換設定).
    protected override uint AdjustJob(uint actionId) => actionId switch
    {
        16144 => 16165,
        16161 => 25758,
        16138 when HasBuff(ReadyToBreak) => 16153,
        16146 when gnashStep == 1 => 16147,
        16146 when gnashStep == 2 => 16150,
        16155 when HasBuff(ReadyToRip) => 16156,
        16155 when HasBuff(ReadyToTear) => 16157,
        16155 when HasBuff(ReadyToGouge) => 16158,
        16155 when HasBuff(ReadyToBlast) => 25759,
        _ => actionId,
    };

    // Aurora and Heart of Corundum are cast on the player; party targets are not simulated.
    protected override bool IsJobSelfAction(uint actionId)
        => actionId is 16141 or 16149 or 16138 or 16159 or 16163 or 25760 or 16155 or 16140 or 16148 or 16151 or 16152 or 16160 or 25758;

    public override bool IsGapCloser(uint actionId) => actionId == 36934;

    protected override uint ComboFrom(uint actionId) => actionId switch
    {
        16139 => 16137,
        16145 => 16139,
        16149 => 16141,
        _ => 0,
    };

    protected override bool AlsoUsesGcd(uint actionId) => actionId is 16146 or 25760;

    protected override bool JobHighlighted(uint actionId) => actionId switch
    {
        16147 => gnashStep == 1,
        16150 => gnashStep == 2,
        16156 => HasBuff(ReadyToRip),
        16157 => HasBuff(ReadyToTear),
        16158 => HasBuff(ReadyToGouge),
        25759 => HasBuff(ReadyToBlast),
        16153 => HasBuff(ReadyToBreak),
        _ => false,
    };

    protected override bool JobCanUse(uint actionId, bool inCombat) => actionId switch
    {
        16146 => cartridges >= 1 && gnashStep == 0,
        16162 or 16163 or 25760 => cartridges >= 1,
        16147 => gnashStep == 1,
        16150 => gnashStep == 2,
        16156 => HasBuff(ReadyToRip),
        16157 => HasBuff(ReadyToTear),
        16158 => HasBuff(ReadyToGouge),
        25759 => HasBuff(ReadyToBlast),
        16153 => HasBuff(ReadyToBreak),
        16155 => false,
        _ => true,
    };

    protected override JobHit? ApplyJob(uint actionId, bool hasTarget)
    {
        switch (actionId)
        {
            case 16138:
                Buff(NoMercy, 20);
                Buff(ReadyToBreak, 30);
                return null;
            case 16140: Buff(Camouflage, 20); return null;
            case 16148: Buff(Nebula, 15); return null;
            case 16151: Buff(Aurora, 18); return null;
            case 16152: Buff(Superbolide, 10); return null;
            case 16160: Buff(HeartOfLight, 15); return null;
            case 25758:
                Buff(HeartOfCorundum, 8);
                Buff(ClarityOfCorundum, 4);
                Buff(CatharsisOfCorundum, 20);
                return null;
            case 16156: ConsumeBuff(ReadyToRip); return new JobHit(actionId, false, false);
            case 16157: ConsumeBuff(ReadyToTear); return new JobHit(actionId, false, false);
            case 16158: ConsumeBuff(ReadyToGouge); return new JobHit(actionId, false, false);
            case 25759: ConsumeBuff(ReadyToBlast); return new JobHit(actionId, false, false);
            case 16164:
                cartridges = 3;
                return new JobHit(actionId, false, false);
        }

        // "發動戰技會導致該效果消失": any weaponskill drops unspent Continuation readiness.
        if (actionId is 16137 or 16139 or 16145 or 16141 or 16149 or 16143 or 16153 or 16146 or 16147 or 16150 or 16162 or 16163 or 25760)
        {
            ClearBuff(ReadyToRip);
            ClearBuff(ReadyToTear);
            ClearBuff(ReadyToGouge);
            ClearBuff(ReadyToBlast);
        }
        if (actionId is 16146 or 16162 or 16163 or 25760) cartridges--;
        if (actionId == 16153) ConsumeBuff(ReadyToBreak);
        var isAoe = actionId is 16141 or 16149 or 16159 or 16163 or 25760;
        if (isAoe && !hasTarget)
        {
            if (actionId is 16141 or 16149) ClearCombo();
            return null;
        }

        switch (actionId)
        {
            case 16137:
                SetCombo(16137);
                break;
            case 16139 when ComboAction == 16137:
                Buff(BrutalShell, 30);
                SetCombo(16139);
                break;
            case 16145 when ComboAction == 16139:
                GrantCartridge();
                ClearCombo();
                break;
            case 16141:
                SetCombo(16141);
                break;
            case 16149 when ComboAction == 16141:
                GrantCartridge();
                ClearCombo();
                break;
            case 16139 or 16145 or 16149:
                ClearCombo();
                break;
            case 16146:
                SetGnash(1);
                Buff(ReadyToRip, 10);
                break;
            case 16147:
                SetGnash(2);
                Buff(ReadyToTear, 10);
                break;
            case 16150:
                SetGnash(0);
                Buff(ReadyToGouge, 10);
                break;
            case 16162:
                Buff(ReadyToBlast, 10);
                break;
        }
        return new JobHit(actionId, isAoe, actionId == 36934);
    }

    protected override void AdvanceJob(double seconds)
    {
        gnashRemaining = Decrease(gnashRemaining, seconds);
        if (gnashRemaining == 0) gnashStep = 0;
    }

    protected override void ResetJob()
    {
        cartridges = 0;
        SetGnash(0);
    }

    private void SetGnash(int step)
    {
        gnashStep = step;
        gnashRemaining = step == 0 ? 0 : ChainSeconds;
    }

    private void GrantCartridge() => cartridges = Math.Min(3, cartridges + 1);

    protected override (int Group, double Recast, int Charges) JobTimingContract(uint actionId)
        => actionId switch
        {
            16138 => (11, 60, 1),
            16159 => (12, 60, 1),
            16165 => (7, 30, 1),
            16146 => (8, 30, 1),
            16155 or 16156 or 16157 or 16158 or 25759 => (1, 1, 1),
            25760 => (13, 60, 1),
            16164 => (21, 120, 1),
            36934 => (9, 30, 2),
            16140 => (16, 90, 1),
            16148 => (22, 120, 1),
            16151 => (20, 60, 2),
            16152 => (25, 360, 1),
            16160 => (15, 90, 1),
            25758 => (6, 25, 1),
            _ => Gcd,
        };
}
