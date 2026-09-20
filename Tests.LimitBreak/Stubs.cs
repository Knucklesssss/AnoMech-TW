using System.Numerics;
using AnoMech.Core.Game.Party;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
namespace FFXIVClientStructs.FFXIV.Client.Game.Object { public struct GameObjectId { public uint ObjectId; } }
namespace FFXIVClientStructs.FFXIV.Client.Game.Character {
 public struct CastInfo { public bool IsCasting; public float CurrentCastTime, TotalCastTime; }
 public struct BattleChara { public byte ClassJob; public CastInfo CastInfo; }
}
namespace FFXIVClientStructs.FFXIV.Client.Game.UI {
 public unsafe struct LimitBreakController { public byte BarCount; public ushort BarUnits, CurrentUnits; public static class StaticAddressPointers { public static LimitBreakController* pInstance; } }
}
namespace Lumina.Excel.Sheets {
 public struct Row { public uint RowId; }
 public struct ClassJob { public Row LimitBreak3 => new() { RowId=199 }; }
 public struct Action { public Row ActionCategory => new() { RowId=9 }; public bool TargetArea => false; public int EffectRange => 30; public int XAxisModifier => 0; public int CastType => 2; }
}
namespace AnoMech {
 public static class Plugin { public static ClientState ClientState = new(); public static DataManager DataManager = new(); }
 public class ClientState { public bool IsPvP; }
 public class DataManager { public Sheet<T> GetExcelSheet<T>() where T:struct => new(); }
 public class Sheet<T> where T:struct { public bool TryGetRow(uint id, out T row) { row = new(); return id is 19 or 199; } public T GetRow(uint id) => new(); }
}
namespace AnoMech.Core.Combat {
 public class LocalCombatSession { public bool Active = true; public uint CastingAction; public void MirrorPracticeCast(uint id,float elapsed,float total,GameObjectId target) {} }
}
namespace AnoMech.Core.SimObjects {
 public unsafe class SimCharacter { public BattleChara* BattleCharaPtr; public bool IsActive=true; public bool Alive=true; public Vector3 Position; public GameObjectId GameObjectId; public bool IsAlive()=>Alive; public void AddStatusParam(ushort id,int param,float duration,GameObjectId source) {} }
 public class SimPlayer:SimCharacter { public void SyncInputLock() {} }
 public class SimParty { public SimCharacter? Member; public SimCharacter? Get(PartyRole role)=>Member; public IEnumerable<SimCharacter> ActiveMembers(){ if(Member!=null) yield return Member; } }
 public class SimWorld { public SimParty Party = new(); public object Coordinates = new(); public AnoMech.Core.Combat.LocalCombatSession? Combat; }
 // Native boundaries are faked: lifecycle/reservation/recovery under test are the production runtime.
 public unsafe class SimCast(SimCharacter caster, object coordinates) {
  private readonly object coordinates = coordinates;
  public uint ActionId; private float recovery; public bool IsCasting=>caster.BattleCharaPtr->CastInfo.IsCasting; public bool IsBusy=>IsCasting||recovery>0;
  public bool Start(uint id, Vector3? location,float? castTime,GameObjectId? targetId,float omenDelay,float omenRotate,byte animationVariation,float animationLock) { ActionId=id; caster.BattleCharaPtr->CastInfo=new(){IsCasting=true,TotalCastTime=2}; recovery=animationLock; return true; }
  public void Tick(float delta) { if(ActionId==0) { recovery=Math.Max(0,recovery-delta); return; } if(caster.BattleCharaPtr->CastInfo.CurrentCastTime>=2) { ActionId=0; caster.BattleCharaPtr->CastInfo.IsCasting=false; } }
  public void Despawn(){ActionId=0;caster.BattleCharaPtr->CastInfo.IsCasting=false;}
 }
}

namespace AnoMech.Core.Combat { internal unsafe class PracticeLimitBreakNativeCast { internal void Clear() {} internal void Mirror(BattleChara* caster, uint action, float elapsed, float total, GameObjectId target) {} } }
