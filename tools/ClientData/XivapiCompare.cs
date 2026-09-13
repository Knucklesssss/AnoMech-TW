namespace ClientData;

public readonly record struct FieldValue(string Sheet, uint Row, string Field, string Value);

public readonly record struct FieldDiff(string Sheet, uint Row, string Field, string Local, string? Remote);

public static class XivapiCompare
{
    public static IReadOnlyList<FieldDiff> Diff(IEnumerable<FieldValue> local, IEnumerable<FieldValue> remote)
    {
        var remoteByKey = remote.ToDictionary(r => (r.Sheet, r.Row, r.Field), r => r.Value);
        return local
            .Select(l => new FieldDiff(l.Sheet, l.Row, l.Field, l.Value, remoteByKey.TryGetValue((l.Sheet, l.Row, l.Field), out var v) ? v : null))
            .Where(d => d.Remote != d.Local)
            .ToList();
    }

    public static string PickVersion(IReadOnlyList<string> versions, IReadOnlyDictionary<string, int> diffCounts)
        => versions.Where(diffCounts.ContainsKey).OrderBy(v => diffCounts[v]).First();
}
