using System.Numerics;
using System.Reflection;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Top.P3HelloWorld;
using static AnoMech.Scenarios.Top.TopConstants;

public static class HelloWorldMechanicsChecks
{
    public static void Run()
    {
        var type = typeof(TopP3HelloWorldAi).Assembly.GetType("AnoMech.Scenarios.Top.P3HelloWorld.TopP3HelloWorldMechanics");
        Check(type != null, "Hello World needs a live contact/tether resolver instead of recorded status assignments");
        var state = new TopP3HelloWorldState();
        var world = new SimWorld();
        for (var i = 0; i < 8; i++) world.Party.Get(i)!.Position = new(i * 30, 0, 0);
        dynamic mechanics = Activator.CreateInstance(type!, world, state)!;
        type!.GetMethod("ApplyInitialPoison", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(mechanics, null);
        var red = world.Party.Get(state.At(0, 0))!;
        var blue = world.Party.Get(state.At(1, 0))!;
        var wrongRecipient = world.Party.Get(state.At(3, 0))!;
        var intendedRecipient = world.Party.Get(state.At(2, 0))!;
        wrongRecipient.Position = red.Position + new Vector3(1, 0, 0);
        mechanics.Tick(0.02f);
        Check(wrongRecipient.HasStatus(StatusId.CriticalErrorUnderflow), "contact must infect the actual recipient, even the wrong role");
        Check(!intendedRecipient.HasStatus(StatusId.CriticalErrorUnderflow), "distant scripted recipient must not get free poison");
        wrongRecipient.Position = blue.Position + new Vector3(1, 0, 0);
        mechanics.Tick(0.02f);
        Check(wrongRecipient.HasStatus(StatusId.CriticalErrorPerformance) && !wrongRecipient.HasStatus(StatusId.CriticalErrorUnderflow),
            "opposite poison contact must overwrite the previous color");
        wrongRecipient.RemoveStatus(StatusId.CriticalErrorUnderflow);
        wrongRecipient.AddStatus(StatusId.RepairedDefectUnderflow, 0);
        wrongRecipient.Position = red.Position + new Vector3(1, 0, 0);
        mechanics.Tick(0.02f);
        Check(!wrongRecipient.HasStatus(StatusId.CriticalErrorUnderflow), "debugger must block reinfection");

        // Start an independent run: one valid break is safe; a second simultaneous break is lethal.
        world = new SimWorld();
        mechanics = Activator.CreateInstance(type, world, state)!;
        var a = world.Party.Get(0)!;
        var b = world.Party.Get(1)!;
        a.Position = new(-6, 0, 0); b.Position = new(6, 0, 0);
        mechanics.Tether(a, b, TetherId.HWPrepLocal);
        var prep = world.Tethers.Single();
        mechanics.Tether(a, b, TetherId.HWLocal, 10f, StatusId.HWLocalTether);
        Check(!prep.IsActive, "active tether must explicitly clear its preparation effect");
        var active = world.Tethers.Last();
        mechanics.Tick(0.02f);
        Check(active.IsActive, "local tether stays while partners are far apart");
        b.Position = new(0, 0, 0);
        mechanics.Tick(0.02f);
        Check(!active.IsActive && active.Resolved, "distance break must despawn the actual tether exactly once");
        Check(!a.HasStatus(StatusId.HWLocalTether) && !b.HasStatus(StatusId.HWLocalTether), "broken tether must remove both debuffs");
        Check(a.IsAlive(), "one tether pair breaking must not kill the party");
        mechanics.Tick(0.02f);
        Check(a.IsAlive(), "resolved tether must not hit again on later frames");
        mechanics.Tether(world.Party.Get(2), world.Party.Get(3), TetherId.HWRemote, 10f, StatusId.HWRemoteTether);
        world.Party.Get(3)!.Position = new(15, 0, 0);
        mechanics.Tick(0.02f);
        Check(!a.IsAlive(), "overlapping tether breaks must cause a real failure");

        world = new SimWorld();
        mechanics = Activator.CreateInstance(type, world, state)!;
        mechanics.Tether(world.Party.Get(0), world.Party.Get(1), TetherId.HWRemote, 10f, StatusId.HWRemoteTether);
        mechanics.Tick(10.1f);
        Check(!world.Party.Get(0)!.IsAlive() && !world.Tethers.Single().IsActive, "unbroken tether timeout must wipe and clear the effect");
        CheckPoisonExpiryAndTowers(type, state);
        CheckAiTrajectory();
        Console.WriteLine("PASS: live poison contact, color overwrite, immunity, tether cleanup, overlap and timeout");
    }

    private static void Check(bool condition, string failure)
    {
        if (!condition) throw new InvalidOperationException(failure);
    }

    private static void CheckPoisonExpiryAndTowers(Type type, TopP3HelloWorldState state)
    {
        var world = new SimWorld();
        for (var i = 0; i < 8; i++) world.Party.Get(i)!.Position = new(i * 30, 0, 100);
        dynamic mechanics = Activator.CreateInstance(type, world, state)!;
        type.GetMethod("ApplyInitialPoison", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(mechanics, null);
        var resolve = type.GetMethod("ResolveTowers", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(resolve != null, "tower statuses must go to actual soakers, not recorded party slots");
        var red = world.Party.Get(state.At(0, 0))!;
        var otherRed = world.Party.Get(state.At(0, 1))!;
        var blue = world.Party.Get(state.At(1, 0))!;
        var otherBlue = world.Party.Get(state.At(1, 1))!;
        red.Position = new(0, 0, 14); otherRed.Position = new(-14, 0, 0);
        blue.Position = new(14, 0, 0); otherBlue.Position = new(0, 0, -14);
        mechanics.Tick(21f);
        resolve!.Invoke((object)mechanics, new object[] { 0 });
        Check(red.HasStatus(StatusId.LatentDefectUnderflow) && blue.HasStatus(StatusId.LatentDefectPerformance), "actual tower occupants receive matching latent defects");
        var recipient = world.Party.Get(state.At(2, 0))!;
        recipient.Position = red.Position + new Vector3(1, 0, 0);
        mechanics.Tick(0.01f);
        mechanics.Tick(1f);
        recipient.Position = new(50, 0, 50);
        mechanics.Tick(5.01f);
        Check(!red.HasStatus(StatusId.CriticalErrorUnderflow) && red.HasStatus(StatusId.RepairedDefectUnderflow), "same-color contact must not refresh original poison timer");
        Check(!red.HasStatus(StatusId.LatentDefectUnderflow), "matching poison expiry cleanses tower status");
        Check(recipient.HasStatus(StatusId.CriticalErrorUnderflow), "new recipient has its own 27 second timer");
        mechanics.Tick(21f);
        Check(!recipient.HasStatus(StatusId.CriticalErrorUnderflow) && recipient.HasStatus(StatusId.RepairedDefectUnderflow), "transferred poison expires from actual pickup time");

        world = new SimWorld();
        mechanics = Activator.CreateInstance(type, world, state)!;
        resolve.Invoke((object)mechanics, new object[] { 0 });
        Check(!world.Party.Get(0)!.IsAlive(), "unsoaked tower must cause a real failure");

        world = new SimWorld();
        for (var i = 0; i < 8; i++) world.Party.Get(i)!.Position = new(i * 30, 0, 100);
        mechanics = Activator.CreateInstance(type, world, state)!;
        type.GetMethod("ApplyInitialPoison", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(mechanics, null);
        world.Party.Get(state.At(1, 0))!.Position = new(0, 0, 14);
        world.Party.Get(state.At(0, 1))!.Position = new(-14, 0, 0);
        world.Party.Get(state.At(0, 0))!.Position = new(14, 0, 0);
        world.Party.Get(state.At(1, 1))!.Position = new(0, 0, -14);
        mechanics.Tick(21f);
        resolve.Invoke((object)mechanics, new object[] { 0 });
        mechanics.Tick(6.1f);
        Check(world.Party.Get(state.At(1, 0))!.HasStatus(StatusId.LatentDefectUnderflow), "blue poison expiry must not cleanse a red tower");
        mechanics.Tick(4f);
        Check(!world.Party.Get(0)!.IsAlive(), "wrong-color tower defect must still wipe at its deadline");
    }

    private static void CheckAiTrajectory()
    {
        foreach (var fps in new[] { 30, 60 })
        for (var layout = 0; layout < 8; layout++)
        {
            var world = new SimWorld();
            var state = new TopP3HelloWorldState
            {
                TransitionRoleOverride = Enumerable.Range(0, 8).Select(i => (PartyRole)((i + layout) % 8)).ToArray()
            };
            foreach (var player in world.Party.ActiveMembers()) player.SimulateMovement = true;
            var ai = new TopP3HelloWorldAi();
            ai.Run(state, world);
            var mechanics = new TopP3HelloWorldMechanics(world, state);
            mechanics.Run();
            var lastPoison = Enumerable.Repeat("", 8).ToArray();
            var history = new List<string>();
            for (var frame = 0; frame < 139 * fps; frame++)
            {
                world.Tick(1f / fps);
                mechanics.Tick(1f / fps);
                for (var i = 0; i < 8; i++)
                {
                    var player = world.Party.Get(i)!;
                    var color = (player.HasStatus(StatusId.CriticalErrorUnderflow) ? "R" : "") + (player.HasStatus(StatusId.CriticalErrorPerformance) ? "B" : "");
                    if (color != lastPoison[i]) history.Add($"{world.Elapsed:F2} role {i}: {color} at {player.Position}");
                    lastPoison[i] = color;
                    Check(player.IsAlive(), $"live HW AI at {fps} fps, layout {layout}, t={world.Elapsed:F2}, role {i}: {player.DeathCause}\n{string.Join('\n', history)}\n" +
                        string.Join('\n', Enumerable.Range(0, 8).Select(j => $"role {j} {world.Party.Get(j)!.Position} {world.Party.Get(j)!.DeathCause}")));
                }
            }
            for (var i = 0; i < 8; i++)
                Check(world.Party.Get(i)!.HasStatus(StatusId.RepairedDefectUnderflow) && world.Party.Get(i)!.HasStatus(StatusId.RepairedDefectPerformance),
                    $"live HW AI at {fps} fps must actually receive and resolve both poison colors, role {i}");
            Check(world.Tethers.All(tether => !tether.IsActive), "all four rounds' tether effects must be gone at the end");
        }
        Console.WriteLine("PASS: four live HW rounds at 6y/s, 30/60 fps, eight starting layouts, both poison colors and all tether effects resolved");
    }
}
