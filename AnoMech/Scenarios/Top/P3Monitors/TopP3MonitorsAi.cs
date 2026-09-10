using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Top.P3Monitors;

public sealed class TopP3MonitorsAi : IScenarioAi<TopP3MonitorsState>
{
    public string Name => "tuuufless";

    private TopP3MonitorsState state = null!;
    private SimWorld world = null!;

    private static readonly PartyRole[] NorthToSouth =
    [
        PartyRole.RegenHealer, PartyRole.MainTank, PartyRole.OffTank, PartyRole.MeleeDpsA,
        PartyRole.MeleeDpsB, PartyRole.PhysRangedDps, PartyRole.CasterDps, PartyRole.ShieldHealer,
    ];

    private static readonly Vector2[] ScreenlessSpotsAgainstAWestFacingScreen =
    [
        new(1.7f, -16.1f), new(-1.7f, -0.3f), new(-13.8f, -0.2f), new(2.2f, 9.4f), new(2.1f, 18.3f),
    ];

    private static readonly Vector2[] ScreenSpotsAgainstAWestFacingScreen =
    [
        new(10.1f, -13.6f), new(15.2f, -5.3f), new(15.6f, 3.4f),
    ];

    private static readonly Vector2[] CannonAimAgainstAWestFacingScreen =
    [
        new(1f, 0f), new(0f, -1f), new(0f, 1f),
    ];

    private static float MirrorForOmegasScreen => TopP3MonitorsState.ScreenFacesEast ? -1f : 1f;

    public void Run(TopP3MonitorsState s, SimWorld w)
    {
        state = s;
        world = w;
        var ai = new AiManager(world);
        ai.Move(0.5f, LineUpOnTheWestEdge, arrivalTime: 6f);
        ai.Move(9f, SpreadOntoTheCannonSpots, arrivalTime: 16.5f);
        world.Events.Add(17f, TurnEachScreenOntoItsOwnTargets);
        world.Events.Add(18f, TurnEachScreenOntoItsOwnTargets);
    }

    private IAiMove LineUpOnTheWestEdge()
    {
        var coords = new Vector2?[8];
        for (var i = 0; i < 8; i++) coords[i] = new Vector2(-13f, -14f + 4f * i);
        return AiMove.Create(coords).Assignments(NorthToSouth);
    }

    private IAiMove SpreadOntoTheCannonSpots()
    {
        var coords = new Vector2?[8];
        var roles = new PartyRole[8];
        var next = 0;

        void Place(int slot, Vector2 spot)
        {
            coords[next] = new Vector2(spot.X * MirrorForOmegasScreen, spot.Y);
            roles[next++] = state.At(slot);
        }

        var screens = ScreenSlotsInPartyOrder();
        var screenless = ScreenlessSlotsInPartyOrder();
        for (var i = 0; i < screens.Count && i < ScreenSpotsAgainstAWestFacingScreen.Length; i++)
            Place(screens[i], ScreenSpotsAgainstAWestFacingScreen[i]);
        for (var i = 0; i < screenless.Count && i < ScreenlessSpotsAgainstAWestFacingScreen.Length; i++)
            Place(screenless[i], ScreenlessSpotsAgainstAWestFacingScreen[i]);

        return AiMove.Create(coords).Assignments(roles);
    }

    private void TurnEachScreenOntoItsOwnTargets()
    {
        var screens = ScreenSlotsInPartyOrder();
        for (var i = 0; i < screens.Count && i < CannonAimAgainstAWestFacingScreen.Length; i++)
        {
            var role = state.At(screens[i]);
            if (role == world.Party.PlayerRole) continue;
            if (state.SideAt(screens[i]) is not { } side) continue;
            var aim = CannonAimAgainstAWestFacingScreen[i];
            world.Party.Get(role)?.SetRotation(
                RotationThatAimsTheCannon(new Vector2(aim.X * MirrorForOmegasScreen, aim.Y), side));
        }
    }

    private static float RotationThatAimsTheCannon(Vector2 aim, ScreenSide side)
    {
        var right = aim * -side.Mul;
        return MathF.Atan2(right.Y, -right.X);
    }

    private List<int> ScreenSlotsInPartyOrder() => SlotsInPartyOrder(state.HasMonitor);

    private List<int> ScreenlessSlotsInPartyOrder() => SlotsInPartyOrder(slot => !state.HasMonitor(slot));

    private List<int> SlotsInPartyOrder(Func<int, bool> keep) =>
        Enumerable.Range(0, TopP3MonitorsState.SlotCount)
                  .Where(keep)
                  .OrderBy(slot => Array.IndexOf(NorthToSouth, state.At(slot)))
                  .ToList();
}
