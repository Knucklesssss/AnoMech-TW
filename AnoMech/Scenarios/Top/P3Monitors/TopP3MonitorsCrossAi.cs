using AnoMech.Core.Game.Ai;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Top.P3Monitors;

public sealed class TopP3MonitorsCrossAi : IScenarioAi<TopP3MonitorsState>
{
    public string Name => "十字法";
    public string? Group => "陸服";

    public void Run(TopP3MonitorsState state, SimWorld world)
    {
        var initial = TopP3MonitorsCrossRules.PlanInitialMoves(world.Party.PlayerRole);
        var moves = TopP3MonitorsCrossRules.PlanMoves(state.Assignment, state.BossSide, state.MonitorStatuses, world.Party.PlayerRole);
        world.Events.Add(TopP3MonitorRules.QueueAt, () =>
        {
            foreach (var move in initial)
                if (world.Party.Get(move.Role) is SimNpc character && character.IsAlive())
                    character.SetPosition(move.Target);
        });
        world.Events.Add(TopP3MonitorRules.MoveStartAt, () =>
        {
            foreach (var move in moves)
                if (world.Party.Get(move.Role) is SimNpc character && character.IsAlive())
                    character.MoveTo(move.Target, TopP3MonitorRules.MoveSpeed, move.FinalRotation);
        });
    }
}
