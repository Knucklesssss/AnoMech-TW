using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Top.P6AlphaOmega;

internal static class P6MeteorChecks
{
    public static void Run()
    {
        var alive = Enumerable.Range(0, 8).Select(i => ((PartyRole)i, new Vector2(i - 4, i))).ToArray();
        foreach (var include in new bool?[] { true, false, null })
        for (ulong seed = 0; seed < 100; seed++)
        {
            SimRandom.Reseed(seed, 0);
            var plan = TopP6MeteorFlarePlan.Create(alive, include, new Rng());
            if (plan.MarkedRoles.Count != 3 || plan.UnmarkedRoles.Count != 5 ||
                plan.MarkedRoles.Intersect(plan.UnmarkedRoles).Any() ||
                plan.MarkedPositions.Values.Distinct().Count() != 3)
                throw new Exception("Meteor must partition eight players and assign three distinct edges.");
            if (include.HasValue && plan.IncludesD3 != include.Value)
                throw new Exception("Meteor must honor the D3 setting.");
            if (plan.IncludesD3 && plan.MarkedPositions[PartyRole.PhysRangedDps] != new Vector2(0, -19))
                throw new Exception("Marked D3 must take the north edge.");
            if (plan.StackPosition != new Vector2(0, plan.IncludesD3 ? 13.63f : -13.63f))
                throw new Exception("Meteor stack must use the opposite A/C waymark.");
            SimRandom.Reseed(seed, 0);
            var repeat = TopP6MeteorFlarePlan.Create(alive, include, new Rng());
            if (!plan.MarkedRoles.SequenceEqual(repeat.MarkedRoles))
                throw new Exception("Meteor assignment must use A's deterministic random source.");
        }
        for (var count = 0; count <= 2; count++)
        {
            var plan = TopP6MeteorFlarePlan.Create(alive.Take(count).ToArray(), false, new Rng());
            if (plan.MarkedRoles.Count != count || plan.MarkedPositions.Count != count)
                throw new Exception("Meteor must handle missing party members.");
        }
        SimRandom.Disable();
        Console.WriteLine("PASS: P6 meteor D3 options, distinct assignments, missing members and deterministic RNG.");
    }
}
