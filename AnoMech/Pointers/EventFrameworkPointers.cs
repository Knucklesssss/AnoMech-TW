using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Event;

namespace AnoMech.Pointers;

internal unsafe class EventFrameworkPointers
{
    [Signature("E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? E8 ?? ?? ?? ?? 8B 54 24 70 48 8B C8 E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? E8 ?? ?? ?? ?? 0F", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text, Fallibility = Fallibility.Fallible)]
    public static InitDirectorDelegate InitDirector { get; private set; } = null!;

    [Signature("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 48 83 EC 70 48 8D B1", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text, Fallibility = Fallibility.Fallible)]
    public static TerminateDirectorDelegate TerminateDirector { get; private set; } = null!;

    [Signature("89 54 24 10 48 89 4C 24 ?? 53 56 57 41 55 41 57 48 83 EC 30 48 8B 99", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text, Fallibility = Fallibility.Fallible)]
    public static SetDirectorDataDelegate SetDirectorData { get; private set; } = null!;

    // Upstream's 7.5 pattern, which finds nothing in the API13 binary — so this stays
    // null and InstanceContentDirectorHelper skips the call.
    //
    // Do not "fix" this by re-deriving from the 7.5 pattern's tail. That was tried:
    // its distinctive fragment (stack cookie then `mov edi, r9d`) does occur exactly
    // once here, and anchoring a pattern at the enclosing function gives a unique
    // match — but the function is a different one, and calling it killed the game
    // from Commence(). A unique match is not evidence of the right function.
    [Signature("40 53 57 48 83 EC ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 44 24 ?? 41 8B F9", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text, Fallibility = Fallibility.Fallible)]
    public static ProcessDirectorUpdateDelegate ProcessDirectorUpdate { get; private set; } = null!;

    public delegate void ProcessDirectorUpdateDelegate(EventFramework* thisPtr, EventId eventId, uint category, uint arg1, uint arg2, uint arg3, uint arg4, uint arg5, uint arg6);
    public delegate void InitDirectorDelegate(EventFramework* thisPtr, EventId eventId, uint contentId, uint flags);
    public delegate void TerminateDirectorDelegate(EventFramework* thisPtr, EventId eventId);
    public delegate void SetDirectorDataDelegate(EventFramework* thisPtr, EventId eventId, byte sequence, byte unknown, byte* unionDataBuffer, ulong length);

    public static void Initialize()
    {
        Plugin.GameInterop.InitializeFromAttributes(new EventFrameworkPointers());
    }
}
