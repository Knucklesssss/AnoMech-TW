using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace AnoMech.Core.Native;

// The API13 ClientStructs exposes these as read-only computed properties, so the
// backing bits are written directly. Masks are taken from the getters in the same
// CS revision the TC Dalamud ships (e95eb0de7) — keep them in sync if that moves.
internal static unsafe class CharacterFlags
{
    public static void SetHostile(Character* c, bool on) => Set(ref c->CharacterData.Flags, 0x01, on);
    public static void SetInCombat(Character* c, bool on) => Set(ref c->CharacterData.Flags, 0x02, on);
    public static void SetPartyMember(Character* c, bool on) => Set(ref c->RelationFlags, 0x01, on);
    public static void SetAllianceMember(Character* c, bool on) => Set(ref c->RelationFlags, 0x02, on);
    public static void SetFriend(Character* c, bool on) => Set(ref c->RelationFlags, 0x04, on);
    public static void SetOffhandDrawn(Character* c, bool on) => Set(ref c->WeaponFlags, 0x01, on);
    public static void SetWeaponDrawn(Character* c, bool on) => Set(ref c->Timeline.Flags3, 0x40, on);

    private static void Set(ref byte flags, byte mask, bool on)
        => flags = (byte)(on ? flags | mask : flags & ~mask);
}
