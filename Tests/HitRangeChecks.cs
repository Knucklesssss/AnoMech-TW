using System.Numerics;
using AnoMech.Core.Game;

internal static class HitRangeChecks
{
    public static void Run()
    {
        HitRangeDebug.Clear();
        HitRangeDebug.Enabled = false;
        HitRangeDebug.Record(HitRangeShape.Circle, Vector3.Zero, 0, 5, 0, true);
        if (HitRangeDebug.Snapshot().Count != 0) throw new Exception("Disabled overlay recorded a hit.");
        HitRangeDebug.Enabled = true;
        for (var i = 0; i < 100; i++)
            HitRangeDebug.Record(HitRangeShape.Circle, new(i, 0, 0), 0, 5, 0, true);
        var snapshot = HitRangeDebug.Snapshot();
        if (snapshot.Count != 64 || snapshot[0].Origin.X != 36) throw new Exception("Hit history must be bounded.");
        HitRangeDebug.Tick(HitRangeDebug.DisplaySeconds + .01f);
        if (HitRangeDebug.Snapshot().Count != 0) throw new Exception("Resolved hits must expire.");
        HitRangeDebug.Enabled = false;
        Console.WriteLine("PASS: optional hit-range history, capacity and expiry.");
    }
}
