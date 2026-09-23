using System.Collections.Generic;
using System.Linq;
using AnoMech.Pointers;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace AnoMech.Core.Map;

// Housing interiors can occupy all 40 EventObject slots, and a client-side zone
// load never clears them, so scenario objects (towers) had nowhere to spawn.
// A released object only returns when the player re-enters the house (the server
// resends it; respawning it ourselves after the house reloads failed for every
// object). So release them one at a time, only when a spawn finds no free slot:
// scenarios that spawn no event objects leave the house intact (verified in game:
// all 40 slots unchanged after a P6 run).
internal static unsafe class HousingEventObjectSlots
{
    // Slot -> BaseId of each house object present when the session began.
    private static readonly Dictionary<byte, uint> houseObjects = new();

    public static void Remember(bool isHousing)
    {
        Forget();
        var manager = EventObjectManager.Instance();
        if (!isHousing || manager == null) return;
        for (byte i = 0; i < 40; i++)
            if (manager->EventObjects[i] != null)
                houseObjects[i] = manager->EventObjects[i].Value->BaseId;
    }

    public static void Forget() => houseObjects.Clear();

    // Frees one house object's slot, taking the most-repeated furniture first so
    // one-of-a-kind objects (door, summoning bell, message book) go last. The
    // BaseId check skips a slot something else has taken since, such as a
    // scenario's own object.
    public static bool TryReleaseOne(EventObjectManager* manager, out byte slot)
    {
        var copies = houseObjects.Values.GroupBy(id => id).ToDictionary(g => g.Key, g => g.Count());
        foreach (var (candidate, baseId) in houseObjects
                     .OrderByDescending(p => copies[p.Value]).ThenByDescending(p => p.Key).ToArray())
        {
            houseObjects.Remove(candidate);
            var obj = manager->EventObjects[candidate];
            if (obj == null || obj.Value->BaseId != baseId) continue;
            var target = candidate;
            PacketDispatcherPointers.HandleDespawnObjectPacket(0, &target);
            if (manager->EventObjects[candidate] != null) continue;
            if (Plugin.LogManager.Enabled)
                Plugin.LogManager.LogHousing($"released slot {candidate} base=0x{baseId:X} ({copies[baseId]} alike)");
            slot = candidate;
            return true;
        }
        slot = 0;
        return false;
    }
}
