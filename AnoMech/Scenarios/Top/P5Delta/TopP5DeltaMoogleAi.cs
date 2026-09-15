using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using static AnoMech.Scenarios.Top.TopConstants.Geometry;

namespace AnoMech.Scenarios.Top.P5Delta;

public sealed class TopP5DeltaMoogleAi : TopP5DeltaAi
{
    public override string Name => "B站莫古力";
    public override string? Group => "陸服";
    private int SafeSouth => state.SwivelCannonSide.Mul * (int)state.EyeSpawn.Mul;
    private bool OuterFistsSwap => state.FistColors[4] == state.FistColors[6];

    // Outer blue waits beside the waymark facing Omega-M; inner blue stands just inside the east diagonals,
    // far enough apart that its remote tether breaks as soon as it lands.
    protected override IAiMove TetherPrePosition() => AiMove.Create(
        new(9.9f, -7), new(9.9f, 7), new(13.6f, -2.5f), new(13.6f, 2.5f),
        new(-4, -6), new(-4, 6), new(-9.5f, -10), new(-9.5f, 10))
        .Assignments(state.TetherOrder).ApplyPositions(AdjustEyePosition);

    protected override void Swap01(IAiRoles roles)
    {
        if (state.FistColors[0] == state.FistColors[2]) roles.ByPosition(2, 3);
    }

    protected override void Swap45(IAiRoles roles)
    {
        if (state.FistColors[4] == state.FistColors[6]) roles.ByPosition(6, 7);
    }

    // After the fists spawn, outer blue steps inward off its fists.
    protected override IAiMove FistResolveSlots() => AiMove.Create(
        new(9.9f, -7), new(9.9f, 7), new(9.9f, -2.5f), new(9.9f, 2.5f),
        new(-8.5f, -10), new(-8.5f, 10), new(-9.5f, -10), new(-9.5f, 10))
        .Assignments(state.TetherOrder).ApplySwaps(Swap01, Swap45).ApplyPositions(AdjustEyePosition);

    // Once its tether has exploded, inner blue stacks with the same-side outer blue for the fists.
    protected override IAiMove TetherResolveStep() => AiMove.Create(
        new(9.9f, -2.5f), new(9.9f, 2.5f), null, null, null, null, null, null)
        .Assignments(state.TetherOrder).ApplyPositions(AdjustEyePosition);

    protected override IAiMove HyperPulseBaitArms() => AiMove.Create(
        new[]{4,5,2,3,0,1}.Select(i => ArmUnitPlacements[i].MoveForward(.5f)
            .RotateAroundOrigin(.15f * state.ArmHandedness[i].Mul * state.EyeSpawn.Mul).Position2)
            .Prepend(new Vector2(0,6)).Prepend(new Vector2(0,-6)).Cast<Vector2?>().ToArray())
        .Assignments(state.TetherOrder).ApplySwaps(Swap01,Swap45).ApplyPositions(AdjustEyePosition);

    protected override IAiMove HyperPulseDodge()
    {
        var move=base.HyperPulseDodge();
        return AiMove.Create(Enumerable.Range(0,8).Select(i =>
            state.TetherOrder.Skip(6).Contains((Core.Game.Party.PartyRole)i)
                ? move[i] * new Vector2(-1,1) : move[i]).ToArray()).NaturalOrder();
    }

    protected override IAiMove MonitorPositions() => AiMove.Create(
        null,null,null,null,new(10,-12),new(10,12),new(-10,-12),new(-10,12))
        .Assignments(state.TetherOrder).ApplySwaps(Swap45).ApplyPositions(AdjustEyePosition);

    protected override IAiMove SwivelDodge()
    {
        var points=new Vector2?[8];
        for(var i=0;i<4;i++) points[i] = state.TetherOrder[i] == state.FarWorldRole ? new(0,19*SafeSouth)
            : state.TetherOrder[i] == state.NearWorldRole ? new(0,6*SafeSouth) : new(9.5f,17*SafeSouth);
        for(var i=4;i<6;i++) points[i] = (i%2==0 ? -1 : 1)==SafeSouth ? new(16,10*SafeSouth) : new(-19,SafeSouth);
        for(var i=6;i<8;i++)
        {
            var side = (i==6)==OuterFistsSwap ? 1 : -1;
            // The unsafe-side local tether goes straight to the safe diagonal once the monitor vulnerability is gone,
            // so its break vulnerability ends well before Hello World.
            points[i] = side==SafeSouth ? new(-14,14*SafeSouth) : new(-10,10*SafeSouth);
        }
        return AiMove.Create(points).Assignments(state.TetherOrder).ApplyPositions(AdjustEyePosition);
    }

    protected override IAiMove BreakBeetleSideTether() => AiMove.Create().NaturalOrder();

    protected override IAiMove RescueUnsafe() => AiMove.Single(
        state.TetherOrder[(SafeSouth>0)^OuterFistsSwap ? 6 : 7],new(-10,10*SafeSouth))
        .ApplyPositions(AdjustEyePosition);

    protected override IAiMove ReturnToMiddle()
    {
        var points=new Vector2?[8];
        var free=0;
        for(var i=0;i<8;i++)
            points[i]= i is 4 or 5 ? ((i%2==0 ? -1 : 1)==SafeSouth ? new(8,4*SafeSouth) : new(-9,SafeSouth))
                : new((free<3 ? -.7f : .7f),(5.7f+.8f*(free++%3))*SafeSouth);
        return AiMove.Create(points).Assignments(state.TetherOrder).ApplyPositions(AdjustEyePosition);
    }

    protected override IAiMove BreakLastTether() => AiMove.Create(
        null,null,null,null,
        SafeSouth<0 ? new(4,-2.7f) : new(-4,2.5f),
        SafeSouth>0 ? new(4,2.7f) : new(-4,-2.5f),null,null)
        .Assignments(state.TetherOrder).ApplyPositions(AdjustEyePosition);
}
