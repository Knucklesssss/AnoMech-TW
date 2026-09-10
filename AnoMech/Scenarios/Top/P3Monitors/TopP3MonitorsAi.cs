using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Top.P3Monitors;

public sealed class TopP3MonitorsAi : IScenarioAi<TopP3MonitorsState>
{
    public string Name => "tuuufless";

    private TopP3MonitorsState state = null!;
    private SimParty party = null!;

    // The party's own order, used both for the pre-position column and for deciding who
    // takes which numbered spot within a group.
    private static readonly PartyRole[] NorthToSouth =
    [
        PartyRole.RegenHealer, PartyRole.MainTank, PartyRole.OffTank, PartyRole.MeleeDpsA,
        PartyRole.MeleeDpsB, PartyRole.PhysRangedDps, PartyRole.CasterDps, PartyRole.ShieldHealer,
    ];

    // Spots 1-5 for the five players without a monitor, measured off the guide's diagram for
    // the case where Omega's screen points west. Spot 2 sits just off the middle and spot 3
    // hugs the east-west line, both of which are what keeps them out of the cannons.
    private static readonly Vector2[] Clear =
    [
        new(1.9f, -16.4f), new(-0.8f, 0f), new(-14.4f, 0f), new(3.7f, 10f), new(3.2f, 18.5f),
    ];

    // The three monitor holders stay on the far side from Omega's screen, fanned out so no
    // cannon can cover two of them.
    private static readonly Vector2[] Holding =
    [
        new(11.4f, -13.9f), new(17.5f, -5.4f), new(18.1f, 4.7f),
    ];

    public void Run(TopP3MonitorsState s, SimWorld world)
    {
        state = s;
        party = world.Party;
        var ai = new AiManager(world);
        ai.Move(0.5f, WestColumn, arrivalTime: 6f);
        if (state.Markers == MarkerMode.System) ai.Automarker(8.6f, ClearSpotMarkers);
        ai.Move(9f, MonitorSpots, arrivalTime: 16.5f);
    }

    // The party's own call: a north-to-south column on the west side, sitting on the line
    // between the two western waymarks.
    private IAiMove WestColumn()
    {
        var coords = new Vector2?[8];
        for (var i = 0; i < 8; i++) coords[i] = new Vector2(-13f, -14f + 4f * i);
        return AiMove.Create(coords).Assignments(NorthToSouth);
    }

    // Monitor holders opposite Omega's screen, everyone else on the numbered spots. The
    // diagram is drawn for a west-facing screen, so an east-facing one mirrors it.
    private IAiMove MonitorSpots()
    {
        var mirror = state.ScreenFacesEast ? -1f : 1f;
        var coords = new Vector2?[8];
        var roles = new PartyRole[8];
        var next = 0;

        void Place(PartyRole role, Vector2 at)
        {
            coords[next] = new Vector2(at.X * mirror, at.Y);
            roles[next++] = role;
        }

        var holders = MonitorHolders();
        var clear = ClearPlayers();

        for (var i = 0; i < holders.Count; i++) Place(holders[i], Holding[i]);
        for (var i = 0; i < clear.Count; i++) Place(clear[i], Clear[i]);

        return AiMove.Create(coords).Assignments(roles);
    }

    private List<PartyRole> InPartyOrder(IEnumerable<int> slots) =>
        slots.Select(state.At)
             .OrderBy(role => Array.IndexOf(NorthToSouth, role))
             .ToList();

    private List<PartyRole> MonitorHolders() =>
        InPartyOrder(Enumerable.Range(0, TopP3MonitorsState.SlotCount).Where(state.HasMonitor));

    // Attack 1-5 name the five numbered spots. Whoever caught a monitor is left unmarked --
    // their own debuff already tells them where to go -- so only these five are called.
    private static readonly (Sign Sign, int Slot)[] HandPlacedPlan =
    [
        (Sign.Attack1, 0), (Sign.Attack2, 1), (Sign.Attack3, 2), (Sign.Attack4, 3), (Sign.Attack5, 4),
    ];

    // System mode marks the five itself; manual mode marks nobody and instead reads the
    // signs the raid placed, so the party's own call decides who takes which spot.
    private List<PartyRole> ClearPlayers()
    {
        var clear = InPartyOrder(Enumerable.Range(0, TopP3MonitorsState.SlotCount)
                                           .Where(slot => !state.HasMonitor(slot)));
        if (state.Markers != MarkerMode.Manual) return clear;

        var holders = MonitorHolders();
        var reordered = HandPlacedSigns.Reorder(party, new RoleList(party, clear.Concat(holders).ToList()),
                                                HandPlacedPlan, holders);
        return Enumerable.Range(0, clear.Count).Select(i => reordered[i]).ToList();
    }

    private Dictionary<PartyRole, Sign> ClearSpotMarkers()
    {
        var clear = InPartyOrder(Enumerable.Range(0, TopP3MonitorsState.SlotCount)
                                           .Where(slot => !state.HasMonitor(slot)));
        var marks = new Dictionary<PartyRole, Sign>();
        for (var i = 0; i < clear.Count && i < HandPlacedPlan.Length; i++)
            marks[clear[i]] = HandPlacedPlan[i].Sign;
        return marks;
    }
}
