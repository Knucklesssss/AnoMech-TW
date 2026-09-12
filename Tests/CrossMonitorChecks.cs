using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Scenarios.Top.P3Monitors;

internal static class CrossMonitorChecks
{
    public static void Run()
    {
        var type = typeof(TopP3MonitorRules).Assembly.GetType("AnoMech.Scenarios.Top.P3Monitors.TopP3MonitorsCrossRules");
        Check(type != null, "Independent cross monitor strategy rules are missing.");
        Vector3 Initial(PartyRole role) => (Vector3)type!.GetMethod("InitialPositionFor")!.Invoke(null, [role])!;
        IReadOnlyList<TopP3MonitorMove> Plan(TopP3MonitorAssignment assignment, TopP3BossSide side,
            IReadOnlyDictionary<PartyRole, TopP3MonitorStatus> statuses, PartyRole local)
            => (IReadOnlyList<TopP3MonitorMove>)type!.GetMethod("PlanMoves")!.Invoke(null, [assignment, side, statuses, local])!;
        PartyRole[] roles = [PartyRole.RegenHealer, PartyRole.MainTank, PartyRole.OffTank, PartyRole.ShieldHealer,
            PartyRole.PhysRangedDps, PartyRole.MeleeDpsA, PartyRole.MeleeDpsB, PartyRole.CasterDps];
        Vector3[] initial = [new(0,0,-19), new(0,0,-7), new(7,0,0), new(19,0,0),
            new(0,0,19), new(0,0,7), new(-7,0,0), new(-19,0,0)];
        Check(roles.Select(Initial).SequenceEqual(initial), "Cross starts TN north/east, DPS south/west, with the guide's inner/outer roles.");
        foreach (var local in roles)
        {
            var preparation = (IReadOnlyList<TopP3MonitorMove>)type!.GetMethod("PlanInitialMoves")!.Invoke(null, [local])!;
            Check(preparation.Count == 7 && preparation.All(m => m.Role != local && m.Target == Initial(m.Role)),
                "Cross preparation excludes the real player and retains each NPC's assigned cross position.");
        }
        for (var a = 0; a < 8; a++)
        for (var b = a + 1; b < 8; b++)
        for (var c = b + 1; c < 8; c++)
        foreach (var side in Enum.GetValues<TopP3BossSide>())
        for (var bits = 0; bits < 8; bits++)
        {
            var assignment = TopP3MonitorRules.Assign([roles[a], roles[b], roles[c]]);
            var statuses = assignment.MonitorRoles.Select((r, i) => (r, s: (TopP3MonitorStatus)((bits >> i) & 1))).ToDictionary(x => x.r, x => x.s);
            var moves = Plan(assignment, side, statuses, (PartyRole)(-1)).ToDictionary(m => m.Role);
            var points = roles.Select(r => new TopP3MonitorPosition(r, moves[r].Target)).ToArray();
            var horizontal = assignment.MonitorRoles.Where(r => MathF.Abs(moves[r].Target.X) >= 7).OrderBy(r => moves[r].Target.X).ToArray();
            Check(points.All(p => TopP3MonitorRules.IsInsideArena(p.Position)), "Cross stays inside arena.");
            foreach (var role in roles)
            {
                var move = moves[role];
                Check(Vector3.Distance(Initial(role), move.Target) < TopP3MonitorRules.MoveSpeed * (TopP3MonitorRules.CircleAt - TopP3MonitorRules.MoveStartAt), "Cross swaps arrive before snapshot at 1x.");
                if (MathF.Abs(move.Target.X) >= 7)
                    Check(move.Target.Z == (assignment.IsMonitor(role) ? role == horizontal[0] ? -1 : 1 : 0), "Horizontal unmarked stay exactly on axis; leftmost of two screens north, rightmost south (including same-side 0+3).");
                else
                    Check(move.Target.X == (side == TopP3BossSide.Right ? -1 : 1) * (assignment.IsMonitor(role) ? 2 : 1), "Vertical marked move two steps safe; unmarked one.");
                Check(move.FinalRotation.HasValue == assignment.IsMonitor(role), "Only monitor holders require a facing.");
            }
            for (var start = 0; start <= 4; start += 4)
            {
                var group = roles.Skip(start).Take(4).ToArray();
                var count = group.Count(assignment.IsMonitor);
                Check(group.Count(r => assignment.IsMonitor(r) && MathF.Abs(moves[r].Target.X) >= 7) == (count + 1) / 2, "Each group places 0/1/2/3 monitors as 0/1/1/2 horizontal.");
                var mask = group.Select((r, i) => assignment.IsMonitor(r) ? 1 << i : 0).Sum();
                int[][] expectedSlots = [[0,1,2,3], [3,1,2,0], [0,2,1,3], [0,2,1,3],
                    [0,1,2,3], [0,1,2,3], [0,1,2,3], [3,1,2,0],
                    [0,1,2,3], [0,1,2,3], [0,1,2,3], [0,2,1,3],
                    [0,2,1,3], [0,1,2,3], [0,1,2,3]];
                for (var i = 0; i < 4; i++)
                {
                    var target = moves[group[i]].Target;
                    var originalSlot = initial[start + expectedSlots[mask][i]];
                    Check(MathF.Abs(originalSlot.X) >= 7 ? target.X == originalSlot.X : target.Z == originalSlot.Z,
                        $"Guide swap mask {mask}: only prescribed same-depth pair moves (both inner marked in three-screen group swaps outer).");
                }
            }
            var circles = new List<TopP3MonitorCircleSnapshot>();
            void Resolve(Vector3 source, float rotation, int multiplier, PartyRole? exclude)
            {
                var right = new Vector3(-MathF.Cos(rotation), 0, MathF.Sin(rotation));
                var targets = points.Where(p => p.Role != exclude && Vector3.Dot(p.Position - source, right) * multiplier < 0).ToArray();
                Check(targets.Length == 2, $"Cross must give production OnSideN exactly two candidates ({a},{b},{c}; {side}; {bits}).");
                foreach (var target in targets)
                    circles.Add(new(target.Position, TopP3MonitorRules.MembersInsideCircle(points, target.Position)));
            }
            Resolve(Vector3.Zero, MathF.PI, TopP3MonitorRules.BossSideMultiplier(side), null);
            foreach (var role in assignment.MonitorRoles)
                Resolve(moves[role].Target, moves[role].FinalRotation!.Value, TopP3MonitorRules.SideMultiplier(statuses[role]), role);
            Check(TopP3MonitorRules.CountHits(circles).All(h => h.Count == 1), "All eight players receive exactly one 7y circle.");
            foreach (var local in roles)
            {
                var aiMoves = Plan(assignment, side, statuses, local);
                Check(aiMoves.Count == 7 && aiMoves.All(m => m.Role != local), "Cross AI excludes each real-player role.");
            }
        }
        Console.WriteLine("PASS: cross monitors all 56 triples × 2 boss sides × 8 status combinations, 7y circles, movement budget and all local roles.");
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }
}
