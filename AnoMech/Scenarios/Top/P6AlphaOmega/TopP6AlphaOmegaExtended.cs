using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Combat;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Top.TopConstants;

namespace AnoMech.Scenarios.Top.P6AlphaOmega;

public sealed partial class TopP6AlphaOmegaScenario
{
    public bool SupportsMultiplayer => extendedStart == null;
    private bool Extended => extendedStart != null;
    private bool startAtSecondUnlimited => extendedStart == "unlimited-second";
    private bool meteorOnly => extendedStart == "cosmo-meteor";
    private bool? meteorD3MarkedOverride;
    private bool? meteorD3MarkedAtRun;
    private TopP6MeteorFlarePlan? meteorFlarePlan;
    private readonly float[] lastLimitBreakAt = new float[8];
    private PartyRole? pendingMagicNumberHealer;
    private float limitBreakClock;
    internal const float SecondCannonAt = SecondArrowDelay + 16.03f;
    // Full-clear tail starts at the second Wild Charge (phase t=178.618).
    // All offsets below are measured from that charge or the second Unlimited
    // invocation, not from cast-end estimates.
    internal const float SecondUnlimitedAt = 8.370f;
    private const float SecondUnlimitedDiveAt = 18.123f;
    internal const float SecondUnlimitedMeteorAt = 40.316f;
    internal const float CasterLimitBreakBegin = SecondUnlimitedMeteorAt + 7.746f;
    internal const float RangedLimitBreakBegin = SecondUnlimitedMeteorAt + 18.164f;
    private const float FirstMagicNumberAt = 84.035f;
    private const float FirstMagicNumberStatusAt = 90.969f;
    private const float SecondMagicNumberAt = 100.186f;
    private const float SecondMagicNumberStatusAt = 107.112f;
    private const float FinalRunAt = 114.311f;
    private const float FinalRunReleaseAt = 130.269f;
    private const float CompleteAt = 141.102f;
    private static readonly (float Swing, float Hit)[] SecondCannonAutoAttacks =
        [(4.116f, 4.923f), (7.251f, 8.057f)];

    private const float MeteorMarkerAt = 20.087f;
    private const float MeteorResolveAt = 28.186f;
    private const float MeteorVisualAt = 29.216f;
    private const float MeteorDirectPreparation = 1f;

    private const uint CosmoMeteor = 31664;
    private const uint CosmoMeteorVisual = 31665;
    private const uint CosmoMeteorPuddle = 31666;
    private const uint CosmoMeteorStack = 31667;
    private const uint CosmoMeteorFlare = 31668;
    private const uint CosmoMeteorSpread = 32699;
    private const uint MagicNumber = 31670;
    private const uint FinalRun = 31648;
    private const ushort MagicNumberStatus = 3532;
    private const uint CosmoMeteorBaseId = 15726;
    private const uint CosmoCometBaseId = 15727;

    private static readonly Vector2[] CosmoMeteorPositions =
    [
        new(0f, -10f), new(0f, 10f),
    ];
    private static readonly Vector2[] CosmoCometPositions =
    [
        new(-6.5f, -11.26f), new(-13f, 0f), new(6.5f, -11.26f),
        new(-6.5f, 11.26f), new(13f, 0f), new(6.5f, 11.26f),
    ];
    private static readonly (float Swing, float Hit)[] SecondDiveAutoAttacks =
        [(14.810f, 15.615f), (17.942f, 18.748f)];

    private SimEnemy?[] cosmoMeteors = [];
    private SimEnemy?[] cosmoComets = [];

    private void StartExtended()
    {
        boss = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.AlphaOmega, NameId: BNpcNameId.AlphaOmega, Level: 90,
            Targetable: false, EnemyList: EnemyListMode.Always, IsVisible: true,
            Placement: new Placement(Vector3.Zero, -MathF.PI)));
        if (boss == null)
        {
            Fail("P6 阿爾法歐米茄未能生成。");
            return;
        }
        damage.MitigationSource = boss;
        InitializeLimitBreakState(postMemory: unlimitedOnly || meteorOnly);
        if (meteorOnly)
        {
            world.EnforceArenaBoundary(Geometry.ArenaRadius, replace: true);
            boss.SetTargetable(true);
            boss.AddStatusParam(StatusId.CodeMi, 0);
            var gather = TopP6FullAi.MeteorGatherPosition();
            foreach (var member in party.ActiveMembers())
            {
                if (member is SimPlayer) continue;
                member.SetPosition(new Placement(new Vector3(gather.X, 0f, gather.Y), MathF.PI));
            }
            world.Events.Add(MeteorDirectPreparation, () =>
            {
                ScheduleCosmoMeteorSequence();
                if (!solo) TopP6FullAi.RunMeteorTail(world, 0f);
            });
            return;
        }
        if (unlimitedOnly)
        {
            world.EnforceArenaBoundary(Geometry.ArenaRadius, replace: true);
            boss.SetTargetable(true);
            boss.AddStatusParam(StatusId.CodeMi, 0);
            foreach (var member in party.ActiveMembers())
                member.SetPosition(new Placement(Vector3.Zero, MathF.PI));
            world.Events.Add(1f, () => ScheduleUnlimitedWaveCannon(startAtSecondUnlimited));
            return;
        }
        // Start just inside the normal 20y fence. Keep the local player manual;
        // only NPCs are repositioned after the short opening hold.
        world.EnforceArenaBoundary(Geometry.ArenaRadius, replace: true);
        foreach (var member in party.ActiveMembers())
            member.SetPosition(new Placement(new Vector3(0f, 0f, 18f), MathF.PI));
        world.Events.Add(0.2f, () =>
        {
            if (failed || boss == null) return;
            boss.SetTargetable(true);
            boss.SetTarget(party.Get(PartyRole.MainTank), follow: false);
            Core.ChatOutput.Coach("[AnoMech] 開場位於南側圍內；可手動往場中移動。7.179 秒後開始宇宙記憶。已略過不可操作過場。");
            var gather = TopP6FullAi.MeteorGatherPosition();
            foreach (var member in party.ActiveMembers())
            {
                if (member is SimPlayer) continue;
                var role = ((ISimPartyMember)member).Role;
                member.MoveTo(role == PartyRole.MainTank ? new Vector3(0f, 0f, -8f)
                    : role == PartyRole.OffTank ? new Vector3(0f, 0f, 16f) : new Vector3(gather.X, 0f, gather.Y));
            }
        });
        world.Events.Add(0.2f + ReturnWindow, StartCombat);
    }

    private void ScheduleFullCosmoDive(float beginAt = 29.592f, bool includeFollowUpAutos = false)
    {
        const float releaseDelay = 5.597f;
        ScheduleBossCast(beginAt, ActionId.CosmoDive, 5.3f, beginAt + releaseDelay);
        if (includeFollowUpAutos)
            ScheduleAutoAttacks(SecondDiveAutoAttacks, beginAt);
        // 龍炎在命中時近引導兩人，六人分攤落在最遠者；不能在王動畫提早鎖人。
        world.Events.Add(beginAt + 8f, () =>
        {
            if (failed || boss == null) return;
            SimCharacter? first = null, second = null, stack = null;
            var firstDistance = float.PositiveInfinity;
            var secondDistance = float.PositiveInfinity;
            var farthestDistance = -1f;
            var count = 0;
            foreach (var member in party.ActiveMembers())
            {
                count++;
                var distance = Vector3.DistanceSquared(member.Position, boss.Position);
                if (distance < firstDistance)
                {
                    second = first;
                    secondDistance = firstDistance;
                    first = member;
                    firstDistance = distance;
                }
                else if (distance < secondDistance)
                {
                    second = member;
                    secondDistance = distance;
                }
                if (distance > farthestDistance)
                {
                    stack = member;
                    farthestDistance = distance;
                }
            }
            if (count < 3) return;
            HitTarget(first, ActionId.CosmoDive_7BA7, true, 0, true);
            HitTarget(second, ActionId.CosmoDive_7BA7, true, 0, true);
            HitTarget(stack, ActionId.CosmoDive_7BA8, false, 6, false);
        });
    }

    private void ScheduleSecondTail()
    {
        if (failed || boss == null) return;
        Core.ChatOutput.Coach("[AnoMech] P6 後半：第二次限制解除、宇宙潛、宇宙隕石；依錄影時間軸持續至通關。");
        ScheduleAutoAttacks(SecondCannonAutoAttacks);
        world.Events.Add(SecondUnlimitedAt, () => ScheduleUnlimitedWaveCannon(second: true));
    }

    private void ScheduleCosmoMeteorSequence()
    {
        ScheduleCosmoMeteor();
        ScheduleMagicNumber(
            FirstMagicNumberAt - SecondUnlimitedAt - SecondUnlimitedMeteorAt,
            FirstMagicNumberStatusAt - SecondUnlimitedAt - SecondUnlimitedMeteorAt,
            PartyRole.MainTank, PartyRole.RegenHealer);
        ScheduleMagicNumber(
            SecondMagicNumberAt - SecondUnlimitedAt - SecondUnlimitedMeteorAt,
            SecondMagicNumberStatusAt - SecondUnlimitedAt - SecondUnlimitedMeteorAt,
            PartyRole.OffTank, PartyRole.ShieldHealer);
        ScheduleBossCast(
            FinalRunAt - SecondUnlimitedAt - SecondUnlimitedMeteorAt, FinalRun, 15.7f,
            FinalRunReleaseAt - SecondUnlimitedAt - SecondUnlimitedMeteorAt);
        world.Events.Add(CompleteAt - SecondUnlimitedAt - SecondUnlimitedMeteorAt, () =>
        {
            if (failed) return;
            if (cosmoMeteors.Concat(cosmoComets).Any(add => add is { IsActive: true }))
                Fail("P6 演練未完成：仍有宇宙流星或宇宙隕星未被極限技擊破。");
            else Plugin.ChatGui.Print("[AnoMech] P6 完整時間軸演練結束。");
        });
    }

    private void ScheduleMagicNumber(float castAt, float statusAt, PartyRole tank, PartyRole healer)
    {
        // Tank LB3 lasts 8s. Check the assigned tank's completed LB at damage,
        // not a button press or the other tank's party-wide status.
        world.Events.Add(castAt, () =>
        {
            if (failed) return;
            if (!solo) TopP6FullAi.StartLimitBreak(world, tank, null);
        });
        world.Events.Add(castAt + 4.968f, () =>
        {
            if (failed) return;
            if (!TopP6LimitBreakRules.IsTankLbActive(lastLimitBreakAt[(int)tank], limitBreakClock))
                Fail($"魔數：{(tank == PartyRole.MainTank ? "MT" : "ST")} 未在傷害結算前開啟有效 LB。");
        });
        // The damage packet precedes the recorded six-second 3532 debuff.
        ScheduleBossCast(castAt, MagicNumber, 4.7f, castAt + 4.968f);
        world.Events.Add(statusAt, () =>
        {
            if (failed) return;
            pendingMagicNumberHealer = healer;
            for (var role = 0; role < 8; role++)
                party.Get(role)?.AddStatusParam(MagicNumberStatus, 0, duration: 6f);
            if (!solo) TopP6FullAi.StartLimitBreak(world, healer, null);
            world.Events.Add(6f, () =>
            {
                if (!failed && pendingMagicNumberHealer == healer)
                    Fail($"魔數：{(healer == PartyRole.RegenHealer ? "H1" : "H2")} 未在 DEBUFF 到期前完成 LB。");
            });
        });
    }

    private void ScheduleCosmoMeteor()
    {
        ScheduleBossCast(0f, CosmoMeteor, 4.7f, 4.969f);
        // FFLogs lacks actor-spawn packets; stage the adds at Meteor's cast release.
        world.Events.Add(4.969f, SpawnCosmoMeteorAdds);
        world.Events.Add(5.014f, DropCosmoMeteorPuddles);

        // The first and second four-person spread waves use different
        // recorded role partitions. Positions remain at separate clock spots.
        var firstSpread = RoleList.Random(party);
        var secondSpread = RoleList.Random(party);
        world.Events.Add(10.119f, () => ResolveCosmoMeteorSpread(firstSpread));
        world.Events.Add(11.147f, () => ResolveCosmoMeteorSpread(firstSpread, 4));
        world.Events.Add(16.155f, () => ResolveCosmoMeteorSpread(secondSpread));
        world.Events.Add(17.182f, () => ResolveCosmoMeteorSpread(secondSpread, 4));

        // 346 is the native large-triangle marker. The marked roles and their
        // movement plan are frozen at marker time from currently alive members.
        world.Events.Add(MeteorMarkerAt, () =>
        {
            meteorFlarePlan = TopP6MeteorFlarePlan.Create(
                party.ActiveMembers().Select(member => (((ISimPartyMember)member).Role, new Vector2(member.Position.X, member.Position.Z))).ToArray(), meteorD3MarkedAtRun, rng);
            foreach (var role in meteorFlarePlan.MarkedRoles)
                party.Get(role)?.AttachLockonVfx(346, persistent: false);
            if (!solo)
                TopP6FullAi.RunMeteorFlares(
                    world, meteorFlarePlan, MeteorResolveAt - MeteorMarkerAt);
        });
        world.Events.Add(MeteorResolveAt, ResolveCosmoMeteorFlares);
        world.Events.Add(MeteorVisualAt, () => boss?.Cast(
            CosmoMeteorVisual, targetId: boss?.GameObjectId));

        // Actual add lifecycle is owned by completed LB callbacks below. A miss,
        // wrong action, or absent LB leaves the actor alive for diagnosis.
    }

    private void InitializeLimitBreakState(bool postMemory)
    {
        foreach (var member in party.AllMembers())
        {
            member.RemoveStatus(StatusId.QuickeningDynamis);
            member.RemoveStatus(StatusId.BrilliantDynamis);
            member.RemoveStatus(StatusId.RadiantDynamis);
            if (postMemory)
                member.AddStatus(StatusId.BrilliantDynamis);
            else
                member.AddStatus(StatusId.QuickeningDynamis, stacks: 3, overrideStacks: true);
        }
        world.LimitBreaks?.Refill();
    }

    private void OnLimitBreakResolved(
        PartyRole role, uint actionId, Vector3? location, SimCharacter? target)
    {
        var caster = party.Get(role);
        if (caster == null) return;

        // The refund is a per-member state transition, not a global or per-run
        // refill. A cast from 3448 spends the bar without creating another one.
        if (caster.FindStatus(StatusId.BrilliantDynamis) != null)
        {
            caster.RemoveStatus(StatusId.BrilliantDynamis);
            caster.RemoveStatus(StatusId.RadiantDynamis);
            caster.AddStatus(StatusId.RadiantDynamis);
            world.LimitBreaks?.Refill();
        }

        if (TopP6LimitBreakRules.IsTankAction(actionId)) lastLimitBreakAt[(int)role] = limitBreakClock;
        if (pendingMagicNumberHealer == role && TopP6LimitBreakRules.IsHealerAction(actionId))
        {
            for (var member = 0; member < 8; member++)
                party.Get(member)?.RemoveStatus(MagicNumberStatus);
            pendingMagicNumberHealer = null;
        }

        if (role == PartyRole.CasterDps)
            ResolveLimitBreakGeometry(actionId, caster, location, target, cosmoComets, ground: true);
        else if (role == PartyRole.PhysRangedDps)
            ResolveLimitBreakGeometry(actionId, caster, location, target, cosmoMeteors, ground: false);
    }

    private void ResolveLimitBreakGeometry(
        uint actionId, SimCharacter caster, Vector3? location, SimCharacter? target,
        SimEnemy?[] adds, bool ground)
    {
        var live = false;
        for (var i = 0; i < adds.Length; i++)
            if (adds[i] is { IsActive: true, Targetable: true })
            {
                live = true;
                break;
            }
        if (!live) return;

        var sheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>();
        if (!sheet.TryGetRow(actionId, out var action)) return;
        var range = (float)action.EffectRange;
        if (range <= 0f) return;

        var center = location ?? default;
        if (ground && location == null) return;
        var origin = ground ? center : caster.Position;
        var rotation = caster.Rotation;
        if (!ground && (location ?? target?.Position) is { } aim)
            rotation = MathF.Atan2(aim.X - origin.X, aim.Z - origin.Z);
        var halfWidth = action.XAxisModifier > 0
            ? (float)action.XAxisModifier * 0.5f
            : range;
        var circle = ground && action.CastType is 2 or 5 or 6;
        var rectangle = !ground && action.CastType is 4 or 8 or 12;
        if (!circle && !rectangle) return;

        for (var i = 0; i < adds.Length; i++)
        {
            var add = adds[i];
            if (add is not { IsActive: true, Targetable: true }) continue;
            var hit = TopP6LimitBreakRules.Hits(new(add.Position.X, add.Position.Z),
                new(origin.X, origin.Z), rotation, range, halfWidth, circle);
            if (!hit) continue;
            add.SetTargetable(false);
            add.Despawn();
        }
    }

    private void DropCosmoMeteorPuddles()
    {
        if (failed) return;
        foreach (var member in party.ActiveMembers())
            ScheduleHelper(0f, 3.987f, new Placement(member.Position, 0f),
                CosmoMeteorPuddle, 3.7f, 0f, true);
    }

    private void ResolveCosmoMeteorSpread(RoleList order, int offset = 0)
    {
        for (var i = 0; i < 4; i++)
            HitTarget(party.Get(order[offset + i]), CosmoMeteorSpread, false, 0, true, size: 5f);
    }

    private void ResolveCosmoMeteorFlares()
    {
        if (meteorFlarePlan is not { } plan) return;
        if (plan.StackTargetRole is { } stackRole)
        {
            var stackTarget = party.Get(stackRole);
            if (stackTarget != null)
                HitTarget(stackTarget, CosmoMeteorStack, false,
                    Math.Min(5, plan.UnmarkedRoles.Count), false, size: 6f);
        }
        foreach (var role in plan.MarkedRoles)
            HitTarget(party.Get(role), CosmoMeteorFlare, false, 0, false,
                killTargets: false, size: 100f);
    }

    private void SpawnCosmoMeteorAdds()
    {
        cosmoMeteors = CosmoMeteorPositions
            .Select(position => SpawnCosmoAdd(CosmoMeteorBaseId, position)).ToArray();
        cosmoComets = CosmoCometPositions
            .Select(position => SpawnCosmoAdd(CosmoCometBaseId, position)).ToArray();
        if (cosmoMeteors.Concat(cosmoComets).Any(add => add == null))
            Fail("宇宙流星：必要的機制物件未能生成，請重試。");
    }

    private SimEnemy? SpawnCosmoAdd(uint baseId, Vector2 position)
        => world.SpawnEnemy(new EnemySpawnConfig(
            // BNpcName 12259＝Cosmo Meteor／宇宙流星；12260＝Cosmo Comet／宇宙隕星。
            BNpcBaseId: baseId, NameId: baseId == CosmoMeteorBaseId ? 12259u : 12260u, Level: Level,
            Targetable: true, EnemyList: EnemyListMode.Always, IsVisible: true,
            Placement: new Placement(new Vector3(position.X, 0f, position.Y), 0f)));


}
