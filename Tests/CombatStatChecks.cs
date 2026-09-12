internal static class CombatStatChecks
{
    public static void Run()
    {
        var type = typeof(CombatStatChecks).Assembly.GetType("AnoMech.Core.Combat.CombatStatMath")
            ?? throw new Exception("Equipment contribution arithmetic is missing.");
        var slotCap = type.GetMethod("SlotCap") ?? throw new Exception("Verified slot-cap rounding is missing.");
        if ((int)slotCap.Invoke(null, new object[] {2555, 140, 100})! != 358
            || (int)slotCap.Invoke(null, new object[] {2047, 140, 100})! != 287
            || (int)slotCap.Invoke(null, new object[] {2555, 100, 100})! != 256)
            throw new Exception("Slot caps must match the installed ilvl635 weapon rows.");
        int Item(int basis, int hq, int custom, int materia, int cap, bool synced)
            => (int)type.GetMethod("ItemValue")!.Invoke(null, new object[] {basis, hq, custom, materia, cap, synced})!;
        if (Item(180, 20, 0, 72, 250, false) != 250 || Item(180, 20, 0, 72, 250, true) != 200)
            throw new Exception("Sync must remove ordinary materia, keep HQ stats and cap contributions.");
        if (Item(100, 0, 160, 72, 250, true) != 250)
            throw new Exception("Custom relic stats survive sync but remain capped.");
        int Food(int value, int bonus, int cap, bool relative)
            => (int)type.GetMethod("FoodBonus")!.Invoke(null, new object[] {value, bonus, cap, relative})!;
        if (Food(2000, 10, 180, true) != 180 || Food(999, 10, 180, true) != 99 || Food(2000, 20, 0, false) != 20)
            throw new Exception("Food percentage uses flooring and a cap, flat food uses the stated value.");
        try
        {
            Item(-1, 0, 0, 0, 250, false);
            throw new Exception("Negative equipment contributions must be rejected.");
        }
        catch (System.Reflection.TargetInvocationException e) when (e.InnerException is ArgumentOutOfRangeException) { }
        Console.WriteLine("PASS: synchronized item, HQ, materia, relic and food contributions.");
    }
}
