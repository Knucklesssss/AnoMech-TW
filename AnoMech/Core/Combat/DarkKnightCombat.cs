using System;
using System.Collections.Generic;

namespace AnoMech.Core.Combat;

// Level-90 rotation, Blood gauge and derived actions only. MP, Dark Arts and
// The Blackest Night are deliberately not simulated (user scope, 2026-09-14).
public sealed class DarkKnightCombat : IJobCombat
{
    private const int GlobalCooldownGroup = 58;
    private const double AnimationLock = 0.6;
    private readonly double gcdSeconds;
    private int blood;
    private uint comboAction;
    private double comboRemaining;
    private double darksideRemaining;
    private double shadowRemaining;
    private int deliriumStacks;
    private double deliriumRemaining;
    private int bloodWeaponStacks;
    private double bloodWeaponRemaining;
    private double saltedEarthRemaining;

    public DarkKnightCombat(double gcdSeconds = 2.5)
    {
        if (!double.IsFinite(gcdSeconds) || gcdSeconds < 0) throw new ArgumentOutOfRangeException(nameof(gcdSeconds));
        this.gcdSeconds = gcdSeconds;
    }

    public int Blood => blood;
    public uint ComboAction => comboAction;
    public double ComboRemaining => comboRemaining;
    public double DarksideRemaining => darksideRemaining;
    public double ShadowRemaining => shadowRemaining;
    public int DeliriumStacks => deliriumStacks;
    public int BloodWeaponStacks => bloodWeaponStacks;
    public double SaltedEarthRemaining => saltedEarthRemaining;
    public CombatTiming Timing { get; } = new();
    public IReadOnlyList<uint> Actions { get; } =
        [3617, 3623, 3632, 3621, 16468, 3624, 7392, 7391, 16470, 16469, 25757, 3641, 3643, 3639, 25755, 7390, 16472, 36926];
    public IReadOnlyList<ushort> StatusIds { get; } = [742, 1972, 749];

    public uint Adjust(uint actionId) => actionId switch
    {
        3625 => 7390,
        16466 => 16469,
        16467 => 16470,
        _ => actionId,
    };

    public bool Supports(uint actionId) => IsSupported(Adjust(actionId));

    public IEnumerable<JobStatus> Statuses()
    {
        yield return new(742, bloodWeaponStacks > 0 ? bloodWeaponRemaining : 0, (ushort)bloodWeaponStacks);
        yield return new(1972, deliriumStacks > 0 ? deliriumRemaining : 0, (ushort)deliriumStacks);
        yield return new(749, saltedEarthRemaining, 0);
    }

    public bool IsSelfAction(uint actionId) => actionId is 3621 or 16468 or 7391 or 3639 or 25755 or 7390 or 16472;

    public bool IsGapCloser(uint actionId) => actionId == 36926;

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
        if (actionId == 36926 && bound) return false;
        if (!IsSelfAction(actionId) && (!hasTarget || !inRange)) return false;
        if (actionId == 25757 && darksideRemaining <= 0) return false;
        if (actionId == 25755 && saltedEarthRemaining <= 0) return false;
        if (actionId is 7392 or 7391 && blood < 50 && deliriumStacks == 0) return false;
        var (group, recast, charges) = TimingContract(actionId);
        return !checkTiming || Timing.IsAvailable(group, recast, charges);
    }

    public JobHit? TryUse(uint actionId, bool hasTarget, bool inRange, bool inCombat, bool alive = true, bool bound = false)
    {
        if (!CanUse(actionId, hasTarget, inRange, inCombat, alive, bound)) return null;
        actionId = Adjust(actionId);
        var (group, recast, charges) = TimingContract(actionId);
        if (!Timing.TryUse(group, recast, charges, AnimationLock)) return null;

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
        if (actionId is 7392 or 7391)
        {
            if (deliriumStacks > 0) deliriumStacks--;
            else blood -= 50;
        }
        if (isAoe && !hasTarget)
        {
            if (actionId is 3621 or 16468) ClearCombo();
            return null;
        }

        var weaponskillOrSpell = actionId is 3617 or 3623 or 3632 or 3621 or 16468 or 3624 or 7392 or 7391;
        if (weaponskillOrSpell && bloodWeaponStacks > 0)
        {
            bloodWeaponStacks--;
            GrantBlood(10);
        }

        switch (actionId)
        {
            case 3617:
                SetCombo(3617);
                break;
            case 3623 when comboAction == 3617:
                SetCombo(3623);
                break;
            case 3632 when comboAction == 3623:
                GrantBlood(20);
                ClearCombo();
                break;
            case 3621:
                SetCombo(3621);
                break;
            case 16468 when comboAction == 3621:
                GrantBlood(20);
                ClearCombo();
                break;
            case 3623 or 3632 or 16468:
                ClearCombo();
                break;
            case 3624:
                Timing.Reduce(8, 5);
                break;
        }
        return new JobHit(actionId, isAoe, actionId == 36926);
    }

    public void Advance(double seconds)
    {
        Timing.Advance(seconds);
        comboRemaining = Decrease(comboRemaining, seconds);
        darksideRemaining = Decrease(darksideRemaining, seconds);
        shadowRemaining = Decrease(shadowRemaining, seconds);
        deliriumRemaining = Decrease(deliriumRemaining, seconds);
        bloodWeaponRemaining = Decrease(bloodWeaponRemaining, seconds);
        saltedEarthRemaining = Decrease(saltedEarthRemaining, seconds);
        if (deliriumRemaining == 0) deliriumStacks = 0;
        if (bloodWeaponRemaining == 0) bloodWeaponStacks = 0;
        if (comboRemaining == 0) comboAction = 0;
    }

    public void Reset()
    {
        Timing.Reset();
        blood = 0;
        darksideRemaining = 0;
        shadowRemaining = 0;
        deliriumStacks = 0;
        deliriumRemaining = 0;
        bloodWeaponStacks = 0;
        bloodWeaponRemaining = 0;
        saltedEarthRemaining = 0;
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

    private void GrantBlood(int amount) => blood = Math.Min(100, blood + amount);

    private static double Decrease(double remaining, double seconds)
        => seconds + 1e-9 >= remaining ? 0 : remaining - seconds;

    private static bool IsSupported(uint actionId)
        => actionId is 3617 or 3623 or 3632 or 3621 or 16468 or 3624 or 7392 or 7391 or 16470 or 16469
            or 25757 or 3641 or 3643 or 3639 or 25755 or 7390 or 16472 or 36926;

    // Groups and durations from docs/client-data/DRK-90.md (TC client 2026.07.22).
    private (int Group, double Recast, int Charges) TimingContract(uint actionId)
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
            _ => (GlobalCooldownGroup, gcdSeconds, 1),
        };
}
