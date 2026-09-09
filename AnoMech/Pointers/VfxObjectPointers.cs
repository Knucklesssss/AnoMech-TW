using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;

namespace AnoMech.Pointers;

// The API13 ClientStructs binds VfxObject's fields but none of its functions, so
// the StaticVfxCreate/Run pair is scanned here. Signatures are upstream CS's.
// CleanupRender (StaticVfxRemove) is Object's vfunc 1 and is called through the
// vtable instead — no signature to go stale.
internal unsafe class VfxObjectPointers
{
    [Signature("E8 ?? ?? ?? ?? F3 0F 10 35 ?? ?? ?? ?? 48 89 43 08", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text, Fallibility = Fallibility.Fallible)]
    public static CreateDelegate Create { get; private set; } = null!;

    [Signature("E8 ?? ?? ?? ?? ?? ?? ?? 8B 4A ?? 85 C9", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text, Fallibility = Fallibility.Fallible)]
    public static UpdateDelegate Update { get; private set; } = null!;

    public delegate VfxObject* CreateDelegate(byte* vfxGamePath, byte* poolName);
    public delegate void UpdateDelegate(VfxObject* thisPtr, float deltaSeconds, int flags);

    public static void CleanupRender(VfxObject* vfx)
    {
        var vtbl = *(nint**)vfx;
        ((delegate* unmanaged<VfxObject*, void>)vtbl[1])(vfx);
    }

    // 0x248 upstream; unnamed in the API13 snapshot, which does name the fields
    // that bracket it (Flags 0x88, VfxResourceInstance 0x2A0) at matching offsets.
    public static ref byte SomeFlags(VfxObject* vfx) => ref *((byte*)vfx + 0x248);

    public static void Initialize()
    {
        Plugin.GameInterop.InitializeFromAttributes(new VfxObjectPointers());
    }
}
