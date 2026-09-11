using System;
using System.Linq;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.Game;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Top.TopConstants;

namespace AnoMech.Scenarios.Top.P4BlueScreen;

public sealed class TopP4BlueScreenMechanics(SimWorld world, TopP4BlueScreenState state)
{
    private SimEnemy? boss;
    private Vector2[] echoDirections = [];
    private bool failed;
    public bool? Passed { get; private set; }

    // Relative to P4 becoming targetable (cactbot timeline 607.1).
    // Visual starts and damage snapshots are distinct; native timings need in-game verification.
    public void Run()
    {
        boss = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId.OmegaFinal, BNpcNameId.OmegaFinal,
            Level: Level, Targetable: true, EnemyList: EnemyListMode.Always, ModelCharaId: 3775, Scale: 1.4f,
            HitboxRadius: 12.502f, Placement: new Placement(Vector3.Zero, MathF.PI)));
        world.Events.Add(9.3f, () => boss?.Cast(ActionId.P4WaveCannonCast, castSeconds: 5f, targetId: boss.GameObjectId));
        world.Events.Add(11.9f, () => MarkStacks(0));
        world.Events.Add(14.9f, () => ResolveSpread(0, 4.7f));
        world.Events.Add(17.3f, () => Visual(ActionId.RapidFireWaveCannon, Vector2.Zero, 5f));
        world.Events.Add(19.6f, () => ResolveEcho(ActionId.P4WaveCannonVisual2));
        world.Events.Add(19.9f, () => ResolveStacks(0));
        world.Events.Add(22.0f, () => MarkStacks(1));
        world.Events.Add(22.3f, () => ResolveRing(0));
        world.Events.Add(24.4f, () => ResolveRing(1));
        world.Events.Add(24.4f, () => boss?.Cast(ActionId.P4WaveCannonVisual2, castSeconds: 0f, targetId: boss.GameObjectId));
        world.Events.Add(25.0f, () => ResolveSpread(1, 4.5f));
        world.Events.Add(26.4f, () => ResolveRing(2));
        world.Events.Add(28.4f, () => ResolveRing(3));
        world.Events.Add(29.5f, () => ResolveEcho(ActionId.P4WaveCannonVisual1));
        world.Events.Add(29.7f, () => ResolveStacks(1));
        world.Events.Add(31.9f, () => MarkStacks(2));
        world.Events.Add(34.5f, () => boss?.Cast(ActionId.P4WaveCannonVisual3, castSeconds: 0f, targetId: boss.GameObjectId));
        world.Events.Add(34.5f, () => Visual(ActionId.RapidFireWaveCannon, Vector2.Zero, 5f));
        world.Events.Add(35.1f, () => ResolveSpread(2, 4.5f));
        world.Events.Add(39.5f, () => ResolveRing(0));
        world.Events.Add(39.6f, () => ResolveEcho(ActionId.P4WaveCannonVisual4));
        world.Events.Add(39.8f, () => ResolveStacks(2));
        world.Events.Add(41.5f, () => ResolveRing(1));
        world.Events.Add(43.5f, () => ResolveRing(2));
        world.Events.Add(45.5f, () => ResolveRing(3));
        world.Events.Add(46.5f, () => boss?.Cast(ActionId.P4BlueScreen, castSeconds: 8f, targetId: boss.GameObjectId));
        world.Events.Add(53.5f, () => Visual(ActionId.P4BlueScreenSuccess, Vector2.Zero, 1f));
        world.Events.Add(55.5f, Finish);
    }

    private void MarkStacks(int round)
    {
        foreach (var role in state.StackTargets[round])
        {
            var member = world.Party.Get(role);
            if (member == null || !member.IsAlive()) continue;
            var helper = Helper(Vector2.Zero);
            helper?.Cast(ActionId.P4StackTarget, castSeconds: 0f, targetId: member.GameObjectId);
            if (helper != null) world.Events.Add(3f, helper.Despawn);
        }
    }

    private void ResolveSpread(int round, float echoDelay)
    {
        var positions = Snapshot();
        echoDirections = positions.Where(p => p != null).Select(p => p!.Value).ToArray();
        Fail(TopP4BlueScreenRules.FailedSpreads(positions, echoDirections), $"P4 第 {round + 1} 輪：分散砲重疊");
        foreach (var direction in echoDirections)
        {
            Visual(ActionId.P4Spread, direction);
            Visual(ActionId.P4SpreadRepeat, direction, echoDelay);
        }
    }

    private void ResolveEcho(uint animation)
    {
        boss?.Cast(animation, castSeconds: 0f, targetId: boss.GameObjectId);
        var positions = Snapshot();
        Fail(positions.Select(p => p is { } pos && echoDirections.Any(d => TopP4BlueScreenRules.LineContains(d, pos))).ToArray(),
            "P4：未離開原本的分散砲線");
    }

    private void ResolveStacks(int round)
    {
        var positions = Snapshot();
        var targets = state.StackTargets[round].Select(role => (int)role).ToArray();
        Fail(TopP4BlueScreenRules.FailedStacks(positions, targets), $"P4 第 {round + 1} 輪：分攤不足四人、重疊或未承傷");
        foreach (var target in targets)
            if (positions[target] is { } direction) Visual(ActionId.P4Stack, direction);
    }

    private void ResolveRing(int ring)
    {
        if (ring > 0) Visual(ActionId.RapidFireWaveCannon + (uint)ring, Vector2.Zero);
        Fail(Snapshot().Select(p => p is { } pos && TopP4BlueScreenRules.RingContains(pos, ring)).ToArray(),
            $"P4：踩到地靈脈第 {ring + 1} 圈");
    }

    private void Finish()
    {
        Passed = !failed && Enumerable.Range(0, 8).All(i => world.Party.Get(i)?.IsAlive() == true);
        ChatOutput.Coach(Passed == true ? "[AnoMech] P4 藍屏練習完成：機制通過（不含輸出／減傷檢定）。"
            : "[AnoMech] P4 藍屏練習結束：本輪有機制失誤。");
    }

    private Vector2?[] Snapshot() => Enumerable.Range(0, 8).Select(i =>
        world.Party.Get(i) is { } member && member.IsAlive() ? (Vector2?)new Vector2(member.Position.X, member.Position.Z) : null).ToArray();

    private void Fail(bool[] failures, string cause)
    {
        for (var i = 0; i < 8; i++)
        {
            if (!failures[i]) continue;
            failed = true;
            if (world.Party.Get(i) is { } member && member.IsAlive()) member.Die(cause);
        }
    }

    private SimEnemy? Helper(Vector2 direction) => world.SpawnEnemy(new EnemySpawnConfig(
        BNpcBaseId.OmegaHelper, BNpcNameId.OmegaFinal, Targetable: false,
        EnemyList: EnemyListMode.Never, IsVisible: false,
        Placement: new Placement(Vector3.Zero, MathF.Atan2(direction.X, direction.Y))));

    private void Visual(uint actionId, Vector2 direction, float duration = 0f)
    {
        var helper = Helper(direction);
        helper?.Cast(actionId, castSeconds: duration, targetId: helper.GameObjectId);
        if (helper != null) world.Events.Add(duration + 3f, helper.Despawn);
    }
}
