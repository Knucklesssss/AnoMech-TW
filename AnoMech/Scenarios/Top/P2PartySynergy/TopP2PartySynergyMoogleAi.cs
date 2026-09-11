using AnoMech.Core.Game.Ai;
using AnoMech.Scenarios.Top.P3Monitors;

namespace AnoMech.Scenarios.Top.P2PartySynergy;

public sealed class TopP2PartySynergyMoogleAi : TopP2PartySynergyAi
{
    public override string Name => "B站莫古力";
    public override string? Group => "陸服";

    protected override IAiMove CongaLine() => AiMove.Create(
        new(-8.4f, 2), new(-6, 2), new(-3.6f, 2), new(-1.2f, 2),
        new(1.2f, 2), new(3.6f, 2), new(6, 2), new(8.4f, 2))
        .Assignments(TopP3MonitorRules.Priority);

    protected override void SwapForCongaOrder(IAiRoles roles)
    {
        for (var pair = 0; pair < 4; pair++)
            if (TopP3MonitorRules.PriorityIndex(roles.RoleAt(pair * 2)) >
                TopP3MonitorRules.PriorityIndex(roles.RoleAt(pair * 2 + 1)))
                roles.ByPosition(pair * 2, pair * 2 + 1);
    }

    protected override void GlitchSwap(IAiRoles roles)
    {
        if (state.Glitch != GlitchType.Far) return;
        roles.ByPosition(1, 7);
        roles.ByPosition(3, 5);
    }

    protected override void AdjustForStacks(IAiRoles roles)
    {
        var a = roles.PositionOf(state.Stacks[0]);
        var b = roles.PositionOf(state.Stacks[1]);
        if (a % 2 != b % 2) return;
        var south = System.Math.Max(a, b);
        roles.ByPosition(south, state.Glitch == GlitchType.Far ? 7 - south : south ^ 1);
    }

    protected override IPositionStep StackPositions() => base.StackPositions()
        .ApplyPositions(p => { if (state.Glitch == GlitchType.Mid) p.Multiply(15.8f / 15f); });

    protected override IPositionStep KnockbackPositions() => base.KnockbackPositions()
        .ApplyPositions(p => { if (state.Glitch == GlitchType.Mid) p.Multiply(2.8f / 2f); });
}
