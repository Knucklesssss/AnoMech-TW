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

    // The party's own order, used for the pre-position column and for deciding who takes
    // which numbered spot within a group.
    private static readonly PartyRole[] NorthToSouth =
    [
        PartyRole.RegenHealer, PartyRole.MainTank, PartyRole.OffTank, PartyRole.MeleeDpsA,
        PartyRole.MeleeDpsB, PartyRole.PhysRangedDps, PartyRole.CasterDps, PartyRole.ShieldHealer,
    ];

    // Spots 1-5 for the five without a monitor, drawn for a west-facing screen. Spots 1, 4
    // and 5 sit on the north-south line; 2 steps just off the middle and 3 runs out along
    // the east-west line, which is what puts the pair of them in Omega's own cannon and
    // keeps them clear of the players' ones.
    private static readonly Vector2[] Clear =
    [
        new(0f, -16.4f), new(-2f, 0f), new(-14.4f, 0f), new(0f, 10f), new(0f, 18.5f),
    ];

    // The three monitor holders stay on the far side from Omega's screen, fanned out so no
    // one cannon can cover two of them.
    private static readonly Vector2[] Holding =
    [
        new(11.4f, -13.9f), new(17.5f, -5.4f), new(18.1f, 4.7f),
    ];

    public void Run(TopP3MonitorsState s, SimWorld world)
    {
        state = s;
        var ai = new AiManager(world);
        ai.Move(0.5f, WestColumn, arrivalTime: 6f);
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

    private List<PartyRole> ClearPlayers() =>
        InPartyOrder(Enumerable.Range(0, TopP3MonitorsState.SlotCount).Where(slot => !state.HasMonitor(slot)));
}
