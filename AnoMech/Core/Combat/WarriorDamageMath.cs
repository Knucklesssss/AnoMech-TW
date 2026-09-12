using System;

namespace AnoMech.Core.Combat;

public sealed record WarriorDamageStats(int Strength, int WeaponDamage, int CriticalHit,
    int DirectHit, int Determination, int Tenacity, int SkillSpeed, double WeaponDelay);

// Level-90 physical floor sequence and 7.x tenacity scaling, cross-checked with
// xiv-gear-planner/gear-planner packages/xivmath/src/{xivmath,xivconstants}.ts.
// This is a local combat model, not a source of authoritative server damage.
public static class WarriorDamageMath
{
    public static uint Calculate(WarriorDamageStats stats, int potency, bool guaranteed,
        double multiplier, double critRoll, double directRoll, double variance, bool autoAttack = false)
    {
        ArgumentNullException.ThrowIfNull(stats);
        if (potency < 0 || stats.Strength < 0 || stats.WeaponDamage < 0 || stats.CriticalHit < 0
            || stats.DirectHit < 0 || stats.Determination < 0 || stats.Tenacity < 0 || stats.SkillSpeed < 0)
            throw new ArgumentOutOfRangeException(nameof(stats));
        if (!double.IsFinite(stats.WeaponDelay) || stats.WeaponDelay <= 0)
            throw new ArgumentOutOfRangeException(nameof(stats));
        if (!double.IsFinite(multiplier) || multiplier < 0)
            throw new ArgumentOutOfRangeException(nameof(multiplier));
        ValidateRoll(critRoll);
        ValidateRoll(directRoll);
        if (!double.IsFinite(variance) || variance < .95 || variance > 1.05)
            throw new ArgumentOutOfRangeException(nameof(variance));
        if (potency == 0 || multiplier == 0) return 0;

        var ap = Math.Max(0, decimal.Truncate(156m * (stats.Strength - 390) / 390) + 100) / 100;
        var det = 1000 + decimal.Floor(140m * (stats.Determination - 390) / 1900);
        if (guaranteed) det += decimal.Floor(140m * (stats.DirectHit - 400) / 1900);
        var ten = 1000 + decimal.Floor(112m * (stats.Tenacity - 400) / 1900);
        var weapon = decimal.Floor(390m * 105 / 1000 + stats.WeaponDamage);
        if (autoAttack) weapon = decimal.Floor(weapon * (decimal)stats.WeaponDelay / 3);
        var amount = decimal.Floor(potency * ap);
        amount = decimal.Floor(amount * det / 1000);
        amount = decimal.Floor(amount * ten / 1000);
        amount = decimal.Floor(amount * weapon / 100);
        if (autoAttack)
            amount = decimal.Floor(amount * (1000 + decimal.Floor(130m * (stats.SkillSpeed - 400) / 1900)) / 1000);
        if (potency < 100) amount++;

        var critical = decimal.Floor(200m * (stats.CriticalHit - 400) / 1900);
        if (guaranteed || (decimal)critRoll < (critical + 50) / 1000)
            amount = decimal.Floor(amount * (1400 + critical) / 1000);
        if (guaranteed || (decimal)directRoll < decimal.Floor(550m * (stats.DirectHit - 400) / 1900) / 1000)
            amount = decimal.Floor(amount * 1.25m);
        amount = decimal.Floor(amount * (decimal)multiplier);
        return checked((uint)Math.Max(1, decimal.Floor(amount * (decimal)variance)));
    }

    public static double Gcd(int skillSpeed)
    {
        if (skillSpeed < 0) throw new ArgumentOutOfRangeException(nameof(skillSpeed));
        var speed = decimal.Floor(130m * (skillSpeed - 400) / 1900);
        return (double)Math.Max(0, decimal.Floor(decimal.Floor((1000 - speed) * 2.5m) / 10) / 100);
    }

    private static void ValidateRoll(double roll)
    {
        if (!double.IsFinite(roll) || roll < 0 || roll >= 1)
            throw new ArgumentOutOfRangeException(nameof(roll));
    }
}
