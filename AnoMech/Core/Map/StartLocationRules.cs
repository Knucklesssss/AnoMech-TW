namespace AnoMech.Core.Map;

internal static class StartLocationRules
{
    internal static bool IsAllowed(uint intendedUse, string territoryName)
        => intendedUse == 2 || (intendedUse == 14 && territoryName.Length >= 3
            && territoryName[2..] is "i1" or "i2" or "i3" or "i4" or "i4_2" or "i5" or "i6");
}
