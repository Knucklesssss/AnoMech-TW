using System;
using System.Collections.Generic;

namespace AnoMech.Core.Combat;

public sealed class WarriorCombat : JobCombatBase
{
    private int beast;
    private double tempestRemaining;
    private double chaosRemaining;
    private int innerReleaseStacks;
    private double innerReleaseRemaining;
    private double rendRemaining;

    public WarriorCombat(double gcdSeconds = 2.5) : base(gcdSeconds) { }

    public int Beast => beast;
    public int InnerReleaseStacks => innerReleaseStacks;
    public double InnerReleaseRemaining => innerReleaseRemaining;
    public double ChaosRemaining => chaosRemaining;
    public double TempestRemaining => tempestRemaining;
    public double RendRemaining => rendRemaining;
    public override IReadOnlyList<uint> Actions { get; } = [31, 37, 42, 45, 41, 16462, 46, 3549, 3550, 16465, 16463, 25753, 7386, 7387, 25752, 52, 7389];
    public override IReadOnlyList<ushort> StatusIds { get; } = [1177, 1897, 2677, 2624];

    public override uint Adjust(uint actionId)
    {
        actionId = actionId switch
        {
            49 => 3549,
            51 => 3550,
            38 => 7389,
            _ => actionId,
        };
        if (chaosRemaining > 0 && beast >= 50)
            return actionId switch
            {
                3549 => 16465,
                3550 => 16463,
                _ => actionId,
            };
        return actionId;
    }

    public override IEnumerable<JobStatus> Statuses()
    {
        yield return new(1177, innerReleaseStacks > 0 ? innerReleaseRemaining : 0, (ushort)innerReleaseStacks);
        yield return new(1897, chaosRemaining, 0);
        yield return new(2677, tempestRemaining, 0);
        yield return new(2624, rendRemaining, 0);
    }

    public override bool IsGapCloser(uint actionId) => actionId is 25753 or 7386;

    public override bool IsSelfAction(uint actionId)
        => actionId is 41 or 16462 or 3550 or 16463 or 25752 or 52 or 7389;

    protected override uint ComboFrom(uint actionId) => actionId switch
    {
        37 => 31,
        42 or 45 => 37,
        16462 => 41,
        _ => 0,
    };

    protected override bool JobCanUse(uint actionId, bool inCombat)
    {
        if (actionId == 52 && !inCombat) return false;
        if (actionId == 25753 && rendRemaining <= 0) return false;
        if (actionId is 16465 or 16463 && (chaosRemaining <= 0 || beast < 50)) return false;
        if (actionId is 3549 or 3550 && beast < 50 && innerReleaseStacks == 0) return false;
        return true;
    }

    protected override JobHit? Apply(uint actionId, bool hasTarget)
    {
        if (actionId == 52)
        {
            beast = Math.Min(100, beast + 50);
            chaosRemaining = 30;
            return null;
        }
        if (actionId == 7389)
        {
            innerReleaseStacks = 3;
            innerReleaseRemaining = 15;
            rendRemaining = 30;
            if (tempestRemaining > 0) tempestRemaining = Math.Min(60, tempestRemaining + 10);
            return null;
        }

        var isAoe = actionId is 41 or 16462 or 3550 or 16463 or 25753 or 25752;
        var gapCloser = actionId is 25753 or 7386;
        if (actionId is 16465 or 16463)
        {
            beast -= 50;
            chaosRemaining = 0;
        }
        else if (actionId is 3549 or 3550)
        {
            if (innerReleaseStacks > 0) innerReleaseStacks--;
            else beast -= 50;
        }
        if (actionId == 25753) rendRemaining = 0;
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
                tempestRemaining = Math.Min(60, tempestRemaining + 30);
                ClearCombo();
                break;
            case 41:
                SetCombo(41);
                break;
            case 16462 when ComboAction == 41:
                GrantBeast(20);
                tempestRemaining = Math.Min(60, tempestRemaining + 30);
                ClearCombo();
                break;
            case 37 or 42 or 45 or 16462:
                ClearCombo();
                break;
        }
        if (actionId is 3549 or 3550 or 16465 or 16463) Timing.Reduce(20, 5);
        return new JobHit(actionId, isAoe, gapCloser);
    }

    protected override void AdvanceJob(double seconds)
    {
        tempestRemaining = Decrease(tempestRemaining, seconds);
        chaosRemaining = Decrease(chaosRemaining, seconds);
        innerReleaseRemaining = Decrease(innerReleaseRemaining, seconds);
        rendRemaining = Decrease(rendRemaining, seconds);
        if (innerReleaseRemaining == 0) innerReleaseStacks = 0;
    }

    protected override void ResetJob()
    {
        beast = 0;
        tempestRemaining = 0;
        chaosRemaining = 0;
        innerReleaseStacks = 0;
        innerReleaseRemaining = 0;
        rendRemaining = 0;
    }

    private void GrantBeast(int amount) => beast = Math.Min(100, beast + amount);

    protected override (int Group, double Recast, int Charges) TimingContract(uint actionId)
        => actionId switch
        {
            52 => (20, 60, 2),
            7389 => (12, 60, 1),
            7386 => (8, 30, 3),
            7387 or 25752 => (9, 30, 1),
            _ => (GlobalCooldownGroup, GcdSeconds, 1),
        };
}
