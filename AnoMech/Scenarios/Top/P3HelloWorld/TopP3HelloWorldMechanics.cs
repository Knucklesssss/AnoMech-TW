using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Top.TopConstants;

namespace AnoMech.Scenarios.Top.P3HelloWorld;

public sealed class TopP3HelloWorldMechanics(SimWorld world, TopP3HelloWorldState state)
{
    // Contact is center-to-center in the simulation, not the actors' rendered models.
    private const float ContactRadius = 3f;
    private const float PoisonDuration = 27f;
    private const float PoisonExplosionRadius = 6f;
    private readonly ushort[] poison = new ushort[8];
    private readonly float[] poisonUntil = new float[8];
    private readonly float[] towerUntil = new float[8];
    private readonly ushort[] towerDefect = new ushort[8];
    private readonly List<(SimTether Tether, float Until)> tethers = [];
    private float elapsed;
    private float breakVulnerabilityUntil;

    public void Run()
    {
        world.Events.Add(42.26f, ApplyInitialPoison);
        RunTethers();
        world.Events.Add(63.38f, () => ResolveTowers(0));
        world.Events.Add(84.43f, () => ResolveTowers(1));
        world.Events.Add(105.50f, () => ResolveTowers(2));
        world.Events.Add(126.57f, () => ResolveTowers(3));
    }

    private void RunTethers()
    {
        world.Events.Add(42.26f, () => Tether(world.Party.Get(state.At(3, 0))!, world.Party.Get(state.At(3, 1))!, TetherId.HWPrepLocal));
        world.Events.Add(42.26f, () => Tether(world.Party.Get(state.At(2, 0))!, world.Party.Get(state.At(2, 1))!, TetherId.HWPrepRemote));
        world.Events.Add(62.26f, () => Tether(world.Party.Get(state.At(3, 0))!, world.Party.Get(state.At(3, 1))!, TetherId.HWLocal, duration: 10.000f, debuffStatusId: StatusId.HWLocalTether));
        world.Events.Add(62.26f, () => Tether(world.Party.Get(state.At(2, 0))!, world.Party.Get(state.At(2, 1))!, TetherId.HWRemote, duration: 10.000f, debuffStatusId: StatusId.HWRemoteTether));
        world.Events.Add(63.24f, () => Tether(world.Party.Get(state.At(1, 0))!, world.Party.Get(state.At(1, 1))!, TetherId.HWPrepRemote, debuffStatusId: StatusId.RepairedDefectOverflow));
        world.Events.Add(63.24f, () => Tether(world.Party.Get(state.At(0, 0))!, world.Party.Get(state.At(0, 1))!, TetherId.HWPrepLocal, debuffStatusId: StatusId.RepairedDefectShared));
        world.Events.Add(83.22f, () => Tether(world.Party.Get(state.At(1, 0))!, world.Party.Get(state.At(1, 1))!, TetherId.HWRemote, duration: 10.000f, debuffStatusId: StatusId.HWRemoteTether));
        world.Events.Add(83.22f, () => Tether(world.Party.Get(state.At(0, 0))!, world.Party.Get(state.At(0, 1))!, TetherId.HWLocal, duration: 10.000f, debuffStatusId: StatusId.HWLocalTether));
        world.Events.Add(84.25f, () => Tether(world.Party.Get(state.At(3, 0))!, world.Party.Get(state.At(3, 1))!, TetherId.HWPrepRemote, debuffStatusId: StatusId.RepairedDefectOverflow));
        world.Events.Add(84.25f, () => Tether(world.Party.Get(state.At(2, 0))!, world.Party.Get(state.At(2, 1))!, TetherId.HWPrepLocal, debuffStatusId: StatusId.RepairedDefectShared));
        world.Events.Add(104.25f, () => Tether(world.Party.Get(state.At(3, 0))!, world.Party.Get(state.At(3, 1))!, TetherId.HWRemote, duration: 10.000f, debuffStatusId: StatusId.HWRemoteTether));
        world.Events.Add(104.25f, () => Tether(world.Party.Get(state.At(2, 0))!, world.Party.Get(state.At(2, 1))!, TetherId.HWLocal, duration: 10.000f, debuffStatusId: StatusId.HWLocalTether));
        world.Events.Add(105.23f, () => Tether(world.Party.Get(state.At(1, 0))!, world.Party.Get(state.At(1, 1))!, TetherId.HWPrepLocal));
        world.Events.Add(105.23f, () => Tether(world.Party.Get(state.At(0, 0))!, world.Party.Get(state.At(0, 1))!, TetherId.HWPrepRemote));
        world.Events.Add(125.23f, () => Tether(world.Party.Get(state.At(1, 0))!, world.Party.Get(state.At(1, 1))!, TetherId.HWLocal, duration: 10.000f, debuffStatusId: StatusId.HWLocalTether));
        world.Events.Add(125.23f, () => Tether(world.Party.Get(state.At(0, 0))!, world.Party.Get(state.At(0, 1))!, TetherId.HWRemote, duration: 10.000f, debuffStatusId: StatusId.HWRemoteTether));
    }

    private void ApplyInitialPoison()
    {
        for (var member = 0; member < 2; member++)
        {
            Infect((int)state.At(0, member), StatusId.CriticalErrorUnderflow);
            Infect((int)state.At(1, member), StatusId.CriticalErrorPerformance);
        }
    }

    public void Tether(SimCharacter? a, SimCharacter? b, ushort id, float duration = 0f, ushort debuffStatusId = 0)
    {
        foreach (var (old, _) in tethers)
        {
            if (old.Resolved || (old.A != a && old.A != b && old.B != a && old.B != b)) continue;
            old.Resolved = true;
            old.Despawn();
        }
        tethers.Add((world.Tether(a, b, id, duration, debuffStatusId), elapsed + duration));
    }

    public void Tick(float delta)
    {
        elapsed += delta;
        TickPoison();
        for (var i = 0; i < 8; i++)
        {
            if (towerUntil[i] > 0f && elapsed >= towerUntil[i])
            {
                towerUntil[i] = 0f;
                world.Party.WipeAllPlayers("Hello World：塔的潛在錯誤未被對應毒到期解除");
            }
        }
        foreach (var (tether, until) in tethers)
        {
            if (tether.Resolved || tether.TetherId is not (TetherId.HWLocal or TetherId.HWRemote)) continue;
            if (tether.A is not { } a || !a.IsAlive() || tether.B is not { } b || !b.IsAlive() || elapsed >= until)
            {
                tether.Resolved = true;
                tether.Despawn();
                world.Party.WipeAllPlayers("Hello World：連線未在時限內解開，或連線對象死亡");
                continue;
            }
            var broken = tether.TetherId == TetherId.HWLocal
                ? tether.StretchLt(Geometry.HwTetherBreakDistance)
                : tether.StretchGt(Geometry.HwTetherBreakDistance);
            if (!broken) continue;
            tether.Resolved = true;
            tether.Despawn();
            PlayEffect(a.Position, ActionId.HwTetherBreak);
            PlayEffect(b.Position, ActionId.HwTetherBreak);
            if (elapsed < breakVulnerabilityUntil)
                world.Party.WipeAllPlayers("Hello World：兩組線拉斷間隔過短");
            breakVulnerabilityUntil = elapsed + Duration.HwTetherBreakStack;
            foreach (var player in world.Party.ActiveMembers())
            {
                player.AddStatus(StatusId.MagicVulnerabilityUpMini, Duration.HwTetherBreakStack, 2);
                player.AddStatus(StatusId.TriceComeRuin, Duration.HwTetherBreakStack, 2);
            }
        }
    }

    private void TickPoison()
    {
        for (var i = 0; i < 8; i++)
        {
            if (poison[i] == 0) continue;
            if (world.Party.Get(i) is not { } player || !player.IsAlive())
            {
                world.Party.Get(i)?.RemoveStatus(poison[i]);
                poison[i] = 0;
                continue;
            }
            if (elapsed >= poisonUntil[i]) ExpirePoison(i, player);
        }

        // Snapshot prevents an infection jumping through several people in one frame.
        // Older holders win simultaneous conflicting contacts; no artificial random mis-pass.
        var sources = (ushort[])poison.Clone();
        foreach (var source in Enumerable.Range(0, 8).OrderBy(i => poisonUntil[i]))
        {
            if (sources[source] == 0 || poison[source] != sources[source] || world.Party.Get(source) is not { } holder || !holder.IsAlive()) continue;
            for (var target = 0; target < 8; target++)
            {
                if (source == target || world.Party.Get(target) is not { } recipient || !recipient.IsAlive()) continue;
                if (DistanceSquared(holder.Position, recipient.Position) <= ContactRadius * ContactRadius)
                    Infect(target, sources[source]);
            }
        }
    }

    private void Infect(int target, ushort color)
    {
        if (poison[target] == color || world.Party.Get(target) is not { } player || !player.IsAlive() || player.HasStatus(Debugger(color))) return;
        if (poison[target] != 0) player.RemoveStatus(poison[target]);
        poison[target] = color;
        poisonUntil[target] = elapsed + PoisonDuration;
        player.AddStatus(color, PoisonDuration);
    }

    private void ExpirePoison(int index, SimCharacter holder)
    {
        var color = poison[index];
        poison[index] = 0;
        holder.RemoveStatus(color);
        PlayEffect(holder.Position, color == StatusId.CriticalErrorUnderflow ? ActionId.CriticalErrorUnderflow : ActionId.CriticalErrorPerformance, holder.GameObjectId);
        foreach (var player in world.Party.ActiveMembers())
        {
            if (player != holder && DistanceSquared(player.Position, holder.Position) <= PoisonExplosionRadius * PoisonExplosionRadius)
                player.Die("Hello World：被別人的毒到期爆炸擊中");
        }
        var defect = color == StatusId.CriticalErrorUnderflow ? StatusId.LatentDefectUnderflow : StatusId.LatentDefectPerformance;
        holder.RemoveStatus(defect);
        if (towerDefect[index] == defect)
        {
            towerUntil[index] = 0f;
            towerDefect[index] = 0;
        }
        holder.AddStatus(Debugger(color));
    }

    private void ResolveTowers(int round)
    {
        foreach (var tower in TopP3HelloWorldAi.StackTowers[round]) ResolveTower(tower, StatusId.LatentDefectUnderflow);
        foreach (var tower in TopP3HelloWorldAi.DefamationTowers[round]) ResolveTower(tower, StatusId.LatentDefectPerformance);
    }

    private void ResolveTower(Vector2 position, ushort status)
    {
        var occupants = Enumerable.Range(0, 8).Where(i => world.Party.Get(i) is { } player && player.IsAlive() &&
            DistanceSquared(player.Position, new Vector3(position.X, 0, position.Y)) <= 36f).ToArray();
        if (occupants.Length != 1)
        {
            world.Party.WipeAllPlayers("Hello World：塔內需要一人，漏塔或多人踩塔");
            return;
        }
        var index = occupants[0];
        world.Party.Get(index)!.AddStatus(status, 10f);
        towerUntil[index] = elapsed + 10f;
        towerDefect[index] = status;
    }

    private void PlayEffect(Vector3 position, uint action, ulong? target = null)
    {
        var helper = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId.OmegaHelper,
            Targetable: false, EnemyList: EnemyListMode.Never, Placement: new Placement(position, 0f)));
        helper?.Cast(action, targetId: target);
        if (helper != null) world.Events.Add(Duration.MonitorHelperLifetime, helper.Despawn);
    }

    private static ushort Debugger(ushort color) => color == StatusId.CriticalErrorUnderflow
        ? StatusId.RepairedDefectUnderflow : StatusId.RepairedDefectPerformance;

    private static float DistanceSquared(Vector3 a, Vector3 b) =>
        (a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z);
}
