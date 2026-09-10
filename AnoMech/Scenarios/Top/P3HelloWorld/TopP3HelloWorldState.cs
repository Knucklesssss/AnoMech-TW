using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Top.P3HelloWorld;

// Per-run randomization for P3's Hello World.
//
// The recorded pull the timeline came from shows the eight players splitting into four
// tethered pairs, each pair walking one slot of the same four-beat cycle: slots 0 and 1
// carry a bug from the opening cast and hand it on, slots 2 and 3 receive one beat later,
// and every pair's two tethers (one near, one far) fire in an order fixed by its slot.
// Both members of a pair share a timeline, so the only thing that varies between pulls is
// *who* sits in each slot — which is all this rolls.
//
// Member order inside a pair is the tether's (source, target), which is the one place the
// two halves differ: a couple of the passes land a second apart.
public sealed class TopP3HelloWorldState
{
    public const int SlotCount = 4;

    private readonly RoleList cycle;

    public TopP3HelloWorldState(SimParty party, TopP3HelloWorldStateOverrides overrides)
    {
        cycle = new RoleListBuilder
        {
            ForcePlayerIndex = overrides.PlayerSlot is { } slot ? [slot * 2, slot * 2 + 1] : [],
        }.Build(party);
    }

    public PartyRole At(int slot, int member) => cycle[slot * 2 + member];

    public int SlotOf(PartyRole role)
    {
        for (var i = 0; i < SlotCount * 2; i++)
            if (cycle[i] == role) return i / 2;
        return -1;
    }
}
