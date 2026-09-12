using System;

namespace AnoMech.Core.Combat;

public static class CombatStatMath
{
    // Two separate rounding stages match xivgear's datamanager_new.ts calcCap.
    public static int SlotCap(int itemLevelBudget, int slotWeight, int jobPercent)
    {
        if (itemLevelBudget < 0 || slotWeight < 0 || jobPercent < 0)
            throw new ArgumentOutOfRangeException(nameof(itemLevelBudget));
        var slot = decimal.Round((decimal)itemLevelBudget * slotWeight / 1000, 0, MidpointRounding.AwayFromZero);
        return checked((int)decimal.Round(slot * jobPercent / 100, 0, MidpointRounding.AwayFromZero));
    }

    public static int ItemValue(int baseValue, int hqBonus, int customValue, int materia, int cap, bool synced)
    {
        if (baseValue < 0 || hqBonus < 0 || customValue < 0 || materia < 0 || cap < 0)
            throw new ArgumentOutOfRangeException(nameof(baseValue));
        return (int)Math.Min(cap, (long)baseValue + hqBonus + customValue + (synced ? 0 : materia));
    }

    public static int FoodBonus(int value, int bonus, int cap, bool relative)
    {
        if (value < 0 || bonus < 0 || cap < 0)
            throw new ArgumentOutOfRangeException(nameof(value));
        return relative ? (int)Math.Min(cap, (long)value * bonus / 100) : bonus;
    }
}
