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
