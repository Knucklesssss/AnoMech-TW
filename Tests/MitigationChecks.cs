using AnoMech.Core.Combat;
internal static class MitigationChecks
{
    public static void Run()
    {
        var boss = new object(); var other = new object();
        TargetMitigation.Clear();
        TargetMitigation.Apply(boss, 7560);
        Check(Math.Abs(TargetMitigation.Reduction(boss, true) - .1f) < .0001f &&
            Math.Abs(TargetMitigation.Reduction(boss, false) - .05f) < .0001f, "Addle damage types");
        TargetMitigation.Apply(other, 7549);
        Check(Math.Abs(TargetMitigation.Reduction(other, false) - .1f) < .0001f &&
            Math.Abs(TargetMitigation.Reduction(other, true) - .05f) < .0001f, "Feint damage types");
        TargetMitigation.Clear();
        TargetMitigation.Apply(boss, 7560); TargetMitigation.Apply(boss, 7549);
        Check(Math.Abs(TargetMitigation.Reduction(boss, true) - .145f) < .0001f, "Multiplicative magic mitigation");
        Check(TargetMitigation.Reduction(other, true) == 0, "Wrong target cannot reduce damage");
        TargetMitigation.Apply(boss, 7560);
        Check(TargetMitigation.Active(boss).Length == 2, "Same effect refreshes without stacking");
        TargetMitigation.Tick(14.9f);
        TargetMitigation.Record(boss, "命中", true);
        Check(TargetMitigation.Reports[^1].Result.Contains("有覆蓋"), "Snapshot sees remaining duration");
        TargetMitigation.Tick(.2f);
        Check(TargetMitigation.Active(boss).Length == 0, "Expired effects cannot cover");
        TargetMitigation.Record(null, "隱藏來源", true);
        Check(TargetMitigation.Reports[^1].Result.Contains("未判定"), "Unknown source is not a miss");
        TargetMitigation.Record(boss, "未知類型", null);
        Check(TargetMitigation.Reports[^1].Result.Contains("未判定"), "Unknown damage type is not a miss");
        TargetMitigation.Apply(boss, 7535);
        Check(Math.Abs(TargetMitigation.Reduction(boss, false) - .1f) < .0001f, "Reprisal covers physical");
        TargetMitigation.Clear();
        TargetMitigation.Apply(boss, 2887); TargetMitigation.Tick(10);
        Check(TargetMitigation.Reduction(boss, false) == 0, "Dismantle expires at ten seconds");
        TargetMitigation.Clear();
        Check(TargetMitigation.Reports.Count == 0 && TargetMitigation.Clock == 0, "Reset clears run state");
        Console.WriteLine("PASS: target mitigation multiplication, target isolation, refresh, expiry, snapshots and reset.");
    }
    private static void Check(bool pass, string message) { if (!pass) throw new Exception(message); }
}
