internal static class CombatRecastChecks
{
    public static void Run()
    {
        var type = typeof(CombatRecastChecks).Assembly.GetType("AnoMech.Core.Combat.CombatRecastView")
            ?? throw new Exception("Native recast projection/restoration is missing.");
        object Call(string name, params object[] args) => type.GetMethod(name)!.Invoke(null, args)!;
        void Expect(object state, bool active, double elapsed, double total)
        {
            if ((bool)type.GetProperty("IsActive")!.GetValue(state)! != active
                || Math.Abs((double)type.GetProperty("Elapsed")!.GetValue(state)! - elapsed) > 1e-6
                || Math.Abs((double)type.GetProperty("Total")!.GetValue(state)! - total) > 1e-6)
                throw new Exception("Incorrect native recast charge/timer presentation.");
        }
        Expect(Call("Restore", true, 10d, 60d, 20d), true, 30, 60);
        Expect(Call("Restore", true, 10d, 60d, 60d), false, 60, 60);
        Expect(Call("Project", 30d, 3, 2, 20d), true, 70, 90);
        Expect(Call("Project", 30d, 3, 3, 0d), false, 90, 90);
        foreach (var invalid in new[] { -1d, double.NaN, double.PositiveInfinity, double.MaxValue })
        {
            try { Call("Project", invalid, 3, 2, 20d); throw new Exception("Invalid native recast accepted."); }
            catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is ArgumentOutOfRangeException) { }
        }
        Console.WriteLine("PASS: native recast charge projection, elapsed restoration and invalid input rejection.");
    }
}
