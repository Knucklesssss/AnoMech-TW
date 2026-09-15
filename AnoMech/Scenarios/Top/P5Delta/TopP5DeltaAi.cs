using System;
using System.Linq;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Top.TopConstants.Geometry;

namespace AnoMech.Scenarios.Top.P5Delta;

// First AI strategy for TOP P5 Delta. Reads the shared TopP5DeltaState so its
// movement decisions stay in sync with the scenario's randomized layout, and
// schedules movement through World.Events so it can react to fight timestamps.
public class TopP5DeltaAi : IScenarioAi<TopP5DeltaState>
{
    // Tether-resolve positions in scenario-local coords (origin (0,0) = world (100,100)).
    // Even slots are caller-specified, odd slots are mirrored across the east-west axis
    // (same X, negated Z) so each pair lands on opposite sides. EyeSpawn==South flips
    // the entire layout 180° (negate both X and Z).

    public virtual string Name => "Standard";
    public virtual string? Group => "原有";

    protected TopP5DeltaState state = null!;

    public void Run(TopP5DeltaState s, SimWorld world)
    {
        state = s;
        var ai = new AiManager(world);
        ai.Move(0.5f, InitialPositions);
        ai.Move(13f, TetherPrePosition);
        ai.Move(21f, FistResolveSlots);
        ai.Move(28.8f, TetherResolveStep);
        ai.Move(31.2f, HyperPulseBaitArms);
        ai.Move(36.2f, HyperPulseDodge, 0);
        ai.Move(38.4f, MonitorPositions);
        ai.Move(40, MonitorAdjustment, 0);
        ai.Move(46f, SwivelDodge);
        ai.Move(46.4f, BreakBeetleSideTether);
        ai.Move(50f, RescueUnsafe);
        ai.Move(56f, ReturnToMiddle);
        ai.Move(56.5f, TankForward);
        ai.Move(60.0f, BreakLastTether);
    }

    protected virtual void Swap01(IAiRoles s)
    {
        if (state.FistColors[0] == state.FistColors[2])
            s.ByPosition(0, 1);
    }

    protected virtual void Swap45(IAiRoles s)
    {
        if (state.FistColors[4] == state.FistColors[6])
            s.ByPosition(4, 5);
    }

    private void BeyondDefence(IAiPositions move)
    {
        move.AddX(state.BeyondDefenseTarget, 13f);
    }

    private void PlayerMonitorOffset(IAiPositions move)
    {
        move.AddY(state.PlayerMonitorRole, 2);
    }

    private void PlayerMonitorFacing(IAiPositions move)
    {
        move.AddX(state.PlayerMonitorRole, 1.2f * state.PlayerMonitorSide.Mul * state.OmegaMonitorSide.Mul);
    }

    private void OmegaMonitorSafeSide(IAiPositions move)
    {
        var mul = -state.OmegaMonitorSide.Mul * state.EyeSpawn.Mul;
        move.MultiplyY(0, mul);
        move.MultiplyY(1, mul);
        move.MultiplyY(2, mul);
        move.MultiplyY(3, mul);
    }

    private void SwivelSafeSide(IAiPositions move)
    {
        move.MultiplyY(state.SwivelCannonSide.Mul * state.EyeSpawn.Mul);
    }

    private void WorldSwaps(IAiRoles s)
    {
        s.ByRole(state.TetherOrder[0], state.FarWorldRole);
        s.ByRole(state.FarWorldTetherIndex == 1 ? state.TetherOrder[0] : state.TetherOrder[1] , state.NearWorldRole);
    }

    private int SafeSide => state.SwivelCannonSide.Mul * (int)state.EyeSpawn.Mul;

    // With the beetle drawn north, the far-side local tether turns clockwise: its left player (+Y while the
    // beetle is west, mirrored when it is east) runs to the beetle's feet, the right one away from it.
    private int FeetSlot => state.EyeSpawn.Mul > 0 ? 7 : 6;

    protected void AdjustEyePosition(IAiPositions move)
    {
        move.MultiplyX(state.EyeSpawn.Mul);
    }

    private IAiMove InitialPositions()
    {
        return AiMove.Create(
            new(-2.10f, -5.08f),
            new(2.10f, -5.08f),
            new(-0.7f, 5.7f),
            new(-0.7f, 6.5f),
            new(-0.7f, 7.3f),
            new(0.7f, 5.7f),
            new(0.7f, 6.5f),
            new(0.7f, 7.3f)
        ).NaturalOrder();
    }

    protected virtual IAiMove TetherPrePosition()
    {
        return AiMove.Create(
            new(-6f, -3f),
            new(-6f, 3f),
            new(-10f, -7f),
            new(-10f, 7f),
            new(4f, -6f),
            new(4f, 6f),
            new(9.5f, -10f),
            new(9.5f, 10f)
        )
        .Assignments(state.TetherOrder)
        .ApplyPositions(AdjustEyePosition);
    }

    protected virtual IAiMove FistResolveSlots()
    {
        return AiMove.Create(
            new Vector2(-10f, -3f),
            new Vector2(-10f, 3f),
            null,
            null,
            new Vector2(8.5f, -10f),
            new Vector2(8.5f, 10f),
            null,
            null
        )
        .Assignments(state.TetherOrder)
        .ApplySwaps(Swap01, Swap45)
        .ApplyPositions(AdjustEyePosition);
    }

    protected virtual IAiMove TetherResolveStep()
    {
        return AiMove.Create(
            null, null,
            new(-10f, -3f),
            new(-10f, 3f),
            null, null, null, null
        )
        .Assignments(state.TetherOrder)
        .ApplyPositions(AdjustEyePosition);
    }

    protected virtual IAiMove HyperPulseBaitArms()
    {
        return AiMove.Create(
            ArmUnitPlacements.Select((placement, i) =>
                                         placement.MoveForward(0.5f)
                                                  .RotateAroundOrigin(
                                                      0.15f * state.ArmHandedness[i].Mul * state.EyeSpawn.Mul)
                                                  .Position2)
                             .Prepend(new Vector2(0f, 6f))
                             .Prepend(new Vector2(0f, -6f))
                             .Cast<Vector2?>()
                             .ToArray()
        )
        .Assignments(state.TetherOrder)
        .ApplySwaps(Swap01, Swap45)
        .ApplyPositions(AdjustEyePosition);
    }

    protected virtual IAiMove HyperPulseDodge()
    {
        return AiMove.Create(
            new(0, 1f), 
            new(0, 1f),
            new(0, 1f),
            new(0, 1f),
            new(0, -12),
            new(0, 12),
            new(9, -10),
            new(9, 10)
        )
        .Assignments(state.TetherOrder)
        .ApplySwaps(Swap45)
        .ApplyPositions(BeyondDefence, PlayerMonitorOffset, OmegaMonitorSafeSide, AdjustEyePosition);
    }

    protected virtual IAiMove MonitorPositions()
    {
        return AiMove.Create(
            null, null, null, null,
            new(-10, -12),
            new(-10, 12),
            new(10, -12),
            new(10, 12)
        )
        .Assignments(state.TetherOrder)
        .ApplySwaps(Swap45)
        .ApplyPositions(AdjustEyePosition);
    }

    private IAiMove MonitorAdjustment()
    {
        return AiMove.Single(state.PlayerMonitorRole, new(0f, 1f))
        .ApplyPositions(
            BeyondDefence,
            PlayerMonitorOffset,
            PlayerMonitorFacing, // move monitor player a little so they face their monitor properly
            OmegaMonitorSafeSide,
            AdjustEyePosition
        );
    }

    // Y is written for a +Y safe half and flipped by SwivelSafeSide. Monitor positions left the beetle-side local
    // tether at (-10, ±12): its safe-half player hugs the edge beside the beetle now, its danger-half player breaks
    // the tether later. The far-side pair keeps its tether: feet player to the beetle, the other to the safe half's
    // far edge. The guide stands there three edge ticks (30°) past the waymark; Swivel Cannon's 105° cone reaches
    // the edge at 31°, so 34° keeps a yalm of margin.
    protected virtual IAiMove SwivelDodge()
    {
        var points = new Vector2?[]
        {
            new(0f, 19f), // far
            new(0f, 6f),  // near
            new(9.5f, 17f),
            new(9.5f, 17f),
            SafeSide < 0 ? new Vector2(-13.8f, 13.8f) : null,
            SafeSide > 0 ? new Vector2(-13.8f, 13.8f) : null,
            null,
            null,
        };
        points[FeetSlot] = new(-19f, 1.5f);
        points[13 - FeetSlot] = new(15.75f, 10.62f);
        return AiMove.Create(points)
            .Assignments(state.TetherOrder)
            .ApplySwaps(WorldSwaps, Swap45)
            .ApplyPositions(SwivelSafeSide, AdjustEyePosition);
    }

    // Leaves when the run crosses the 10 y break distance at 15 s left on the tether (applied 28.1 s, 36 s), and
    // reaches the safe waymark beside the beetle leaning away from Hello Near World before Swivel Cannon lands.
    protected virtual IAiMove BreakBeetleSideTether()
    {
        return AiMove.Create(
            null, null, null, null,
            SafeSide > 0 ? new Vector2(-10.3f, 9.9f) : null,
            SafeSide < 0 ? new Vector2(-10.3f, 9.9f) : null,
            null, null
        )
        .Assignments(state.TetherOrder)
        .ApplySwaps(Swap45)
        .ApplyPositions(SwivelSafeSide, AdjustEyePosition);
    }

    // The danger-half beetle-side player is already safe from BreakBeetleSideTether.
    protected virtual IAiMove RescueUnsafe() => AiMove.Create().NaturalOrder();

    protected virtual IAiMove ReturnToMiddle()
    {
        var points = new Vector2?[]
        {
            new(-0.7f, 5.7f),
            new(-0.7f, 6.5f),
            new(-0.7f, 7.3f),
            new(0.7f, 5.7f),
            new(0.7f, 6.5f),
            new(0.7f, 7.3f),
            null,
            null,
        };
        points[FeetSlot] = new(-9f, 1f);
        points[13 - FeetSlot] = new(8f, 4f);
        return AiMove.Create(points)
            .Assignments(state.TetherOrder)
            .ApplySwaps(WorldSwaps)
            .ApplyPositions(SwivelSafeSide, AdjustEyePosition);
    }

    private IAiMove TankForward()
    {
        return AiMove.Single(PartyRole.OffTank, new(0, -8f))
            .ApplyPositions(SwivelSafeSide, AdjustEyePosition);
    }

    protected virtual IAiMove BreakLastTether()
    {
        var points = new Vector2?[8];
        points[FeetSlot] = new(-4f, 2.5f);
        points[13 - FeetSlot] = new(4f, 2.7f);
        return AiMove.Create(points)
            .Assignments(state.TetherOrder)
            .ApplyPositions(SwivelSafeSide, AdjustEyePosition);
    }
}
