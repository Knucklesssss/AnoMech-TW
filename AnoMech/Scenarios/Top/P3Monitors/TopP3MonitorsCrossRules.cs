using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Top.P3Monitors;

public static class TopP3MonitorsCrossRules
{
    private static readonly PartyRole[] Roles =
    [
        PartyRole.RegenHealer, PartyRole.MainTank, PartyRole.OffTank, PartyRole.ShieldHealer,
        PartyRole.PhysRangedDps, PartyRole.MeleeDpsA, PartyRole.MeleeDpsB, PartyRole.CasterDps,
    ];

    public static Vector3 InitialPositionFor(PartyRole role)
    {
        var index = Array.IndexOf(Roles, role);
        if (index < 0) throw new ArgumentOutOfRangeException(nameof(role));
        var sign = index < 4 ? 1 : -1;
        return (index % 4) switch
        {
            0 => new(0, 0, -19 * sign),
            1 => new(0, 0, -7 * sign),
            2 => new(7 * sign, 0, 0),
            _ => new(19 * sign, 0, 0),
        };
    }

    public static IReadOnlyList<TopP3MonitorMove> PlanInitialMoves(PartyRole localRole)
        => Roles.Where(r => r != localRole).Select(r => new TopP3MonitorMove(r, InitialPositionFor(r), null)).ToArray();

    public static IReadOnlyList<TopP3MonitorMove> PlanMoves(
        TopP3MonitorAssignment assignment,
        TopP3BossSide bossSide,
        IReadOnlyDictionary<PartyRole, TopP3MonitorStatus> statuses,
        PartyRole localRole)
    {
        var slots = Roles.ToArray();
        for (var start = 0; start < 8; start += 4)
        {
            var marked = Enumerable.Range(0, 4).Where(i => assignment.IsMonitor(Roles[start + i])).ToArray();
            var swap = marked.Length switch
            {
                1 when marked[0] < 2 => marked[0],
                2 when (marked[0] < 2) == (marked[1] < 2) => 1,
                3 when !assignment.IsMonitor(Roles[start + 2]) => 1,
                3 when !assignment.IsMonitor(Roles[start + 3]) => 0,
                _ => -1,
            };
            if (swap >= 0)
                (slots[start + swap], slots[start + 3 - swap]) = (slots[start + 3 - swap], slots[start + swap]);
        }

        var safe = bossSide == TopP3BossSide.Right ? -1 : 1;
        var horizontalMonitors = Enumerable.Range(0, 8)
            .Where(i => i % 4 >= 2 && assignment.IsMonitor(slots[i]))
            .OrderBy(i => InitialPositionFor(Roles[i]).X).ToArray();
        var moves = new List<TopP3MonitorMove>(7);
        for (var i = 0; i < slots.Length; i++)
        {
            var role = slots[i];
            if (role == localRole) continue;
            var point = InitialPositionFor(Roles[i]);
            var marked = assignment.IsMonitor(role);
            Vector3 normal;
            if (point.X == 0)
            {
                point.X = safe * (marked ? 2 : 1);
                normal = new(safe, 0, 0);
            }
            else
            {
                var direction = i == horizontalMonitors[0] ? -1 : 1;
                point.Z = marked ? direction : 0;
                normal = new(0, 0, direction);
            }
            float? rotation = marked
                ? MathF.Atan2(normal.X, normal.Z) + (statuses[role] == TopP3MonitorStatus.Right ? MathF.PI / 2 : -MathF.PI / 2)
                : null;
            moves.Add(new(role, point, rotation));
        }
        return moves;
    }

    public static string PositionLabel(Vector3 point)
        => $"{(MathF.Abs(point.X) >= 7 ? point.X > 0 ? "東" : "西" : point.Z < 0 ? "北" : "南")}{(MathF.Max(MathF.Abs(point.X), MathF.Abs(point.Z)) > 10 ? "外" : "內")}";
}
