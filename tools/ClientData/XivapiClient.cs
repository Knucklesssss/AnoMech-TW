using System.Text.Json;

namespace ClientData;

public static class XivapiClient
{
    public static readonly string[] DefaultVersions = ["7.3", "7.31", "7.35", "7.38", "7.4"];
    private static readonly string[] ActionFields = ["ClassJobLevel", "Cast100ms", "Recast100ms", "CooldownGroup", "AdditionalCooldownGroup", "MaxCharges", "PrimaryCostType", "PrimaryCostValue", "Range", "EffectRange", "CastType", "CanTargetSelf", "CanTargetParty", "CanTargetHostile", "IsPlayerAction", "IsPvP", "ActionCombo", "ClassJobCategory"];
    private static readonly string[] TraitFields = ["Level", "ClassJob", "Value"];
    private static readonly string[] StatusFields = ["MaxStacks", "StatusCategory", "IsPermanent", "CanDispel"];
    private static readonly HashSet<string> RowReferences = ["ActionCombo", "ClassJobCategory", "ClassJob"];

    public static IEnumerable<FieldValue> LocalValues(JobData job)
    {
        static string B(bool value) => value ? "true" : "false";
        foreach (var a in job.Actions)
        {
            (string Field, string Value)[] values =
            [
                ("ClassJobLevel", a.Level.ToString()), ("Cast100ms", a.Cast100ms.ToString()), ("Recast100ms", a.Recast100ms.ToString()),
                ("CooldownGroup", a.CooldownGroup.ToString()), ("AdditionalCooldownGroup", a.AdditionalCooldownGroup.ToString()),
                ("MaxCharges", a.MaxCharges.ToString()), ("PrimaryCostType", a.CostType.ToString()), ("PrimaryCostValue", a.CostValue.ToString()),
                ("Range", a.Range.ToString()), ("EffectRange", a.EffectRange.ToString()), ("CastType", a.CastType.ToString()),
                ("CanTargetSelf", B(a.CanTargetSelf)), ("CanTargetParty", B(a.CanTargetParty)), ("CanTargetHostile", B(a.CanTargetHostile)),
                ("IsPlayerAction", B(a.IsPlayerAction)), ("IsPvP", B(a.IsPvP)), ("ActionCombo", a.ComboFrom.ToString()),
                ("ClassJobCategory", a.ClassJobCategory.ToString()),
            ];
            foreach (var (field, value) in values) yield return new("Action", a.Id, field, value);
        }
        foreach (var t in job.Traits)
        {
            yield return new("Trait", t.Id, "Level", t.Level.ToString());
            yield return new("Trait", t.Id, "ClassJob", t.ClassJob.ToString());
            yield return new("Trait", t.Id, "Value", t.Value.ToString());
        }
        foreach (var s in job.Statuses)
        {
            yield return new("Status", s.Id, "MaxStacks", s.MaxStacks.ToString());
            yield return new("Status", s.Id, "StatusCategory", s.StatusCategory.ToString());
            yield return new("Status", s.Id, "IsPermanent", B(s.IsPermanent));
            yield return new("Status", s.Id, "CanDispel", B(s.CanDispel));
        }
    }

    public static XivapiCheck Check(JobData job, string? onlyVersion)
    {
        string[] versions = onlyVersion != null ? [onlyVersion] : DefaultVersions;
        var local = LocalValues(job).ToList();
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var counts = new Dictionary<string, int>();
            var diffs = new Dictionary<string, IReadOnlyList<FieldDiff>>();
            foreach (var version in versions)
            {
                var remote = new List<FieldValue>();
                Fetch(http, version, "Action", job.Actions.Select(a => a.Id), ActionFields, remote);
                Fetch(http, version, "Trait", job.Traits.Select(t => t.Id), TraitFields, remote);
                Fetch(http, version, "Status", job.Statuses.Select(s => s.Id), StatusFields, remote);
                diffs[version] = XivapiCompare.Diff(local, remote);
                counts[version] = diffs[version].Count;
            }
            var best = XivapiCompare.PickVersion(versions, counts);
            return new XivapiCheck(true, best, counts, diffs[best], null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException or FormatException or OverflowException or ArgumentException)
        {
            return XivapiCheck.Skipped($"xivapi 連線或查詢失敗：{ex.Message}");
        }
    }

    private static void Fetch(HttpClient http, string version, string sheet, IEnumerable<uint> ids, string[] fields, List<FieldValue> into)
    {
        var requested = string.Join(",", fields.Select(f => RowReferences.Contains(f) ? f + ".value" : f));
        foreach (var chunk in ids.Chunk(100))
        {
            var url = $"https://v2.xivapi.com/api/sheet/{sheet}?version={Uri.EscapeDataString(version)}&rows={string.Join(",", chunk)}&fields={requested}";
            using var response = http.GetAsync(url).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode) throw new HttpRequestException($"{sheet} 版本 {version} 回傳 HTTP {(int)response.StatusCode}");
            using var document = JsonDocument.Parse(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            foreach (var row in document.RootElement.GetProperty("rows").EnumerateArray())
            {
                var id = row.GetProperty("row_id").GetUInt32();
                var values = row.GetProperty("fields");
                foreach (var field in fields)
                    if (values.TryGetProperty(field, out var value)) into.Add(new(sheet, id, field, Normalize(value)));
            }
        }
    }

    private static string Normalize(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Object when value.TryGetProperty("value", out var inner) => Normalize(inner),
        _ => value.ToString(),
    };
}
