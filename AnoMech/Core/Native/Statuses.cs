using System;
using System.Runtime.InteropServices;
using AnoMech.Pointers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace AnoMech.Core.Native;

// Status helpers for sim-only buffs. Apply writes directly into the
// StatusManager.Status array — bypasses bc->StatusManager.AddStatus, which
// drives _flags / ExtraFlags from the Status sheet AND auto-prunes any status
// whose sheet MaxDuration is 0 (most sim-only ids). Direct-slot insertion lets
// any statusId stick at the duration we ask for, with no sheet dependency.
//
// Trade-off: _flags / ExtraFlags don't get the sheet-driven bit set. That bit
// vector controls "is this entity stunned/silenced/etc" gameplay flags; for
// purely visual debuffs (tether markers, raid buffs we just want shown) this
// doesn't matter. If a future caller needs gameplay-effective flags, route
// that one through bc->StatusManager.AddStatus instead.
internal static unsafe class Statuses
{
    public static void Apply(Character* chara, ushort statusId, float duration, ushort param = 0, GameObjectId? sourceObject = null)
    {
        if (chara == null || statusId == 0) return;
        var bc = (BattleChara*)chara;
        var slots = bc->StatusManager.Status;

        // Refresh in place if the status is already present (matches sheet
        // behavior of "reapply replaces" without needing a Remove + re-Add).
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].StatusId != statusId) continue;
            if (sourceObject is {} source && slots[i].SourceObject != source) continue;
            slots[i].Param = param;
            slots[i].RemainingTime = duration == 0 ? 20: duration;
            if (sourceObject is {} explicitSource) slots[i].SourceObject = explicitSource;
            return;
        }

        // Otherwise drop into the first empty slot. Keep the array packed at
        // low indices — see Remove's comment for why that matters to PartyHud.
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].StatusId != 0) continue;
            slots[i].StatusId = statusId;
            slots[i].Param = param;
            slots[i].RemainingTime = duration == 0 ? 20: duration;
            slots[i].SourceObject = sourceObject.GetValueOrDefault();
            if (bc->StatusManager.NumValidStatuses <= i)
                bc->StatusManager.NumValidStatuses = (byte)(i + 1);
            return;
        }
    }

    // NPCs need native AddStatus for form swaps. For the local player, insert
    // the slot before OnGainStatus so the effect receives its real lifetime.
    public static void AddStatusInit(Character* chara, ushort statusId, ushort param, float duration = 0f, GameObjectId? sourceObject = null)
    {
        if (chara == null || statusId == 0) return;
        var bc = (BattleChara*)chara;
        var isLocalPlayer = Plugin.ObjectTable.LocalPlayer?.Address == (nint)chara;
        if ((isLocalPlayer || sourceObject.HasValue) && FindSlot(bc, statusId, sourceObject) >= 0)
        {
            Apply(chara, statusId, duration, param, sourceObject);
            return;
        }

        // Native AddStatus matches by ID only; never let it replace another caster's copy.
        if (!isLocalPlayer && (!sourceObject.HasValue || bc->StatusManager.GetStatusIndex(statusId) < 0))
        {
            bc->StatusManager.AddStatus(statusId, param);
            var addedSlot = bc->StatusManager.GetStatusIndex(statusId);
            if (addedSlot >= 0 && sourceObject is {} source)
                bc->StatusManager.Status[addedSlot].SourceObject = source;
        }

        var slot = FindSlot(bc, statusId, sourceObject);
        if (slot >= 0)
        {
            if (duration != 0f) bc->StatusManager.Status[slot].RemainingTime = duration;
            return;
        }

        Apply(chara, statusId, duration, param, sourceObject);
        // A full status array cannot accept the effect either.
        slot = FindSlot(bc, statusId, sourceObject);
        if (slot >= 0)
        {
            // Direct insertion skips the sheet flags used by player buff effects.
            if (isLocalPlayer)
                bc->StatusManager.SetStatus(slot, statusId, bc->StatusManager.Status[slot].RemainingTime, param,
                    bc->StatusManager.Status[slot].SourceObject, true);
            StatusManagerPointers.OnGainStatus(&bc->StatusManager, statusId, duration, param, 0, 0);
        }
    }

    public static bool Has(Character* chara, ushort statusId, GameObjectId sourceObject)
        => chara != null && statusId != 0 && FindSlot((BattleChara*)chara, statusId, sourceObject) >= 0;

    private static int FindSlot(BattleChara* bc, ushort statusId, GameObjectId? sourceObject)
    {
        var slots = bc->StatusManager.Status;
        for (int i = 0; i < slots.Length; i++)
            if (slots[i].StatusId == statusId && (sourceObject is not {} source || slots[i].SourceObject == source))
                return i;
        return -1;
    }

    public static void Remove(Character* chara, ushort statusId, GameObjectId? sourceObject = null)
    {
        if (chara == null || statusId == 0) return;
        var bc = (BattleChara*)chara;
        var slot = FindSlot(bc, statusId, sourceObject);
        if (slot >= 0 && slot <= bc->StatusManager.NumValidStatuses)
        {
            if (Plugin.ObjectTable.LocalPlayer?.Address == (nint)chara)
                bc->StatusManager.SetStatus(slot, 0, 0f, 0, default, true);
            if (bc->StatusManager.Status[slot].StatusId == statusId)
                bc->StatusManager.RemoveStatus(slot);
        }
    }
}
