using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace AnoMech.Pointers;

internal unsafe class ModelContainerPointers
{
    // Not the E8 call pattern: that one resolves to EventObjectPointers.SetEventObjectState.
    [Signature("40 53 48 83 EC 20 48 8B D9 48 8B 49 08 48 8B 01 FF 50 10 83 F8 02", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text, Fallibility = Fallibility.Fallible)]
    public static CalculateUnscaledRadiusDelegate CalculateUnscaledRadius { get; private set; } = null!;

    public delegate float CalculateUnscaledRadiusDelegate(ModelContainer* modelContainer);

    public static void Initialize()
    {
        Plugin.GameInterop.InitializeFromAttributes(new ModelContainerPointers());
    }
}
