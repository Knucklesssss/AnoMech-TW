using AnoMech.Pointers;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace AnoMech.Core.Map;

// Housing interiors can occupy all 40 EventObject slots, and a client-side zone
// load never clears them, so scenario objects (towers) had nowhere to spawn.
// Respawning them right after the house reloads failed for every object, so
// they only return when the player re-enters the house (the server resends them).
internal static unsafe class HousingEventObjectSlots
{
    public static void Release()
    {
        var manager = EventObjectManager.Instance();
        if (manager == null) return;
        var released = 0;
        for (byte i = 0; i < 40; i++)
        {
            if (manager->EventObjects[i] == null) continue;
            var slot = i;
            PacketDispatcherPointers.HandleDespawnObjectPacket(0, &slot);
            released++;
        }
        Plugin.Log.Info($"[HousingEventObjectSlots] Released {released} housing event objects to free slots.");
    }
}
