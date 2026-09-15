using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

using AnoMech.Core.SimObjects;

namespace AnoMech.Core;

// Party head-marker "signs" (the 17-slot icon set selectable from the party
// list — Attack 1-8, Bind 1-3, Ignore 1-2, Square/Circle/Cross/Triangle).
// Index value is the slot in MarkingController._markers; do not renumber.
public enum Sign
{
    Attack1 = 0,
    Attack2 = 1,
    Attack3 = 2,
    Attack4 = 3,
    Attack5 = 4,
    Bind1 = 5,
    Bind2 = 6,
    Bind3 = 7,
    Ignore1 = 8,
    Ignore2 = 9,
    Square = 10,
    Circle = 11,
    Cross = 12,
    Triangle = 13,
    Attack6 = 14,
    Attack7 = 15,
    Attack8 = 16,
}

// Client-side party-sign writer. Bypasses the network path (the canonical
// MarkingController.MarkObject member function broadcasts to the party); we
// just stamp the GameObjectId directly into _markers, which is what the
// nameplate renderer reads each frame. Empty/cleared slots are 0.
//
// Lemegeton drives signs by sig-scanning the same member function; for a
// single-player simulator we don't need network broadcast, so direct writes
// (mirroring Waymarks) are simpler and have no side effects.
internal static unsafe class Markings
{
    private const int SlotCount = 17;

    public static void Set(Sign sign, GameObjectId target)
    {
        var idx = (int)sign;
        if (idx < 0 || idx >= SlotCount) return;
        var ctrl = MarkingController.Instance();
        if (ctrl == null) return;
        ctrl->Markers[idx] = target;
    }

    public static GameObjectId Get(Sign sign)
    {
        var idx = (int)sign;
        if (idx < 0 || idx >= SlotCount) return default;
        var ctrl = MarkingController.Instance();
        if (ctrl == null) return default;
        return ctrl->Markers[idx];
    }

    public static void Clear(Sign sign)
    {
        var idx = (int)sign;
        if (idx < 0 || idx >= SlotCount) return;
        var ctrl = MarkingController.Instance();
        if (ctrl == null) return;
        ctrl->Markers[idx] = default;
    }

    private static bool clearPrimingNextFrame;
    internal static bool Priming => clearPrimingNextFrame;

    // A doppel is invisible to the <mo> placeholder until it has carried a sign at least
    // once, which is why a player's own /mk macro silently does nothing on a fresh party
    // and starts working the moment they mark someone by hand. Stamping every slot once at
    // scenario start and wiping it on the next frame gets that out of the way for them.
    public static void PrimeMouseoverTargets(SimParty party)
    {
        var marked = false;
        for (var role = 0; role < SlotCount && role < 8; role++)
        {
            if (party.Get(role) is not { } member) continue;
            Set((Sign)role, member.GameObjectId);
            marked = true;
        }

        clearPrimingNextFrame = marked;
    }

    internal static void TickPriming()
    {
        if (!clearPrimingNextFrame) return;
        clearPrimingNextFrame = false;
        ClearAll();
    }

    public static void ClearAll()
    {
        var ctrl = MarkingController.Instance();
        if (ctrl == null) return;
        for (int i = 0; i < SlotCount; i++)
            ctrl->Markers[i] = default;
    }
}
