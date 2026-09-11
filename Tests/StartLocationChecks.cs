using AnoMech.Core.Map;

internal static class StartLocationChecks
{
    public static void Run()
    {
        foreach (var (use, name, allowed) in new (uint, string, bool)[]
        {
            (2, "s1i0", true),
            (14, "s1i1", true),
            (14, "f1i2", true),
            (14, "w1i3", true),
            (14, "e1i4", true),
            (14, "r1i4_2", true),
            (14, "s1i5", false),
            (14, "s1i6", false),
            (13, "s1i1", false),
            (1, "s1i1", false),
            (16, "s1i1", false),
            (14, "", false),
            (14, "s1i10", false),
            (0, "", false),
        })
        {
            if (StartLocationRules.IsAllowed(use, name) != allowed)
                throw new Exception($"Start location {use}/{name}: expected {allowed}");
        }
        Console.WriteLine("Start locations: inns and residential interiors allowed; wards, workshops and lobbies rejected.");
    }
}
