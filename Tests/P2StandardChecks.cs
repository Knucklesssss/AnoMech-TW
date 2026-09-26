using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Top;
using AnoMech.Scenarios.Top.P2PartySynergy;

internal static class P2StandardChecks
{
    private sealed class Probe : TopP2PartySynergyAi
    {
        public void Set(TopP2PartySynergyState value) => state = value;
        public void Adjust(IAiRoles roles) => AdjustForStacks(roles);
    }

    public static void Run()
    {
        foreach (var glitch in new[] { GlitchType.Mid, GlitchType.Far })
        for (var north = 0; north < 6; north++)
        for (var south = north + 2; south < 8; south += 2)
        {
            var world = new SimWorld();
            var state = new TopP2PartySynergyState(world.Party, new() { Glitch = glitch });
            var remaining = new Queue<PartyRole>(Enum.GetValues<PartyRole>().Except(state.Stacks.List));
            var order = Enumerable.Range(0, 8).Select(i => i == north ? state.Stacks[0]
                : i == south ? state.Stacks[1] : remaining.Dequeue()).ToArray();
            var ai = new Probe();
            ai.Set(state);
            var move = AiMove.Create(Enumerable.Range(0, 8).Select(i => (Vector2?)new Vector2(i, 0)).ToArray())
                .Assignments(order).ApplySwaps(ai.Adjust);
            if (move[(int)state.Stacks[0]]!.Value.X != north || move[(int)state.Stacks[1]]!.Value.X == south)
                throw new Exception("Standard same-side stacks must swap the southern target only.");
        }
        Console.WriteLine("PASS: P2 Standard same-side swaps for both glitches and all row pairs.");

        foreach (var strat in new TopP2PartySynergyAi[] { new(), new TopP2PartySynergyTuuuflessAi() })
        foreach (var glitch in new[] { GlitchType.Mid, GlitchType.Far })
        for (var first = 0; first < 8; first++)
        for (var second = first + 1; second < 8; second++)
        {
            var world = new SimWorld();
            var state = new TopP2PartySynergyState(world.Party, new() { Glitch = glitch });
            state.Stacks.List[0] = state.Order[first];
            state.Stacks.List[1] = state.Order[second];
            strat.Run(state, world);
            var stack = MoogleChecks.Move(strat, "StackPositions");
            for (var pair = 0; pair < 4; pair++)
            {
                var distance = Vector2.Distance(stack[(int)state.Order[pair * 2]]!.Value, stack[(int)state.Order[pair * 2 + 1]]!.Value);
                if (glitch == GlitchType.Far ? distance - .6f <= 34f : distance - .6f <= 21f || distance + .6f >= 26f)
                    throw new Exception($"{strat.Name} {glitch} stack pair {pair} stands {distance:F2}y apart; movement jitter can break the tether range.");
            }
        }
        Console.WriteLine("PASS: P2 Standard and tuuufless stack pairs keep the glitch tether range under movement jitter.");
    }
}
