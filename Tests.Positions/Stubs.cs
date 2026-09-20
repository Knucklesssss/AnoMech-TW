using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;

sealed class Moves(Vector2?[] positions) : IAiMove { public Vector2? this[int slot] => positions[slot]; }
namespace AnoMech.Core.Game.Ai { public interface IAiMove { Vector2? this[int slot] { get; } } }
namespace AnoMech.Core.Game {
 public static class SimRandom {
  public static bool InHostOnly => false;
  public static void Disable() {}
  public static IDisposable HostOnly() => new Scope();
  sealed class Scope : IDisposable { public void Dispose() {} }
 }
}
namespace AnoMech.Core {
 public enum Sign { Attack1 }
 public static class Markings { public static void ClearAll() {} public static void Set(Sign sign, uint id) {} }
}
namespace AnoMech.Core.SimObjects {
 // Fake actors isolate native movement; AiManager and EventScheduler are production code.
 public sealed class SimCharacter { public Vector3 Position; public uint GameObjectId; public List<Vector3> Moves = new(); public bool IsAlive()=>true; public void MoveTo(Vector3 target)=>Moves.Add(target); }
 public sealed class SimParty {
  public SimCharacter[] Members = Enumerable.Range(0,8).Select(_=>new SimCharacter()).ToArray();
  public SimCharacter Get(int index)=>Members[index]; public SimCharacter Get(PartyRole role)=>Get((int)role);
  public void GiveInvuln(PartyRole role, float seconds) {}
 }
 public sealed class SimWorld(PracticePositions positions) { public EventScheduler Events = new(); public SimParty Party = new(); public PracticePositions PracticePositions = positions; }
}
