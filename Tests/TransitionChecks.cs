using System;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Scenarios.Top.P3HelloWorld;

internal static class TransitionChecks
{
    public static void Run()
    {
        Check(TopP3TransitionRules.RingContains(new(0, 5.9f), 0), "first circle hits inside six");
        Check(!TopP3TransitionRules.RingContains(new(0, 6.1f), 0), "first circle excludes outside six");
        Check(TopP3TransitionRules.RingContains(new(0, 10), 1), "second ring hits ten");
        Check(!TopP3TransitionRules.RingContains(new(0, 13), 1), "second ring excludes thirteen");
        Check(TopP3TransitionRules.RingContains(new(0, 17), 2), "third ring hits seventeen");
        Check(TopP3TransitionRules.RingContains(new(0, 19), 3), "fourth ring hits outer arena");
        Check(!TopP3TransitionRules.RingContains(new(0, 17), 3), "fourth ring permits return inside eighteen");
        Check(TopP3TransitionRules.CenterContains(new(0, 5)), "centre hits within six");
        Check(!TopP3TransitionRules.CenterContains(new(0, 7)), "centre excludes outside six");

        foreach (var south in new[] { true, false })
        {
            var final = Enumerable.Range(0, 8).Select(i => (Vector2?)TopP3TransitionRules.PositionFor(i, south, TopP3TransitionStep.Resolve)).ToArray();
            Check(!TopP3TransitionRules.FailedCannonSlots(final).Any(x => x), $"valid two stacks and four spreads: south={south}");
            var overlap = (Vector2?[])final.Clone();
            overlap[4] = overlap[5];
            var failures = TopP3TransitionRules.FailedCannonSlots(overlap);
            Check(failures[4] && failures[5], "two spreads overlapping must both fail");
            var missing = (Vector2?[])final.Clone();
            missing[1] = new Vector2(0, 0);
            failures = TopP3TransitionRules.FailedCannonSlots(missing);
            Check(failures[0] && failures[1], "unshared high cannon and missing soaker fail");
            var dead = (Vector2?[])final.Clone();
            dead[1] = null;
            Check(TopP3TransitionRules.FailedCannonSlots(dead)[0], "dead partner cannot soak");
            var extra = (Vector2?[])final.Clone();
            extra[4] = extra[0];
            failures = TopP3TransitionRules.FailedCannonSlots(extra);
            Check(failures[0] && failures[1] && failures[4], "three-person stack is not valid");

            for (var slot = 0; slot < 8; slot++)
            {
                var outer = TopP3TransitionRules.PositionFor(slot, south, TopP3TransitionStep.Outer);
                var inner = TopP3TransitionRules.PositionFor(slot, south, TopP3TransitionStep.Inner);
                var target = final[slot]!.Value;
                Check(outer.Length() > 18.3f && outer.Length() < 19.7f, "wait outside third ring within death wall");
                Check(inner.Length() > 12.3f && inner.Length() < 17.7f, "first fourth ring and second second ring safe");
                Check(target.Length() > 6.3f && target.Length() < 17.7f, "final inside fourth ring outside puddle");
                Check(Vector2.Distance(outer, target) / 6f < 1.5f, "normal run reaches cannon snapshot from post-ring-three move");
                for (var arm = 0; arm < 3; arm++)
                {
                    Check(Vector2.Distance(outer, TopP3TransitionRules.ArmPosition(south, arm)) > 11.3f, "wait clear of first arm triad");
                    Check(Vector2.Distance(target, TopP3TransitionRules.ArmPosition(!south, arm)) > 11.3f, "resolve clear of second arm triad");
                }
                Check(Enumerable.Range(0, 3).Any(a => Vector2.Distance(target, TopP3TransitionRules.ArmPosition(south, a)) < 11), "move into a detonated first-arm circle");
            }
        }

        var roles = Enum.GetValues<PartyRole>();
        var assignment = TopP3TransitionRules.Assign(roles);
        Check(assignment.Distinct().Count() == 8, "every role assigned once");
        Check(assignment[0] == PartyRole.MainTank && assignment[2] == PartyRole.OffTank, "high cannons ordered by priority");
        Check(assignment[1] == PartyRole.RegenHealer && assignment[3] == PartyRole.ShieldHealer, "unmarked partners ordered by priority");
        Console.WriteLine("PASS: P3 transition geometry, cannon snapshots and assignments");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
