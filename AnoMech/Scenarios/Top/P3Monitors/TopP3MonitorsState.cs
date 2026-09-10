using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Top.P3Monitors;

// Mul is the sign CharacterFind.OnSideN applies when it picks the half of the arena a
// screen fires into: +1 keeps the players on the holder's left, -1 the ones on their
// right. Same convention as the P5 monitors, so the two stay comparable.
public sealed record ScreenSide(int Mul, ushort Status)
{
    public static readonly ScreenSide Left = new(1, TopConstants.StatusId.PlayerMonitorLeft);
    public static readonly ScreenSide Right = new(-1, TopConstants.StatusId.PlayerMonitorRight);
}

// Per-run randomization for the Oversampled Wave Cannon.
//
// The timeline is one recorded pull, so which slots end up holding a screen (1, 3 and 5)
// and who each baked cannon hits are both fixed. Rolling the slot order re-deals those
// jobs every run while keeping the recording's relationships.
//
// The left/right facing of a screen feeds none of that — it only decides which way its
// holder has to turn — so it is rolled fresh for each holder, which is what the fight
// does. Without this the same three seats always got the same sides and the debuff was
// never worth reading.
public sealed class TopP3MonitorsState
{
    public const int SlotCount = 8;
    public static readonly int[] MonitorSlots = [1, 3, 5];

    // Omega's screen points east for the whole of this scenario. Nothing here chooses that:
    // the screen is part of the recorded animation, and the sim has no way to turn it, so a
    // setting offering the other case would move the bots while the picture stayed put.
    public const bool ScreenFacesEast = true;

    private readonly Rng rng = new();
    private readonly RoleList order;
    private readonly ScreenSide?[] sides = new ScreenSide?[SlotCount];

    public TopP3MonitorsState(SimParty party, TopP3MonitorsStateOverrides overrides)
    {
        order = new RoleListBuilder
        {
            ForcePlayerIndex = overrides.PlayerSlot is { } slot ? [slot] : [],
        }.Build(party);

        foreach (var monitor in MonitorSlots)
            sides[monitor] = rng.NextObj(ScreenSide.Left, ScreenSide.Right);
    }

    public PartyRole At(int slot) => order[slot];

    public ScreenSide? SideAt(int slot) => slot >= 0 && slot < SlotCount ? sides[slot] : null;

    public bool HasMonitor(int slot) => SideAt(slot) != null;
}
