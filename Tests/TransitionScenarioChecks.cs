using System.Numerics;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Top.P3HelloWorld;
using static AnoMech.Scenarios.Top.TopConstants;

internal static class TransitionScenarioChecks
{
    public static void Run()
    {
        foreach (var south in new[] { true, false })
        foreach (var dt in new[] { 1f / 60, 1f / 30 })
            RunAiTrajectory(south, dt);
        CheckHazardWiring();
        CheckCenterWindow();
        CheckCannonSnapshot();
        Console.WriteLine("PASS: live transition schedule, 6y/s AI at 30/60 fps, center window and cannon snapshots");
    }

    private static void RunAiTrajectory(bool south, float dt)
    {
        var state = new TopP3HelloWorldState { TransitionFirstArmsSouth = south };
        var world = new SimWorld();
        var random = new Random(731);
        foreach (var member in world.Party.ActiveMembers())
        {
            var angle = (float)random.NextDouble() * MathF.Tau;
            var radius = (float)random.NextDouble() * 2.5f;
            member.Position = new(MathF.Sin(angle) * radius, 0, MathF.Cos(angle) * radius);
            member.SimulateMovement = true;
        }
        var transition = new TopP3Transition(world, state);
        new TopP3HelloWorldAi().Run(state, world);
        transition.Run();

        world.Events.Add(7f, () =>
        {
            for (var slot = 0; slot < 8; slot++)
            {
                var member = world.Party.Get(state.TransitionRoles[slot])!;
                Check(member.HasStatus(StatusId.HighPoweredSniperCannon) == (slot is 0 or 2), "high cannon role assignment");
                Check(member.HasStatus(StatusId.SniperCannon) == (slot >= 4), "spread cannon role assignment");
            }
        });
        world.Events.Add(9.60f, () => Check(world.Enemies.Count == 6 && world.Enemies.All(arm => !arm.Visible), "arms spawn hidden"));
        world.Events.Add(9.70f, () => Check(world.Enemies.Take(3).All(arm => arm.Visible) && world.Enemies.Skip(3).All(arm => !arm.Visible), "only first arm group appears"));
        world.Events.Add(12.80f, () => Check(world.Enemies.All(arm => arm.Visible), "second arm group appears"));
        world.Events.Add(25.97f, () => Check(world.Party.ActiveMembers().All(member =>
            !member.HasStatus(StatusId.HighPoweredSniperCannon) && !member.HasStatus(StatusId.SniperCannon)), "cannon statuses clear before damage"));
        world.Events.Add(26.10f, () => Check(world.Party.ActiveMembers().All(member => member.HasStatus(StatusId.MagicVulnerabilityUp)), "every cannon participant receives vulnerability"));

        Advance(world, transition, 31.6f, dt);
        var deaths = Enumerable.Range(0, 8).Select(i => world.Party.Get(i)!.DeathCause).Where(cause => cause != null);
        Check(!deaths.Any(), $"all AI survive south={south}, dt={dt}: {string.Join(", ", deaths)}");
        var armCasts = world.Casts.Where(cast => cast.ActionId == ActionId.ColossalBlow).ToArray();
        Check(armCasts.Length == 6, "both arm groups cast exactly three blasts");
        Check(armCasts.Count(cast => MathF.Abs(cast.Time - 22.05f) <= dt + 0.001f) == 3, "first arm cast time");
        Check(armCasts.Count(cast => MathF.Abs(cast.Time - 24.59f) <= dt + 0.001f) == 3, "second arm cast time");
        Check(armCasts.All(cast => MathF.Abs(cast.CastSeconds - 1.7f) < 0.001f), "arm cast duration");
        Check(world.Casts.Count(cast => cast.ActionId == ActionId.HighPoweredSniperCannon) == 2, "two high cannon effects");
        Check(world.Casts.Count(cast => cast.ActionId == ActionId.SniperCannon) == 4, "four spread cannon effects");
        Check(world.Enemies.Take(6).All(arm => !arm.Visible && arm.Despawned), "both arm groups hide and despawn");
        Check(world.Enemies.Skip(6).All(helper => helper.Despawned), "cannon helpers despawn");
    }

    private static void CheckHazardWiring()
    {
        (float time, float radius, int ring)[] rings =
        [(11.95f, 3f, 1), (14.05f, 9f, 2), (16.10f, 15f, 3), (18.16f, 21f, 4),
         (20.08f, 3f, 1), (22.23f, 9f, 2), (24.28f, 15f, 3), (26.33f, 21f, 4)];
        foreach (var (time, radius, ring) in rings)
        {
            var (world, transition, _) = EmptyArena();
            var target = world.Party.Get(0)!;
            world.Events.Add(time - 0.001f, () => target.Position = new(radius, 0, 0));
            // Event-only stepping isolates each damage snapshot from the center's per-frame hazard.
            world.Tick(time + 0.001f);
            Check(target.DeathCause == $"P3 轉場：速射式波動砲第 {ring} 圈", $"ring damage wired at {time}");
        }
        foreach (var south in new[] { true, false })
        foreach (var group in new[] { 0, 1 })
        {
            var (world, _, state) = EmptyArena();
            state.TransitionFirstArmsSouth = south;
            var time = group == 0 ? 24.01f : 26.56f;
            var center = TopP3TransitionRules.ArmPosition((group == 0) == south, 0);
            world.Events.Add(time - 0.001f, () => world.Party.Get(0)!.Position = new(center.X, 0, center.Y));
            world.Tick(time + 0.001f);
            Check(world.Party.Get(0)!.DeathCause == $"P3 轉場：第 {group + 1} 組手臂爆炸", "arm damage follows selected pattern");
        }
    }

    private static void CheckCenterWindow()
    {
        var (world, transition, _) = EmptyArena();
        world.Tick(12.45f);
        var before = world.Party.Get(0)!;
        before.Position = Vector3.Zero;
        transition.Tick();
        Check(before.IsAlive(), "center is safe before 12.46");
        world.Tick(0.02f);
        transition.Tick();
        Check(before.DeathCause == "P3 轉場：踏入中央危險區", "center activates at 12.46");

        world.Tick(28.46f - world.Elapsed);
        var lastActive = world.Party.Get(1)!;
        lastActive.Position = Vector3.Zero;
        transition.Tick();
        Check(!lastActive.IsAlive(), "center remains active immediately before 28.47");
        world.Tick(0.02f);
        var after = world.Party.Get(2)!;
        after.Position = Vector3.Zero;
        transition.Tick();
        Check(after.IsAlive(), "center stops applying damage after 28.47");
    }

    private static void CheckCannonSnapshot()
    {
        foreach (var wrongSoak in new[] { false, true })
        {
            var (world, _, state) = EmptyArena();
            world.Events.Add(26.01f, () =>
            {
                Vector2[] positions = [new(-30, -30), new(-30, -30), new(30, -30), new(30, -30),
                    new(-40, 0), new(-20, 30), new(20, 30), new(40, 0)];
                if (wrongSoak) positions[1] = new(0, 40);
                else positions[4] = positions[5];
                for (var slot = 0; slot < 8; slot++)
                    world.Party.Get(state.TransitionRoles[slot])!.Position = new(positions[slot].X, 0, positions[slot].Y);
            });
            world.Tick(26.03f);
            var failedSlots = Enumerable.Range(0, 8).Where(slot =>
                world.Party.Get(state.TransitionRoles[slot])!.DeathCause == "P3 轉場：狙擊砲重疊或分攤人數錯誤").ToArray();
            Check(failedSlots.SequenceEqual(wrongSoak ? [0, 1] : [4, 5]), "cannons resolve simultaneous live positions, including both overlapping spread victims");
        }
    }

    private static (SimWorld world, TopP3Transition transition, TopP3HelloWorldState state) EmptyArena()
    {
        var world = new SimWorld();
        var state = new TopP3HelloWorldState();
        Vector2[] positions = [new(-100, -100), new(-100, -100), new(100, -100), new(100, -100),
            new(-100, 100), new(-50, 100), new(50, 100), new(100, 100)];
        for (var slot = 0; slot < 8; slot++)
            world.Party.Get(state.TransitionRoles[slot])!.Position = new(positions[slot].X, 0, positions[slot].Y);
        var transition = new TopP3Transition(world, state);
        transition.Run();
        return (world, transition, state);
    }

    private static void Advance(SimWorld world, TopP3Transition transition, float end, float dt)
    {
        while (world.Elapsed < end)
        {
            world.Tick(MathF.Min(dt, end - world.Elapsed));
            transition.Tick();
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
