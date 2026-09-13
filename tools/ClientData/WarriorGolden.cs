namespace ClientData;

// Values hand-verified in docs/superpowers/specs/2026-09-13-warrior-client-contract.md.
public static class WarriorGolden
{
    public static IReadOnlyList<string> Check(JobData job)
    {
        var failures = new List<string>();
        var actions = job.Actions.ToDictionary(a => a.Id);
        ActionRow? A(uint id)
        {
            if (actions.TryGetValue(id, out var a)) return a;
            failures.Add($"缺少技能 {id}");
            return null;
        }
        void Expect(bool ok, string what) { if (!ok) failures.Add(what); }

        foreach (var id in new uint[] { 31, 37, 42, 45, 41, 16462, 3549, 3550, 16465, 16463, 25753 })
            if (A(id) is { } a) Expect(a.CooldownGroup == 58, $"{id} 冷卻組應為 58，實際 {a.CooldownGroup}");
        if (A(52) is { } infuriate)
            Expect(infuriate is { Recast100ms: 600, CooldownGroup: 20, AdditionalCooldownGroup: 71, MaxCharges: 2 },
                $"52 應為 600/20/71/2，實際 {infuriate.Recast100ms}/{infuriate.CooldownGroup}/{infuriate.AdditionalCooldownGroup}/{infuriate.MaxCharges}");
        if (A(7386) is { } onslaught)
            Expect(onslaught is { Recast100ms: 300, CooldownGroup: 8, AdditionalCooldownGroup: 72, MaxCharges: 2 },
                $"7386 應為 300/8/72/2，實際 {onslaught.Recast100ms}/{onslaught.CooldownGroup}/{onslaught.AdditionalCooldownGroup}/{onslaught.MaxCharges}");
        if (A(7387) is { } upheaval && A(25752) is { } orogeny)
            Expect(upheaval.CooldownGroup == orogeny.CooldownGroup, $"7387 與 25752 應同冷卻組，實際 {upheaval.CooldownGroup}/{orogeny.CooldownGroup}");
        foreach (var id in new uint[] { 41, 16462, 3550, 16463, 25752 })
            if (A(id) is { } a) Expect(a.EffectRange == 5, $"{id} 效果範圍應為 5，實際 {a.EffectRange}");
        foreach (var id in new uint[] { 46, 7386, 25753 })
            if (A(id) is { } a) Expect(a.Range == 20, $"{id} 射程應為 20，實際 {a.Range}");

        var statuses = job.Statuses.ToDictionary(s => s.Id);
        foreach (var id in new uint[] { 1177, 1303 })
            Expect(statuses.TryGetValue(id, out var s) && s.DuplicateName, $"狀態 {id} 應列出並標為同名");
        foreach (var id in new uint[] { 1897, 2677, 2624, 1191 })
            Expect(statuses.ContainsKey(id), $"缺少狀態 {id}");
        foreach (var (from, to) in new (uint, uint)[] { (38, 7389), (49, 3549), (51, 3550), (3551, 25751) })
            Expect(job.Replacements.Any(r => r.From == from && r.To == to) || job.ManualChecks.Any(m => m.Contains($"{from} → {to}")),
                $"缺少升級 {from} → {to}");
        foreach (var id in new uint[] { 157, 267, 421, 505 })
            Expect(job.Traits.Any(t => t.Id == id), $"缺少特性 {id}");
        return failures;
    }
}
