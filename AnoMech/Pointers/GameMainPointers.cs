using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AnoMech.Pointers;

internal unsafe class GameMainPointers
{
    // The 7.5 pattern upstream carries does not occur in the API13 binary. This one
    // is Hyperborea's, whose api13-tw branch targets the same client, and it matches
    // exactly once there. That build also declares one more argument than upstream
    // AnoMech does — passing the extra one is harmless if the callee ignores it,
    // whereas omitting a real one leaves the callee reading stack garbage.
    [Signature("40 55 41 54 41 55 41 56 41 57 48 83 EC 60 4C 8B F1", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text, Fallibility = Fallibility.Fallible)]
    public static LoadZoneDelegate LoadZone = null!;

    public delegate nint LoadZoneDelegate(GameMain* thisPtr, uint territoryTypeId, int transitionTerritoryFilterKey, byte a4, byte a5, byte a6);

    public static void Initialize()
    {
        Plugin.GameInterop.InitializeFromAttributes(new GameMainPointers());
    }
}
