using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Top.P3Monitors;

// Per-run randomization for the Oversampled Wave Cannon.
//
// The timeline is one recorded pull, so who does what is fixed by slot: slot 3 takes a
// right-facing monitor, slots 1 and 5 take left-facing ones, and the rest are the players
// those monitors have to be aimed away from. Rolling the slots re-deals that assignment
// every run while keeping the recording's relationships.
public sealed class TopP3MonitorsState
{
    public const int SlotCount = 8;
    public static readonly int[] MonitorSlots = [1, 3, 5];

    private readonly RoleList order;

    // Omega's own screen always points due east or due west; the three player monitors are
    // rolled independently of it. The recording does not carry Omega's facing, so it is
    // rolled here and the scenario turns the model to match, which is what makes the case
    // readable in game.
    public bool ScreenFacesEast { get; }

    public TopP3MonitorsState(SimParty party, TopP3MonitorsStateOverrides overrides)
    {
        order = new RoleListBuilder
        {
            ForcePlayerIndex = overrides.PlayerSlot is { } slot ? [slot] : [],
        }.Build(party);
        ScreenFacesEast = overrides.ScreenFacesEast ?? new Rng().NextBool();
    }

    public PartyRole At(int slot) => order[slot];

    public bool HasMonitor(int slot) => System.Array.IndexOf(MonitorSlots, slot) >= 0;
}
