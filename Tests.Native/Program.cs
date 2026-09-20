using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using AnoMech.Core.Native;
using AnoMech.Core.SimObjects;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

unsafe
{
    BattleChara player = default;
    AnoMech.Plugin.ObjectTable.LocalPlayer = new() { Address = (nint)(&player) };
    var status = new SimStatus(new SimCharacter { BattleCharaPtr = &player }, 123, 9f, 2);
    Check(StatusManager.AddCalls == 0, "Local player must bypass native AddStatus and its orphan VFX.");
    Check(AnoMech.Pointers.StatusManagerPointers.GainCalls == 1, "Player must gain exactly one effect.");
    Check(AnoMech.Pointers.StatusManagerPointers.GainDuration == 9f, "OnGain must see the requested duration immediately.");
    Check(player.StatusManager.Status[0].RemainingTime == 9f, "Slot must be initialized before first Tick.");
    Check(StatusManager.FlagRefreshCalls == 1, "Player gain must refresh native buff flags.");
    Statuses.AddStatusInit((Character*)(&player), 123, 3, 9f);
    Check(AnoMech.Pointers.StatusManagerPointers.GainCalls == 1, "Refresh must not replay gain VFX.");
    status.Tick(1f);
    Check(player.StatusManager.Status[0].RemainingTime == 8f, "Simulated countdown must remain authoritative.");
    player.StatusManager.Status[0] = default;
    status.Tick(1f);
    Check(status.IsActive && player.StatusManager.Status[0].RemainingTime == 7f,
        "A missing native slot must be restored for the remaining simulated lifetime.");
    status.Despawn();
    Check(player.StatusManager.GetStatusIndex(123) == -1, "Despawn must clear the status.");
    Check(StatusManager.FlagRefreshCalls == 2, "Player removal must refresh flags to stop buff effects.");
    BattleChara boss = default;
    var transformation = new SimStatus(new SimCharacter { BattleCharaPtr = &boss }, 456, 0f, 490);
    Check(StatusManager.AddCalls == 1, "NPC transformations still need native AddStatus.");
    Check(boss.StatusManager.Status[0].Param == 490, "Transformation parameter must survive.");
    transformation.Despawn();
    Statuses.Apply((Character*)(&player), 789, 12f, 1, new GameObjectId { ObjectId = 11 });
    Statuses.Apply((Character*)(&player), 789, 6f, 2, new GameObjectId { ObjectId = 22 });
    Check(player.StatusManager.Status[0].SourceObject.ObjectId == 11 && player.StatusManager.Status[1].SourceObject.ObjectId == 22,
        "The same buff from two casters must keep separate slots.");
    Statuses.Apply((Character*)(&player), 789, 5f, 3);
    Check(player.StatusManager.Status[0].SourceObject.ObjectId == 11,
        "Legacy ID-only refresh must preserve an existing source.");
    var gainCalls = AnoMech.Pointers.StatusManagerPointers.GainCalls;
    Statuses.AddStatusInit((Character*)(&player), 789, 4, 3f, new GameObjectId { ObjectId = 22 });
    Check(player.StatusManager.Status[0].RemainingTime == 5f && player.StatusManager.Status[1].RemainingTime == 3f
        && player.StatusManager.Status[1].Param == 4 && AnoMech.Pointers.StatusManagerPointers.GainCalls == gainCalls,
        "Exact-source refresh must preserve the other caster and avoid replaying gain effects.");
    Statuses.Remove((Character*)(&player), 789, new GameObjectId { ObjectId = 22 });
    Check(Statuses.Has((Character*)(&player), 789, new GameObjectId { ObjectId = 11 })
        && !Statuses.Has((Character*)(&player), 789, new GameObjectId { ObjectId = 22 }),
        "Removing one caster's buff must preserve the other's.");
    Statuses.AddStatusInit((Character*)(&boss), 790, 1, 10f, new GameObjectId { ObjectId = 11 });
    var nativeAdds = StatusManager.AddCalls;
    Statuses.AddStatusInit((Character*)(&boss), 790, 2, 4f, new GameObjectId { ObjectId = 22 });
    Check(StatusManager.AddCalls == nativeAdds && boss.StatusManager.Status[0].SourceObject.ObjectId == 11
        && boss.StatusManager.Status[1].SourceObject.ObjectId == 22,
        "NPC gain must not let native ID-only insertion overwrite another caster.");
    var tankBuff = new SimStatus(new SimCharacter { BattleCharaPtr = &player }, 791, 9f, 0,
        new GameObjectId { ObjectId = 22 });
    tankBuff.Tick(1f);
    Check(Statuses.Has((Character*)(&player), 791, new GameObjectId { ObjectId = 22 }),
        "Simulated party buffs must retain the caster through ticks.");
    Statuses.Apply((Character*)(&player), 791, 6f, 0, new GameObjectId { ObjectId = 11 });
    tankBuff.Despawn();
    Check(!Statuses.Has((Character*)(&player), 791, new GameObjectId { ObjectId = 22 })
        && Statuses.Has((Character*)(&player), 791, new GameObjectId { ObjectId = 11 }),
        "Simulated party buff expiry must remove only its caster's copy.");
    for (int i = 0; i < player.StatusManager.Status.Length; i++)
        player.StatusManager.Status[i] = new Status { StatusId = 800, SourceObject = new GameObjectId { ObjectId = 11 } };
    gainCalls = AnoMech.Pointers.StatusManagerPointers.GainCalls;
    Statuses.AddStatusInit((Character*)(&player), 800, 1, 9f, new GameObjectId { ObjectId = 22 });
    Check(AnoMech.Pointers.StatusManagerPointers.GainCalls == gainCalls
        && !Statuses.Has((Character*)(&player), 800, new GameObjectId { ObjectId = 22 }),
        "A full array must not overwrite another caster or play an orphan gain effect.");
    Console.WriteLine("PASS: buff flags, source isolation, full slots, player VFX, NPC transformation and authoritative lifetime.");
}
static void Check(bool value, string message) { if (!value) throw new Exception(message); }

namespace AnoMech
{
    public static class Plugin { public static ObjectTable ObjectTable { get; } = new(); }
    public sealed class ObjectTable { public Player? LocalPlayer { get; set; } }
    public sealed class Player { public nint Address { get; set; } }
}
namespace AnoMech.Core.SimObjects
{
    public unsafe class SimCharacter { public BattleChara* BattleCharaPtr; }
    public interface ISimObject { bool IsActive { get; } void Tick(float delta); void Despawn(); }
}
namespace FFXIVClientStructs.FFXIV.Client.Game.Object
{
    public record struct GameObjectId { public uint ObjectId; }
}
namespace FFXIVClientStructs.FFXIV.Client.Game.Character
{
    public struct Character { public byte Placeholder; }
    public struct BattleChara { public StatusManager StatusManager; }
}
namespace FFXIVClientStructs.FFXIV.Client.Game
{
    public struct Status { public ushort StatusId, Param; public float RemainingTime; public GameObjectId SourceObject; }
    [InlineArray(60)] public struct StatusSlots { private Status first; }
    public struct StatusManager
    {
        private StatusSlots slots;
        public byte NumValidStatuses;
        public static int AddCalls, FlagRefreshCalls;
        public Span<Status> Status => MemoryMarshal.CreateSpan(ref slots[0], 60);
        public void AddStatus(ushort id, ushort param)
        {
            AddCalls++;
            Status[0] = new Status { StatusId = id, Param = param };
            NumValidStatuses = 1;
        }
        public int GetStatusIndex(ushort id)
        {
            for (var i = 0; i < 60; i++) if (Status[i].StatusId == id) return i;
            return -1;
        }
        public void SetStatus(int index, ushort id, float duration, ushort param, GameObjectId source, bool refreshFlags)
        {
            if (refreshFlags) FlagRefreshCalls++;
            Status[index] = new Status { StatusId = id, RemainingTime = duration, Param = param, SourceObject = source };
        }
        public void RemoveStatus(int index) => Status[index] = default;
    }
}
namespace AnoMech.Pointers
{
    public static unsafe class StatusManagerPointers
    {
        public static int GainCalls;
        public static float GainDuration;
        public static void OnGainStatus(StatusManager* manager, ushort id, float duration, ushort param, uint source, byte flags)
        {
            GainCalls++;
            GainDuration = duration;
            if (manager->GetStatusIndex(id) < 0) throw new Exception("Gain fired before slot insertion.");
        }
    }
}
