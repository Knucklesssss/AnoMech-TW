using System;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.Game;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Top.TopConstants;

namespace AnoMech.Scenarios.Top.P3HelloWorld;

public sealed class TopP3Transition(SimWorld world, TopP3HelloWorldState state)
{
    private readonly SimEnemy?[] arms = new SimEnemy?[6];
    private readonly bool[] centerHit = new bool[8];
    private bool centerActive;

    public void Run()
    {
        world.Events.Add(3.67f, SpawnArms);
        world.Events.Add(6.90f, ApplyCannons);
        if (state.TransitionAutoMarkers)
        {
            world.Events.Add(7f, ApplyMarkers);
            world.Events.Add(28.5f, Markings.ClearAll);
        }
        world.Events.Add(9.64f, () => ShowArms(0));
        world.Events.Add(11.95f, () => ResolveRing(0));
        world.Events.Add(12.46f, () => centerActive = true);
        world.Events.Add(12.74f, () => ShowArms(1));
        world.Events.Add(14.05f, () => ResolveRing(1));
        world.Events.Add(16.10f, () => ResolveRing(2));
        world.Events.Add(18.16f, () => ResolveRing(3));
        world.Events.Add(20.08f, () => ResolveRing(0));
        world.Events.Add(22.05f, () => CastArms(0));
        world.Events.Add(22.23f, () => ResolveRing(1));
        world.Events.Add(24.01f, () => ResolveArms(0));
        world.Events.Add(24.28f, () => ResolveRing(2));
        world.Events.Add(24.59f, () => CastArms(1));
        world.Events.Add(25.93f, RemoveCannons);
        world.Events.Add(26.02f, ResolveCannons);
        world.Events.Add(26.33f, () => ResolveRing(3));
        world.Events.Add(26.56f, () => ResolveArms(1));
        world.Events.Add(28.47f, () => centerActive = false);
        world.Events.Add(28.56f, () => HideArms(0));
        world.Events.Add(28.66f, () =>
        {
            for (var i = 3; i < 6; i++) arms[i]?.PlayActionTimeline(TimelineId.WarpOut);
        });
        world.Events.Add(31.06f, () => HideArms(1));
        world.Events.Add(31.40f, () =>
        {
            foreach (var arm in arms) arm?.Despawn();
        });
    }

    public void Tick()
    {
        if (!centerActive) return;
        for (var i = 0; i < 8; i++)
        {
            if (centerHit[i] || world.Party.Get(i) is not { } member || !member.IsAlive()) continue;
            if (!TopP3TransitionRules.CenterContains(Xz(member.Position))) continue;
            centerHit[i] = true;
            member.Die("P3 轉場：踏入中央危險區");
        }
    }

    private void ApplyMarkers()
    {
        Sign[] signs = [Sign.Bind1, Sign.Ignore1, Sign.Bind2, Sign.Ignore2,
            Sign.Attack1, Sign.Attack2, Sign.Attack3, Sign.Attack4];
        Markings.ClearAll();
        for (var slot = 0; slot < 8; slot++)
            if (world.Party.Get(state.TransitionRoles[slot]) is { } member && member.IsAlive())
                Markings.Set(signs[slot], member.GameObjectId);
    }

    private void SpawnArms()
    {
        for (var i = 0; i < 6; i++)
        {
            var first = i < 3;
            var pos = TopP3TransitionRules.ArmPosition(first == state.TransitionFirstArmsSouth, i % 3);
            arms[i] = world.SpawnEnemy(new EnemySpawnConfig(
                BNpcBaseId: first ? BNpcBaseId.LeftArmUnit : BNpcBaseId.RightArmUnit,
                NameId: first ? BNpcNameId.LeftArmUnit : BNpcNameId.RightArmUnit,
                Level: 90, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false,
                Placement: new Placement(new Vector3(pos.X, 0, pos.Y), MathF.Atan2(-pos.X, -pos.Y))));
        }
    }

    private void ShowArms(int group)
    {
        for (var i = group * 3; i < group * 3 + 3; i++)
        {
            arms[i]?.SetVisible(true);
            arms[i]?.PlayActionTimeline(TimelineId.Spawn);
        }
    }

    private void HideArms(int group)
    {
        for (var i = group * 3; i < group * 3 + 3; i++) arms[i]?.SetVisible(false);
    }

    private void CastArms(int group)
    {
        for (var i = group * 3; i < group * 3 + 3; i++)
            if (arms[i] is { } arm)
                arm.Cast(ActionId.ColossalBlow, targetLocation: arm.Position, castSeconds: 1.7f,
                    targetId: arm.GameObjectId, fireDelay: group == 0 ? 0.26f : 0.27f);
    }

    private void ResolveArms(int group)
    {
        foreach (var member in world.Party.ActiveMembers())
            for (var arm = 0; arm < 3; arm++)
            {
                var center = TopP3TransitionRules.ArmPosition((group == 0) == state.TransitionFirstArmsSouth, arm);
                if (Vector2.DistanceSquared(Xz(member.Position), center) > TopP3TransitionRules.ArmRadius * TopP3TransitionRules.ArmRadius) continue;
                member.Die($"P3 轉場：第 {group + 1} 組手臂爆炸");
                break;
            }
    }

    private void ResolveRing(int ring)
    {
        foreach (var member in world.Party.ActiveMembers())
            if (TopP3TransitionRules.RingContains(Xz(member.Position), ring))
                member.Die($"P3 轉場：速射式波動砲第 {ring + 1} 圈");
    }

    private void ApplyCannons()
    {
        for (var i = 0; i < 8; i++)
        {
            if (i is 1 or 3) continue;
            world.Party.Get(state.TransitionRoles[i])?.AddStatus(
                i is 0 or 2 ? StatusId.HighPoweredSniperCannon : StatusId.SniperCannon, 19f);
        }
    }

    private void RemoveCannons()
    {
        foreach (var role in state.TransitionRoles)
        {
            world.Party.Get(role)?.RemoveStatus(StatusId.SniperCannon);
            world.Party.Get(role)?.RemoveStatus(StatusId.HighPoweredSniperCannon);
        }
    }

    private void ResolveCannons()
    {
        var positions = new Vector2?[8];
        for (var i = 0; i < 8; i++)
            if (world.Party.Get(state.TransitionRoles[i]) is { } member && member.IsAlive())
                positions[i] = Xz(member.Position);
        var failures = TopP3TransitionRules.FailedCannonSlots(positions);

        for (var i = 0; i < 8; i++)
        {
            if (i is 1 or 3 || positions[i] is not { } center) continue;
            var helper = world.SpawnEnemy(new EnemySpawnConfig(
                BNpcBaseId: BNpcBaseId.OmegaHelper, NameId: BNpcNameId.OmegaFinal,
                Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false,
                Placement: new Placement(new Vector3(center.X, 0, center.Y), 0)));
            helper?.Cast(i is 0 or 2 ? ActionId.HighPoweredSniperCannon : ActionId.SniperCannon,
                castSeconds: 0f, targetId: world.Party.Get(state.TransitionRoles[i])?.GameObjectId);
            if (helper != null) world.Events.Add(5f, helper.Despawn);
        }

        for (var i = 0; i < 8; i++)
        {
            if (positions[i] == null || world.Party.Get(state.TransitionRoles[i]) is not { } member) continue;
            if (failures[i]) member.Die("P3 轉場：狙擊砲重疊或分攤人數錯誤");
            else member.AddStatus(StatusId.MagicVulnerabilityUp, 1.96f);
        }
    }

    private static Vector2 Xz(Vector3 position) => new(position.X, position.Z);
}
