using System.Numerics;
using AnoMech.Scenarios.Top.P6AlphaOmega;

internal static class P6LimitBreakGeometryChecks
{
    public static void Run()
    {
        if (!TopP6LimitBreakRules.IsTankAction(199) || TopP6LimitBreakRules.IsTankAction(208) ||
            !TopP6LimitBreakRules.IsHealerAction(24859) || TopP6LimitBreakRules.IsHealerAction(17105))
            throw new Exception("Role overrides must not turn healer LB into tank mitigation or tank LB into cleanse.");
        if (!TopP6LimitBreakRules.Hits(new(6.5f, 11.26f), Vector2.Zero, 0, 15, 0, true) ||
            TopP6LimitBreakRules.Hits(new(16, 0), Vector2.Zero, 0, 15, 0, true))
            throw new Exception("Caster LB must use its ground circle, including the six comet positions.");
        foreach (var z in new[] { -10f, 10f })
            if (!TopP6LimitBreakRules.Hits(new(0, z), new(0, -19), 0, 30, 4, false))
                throw new Exception("Ranged LB aimed north-to-south must hit both meteors.");
        if (TopP6LimitBreakRules.Hits(new(5, 0), new(0, -19), 0, 30, 4, false) ||
            TopP6LimitBreakRules.Hits(new(0, -20), new(0, -19), 0, 30, 4, false))
            throw new Exception("Ranged LB must reject targets outside its width or behind the caster.");
        if (!TopP6LimitBreakRules.IsTankLbActive(10f, 17.99f) ||
            TopP6LimitBreakRules.IsTankLbActive(10f, 18f) ||
            TopP6LimitBreakRules.IsTankLbActive(float.NegativeInfinity, 1f))
            throw new Exception("Tank LB covers eight seconds after completion, not merely a button press.");
        Console.WriteLine("PASS: P6 completed LB windows and caster/ranged LB geometry.");
    }
}
