using System.Numerics;
using System.Reflection;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Top.P3HelloWorld;
using static AnoMech.Scenarios.Top.TopConstants;

public static class HelloWorldChecks
{
    public static void Run()
    {
        CheckNearestTowerAssignment();
        int[] defamationSlots = [1, 3, 0, 2];
        int[] stackSlots = [0, 2, 1, 3];
        int[] nearSlots = [3, 0, 2, 1];
        int[] farSlots = [2, 1, 3, 0];
        for (var round = 0; round < 4; round++)
        {
            var state = new TopP3HelloWorldState();
            var world = new SimWorld();
            var ai = new TopP3HelloWorldAi();
            ai.Run(state, world);

            IAiMove Evaluate(string method) => (IAiMove)typeof(TopP3HelloWorldAi)
                .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(ai, [round])!;
            Vector2 At(IAiMove move, int slot, int member) => move[(int)state.At(slot, member)]!.Value;
            void Place(IAiMove move)
            {
                for (var slot = 0; slot < 4; slot++)
                for (var member = 0; member < 2; member++)
                {
                    var point = At(move, slot, member);
                    world.Party.Get(state.At(slot, member))!.Position = new Vector3(point.X, 0, point.Y);
                }
            }
            void Set(int slot, ushort status, bool present = true, int? onlyMember = null)
            {
                for (var member = 0; member < 2; member++)
                {
                    if (onlyMember is { } index && member != index) continue;
                    var statuses = world.Party.Get(state.At(slot, member))!.Statuses;
                    if (present) statuses.Add(status); else statuses.Remove(status);
                }
            }

            var def = defamationSlots[round];
            var stack = stackSlots[round];
            var near = nearSlots[round];
            var far = farSlots[round];
            var waiting = Evaluate("TakeTowers");
            Place(waiting);
            var before = Evaluate("ReachForThePass");
            Check(Vector2.Distance(At(before, near, 0), At(waiting, near, 0)) < 0.01f,
                $"round {round + 1}: near must hold before towers resolve");

            Set(def, StatusId.LatentDefectUnderflow);
            var wrongColor = Evaluate("ReachForThePass");
            Check(Vector2.Distance(At(wrongColor, def, 0), At(waiting, def, 0)) < 0.01f,
                $"round {round + 1}: wrong tower status cannot release defamation holders");
            Set(def, StatusId.LatentDefectUnderflow, false);

            Set(stack, StatusId.LatentDefectUnderflow);
            var stackResolved = Evaluate("ReachForThePass");
            if (round < 3)
                Check(Vector2.Distance(At(stackResolved, far, 0), At(waiting, stack, 0)) < 0.01f ||
                      Vector2.Distance(At(stackResolved, far, 0), At(waiting, stack, 1)) < 0.01f,
                    $"round {round + 1}: far must collect poison on tower status appearance, while it is still present");
            Check(Vector2.Distance(At(stackResolved, def, 0), At(waiting, def, 0)) < 0.01f,
                $"round {round + 1}: stack resolution cannot release defamation holders");

            Set(def, StatusId.LatentDefectPerformance, onlyMember: 0);
            var oneTower = Evaluate("ReachForThePass");
            Check(Vector2.Distance(At(oneTower, def, 0), At(waiting, def, 0)) < 0.01f,
                $"round {round + 1}: wait until both corresponding towers resolve");
            Set(def, StatusId.LatentDefectPerformance);
            var resolved = Evaluate("ReachForThePass");
            Check(MathF.Abs(At(resolved, def, 0).Length() - 13.63f) < 0.01f,
                $"round {round + 1}: resolved defamation holder must move to waymark while tower status is present");
            Place(resolved);

            if (round < 3)
            {
                Set(near, StatusId.CriticalErrorPerformance, onlyMember: 0);
                var firstPickup = Evaluate("ReachForThePass");
                Check(Vector2.Distance(At(firstPickup, near, 0), At(firstPickup, near, 1)) > 10f,
                    $"round {round + 1}: near cannot connect until both members have poison");
                Set(near, StatusId.CriticalErrorPerformance);
                Set(far, StatusId.CriticalErrorUnderflow);
                var escaped = Evaluate("ReachForThePass");
                Check(Vector2.Distance(At(escaped, near, 0), At(escaped, near, 1)) < 10f,
                    $"round {round + 1}: near must converge after both poison pickups");
                CheckSafeFromHolders(escaped);
            }
            else
            {
                Check(Vector2.Distance(At(waiting, near, 0), At(waiting, near, 1)) < 10f,
                    "round 4: near must start together beside stack towers");
                Check(Vector2.Distance(At(resolved, far, 0), At(resolved, far, 1)) > 10f,
                    "round 4: far must separate without visiting poison holders");
                CheckSafeFromHolders(resolved);
            }

            void CheckSafeFromHolders(IAiMove move)
            {
                foreach (var receiver in new[] { near, far })
                foreach (var holder in new[] { def, stack })
                for (var member = 0; member < 2; member++)
                for (var source = 0; source < 2; source++)
                    Check(Vector2.Distance(At(move, receiver, member), At(move, holder, source)) > 6f,
                        $"round {round + 1}: tether receiver must escape the poison holder's explosion");
            }

            Set(def, StatusId.LatentDefectPerformance, false);
            Set(stack, StatusId.LatentDefectUnderflow, false);
            var after = Evaluate("ReachForThePass");
            Check(MathF.Abs(At(after, def, 0).Length() - 13.63f) < 0.01f,
                $"round {round + 1}: tower resolution remains latched after status removal");

            ai.Run(state, new SimWorld());
            var restarted = Evaluate("ReachForThePass");
            Check(Vector2.Distance(At(restarted, def, 0), At(waiting, def, 0)) < 0.01f,
                $"round {round + 1}: restarting must clear the previous run's tower latch");
        }
        Console.WriteLine("PASS: all four Hello World handoffs, poison escape and restart");
    }

    private static void Check(bool condition, string failure)
    {
        if (!condition) throw new InvalidOperationException(failure);
    }

    private static void CheckNearestTowerAssignment()
    {
        var state = new TopP3HelloWorldState();
        var world = new SimWorld();
        var ai = new TopP3HelloWorldAi();
        ai.Run(state, world);
        world.Party.Get(state.At(1, 0))!.Position = new(0, 0, -17);
        world.Party.Get(state.At(1, 1))!.Position = new(17, 0, 0);
        var move = (IAiMove)typeof(TopP3HelloWorldAi).GetMethod("TakeTowers", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(ai, [0])!;
        Check(Vector2.Distance(move[(int)state.At(1, 0)]!.Value, new(0, -17)) < 0.01f,
            "holder already at north tower must not cross the arena to east");
        Check(Vector2.Distance(move[(int)state.At(1, 1)]!.Value, new(17, 0)) < 0.01f,
            "nearest tower assignments must remain one-to-one");
    }
}
