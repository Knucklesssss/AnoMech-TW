using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace AnoMech.Pointers;

internal unsafe class EventObjectManagerPointers
{
    [Signature("48 89 5C 24 ?? 48 89 6C 24 ?? 57 41 54 41 57 48 83 EC ?? 8B", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text, Fallibility = Fallibility.Fallible)]
    public static CreateEventObjectDelegate CreateEventObject { get; private set; } = null!;

    // Upstream's pattern is four bytes long. On the API13 binary it matches three
    // times, and the first hit lands one byte into a `cmp dx, 27h` — Dalamud takes
    // that one, so the delegate pointed at the middle of an unrelated instruction
    // and would have executed garbage rather than failing loudly. Extended to cover
    // the whole (tiny) function: bounds-check 39, index the array at +0x10, return.
    [Signature("83 FA 27 77 09 48 63 C2 48 8B 44 C1 10 C3 33 C0 C3", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text, Fallibility = Fallibility.Fallible)]
    public static GetEventObjectByIndexDelegate GetEventObjectByIndex { get; private set; } = null!;

    public delegate int CreateEventObjectDelegate(EventObjectManager* thisPtr, uint entityId, uint eObjId, ulong a4, uint layoutId, uint gimmickId, int objectIndex, byte flag);
    public delegate GameObject* GetEventObjectByIndexDelegate(EventObjectManager* eventObjectManager, uint index);

    public static void Initialize()
    {
        Plugin.GameInterop.InitializeFromAttributes(new EventObjectManagerPointers());
    }
}
