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
            var from = byLength.FirstOrDefault(n => at >= n.Key.Length && string.CompareOrdinal(traitText, at - n.Key.Length, n.Key, 0, n.Key.Length) == 0);
            var after = at + Becomes.Length;
            var to = byLength.FirstOrDefault(n => string.CompareOrdinal(traitText, after, n.Key, 0, n.Key.Length) == 0 && after + n.Key.Length <= traitText.Length);
            if (from.Key != null && to.Key != null) result.Add((from.Value, to.Value));
        }
        return result;
    }
}
