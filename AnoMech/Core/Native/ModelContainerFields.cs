using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace AnoMech.Core.Native;

// ModelScaleId (0x21) and ModeAttributeFlags (0x22) are unnamed in the API13
// ClientStructs snapshot. The fields bracketing them — ModelSkeletonId_2 at 0x1C
// and UnscaledRadius at 0x24 — sit at the same offsets as upstream, so the
// layout in between carries over.
internal static unsafe class ModelContainerFields
{
    public static ref byte ModelScaleId(ModelContainer* mc) => ref *((byte*)mc + 0x21);
    public static ref byte ModeAttributeFlags(ModelContainer* mc) => ref *((byte*)mc + 0x22);
}
