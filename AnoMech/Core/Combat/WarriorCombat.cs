using System;
using System.Collections.Generic;

namespace AnoMech.Core.Combat;

public sealed class WarriorCombat : IJobCombat
{
    private const int GlobalCooldownGroup = 58;
    private const double AnimationLock = 0.6;
    private readonly double gcdSeconds;
    private int beast;
    private uint comboAction;
    private double comboRemaining;
    private double tempestRemaining;
    private double chaosRemaining;
    private int innerReleaseStacks;
    private double innerReleaseRemaining;
    private double rendRemaining;

    public WarriorCombat(double gcdSeconds = 2.5)
    {
        if (!double.IsFinite(gcdSeconds) || gcdSeconds < 0) throw new ArgumentOutOfRangeException(nameof(gcdSeconds));
        this.gcdSeconds = gcdSeconds;
    }

    public int Beast => beast;
    public uint ComboAction => comboAction;
    public double ComboRemaining => comboRemaining;
    public int InnerReleaseStacks => innerReleaseStacks;
    public double InnerReleaseRemaining => innerReleaseRemaining;
    public double ChaosRemaining => chaosRemaining;
    public double TempestRemaining => tempestRemaining;
    public double RendRemaining => rendRemaining;
    public CombatTiming Timing { get; } = new();
    public IReadOnlyList<uint> Actions { get; } = [31, 37, 42, 45, 41, 16462, 46, 3549, 3550, 16465, 16463, 25753, 7386, 7387, 25752, 52, 7389];
    public IReadOnlyList<ushort> StatusIds { get; } = [1177, 1897, 2677, 2624];

    public uint Adjust(uint actionId)
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

    public bool Supports(uint actionId) => IsSupported(Adjust(actionId));

    public IEnumerable<JobStatus> Statuses()
    {
        yield return new(1177, innerReleaseStacks > 0 ? innerReleaseRemaining : 0, (ushort)innerReleaseStacks);
        yield return new(1897, chaosRemaining, 0);
        yield return new(2677, tempestRemaining, 0);
        yield return new(2624, rendRemaining, 0);
    }

    public bool IsGapCloser(uint actionId) => actionId is 25753 or 7386;

    public (int Group, double Recast, int Charges) GetCooldown(uint actionId)
    {
        actionId = Adjust(actionId);
        if (!IsSupported(actionId)) throw new ArgumentOutOfRangeException(nameof(actionId));
        return TimingContract(actionId);
    }

    public bool CanUse(uint actionId, bool hasTarget, bool inRange, bool inCombat, bool alive = true, bool bound = false, bool checkTiming = true)
    {
        actionId = Adjust(actionId);
        if (!IsSupported(actionId) || !alive) return false;
        if (actionId == 52 && !inCombat) return false;
        if (actionId == 25753 && rendRemaining <= 0) return false;
        if (actionId is 25753 or 7386 && bound) return false;
        if (!IsSelfAction(actionId) && (!hasTarget || !inRange)) return false;
        if (actionId is 16465 or 16463 && (chaosRemaining <= 0 || beast < 50)) return false;
        if (actionId is 3549 or 3550 && beast < 50 && innerReleaseStacks == 0) return false;
        var (group, recast, charges) = TimingContract(actionId);
        return !checkTiming || Timing.IsAvailable(group, recast, charges);
    }

    public JobHit? TryUse(uint actionId, bool hasTarget, bool inRange, bool inCombat, bool alive = true, bool bound = false)
    {
        if (!CanUse(actionId, hasTarget, inRange, inCombat, alive, bound)) return null;
        actionId = Adjust(actionId);
        var (group, recast, charges) = TimingContract(actionId);
        if (!Timing.TryUse(group, recast, charges, AnimationLock)) return null;

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
            if (innerReleaseStacks > 0)
            {
                innerReleaseStacks--;
            }
            else
            {
                beast -= 50;
            }
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
            case 37 when comboAction == 31:
                GrantBeast(10);
                SetCombo(37);
                break;
            case 37:
                ClearCombo();
                break;
            case 42 when comboAction == 37:
                GrantBeast(20);
                ClearCombo();
                break;
            case 42:
                ClearCombo();
                break;
            case 45 when comboAction == 37:
                GrantBeast(10);
                tempestRemaining = Math.Min(60, tempestRemaining + 30);
                ClearCombo();
                break;
            case 45:
                ClearCombo();
                break;
            case 41:
                SetCombo(41);
                break;
            case 16462 when comboAction == 41:
                GrantBeast(20);
                tempestRemaining = Math.Min(60, tempestRemaining + 30);
                ClearCombo();
                break;
            case 16462:
                ClearCombo();
                break;
        }
        if (actionId is 3549 or 3550 or 16465 or 16463) Timing.Reduce(20, 5);
        return new JobHit(actionId, isAoe, gapCloser);
    }

    public void Advance(double seconds)
    {
        Timing.Advance(seconds);
        comboRemaining = Decrease(comboRemaining, seconds);
        tempestRemaining = Decrease(tempestRemaining, seconds);
        chaosRemaining = Decrease(chaosRemaining, seconds);
        innerReleaseRemaining = Decrease(innerReleaseRemaining, seconds);
        rendRemaining = Decrease(rendRemaining, seconds);
        if (innerReleaseRemaining == 0) innerReleaseStacks = 0;
        if (comboRemaining == 0) comboAction = 0;
    }

    public void Reset()
    {
        Timing.Reset();
        beast = 0;
        tempestRemaining = 0;
        chaosRemaining = 0;
        innerReleaseStacks = 0;
        innerReleaseRemaining = 0;
        rendRemaining = 0;
        ClearCombo();
    }

    private void SetCombo(uint actionId)
    {
        comboAction = actionId;
        comboRemaining = 30;
    }

    private void ClearCombo()
    {
        comboAction = 0;
        comboRemaining = 0;
    }

    private void GrantBeast(int amount) => beast = Math.Min(100, beast + amount);

    private static double Decrease(double remaining, double seconds)
        => seconds + 1e-9 >= remaining ? 0 : remaining - seconds;

    private static bool IsSupported(uint actionId)
        => actionId is 31 or 37 or 42 or 45 or 41 or 16462 or 46 or 3549 or 3550 or 16465 or 16463 or 25753 or 7386 or 7387 or 25752 or 52 or 7389;

    public bool IsSelfAction(uint actionId)
        => actionId is 41 or 16462 or 3550 or 16463 or 25752 or 52 or 7389;

    private (int Group, double Recast, int Charges) TimingContract(uint actionId)
        => actionId switch
        {
            52 => (20, 60, 2),
            7389 => (12, 60, 1),
            7386 => (8, 30, 3),
            7387 or 25752 => (9, 30, 1),
            _ => (GlobalCooldownGroup, gcdSeconds, 1),
        };
}
