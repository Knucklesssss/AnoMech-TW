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
        foreach (var bad in new[] {double.NaN, double.PositiveInfinity, -1d, 1d})
        {
            try
            {
                WarriorDamageMath.Calculate(stats, 200, false, 1, bad, .999, 1);
                throw new Exception("Invalid RNG input must be rejected.");
            }
            catch (ArgumentOutOfRangeException) { }
        }
        Console.WriteLine("PASS: Warrior level-90 physical damage and guaranteed crit/direct hit fixtures.");
    }
}
