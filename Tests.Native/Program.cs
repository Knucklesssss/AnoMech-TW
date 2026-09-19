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
    Statuses.AddStatusInit((Character*)(&player), 123, 3, 9f);
    Check(AnoMech.Pointers.StatusManagerPointers.GainCalls == 1, "Refresh must not replay gain VFX.");
    status.Tick(1f);
    Check(player.StatusManager.Status[0].RemainingTime == 8f, "Simulated countdown must remain authoritative.");
    status.Despawn();
    Check(player.StatusManager.GetStatusIndex(123) == -1, "Despawn must clear the status.");
    BattleChara boss = default;
    var transformation = new SimStatus(new SimCharacter { BattleCharaPtr = &boss }, 456, 0f, 490);
    Check(StatusManager.AddCalls == 1, "NPC transformations still need native AddStatus.");
    Check(boss.StatusManager.Status[0].Param == 490, "Transformation parameter must survive.");
    transformation.Despawn();
    Console.WriteLine("PASS: status initialization order, local-player VFX, NPC transformation and countdown.");
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
    public struct GameObjectId { public uint ObjectId; }
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
        public static int AddCalls;
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
