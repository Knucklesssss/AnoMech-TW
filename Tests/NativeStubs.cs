using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;

// Replace only the live game's characters and effects; AI, assignment and scheduler
// implementations are linked from production. Movement is optionally advanced at 6y/s,
// matching Movement.Tick with no obstacles or animation lock.
namespace AnoMech.Core.SimObjects
{
    public sealed class SimWorld
    {
        public SimParty Party { get; } = new();
        public EventScheduler Events { get; } = new();
        public float Elapsed { get; private set; }
        public List<SimEnemy> Enemies { get; } = [];
        public List<RecordedCast> Casts { get; } = [];
        public List<SimTether> Tethers { get; } = [];
        public SimTether Tether(SimCharacter? a, SimCharacter? b, ushort id, float duration = 0f, ushort debuffStatusId = 0)
        {
            var tether = new SimTether(a, b, id, debuffStatusId);
            if (duration > 0 && debuffStatusId != 0)
            {
                a?.AddStatus(debuffStatusId, duration);
                b?.AddStatus(debuffStatusId, duration);
            }
            Tethers.Add(tether);
            return tether;
        }

        public SimEnemy SpawnEnemy(EnemySpawnConfig config)
        {
            var enemy = new SimEnemy(this, config, (uint)(100 + Enemies.Count));
            Enemies.Add(enemy);
            return enemy;
        }

        public void Tick(float dt)
        {
            Elapsed += dt;
            Events.Tick(dt);
            foreach (var member in Party.ActiveMembers()) member.TickMovement(dt);
        }
    }

    public sealed class SimParty
    {
        public static SimParty Empty { get; } = new();
        public PartyRole PlayerRole => PartyRole.MainTank;
        private readonly SimCharacter[] members = Enumerable.Range(0, 8).Select(i => new SimCharacter { GameObjectId = (uint)(i + 1) }).ToArray();
        public SimCharacter? Get(int role) => members[role];
        public SimCharacter? Get(PartyRole role) => Get((int)role);
        public void GiveInvuln(PartyRole role, float seconds) { }
        public void WipeAllPlayers(string cause)
        {
            foreach (var member in members) member.Die(cause);
        }
        public IEnumerable<SimCharacter> ActiveMembers() => members.Where(member => member.IsAlive());
    }

    public sealed class SimCharacter
    {
        public Vector3 Position { get; set; }
        public uint GameObjectId { get; init; }
        public HashSet<ushort> Statuses { get; } = [];
        public Dictionary<ushort, int> StatusStacks { get; } = [];
        public bool SimulateMovement { get; set; }
        public string? DeathCause { get; private set; }
        private Vector3? destination;
        public bool HasStatus(ushort id) => Statuses.Contains(id);
        public void AddStatus(ushort id, float duration = 0, int stacks = 1)
        {
            Statuses.Add(id);
            StatusStacks[id] = StatusStacks.GetValueOrDefault(id) + stacks;
        }
        public TestStatus? FindStatus(ushort id) => Statuses.Contains(id) ? new(StatusStacks.GetValueOrDefault(id, 1)) : null;
        public sealed record TestStatus(int Stacks);
        public void RemoveStatus(ushort id) => Statuses.Remove(id);
        public bool IsAlive() => DeathCause == null;
        public void Die(string cause) => DeathCause ??= cause;
        public void MoveTo(Vector3 position)
        {
            if (SimulateMovement) destination = position;
            else Position = position;
        }
        public void TickMovement(float dt)
        {
            if (destination is not { } target) return;
            var delta = target - Position;
            if (delta.Length() <= 6f * dt)
            {
                Position = target;
                destination = null;
            }
            else Position += Vector3.Normalize(delta) * (6f * dt);
        }
    }

    public enum EnemyListMode { Never, Always }
    public sealed record EnemySpawnConfig(uint BNpcBaseId, uint NameId = 0, int Level = 1,
        bool Targetable = false, EnemyListMode EnemyList = EnemyListMode.Always,
        bool IsVisible = true, Placement? Placement = null, uint? ModelCharaId = null,
        float? Scale = null, float? HitboxRadius = null);
    public sealed record RecordedCast(float Time, uint ActionId, Vector3 SourcePosition,
        ulong? TargetId, float CastSeconds, float FireDelay);

    public sealed class SimEnemy(SimWorld world, EnemySpawnConfig config, uint id)
    {
        public bool Targetable => config.Targetable;
        public Vector3 Position => config.Placement!.Value.Position;
        public uint GameObjectId => id;
        public bool Visible { get; private set; } = config.IsVisible;
        public bool Despawned { get; private set; }
        public List<uint> Timelines { get; } = [];
        public void SetVisible(bool visible) => Visible = visible;
        public void PlayActionTimeline(uint id) => Timelines.Add(id);
        public void Despawn() => Despawned = true;
        public void Cast(uint actionId, Vector3? targetLocation = null, float castSeconds = 0f,
            ulong? targetId = null, float fireDelay = 0f) =>
            world.Casts.Add(new(world.Elapsed, actionId, Position, targetId, castSeconds, fireDelay));
    }

    public sealed class SimTether(SimCharacter? a, SimCharacter? b, ushort id, ushort status)
    {
        public SimCharacter? A => a;
        public SimCharacter? B => b;
        public ushort TetherId => id;
        public bool IsActive { get; private set; } = true;
        public bool Resolved { get; set; }
        public bool StretchGt(float distance) => a != null && b != null && Vector3.Distance(a.Position, b.Position) > distance;
        public bool StretchLt(float distance) => a != null && b != null && Vector3.Distance(a.Position, b.Position) < distance;
        public void Despawn()
        {
            IsActive = false;
            a?.RemoveStatus(status); b?.RemoveStatus(status);
        }
    }
}

namespace AnoMech.Core
{
    public enum Sign { Attack1, Attack2, Attack3, Attack4, Attack5, Bind1, Bind2, Bind3, Ignore1, Ignore2, Triangle, Cross, Attack6, Square }
    public static class Markings
    {
        public static void ClearAll() { }
        public static void Set(Sign sign, uint id) { }
    }
    public static class ChatOutput
    {
        public static void Coach(string text) { }
    }
}

namespace AnoMech
{
    public static class Plugin
    {
        public static TestLog Log { get; } = new();
        public sealed class TestLog { public void Info(string text) { } public void Warning(string text) { } }
    }
}

namespace AnoMech.Scenarios.Top
{
    internal static class HandPlacedSigns
    {
        public static RoleList Reorder(AnoMech.Core.SimObjects.SimParty party, RoleList fallback,
            IReadOnlyList<(AnoMech.Core.Sign Sign, int Slot)> plan,
            IReadOnlyCollection<PartyRole> keepInPlace) =>
            throw new NotSupportedException("Live player signs are outside these tests.");
    }
}

namespace AnoMech.Scenarios.Top.P3HelloWorld
{
    public sealed class TopP3HelloWorldState
    {
        public const int SlotCount = 4;
        private readonly PartyRole[] roles = [PartyRole.CasterDps, PartyRole.MainTank,
            PartyRole.PhysRangedDps, PartyRole.RegenHealer, PartyRole.MeleeDpsB,
            PartyRole.ShieldHealer, PartyRole.OffTank, PartyRole.MeleeDpsA];
        public PartyRole At(int slot, int member) => roles[slot * 2 + member];
        public bool TransitionFirstArmsSouth { get; set; }
        public bool TransitionAutoMarkers { get; set; }
        public PartyRole[]? TransitionRoleOverride { get; set; }
        public PartyRole[] TransitionRoles => TransitionRoleOverride ?? roles;
    }
}
