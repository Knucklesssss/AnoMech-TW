internal static class CombatInputChecks
{
    public static void Run()
    {
        var type = typeof(CombatInputChecks).Assembly.GetType("AnoMech.Core.Combat.CombatInputBuffer")
            ?? throw new Exception("Manual input buffer is missing.");
        var buffer = Activator.CreateInstance(type)!;
        bool Queue(uint id, ulong target, double wait) => (bool)type.GetMethod("Queue")!.Invoke(buffer, new object[] {id, target, wait})!;
        object? Take(bool ready) => type.GetMethod("Take")!.Invoke(buffer, new object[] {ready});
        void Advance(double time) => type.GetMethod("Advance")!.Invoke(buffer, new object[] {time});
        void Expect(object? entry, uint id, ulong target)
        {
            if (entry == null || (uint)entry.GetType().GetProperty("ActionId")!.GetValue(entry)! != id
                || (ulong)entry.GetType().GetProperty("TargetId")!.GetValue(entry)! != target)
                throw new Exception("Buffered action and target must match the latest manual input.");
        }
        if (!Queue(31, 100, .2) || Take(false) != null) throw new Exception("Buffer must wait for readiness.");
        Advance(.2);
        Expect(Take(true), 31, 100);
        if (Take(true) != null) throw new Exception("One input must execute only once.");
        Queue(31, 100, .2);
        Queue(37, 200, .3);
        Expect(Take(true), 37, 200);
        if (Queue(31, 100, .5001) || Queue(31, 100, 0)) throw new Exception("Only the short pre-ready window can queue.");
        Queue(31, 100, .5);
        Advance(.5);
        Expect(Take(true), 31, 100);
        Queue(31, 100, .5);
        Advance(.5001);
        if (Take(true) != null) throw new Exception("Expired manual input must never fire later.");
        Queue(31, 100, .2);
        type.GetMethod("Reset")!.Invoke(buffer, null);
        if (Take(true) != null) throw new Exception("Reset must discard pending input.");
        foreach (var invalid in new[] {double.NaN, double.PositiveInfinity, -1d})
        {
            Queue(31, 100, .2);
            try
            {
                Advance(invalid);
                throw new Exception("Invalid elapsed time must reject without discarding queued input.");
            }
            catch (System.Reflection.TargetInvocationException e) when (e.InnerException is ArgumentOutOfRangeException) { }
            Expect(Take(true), 31, 100);
        }
        Console.WriteLine("PASS: bounded manual input, target replacement, expiry and one-shot consumption.");
    }
}
