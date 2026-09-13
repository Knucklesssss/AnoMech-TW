namespace ClientData;

public static class TraitUpgrades
{
    private const string Becomes = "變為";

    // Longest names win so 「鋼鐵旋風」 is not read as 「旋風」.
    public static IReadOnlyList<(uint From, uint To)> Find(string traitText, IReadOnlyDictionary<string, uint> actionIdsByName)
    {
        var result = new List<(uint, uint)>();
        var byLength = actionIdsByName.Where(n => n.Key.Length > 0).OrderByDescending(n => n.Key.Length).ToList();
        for (var at = traitText.IndexOf(Becomes, StringComparison.Ordinal); at >= 0; at = traitText.IndexOf(Becomes, at + Becomes.Length, StringComparison.Ordinal))
        {
            var after = at + Becomes.Length;
            var to = byLength.FirstOrDefault(n => string.CompareOrdinal(traitText, after, n.Key, 0, n.Key.Length) == 0 && after + n.Key.Length <= traitText.Length);
            if (to.Key == null) continue;
            foreach (var from in ParseNameList(traitText, at, byLength))
                result.Add((actionIdsByName[from], to.Value));
        }
        return result;
    }

    // Walks backward from `end` (the start of 「變為」) peeling off known action names separated by
    // 「與」 or 「、」 (e.g. 「暴雪與火焰變為悖論」). Stops, dropping any earlier segment, as soon as a
    // segment does not match a known action name.
    private static List<string> ParseNameList(string text, int end, IReadOnlyList<KeyValuePair<string, uint>> byLength)
    {
        var names = new List<string>();
        var pos = end;
        while (true)
        {
            var match = byLength.FirstOrDefault(n => pos >= n.Key.Length && string.CompareOrdinal(text, pos - n.Key.Length, n.Key, 0, n.Key.Length) == 0);
            if (match.Key == null) break;
            names.Insert(0, match.Key);
            pos -= match.Key.Length;
            if (pos > 0 && (text[pos - 1] == '與' || text[pos - 1] == '、')) pos -= 1;
            else break;
        }
        return names;
    }
}
