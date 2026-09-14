using System;
using System.Collections.Generic;

namespace AnoMech.Core.Combat;

// Level-90 procs, feathers, Esprit and dance steps. Rules from docs/client-data/DNC-90.md.
public sealed class DancerCombat : RangedCombatBase
{
    private const ushort StandardStep = 1818, TechnicalStep = 1819, ThreefoldFanDance = 1820, StandardFinish = 1821,
        TechnicalFinish = 1822, ClosedPosition = 1823, Devilment = 1825, ShieldSamba = 1826, Improvisation = 1827,
        EspritStatus = 1847, SilkenSymmetry = 2693, SilkenFlow = 2694, RisingRhythm = 2696, ImprovisedFinish = 2697,
        FlourishingFinish = 2698, FourfoldFanDance = 2699, FlourishingStarfall = 2700, FlourishingSymmetry = 3017,
        FlourishingFlow = 3018;
    private static readonly uint[] StepActions = [15999, 16000, 16001, 16002];
    private readonly int[] steps = new int[4];
    private int stepCount;
    private int stepIndex;
    private int feathers;
    private int esprit;
    private double rhythmTick;

    public DancerCombat(double gcdSeconds = 2.5) : base(gcdSeconds) { }

    public int Feathers => feathers;
    public int EspritGauge => esprit;
    public IReadOnlyList<int> Steps => steps;
    public int StepCount => stepCount;
    public int StepIndex => stepIndex;
    private bool Dancing => HasBuff(StandardStep) || HasBuff(TechnicalStep);
    protected override IReadOnlyList<uint> JobActions { get; } =
        [15989, 15990, 15991, 15992, 15993, 15994, 15995, 15996, 15997, 15998, 16003, 16004, 15999, 16000, 16001, 16002, 16005,
         16006, 16007, 16008, 16009, 16010, 16011, 16012, 16013, 16014, 25789, 16015, 25790, 25791, 25792];
    protected override IReadOnlyList<ushort> JobStatusIds { get; } =
        [StandardStep, TechnicalStep, ThreefoldFanDance, StandardFinish, TechnicalFinish, ClosedPosition, Devilment, ShieldSamba,
         Improvisation, EspritStatus, SilkenSymmetry, SilkenFlow, RisingRhythm, ImprovisedFinish, FlourishingFinish,
         FourfoldFanDance, FlourishingStarfall, FlourishingSymmetry, FlourishingFlow];
    protected override string JobDebugState => $"feathers={feathers} esprit={esprit} steps={string.Join("", steps)}@{stepIndex}/{stepCount}";

    public override uint Adjust(uint actionId)
    {
        if (Dancing)
        {
            switch (actionId)
            {
                case 15989 or 15993: return 15999;
                case 15990 or 15994: return 16000;
                case 15991 or 15995: return 16001;
                case 15992 or 15996: return 16002;
                case 15997 when HasBuff(StandardStep): return 16003;
                case 15998 when HasBuff(TechnicalStep): return 16004;
            }
        }
        return actionId switch
        {
            15998 when HasBuff(FlourishingFinish) => 25790,
            16014 when HasBuff(Improvisation) => 25789,
            _ => actionId,
        };
    }

    public override bool IsGapCloser(uint actionId) => actionId == 16010;
    protected override uint ComboFrom(uint actionId) => actionId switch { 15990 => 15989, 15994 => 15993, _ => 0 };

    // Closed Position only toggles the player's own status; the partner is not simulated.
    protected override bool IsJobSelfAction(uint actionId)
        => actionId is not (15989 or 15990 or 15991 or 15992 or 16005 or 16007 or 16009 or 25791 or 25792);

    protected override bool JobHighlighted(uint actionId) => actionId switch
    {
        15999 or 16000 or 16001 or 16002 => Dancing && stepIndex < stepCount && StepActions[steps[stepIndex] - 1] == actionId,
        16003 or 16004 => Dancing && stepIndex == stepCount,
        15991 or 15995 => HasBuff(SilkenSymmetry) || HasBuff(FlourishingSymmetry),
        15992 or 15996 => HasBuff(SilkenFlow) || HasBuff(FlourishingFlow),
        16009 => HasBuff(ThreefoldFanDance),
        25791 => HasBuff(FourfoldFanDance),
        25790 => HasBuff(FlourishingFinish),
        25792 => HasBuff(FlourishingStarfall),
        16005 => esprit >= 50,
        _ => false,
    };

    protected override bool RangedCanUse(uint actionId, bool inCombat)
    {
        if (Dancing && actionId is not (15999 or 16000 or 16001 or 16002 or 16003 or 16004 or 16010 or 16015 or 16012
                or 7541 or 7548 or 7551 or 7553 or 7554 or 7557))
            return false;
        return actionId switch
        {
            15999 or 16000 or 16001 or 16002 => Dancing && stepIndex < stepCount,
            16003 => HasBuff(StandardStep),
            16004 => HasBuff(TechnicalStep),
            15991 or 15995 => HasBuff(SilkenSymmetry) || HasBuff(FlourishingSymmetry),
            15992 or 15996 => HasBuff(SilkenFlow) || HasBuff(FlourishingFlow),
            16005 => esprit >= 50,
            16007 or 16008 => feathers > 0,
            16009 => HasBuff(ThreefoldFanDance),
            25791 => HasBuff(FourfoldFanDance),
            25790 => HasBuff(FlourishingFinish),
            25792 => HasBuff(FlourishingStarfall),
            25789 => HasBuff(Improvisation),
            16013 => inCombat,
            _ => true,
        };
    }

    protected override JobHit? ApplyJob(uint actionId, bool hasTarget)
    {
        switch (actionId)
        {
            case 15999 or 16000 or 16001 or 16002:
                if (stepIndex < stepCount && StepActions[steps[stepIndex] - 1] == actionId) stepIndex++;
                return null;
            case 15997: StartDance(StandardStep, 2); return null;
            case 15998: StartDance(TechnicalStep, 4); return null;
            case 16003:
                ClearBuff(StandardStep);
                if (stepIndex > 0)
                {
                    Buff(StandardFinish, 60, (ushort)stepIndex);
                    Buff(EspritStatus, 60);
                }
                EndDance();
                return Area(actionId, hasTarget);
            case 16004:
                ClearBuff(TechnicalStep);
                if (stepIndex > 0)
                {
                    Buff(TechnicalFinish, 20, (ushort)stepIndex);
                    if (BuffRemaining(EspritStatus) < 20) Buff(EspritStatus, 20);
                }
                Buff(FlourishingFinish, 30);
                EndDance();
                return Area(actionId, hasTarget);
            case 15989 or 15993:
                GainEsprit(5);
                if (Chance(0.5)) Buff(SilkenSymmetry, 30);
                SetCombo(actionId);
                return actionId == 15989 ? new JobHit(actionId, false, false) : Area(actionId, hasTarget);
            case 15990 or 15994:
                GainEsprit(5);
                if (ComboAction == ComboFrom(actionId) && Chance(0.5)) Buff(SilkenFlow, 30);
                ClearCombo();
                return actionId == 15990 ? new JobHit(actionId, false, false) : Area(actionId, hasTarget);
            case 15991 or 15995:
                GainEsprit(10);
                if (!ConsumeBuff(SilkenSymmetry)) ClearBuff(FlourishingSymmetry);
                GainFeatherChance();
                return actionId == 15991 ? new JobHit(actionId, false, false) : Area(actionId, hasTarget);
            case 15992 or 15996:
                GainEsprit(10);
                if (!ConsumeBuff(SilkenFlow)) ClearBuff(FlourishingFlow);
                GainFeatherChance();
                return actionId == 15992 ? new JobHit(actionId, false, false) : Area(actionId, hasTarget);
            case 16005:
                esprit -= 50;
                return new JobHit(actionId, true, false);
            case 16007 or 16008:
                feathers--;
                if (Chance(0.5)) Buff(ThreefoldFanDance, 30);
                return actionId == 16007 ? new JobHit(actionId, false, false) : Area(actionId, hasTarget);
            case 16009:
                ClearBuff(ThreefoldFanDance);
                return new JobHit(actionId, true, false);
            case 25791:
                ClearBuff(FourfoldFanDance);
                return new JobHit(actionId, true, false);
            case 25790:
                ClearBuff(FlourishingFinish);
                esprit = Math.Min(100, esprit + 50);
                return Area(actionId, hasTarget);
            case 25792:
                ClearBuff(FlourishingStarfall);
                return new JobHit(actionId, true, false);
            case 16006:
                if (!ConsumeBuff(ClosedPosition)) Buff(ClosedPosition, double.PositiveInfinity);
                return null;
            case 16011:
                Buff(Devilment, 20);
                Buff(FlourishingStarfall, 20);
                return null;
            case 16012: Buff(ShieldSamba, 15); return null;
            case 16013:
                Buff(FlourishingSymmetry, 30);
                Buff(FlourishingFlow, 30);
                Buff(ThreefoldFanDance, 30);
                Buff(FourfoldFanDance, 30);
                return null;
            case 16014:
                Buff(Improvisation, 15);
                rhythmTick = 0;
                return null;
            case 25789:
                ClearBuff(Improvisation);
                ClearBuff(RisingRhythm);
                Buff(ImprovisedFinish, 30);
                return null;
        }
        return null;
    }

    private static JobHit? Area(uint actionId, bool hasTarget) => hasTarget ? new JobHit(actionId, true, false) : null;

    // Amounts from trait 255 (+5 for Cascade/Fountain/Windmill/Bladeshower) and trait 454
    // (+10 for Reverse Cascade/Fountainfall/Rising Windmill/Bloodshower), only while Esprit status is up.
    private void GainEsprit(int amount)
    {
        if (HasBuff(EspritStatus)) esprit = Math.Min(100, esprit + amount);
    }

    private void GainFeatherChance()
    {
        if (Chance(0.5)) feathers = Math.Min(4, feathers + 1);
    }

    private void StartDance(ushort status, int count)
    {
        int[] order = [1, 2, 3, 4];
        for (var i = order.Length - 1; i > 0; i--)
        {
            var j = Math.Min(i, (int)(Roll() * (i + 1)));
            (order[i], order[j]) = (order[j], order[i]);
        }
        Array.Clear(steps);
        Array.Copy(order, steps, count);
        stepCount = count;
        stepIndex = 0;
        Buff(status, 15);
    }

    private void EndDance()
    {
        Array.Clear(steps);
        stepCount = 0;
        stepIndex = 0;
    }

    protected override void AdvanceJob(double seconds)
    {
        if (!Dancing && stepCount > 0) EndDance();
        if (!HasBuff(Improvisation))
        {
            rhythmTick = 0;
            return;
        }
        for (rhythmTick += seconds; rhythmTick >= 3; rhythmTick -= 3)
            Buff(RisingRhythm, BuffRemaining(Improvisation), (ushort)Math.Min(4, BuffParam(RisingRhythm) + 1));
    }

    protected override void ResetJob()
    {
        EndDance();
        feathers = 0;
        esprit = 0;
        rhythmTick = 0;
    }

    // ponytail: dance start does not also start the GCD, so the first step waits only the animation lock.
    protected override (int Group, double Recast, int Charges) JobTimingContract(uint actionId) => actionId switch
    {
        // ponytail: the steps are missing from the client table; 1.5 s is from the Action sheet row, the
        // in-game step GCD is unverified.
        15999 or 16000 or 16001 or 16002 => (GlobalCooldownGroup, 1.5, 1),
        15997 => (7, 30, 1),
        15998 => (20, 120, 1),
        16006 => (8, 30, 1),
        16007 => (2, 1, 1),
        16008 => (3, 1, 1),
        16009 => (4, 1, 1),
        25791 => (1, 1, 1),
        16010 => (15, 30, 3),
        16011 => (21, 120, 1),
        16012 => (22, 90, 1), // trait 456 (lv88): Shield Samba recast shortened to 90s
        16013 => (11, 60, 1),
        16014 => (19, 120, 1),
        25789 => (5, 1.5, 1),
        16015 => (12, 60, 1),
        _ => Gcd,
    };
}
