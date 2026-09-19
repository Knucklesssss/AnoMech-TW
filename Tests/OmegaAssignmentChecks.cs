using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Top;
using AnoMech.Scenarios.Top.P5Omega;

internal static class OmegaAssignmentChecks
{
    private sealed class Probe : TopP5OmegaAi
    {
        public RoleList Second(SimParty party) => solveHelloWorld2(party);
    }

    public static void Run()
    {
        foreach (var strategyIndex in new[] { 1, 2 })
        for (var seed = 0; seed < 100; seed++)
        foreach (var extra in new bool?[] { null, false, true })
        foreach (var orderOption in Enum.GetValues<HelloWorldOrderOption>())
        foreach (var typeOption in Enum.GetValues<HelloWorldTypeOption>())
        {
            var world = new SimWorld();
            AnoMech.Core.Game.SimRandom.Reseed((ulong)seed, 0);
            var state = new TopP5OmegaState(world.Party, new() { ExtraDynamis = extra, HelloWorldOrder = orderOption, HelloWorldType = typeOption }, strategyIndex);
            if (!state.HelloWorldTargets.List.Skip(2).Any(state.DoubleDynamicTargets.Contains) ||
                state.DoubleDynamicTargets.List.Distinct().Count() != 4 ||
                (extra is { } expected && state.DoubleDynamicTargets.Contains(world.Party.PlayerRole) != expected))
                throw new Exception($"Omega strategy {strategyIndex}, seed {seed}: missing two-stack second target or invalid player override.");
        }
        Console.WriteLine("PASS: Moogle and second-target Omega require a two-stack second target across 9000 seeds/settings combinations.");
        AnoMech.Core.Game.SimRandom.Disable();
        for (var mask = 0; mask < 256; mask++)
        {
            var world = new SimWorld();
            var state = new TopP5OmegaState(world.Party, new());
            foreach (var role in Enum.GetValues<PartyRole>())
                if ((mask & (1 << (int)role)) != 0)
                    world.Party.Get(role)!.AddStatus(TopConstants.StatusId.QuickeningDynamis, stacks: 3);
            var ai = new Probe();
            ai.Run(state, world);
            var order = ai.Second(world.Party).List;
            if (order.Length != 8 || order.Distinct().Count() != 8 ||
                order[0] != state.HelloWorldTargets[2] || order[1] != state.HelloWorldTargets[3])
                throw new Exception($"Invalid Omega assignment for status mask {mask}.");
        }
        Console.WriteLine("PASS: Omega assignments with every missing/excess dynamis combination.");
    }
}
