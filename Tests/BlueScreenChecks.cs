using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Scenarios.Top.P4BlueScreen;
using AnoMech.Core.SimObjects;

internal static class BlueScreenChecks
{
    public static void Run()
    {
        for (var a = 0; a < 8; a++)
        for (var b = a + 1; b < 8; b++)
        {
            var sides = TopP4BlueScreenRules.StackSides((PartyRole)a, (PartyRole)b);
            Check(sides.Count(west => west) == 4 && sides[a] != sides[b], "four per side, one marker per side");
            if (a % 2 != b % 2)
                Check(Enumerable.Range(0, 8).All(i => sides[i] == (i % 2 == 0)), "split markers do not swap");
            else
                Check(Enumerable.Range(0, 8).Count(i => sides[i] != (i % 2 == 0)) == 2, "same-side markers swap exactly two");
            var stack = Enumerable.Range(0, 8).Select(i => (Vector2?)TopP4BlueScreenRules.StackPosition(sides[i], 14.5f)).ToArray();
            Check(!TopP4BlueScreenRules.FailedStacks(stack, [a, b]).Any(x => x), "two valid line stacks");
            stack[Enumerable.Range(0, 8).First(i => i != a && i != b)] = new Vector2(0, -15);
            Check(TopP4BlueScreenRules.FailedStacks(stack, [a, b]).Any(x => x), "missing stack member is detected");
        }
        var example = TopP4BlueScreenRules.StackSides(PartyRole.MainTank, PartyRole.RegenHealer);
        Check(example[0] && !example[2] && example[5], "guide MT/H1: H1 swaps with D2");
        example = TopP4BlueScreenRules.StackSides(PartyRole.MeleeDpsB, PartyRole.CasterDps);
        Check(!example[7] && example[5] && !example[4], "guide D2/D4: D2 swaps with D1");
        var spread = Enumerable.Range(0, 8).Select(i => (Vector2?)TopP4BlueScreenRules.SpreadPosition((PartyRole)i)).ToArray();
        var directions = spread.Select(p => p!.Value).ToArray();
        Check(!TopP4BlueScreenRules.FailedSpreads(spread, directions).Any(x => x), "eight isolated protean lanes");
        var overlap = spread.ToArray();
        overlap[1] = overlap[0];
        Check(TopP4BlueScreenRules.FailedSpreads(overlap, overlap.Select(p => p!.Value).ToArray()).Take(2).All(x => x), "overlapping spreads kill both");
        Check(TopP4BlueScreenRules.LineContains(new(0, 15), new(0, 5)), "line stack is not a circle around its target");
        Check(!TopP4BlueScreenRules.LineContains(new(0, 15), new(0, -5)), "line does not hit behind caster");
        Check(!TopP4BlueScreenRules.LineContains(new(0, 15), new(3.1f, 10)), "line has 3y half-width");
        for (var ring = 0; ring < 4; ring++)
        {
            Check(TopP4BlueScreenRules.RingContains(new(0, ring * 6 + 3), ring), "ring hits its band");
            Check(!TopP4BlueScreenRules.RingContains(new(0, (ring + 1) * 6 + 0.1f), ring), "ring does not hit outer band");
        }
        Console.WriteLine("PASS: P4 all 28 marker pairs, guide swaps, line spreads/stacks and ring boundaries");
        for (var a = 0; a < 8; a++)
        for (var b = a + 1; b < 8; b++)
        foreach (var dt in new[] { 1f / 30, 1f / 60 })
        foreach (var moogle in new[] { false, true })
        {
            var world = new SimWorld();
            foreach (var member in world.Party.ActiveMembers())
            {
                member.Position = new(0, 0, 16);
                member.SimulateMovement = true;
            }
            var state = new TopP4BlueScreenState(world.Party, null);
            for (var round = 0; round < 3; round++)
                state.StackTargets[round] = [(PartyRole)((a + round * 2) % 8), (PartyRole)((b + round * 2) % 8)];
            var mechanics = new TopP4BlueScreenMechanics(world, state);
            mechanics.Run();
            Check(world.Enemies[0].Targetable, "P4 boss must be targetable when the phase starts");
            new TopP4BlueScreenAi(moogle).Run(state, world);
            while (world.Elapsed < 60f)
            {
                world.Tick(dt);
                Check(world.Party.ActiveMembers().All(m => m.Position.Length() < 20f), "AI remains inside arena");
            }
            var failures = string.Join("; ", Enumerable.Range(0, 8).Select(i => world.Party.Get(i)!.DeathCause).Where(x => x != null));
            Check(mechanics.Passed == true, $"P4 AI a={a}, b={b}, dt={dt}: {failures}");
            Check(world.Casts.Count(c => c.ActionId == 22393) == 6, "six real stack marker effects");
            Check(world.Casts.Count(c => c.ActionId == 31614) == 24, "24 immediate protean rays");
            Check(world.Casts.Count(c => c.ActionId == 31616) == 24, "24 snapshotted echo rays");
            Check(world.Casts.Count(c => c.ActionId == 31615) == 6, "six stack rays");
            Check(world.Casts.Where(c => c.ActionId == 31617).All(c => Math.Abs(c.CastSeconds - 4.7f) < .001f), "P4 opening cast follows recorded 4.7 seconds");
            Check(world.Casts.Any(c => c.ActionId == 31616 && Math.Abs(c.Time - 14.36f) < .05f), "Echo cast precedes first spread snapshot");
            Check(world.Enemies.Skip(1).All(e => e.Despawned), "all visual helpers cleaned up");
        }
        Console.WriteLine("PASS: P4 full 1x AI timeline, 28 rotating marker pairs at 30/60fps");
        CheckFailure(14.88f, 14.95f, world => world.Party.Get(1)!.Position = world.Party.Get(0)!.Position, "分散砲重疊");
        CheckFailure(19.60f, 19.72f, world => world.Party.Get(0)!.Position = Xz(TopP4BlueScreenRules.SpreadPosition(PartyRole.MainTank)), "原本的分散砲線");
        CheckFailure(19.78f, 19.9f, world => world.Party.Get(2)!.Position = new(0, 0, -15), "分攤不足四人");
        CheckFailure(19.78f, 19.9f, world => world.Party.Get(1)!.Position = world.Party.Get(0)!.Position, "分攤不足四人");
        CheckFailure(19.78f, 19.9f, world => world.Party.Get(1)!.Die("missing stack target"), "分攤不足四人");
        CheckFailure(22.28f, 22.4f, world => world.Party.Get(0)!.Position = new(0, 0, 3), "地靈脈第 1 圈");
        CheckFailure(43.88f, 44f, world => world.Party.Get(0)!.Position = new(0, 0, 15), "地靈脈第 3 圈");
        foreach (var force in new[] { true, false })
        for (var repeat = 0; repeat < 20; repeat++)
        {
            var world = new SimWorld();
            var state = new TopP4BlueScreenState(world.Party, force);
            Check(state.StackTargets.All(pair => pair.Distinct().Count() == 2 && pair.Contains(world.Party.PlayerRole) == force),
                "player stack-target override applies to all rounds without duplicate markers");
        }
        Console.WriteLine("PASS: P4 live overlap, old-lane echo, missing/overlapping/dead stacks, ring hits and target overrides");
    }

    private static void CheckFailure(float injectAt, float resolveAt, Action<SimWorld> inject, string expected)
    {
        var world = new SimWorld();
        var state = new TopP4BlueScreenState(world.Party, null);
        for (var round = 0; round < 3; round++) state.StackTargets[round] = [PartyRole.MainTank, PartyRole.OffTank];
        var mechanics = new TopP4BlueScreenMechanics(world, state);
        mechanics.Run();
        new TopP4BlueScreenAi().Run(state, world);
        world.Events.Add(injectAt, () => inject(world));
        while (world.Elapsed < resolveAt) world.Tick(0.01f);
        Check(Enumerable.Range(0, 8).Any(i => world.Party.Get(i)!.DeathCause?.Contains(expected) == true), $"live failure: {expected}");
        while (world.Elapsed < 60f) world.Tick(0.02f);
        Check(mechanics.Passed == false, "a failed run must not report success");
    }

    private static Vector3 Xz(Vector2 position) => new(position.X, 0, position.Y);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
