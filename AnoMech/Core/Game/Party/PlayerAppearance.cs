using System;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Lumina.Excel.Sheets;

namespace AnoMech.Core.Game.Party;

// A player's unmodded look (Customize, five armour pieces, both weapons) packed for the
// multiplayer wire, so friends see each other instead of the default doppels. Mods are not
// carried. Remote bytes pick the skeleton, and an invalid race/tribe/sex crashes the model
// load, so a blob that fails validation is dropped and the preset look is kept.
public static unsafe class PlayerAppearance
{
    private const int CustomizeBytes = 26;
    private const int ArmourSlots = 5;
    public const int Length = CustomizeBytes + ArmourSlots * 8 + 2 * 8 + 2 * 4;

    private static readonly DrawDataContainer.EquipmentSlot[] Armour =
    [
        DrawDataContainer.EquipmentSlot.Head, DrawDataContainer.EquipmentSlot.Body, DrawDataContainer.EquipmentSlot.Hands,
        DrawDataContainer.EquipmentSlot.Legs, DrawDataContainer.EquipmentSlot.Feet,
    ];

    // Multiplayer run only: per party slot, the owner's blob to dress that doppel in.
    public static byte[]?[] ForSlots { get; set; } = new byte[]?[8];

    public static byte[]? Capture()
    {
        if (Plugin.ObjectTable.LocalPlayer is not { } local) return null;
        var chara = (BattleChara*)local.Address;
        var blob = new byte[Length];
        var span = blob.AsSpan();
        new ReadOnlySpan<byte>(&chara->DrawData.CustomizeData, CustomizeBytes).CopyTo(span);
        var offset = CustomizeBytes;
        foreach (var slot in Armour)
            Put(span, ref offset, chara->DrawData.Equipment(slot).Value);
        Put(span, ref offset, chara->DrawData.Weapon(DrawDataContainer.WeaponSlot.MainHand).ModelId.Value);
        Put(span, ref offset, chara->DrawData.Weapon(DrawDataContainer.WeaponSlot.OffHand).ModelId.Value);
        BitConverter.TryWriteBytes(span[offset..], chara->Height);
        BitConverter.TryWriteBytes(span[(offset + 4)..], chara->VfxScale);
        return IsValid(blob) ? blob : null;
    }

    public static bool IsValid(byte[]? blob)
    {
        if (blob is not { Length: Length }) return false;
        var height = BitConverter.ToSingle(blob, Length - 8);
        var vfxScale = BitConverter.ToSingle(blob, Length - 4);
        return blob[1] <= 1
               && Plugin.DataManager.GetExcelSheet<Race>().HasRow(blob[0]) && blob[0] != 0
               && Plugin.DataManager.GetExcelSheet<Tribe>().HasRow(blob[4]) && blob[4] != 0
               && float.IsFinite(height) && height is > 0f and < 10f
               && float.IsFinite(vfxScale) && vfxScale is > 0f and < 10f;
    }

    // Only before the doppel is first drawn: a race change swaps the skeleton.
    public static void Apply(BattleChara* chara, byte[] blob)
    {
        var span = blob.AsSpan();
        span[..CustomizeBytes].CopyTo(new Span<byte>(&chara->DrawData.CustomizeData, CustomizeBytes));
        var offset = CustomizeBytes;
        foreach (var slot in Armour)
            chara->DrawData.Equipment(slot).Value = Take(span, ref offset);
        chara->DrawData.Weapon(DrawDataContainer.WeaponSlot.MainHand).ModelId.Value = Take(span, ref offset);
        chara->DrawData.Weapon(DrawDataContainer.WeaponSlot.OffHand).ModelId.Value = Take(span, ref offset);
        chara->Height = BitConverter.ToSingle(span[offset..]);
        chara->VfxScale = BitConverter.ToSingle(span[(offset + 4)..]);
    }

    private static void Put(Span<byte> span, ref int offset, ulong value)
    {
        BitConverter.TryWriteBytes(span[offset..], value);
        offset += 8;
    }

    private static ulong Take(ReadOnlySpan<byte> span, ref int offset)
    {
        var value = BitConverter.ToUInt64(span[offset..]);
        offset += 8;
        return value;
    }
}
