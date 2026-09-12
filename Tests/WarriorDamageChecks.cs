using AnoMech.Core.Combat;

internal static class WarriorDamageChecks
{
    public static void Run()
    {
        var stats = new WarriorDamageStats(3000, 126, 2000, 1000, 1800, 600, 400, 3.36);
        uint Hit(int potency, bool guaranteed) => WarriorDamageMath.Calculate(
            stats, potency, guaranteed, 1, .999, .999, 1);
        if (Hit(200, false) != 4233) throw new Exception("Normal physical damage must apply attribute scaling and staged rounding.");
        if (Hit(200, true) != 8627) throw new Exception("Guaranteed crit/direct hit must include direct-hit stat conversion.");
        if (Hit(0, false) != 0) throw new Exception("A non-damaging ability must not cause minimum damage.");
        if (WarriorDamageMath.Gcd(400) != 2.5)
            throw new Exception("Base skill speed must yield a 2.5 second GCD.");
        if (WarriorDamageMath.Gcd(780) != 2.43)
            throw new Exception("Skill speed must shorten GCD with game-style rounding.");
        var autoDamage = WarriorDamageMath.Calculate(stats with { WeaponDelay = 3 },
            90, false, 1, .999, .999, 1, true);
        if (autoDamage != 1903) throw new Exception("Auto-attack must use weapon delay and low-potency rounding.");
        if (WarriorDamageMath.Calculate(stats with { WeaponDelay = 3 },
                200, false, 1, .999, .999, 1, true) != 1903)
            throw new Exception("Auto-attack must ignore caller-selected positive potency and use 90.");
        if (WarriorDamageMath.Calculate(stats with { WeaponDelay = 3 },
                0, false, 1, .999, .999, 1, true) != 1903)
            throw new Exception("Auto-attack must use 90 potency even when caller potency is zero.");

        if (WarriorDamageMath.Calculate(stats, 200, false, 1, .217, .999, 1) != 6637
            || WarriorDamageMath.Calculate(stats, 200, false, 1, .218, .999, 1) != 4233)
            throw new Exception("Ordinary critical-hit probability must use a strict 21.8% boundary.");
        if (WarriorDamageMath.Calculate(stats, 200, false, 1, .999, .172, 1) != 5291
            || WarriorDamageMath.Calculate(stats, 200, false, 1, .999, .173, 1) != 4233)
            throw new Exception("Ordinary direct-hit probability must use a strict 17.3% boundary.");

        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, -1d })
            Reject<ArgumentOutOfRangeException>(() => WarriorDamageMath.Calculate(stats, 200, false, bad, .999, .999, 1),
                "Non-finite or negative multipliers must be rejected.");
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, -1d, 1d })
        {
            Reject<ArgumentOutOfRangeException>(() => WarriorDamageMath.Calculate(stats, 200, false, 1, bad, .999, 1),
                "Invalid critical-hit rolls must be rejected.");
            Reject<ArgumentOutOfRangeException>(() => WarriorDamageMath.Calculate(stats, 200, false, 1, .999, bad, 1),
                "Invalid direct-hit rolls must be rejected.");
        }
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, .949, 1.051 })
            Reject<ArgumentOutOfRangeException>(() => WarriorDamageMath.Calculate(stats, 200, false, 1, .999, .999, bad),
                "Non-finite or out-of-range variance must be rejected.");
        if (WarriorDamageMath.Calculate(stats, 200, false, 1, .999, .999, .95) != 4021
            || WarriorDamageMath.Calculate(stats, 200, false, 1, .999, .999, 1.05) != 4444)
            throw new Exception("Variance endpoints from 0.95 through 1.05 must remain valid.");

        Reject<ArgumentOutOfRangeException>(() => WarriorDamageMath.Calculate(stats, -1, false, 1, .999, .999, 1, true),
            "Negative potency must be rejected before auto-attack normalization.");
        foreach (var badStats in new[]
        {
            stats with { Strength = -1 }, stats with { WeaponDamage = -1 }, stats with { CriticalHit = -1 },
            stats with { DirectHit = -1 }, stats with { Determination = -1 }, stats with { Tenacity = -1 },
            stats with { SkillSpeed = -1 }
        })
            Reject<ArgumentOutOfRangeException>(() => WarriorDamageMath.Calculate(badStats, 200, false, 1, .999, .999, 1),
                "Negative combat stats must be rejected.");
        foreach (var badDelay in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, 0, -1 })
            Reject<ArgumentOutOfRangeException>(() => WarriorDamageMath.Calculate(stats with { WeaponDelay = badDelay },
                    200, false, 1, .999, .999, 1),
                "Non-finite or non-positive weapon delay must be rejected.");
        Reject<ArgumentNullException>(() => WarriorDamageMath.Calculate(null!, 200, false, 1, .999, .999, 1),
            "Missing combat stats must be rejected.");
        Reject<ArgumentOutOfRangeException>(() => WarriorDamageMath.Gcd(-1),
            "Negative skill speed must be rejected for GCD calculation.");
        Reject<OverflowException>(() => WarriorDamageMath.Calculate(stats, int.MaxValue, false, 1, .999, .999, 1),
            "Damage exceeding UInt32 must be rejected instead of wrapping.");
        Console.WriteLine("PASS: Warrior level-90 physical damage and guaranteed crit/direct hit fixtures.");
    }

    private static void Reject<T>(Action action, string message) where T : Exception
    {
        try
        {
            action();
            throw new Exception(message);
        }
        catch (T) { }
    }
}
