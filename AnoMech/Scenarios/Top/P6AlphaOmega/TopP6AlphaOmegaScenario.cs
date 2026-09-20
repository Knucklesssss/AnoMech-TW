using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using static AnoMech.Scenarios.Top.TopConstants;

namespace AnoMech.Scenarios.Top.P6AlphaOmega;

// 20260919-151921-t1122.jsonl, t0 = 5155.160 (Cosmo Memory cast).
// Cast packets encode rotation in [-pi, pi], NOT [0, 2pi]. Use the original
// Exasquares/WC2 placements and propagation rather than treating packet angles as radians.
// Continue through the first Wave Cannon, two autos and the second Cosmo Arrow.
public sealed partial class TopP6AlphaOmegaScenario(bool unlimitedOnly = false, string? extendedStart = null) : IScenario
{
    public string Name => extendedStart switch
    {
        "full" => "阿爾法歐米茄（完整時間軸／LB 練習）",
        "unlimited" => "限制解除至 P6 結尾（LB 練習）",
        "unlimited-second" => "第二次限制解除至 P6 結尾（LB 練習）",
        "cosmo-meteor" => "宇宙流星至 P6 結尾（LB 練習）",
        _ => unlimitedOnly ? "波動砲：限制解除（開場段）" : "阿爾法歐米茄開場",
    };
    public IPhase Phase => TopZone.P6;
    public bool SupportsSolo => extendedStart == null;
    public IReadOnlyList<IScenarioAi> AiStrats { get; } = extendedStart == null ? [new TopP6AlphaOmegaAi()] : [new TopP6FullAi()];

    private SimWorld world = null!;
    private SimParty party = null!;
    private DamageSolver damage = null!;
    private SimEnemy? boss;
    private bool failed;
    private bool solo;
    private readonly Rng rng = new();

    // Actual effects, not cast-end estimates: packets include ~0.3s cast slide.
    private static readonly (float Swing, float Hit)[] AutoAttacks =
        [(10.143f, 10.951f), (13.281f, 14.089f), (44.327f, 45.128f), (47.451f, 48.253f)];
    // TC source uses the cactbot TOP timeline beyond its incomplete local capture:
    // Wild Charge 1244.3; swings 1248.4/1251.6; hits 1249.3/1252.5;
    // Cosmo Arrow effect 1258.7. Recheck against the complete gameplay video.
    // https://github.com/OverlayPlugin/cactbot/blob/main/ui/raidboss/data/06-ew/ultimate/the_omega_protocol.txt
    private static readonly (float Swing, float Hit)[] FollowUpAutoAttacks =
        [(4.1f, 5f), (7.3f, 8.2f)];
    internal const float SecondArrowDelay = 6.5f; // Sequence cast starts at +1.9s.
    private static readonly float[] ExaflareOffsets = [0f, 1.033f, 2.006f, 3.033f];
    private const float UnlimitedAt = 48.562f;
    internal const float FirstPuddleAt = 10.033f; // Relative to Unlimited's cast.
    internal const int PuddleCount = 6;
    internal const float PuddleInterval = 2.002f;
    internal const float PuddleDelay = 2.990f;
    internal const float LastPuddleAt = FirstPuddleAt + (PuddleCount - 1) * PuddleInterval;
    // The opening capture ends after bait four. Follow-up offsets use the existing
    // P6 Wave Cannon sequence, starting as the last puddle resolves.
    internal const float CannonAt = LastPuddleAt + PuddleDelay;
    internal const float SecondProteanAt = CannonAt + 5.04f;
    private const float WildChargeAt = CannonAt + 11.37f;

    // 32626 盲信: recorded fixed south knockback to local Z≈22.45.
    // Skip the 54.6s non-interactive movie, but retain the 7.179s return window.
    private const uint TransitionKnockback = 32626;
    private const float KnockbackAt = 1.166f;
    private const float ReturnAt = 1.4f;
    private const float ReturnWindow = 7.179f;
    private const float KnockbackLandingZ = 22.45f;

    public void DrawSettings()
    {
        if (!Extended)
        {
            Dalamud.Bindings.ImGui.ImGui.TextWrapped("練習範圍至第二次宇宙天箭；後段時距仍待遊戲內校準。減傷效果不計算。");
            return;
        }
        Dalamud.Bindings.ImGui.ImGui.TextWrapped("單人房間練習；一般職業技能與減傷提示沿用現有功能。需正確完成坦克、治療、遠程與法系極限技；不判定輸出是否足以通關，後段時序待遊戲內驗證。");
        Dalamud.Bindings.ImGui.ImGui.TextWrapped("本場固定以正常速度執行。請使用符合選定職能的職業。核爆目前提供標記與走位演練，尚未判定距離衰減傷害。");
        if (Dalamud.Bindings.ImGui.ImGui.RadioButton("核爆隨機", meteorD3MarkedOverride == null)) meteorD3MarkedOverride = null;
        if (Dalamud.Bindings.ImGui.ImGui.RadioButton("核爆包含 D3", meteorD3MarkedOverride == true)) meteorD3MarkedOverride = true;
        if (Dalamud.Bindings.ImGui.ImGui.RadioButton("核爆不含 D3", meteorD3MarkedOverride == false)) meteorD3MarkedOverride = false;
    }

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        party = world.Party;
        solo = selectedAi is null;
        damage = new DamageSolver(party);
        damage.SetStatuses(DamageType.Magic, StatusId.MagicVulnerabilityUp);
        failed = false;
        boss = null;
        if (Extended)
        {
            limitBreakClock = 0f;
            meteorD3MarkedAtRun = meteorD3MarkedOverride;
            meteorFlarePlan = null;
            Array.Fill(lastLimitBreakAt, float.NegativeInfinity);
            pendingMagicNumberHealer = null;
            cosmoMeteors = [];
            cosmoComets = [];
            world.LimitBreaks = new AnoMech.Core.Combat.PracticeLimitBreakRuntime(world, OnLimitBreakResolved);
            world.Events.Add(0.2f, StartExtended);
        }
        else world.Events.Add(0.2f, Start);
    }

    public void Tick(float delta, float elapsed) { limitBreakClock += delta; }

    private void Start()
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
        if (unlimitedOnly)
        {
            world.EnforceArenaBoundary(Geometry.ArenaRadius, replace: true);
            boss.SetTargetable(true);
            boss.AddStatusParam(StatusId.CodeMi, 0);
            foreach (var member in party.ActiveMembers())
                member.SetPosition(new Placement(Vector3.Zero, MathF.PI));
            world.Events.Add(1f, () => ScheduleUnlimitedWaveCannon());
            return;
        }
        // Replace the zone's normal fence: the transition explicitly puts us outside it.
        world.EnforceArenaBoundary(23f, replace: true);
        foreach (var member in party.ActiveMembers())
            member.SetPosition(new Placement(new Vector3(0f, 0f, 4f), MathF.PI));
        // TopZone initializes its map effects at t=1; apply the recorded wall-hide after that.
        world.Events.Add(0.9f, () => world.Map.AddEffect(0x00040008, 0));
        ScheduleHelper(0f, KnockbackAt, new Placement(Vector3.Zero, 0f),
            TransitionKnockback, 0.9f, 0f, false);
        world.Events.Add(KnockbackAt, () =>
        {
            foreach (var member in party.ActiveMembers())
            {
                // Fixed southward displacement; measured local-player slide is ~100y/s.
                var distance = KnockbackLandingZ - member.Position.Z;
                ((ISimPartyMember)member).Knockback(member.Position - Vector3.UnitZ, distance, 100f);
            }
        });
        world.Events.Add(ReturnAt, () =>
        {
            if (failed || boss == null) return;
            boss.SetTargetable(true);
            boss.SetTarget(party.Get(PartyRole.MainTank), follow: false);
            world.Map.AddEffect(0x00010002, 0);
            Plugin.ChatGui.Print("[AnoMech] 擊退後可往場中移動；7.179 秒後開始宇宙記憶。已略過不可操作過場。");
            foreach (var member in party.ActiveMembers())
            {
                var role = ((ISimPartyMember)member).Role;
                if (member is SimPlayer || MultiplayerContext.IsClient || MultiplayerContext.IsHumanControlled((int)role)) continue;
                member.MoveTo(new Vector3(0f, 0f, role == PartyRole.MainTank ? -8f : role == PartyRole.OffTank ? 16f : 6f));
            }
        });
        world.Events.Add(ReturnAt + ReturnWindow, StartCombat);
    }

    private void StartCombat()
    {
        if (failed || boss == null) return;
        world.EnforceArenaBoundary(Geometry.ArenaRadius, replace: true);
        ScheduleBossCast(0f, ActionId.CosmoMemory, 5.7f, 5.996f);
        if (Extended && !solo) TopP6FullAi.StartLimitBreak(world, PartyRole.MainTank, null);
        world.Events.Add(5.996f, () =>
        {
            if (Extended && !TopP6LimitBreakRules.IsTankLbActive(lastLimitBreakAt[(int)PartyRole.MainTank], limitBreakClock) &&
                !TopP6LimitBreakRules.IsTankLbActive(lastLimitBreakAt[(int)PartyRole.OffTank], limitBreakClock))
            { Fail("宇宙記憶：傷害結算前未開啟有效坦克極限技。"); return; }
            damage.Resolve(boss, ActionId.CosmoMemory, [DamageType.Magic], []);
            if (Extended) InitializeLimitBreakState(postMemory: true);
        });
        // Recorded status+ at t0+8.324, before the first 31747 swing.
        world.Events.Add(8.324f, () => boss?.AddStatusParam(StatusId.CodeMi, 0));
        var inFirst = rng.NextBool();
        ScheduleCosmoArrow(inFirst);
        if (!solo) ScenarioAiRunner.Run(AiStrats, 0, inFirst, world);
        if (Extended) ScheduleFullCosmoDive(); else ScheduleCosmoDive();
        ScheduleAutoAttacks(AutoAttacks);
        world.Events.Add(UnlimitedAt, () => ScheduleUnlimitedWaveCannon());
    }

    private void ScheduleBossCast(float at, uint action, float cast, float hit)
    {
        world.Events.Add(at, () =>
        {
            if (!failed && boss != null)
                boss.Cast(action, castSeconds: cast, targetId: boss.GameObjectId,
                    fireDelay: MathF.Max(0f, hit - at - cast));
        });
    }

    private void ScheduleCosmoArrow(bool inFirst)
    {
        Plugin.ChatGui.Print($"[AnoMech] 宇宙天箭：{(inFirst ? "內先" : "外先")}；兩點集合，雙坦離群引導宇宙龍炎。");
        world.Events.Add(14.40f, () => boss?.Cast(ActionId.CosmoArrow,
            targetLocation: new Vector3(-0.008f, -0.015f, -0.008f), targetId: boss?.GameObjectId));
        TopP6CosmoArrowSequence.Run(world, damage, inFirst, 12.5f);
    }

    private void ScheduleAutoAttacks((float Swing, float Hit)[] attacks, float offset = 0f)
    {
        var firstHelper = SpawnHelper(new Placement(Vector3.Zero, 0f));
        var farthestHelper = SpawnHelper(new Placement(Vector3.Zero, 0f));
        foreach (var (swing, hit) in attacks)
        {
            SimCharacter? first = null;
            SimCharacter? farthest = null;
            world.Events.Add(offset + swing, () =>
            {
                if (failed || boss == null) return;
                Span<float> distances = stackalloc float[8];
                distances.Fill(float.PositiveInfinity);
                foreach (var member in party.ActiveMembers())
                    distances[(int)((ISimPartyMember)member).Role] =
                        Vector3.DistanceSquared(member.Position, boss.Position);
                var targets = TopP6AutoAttackTargets.Select(distances, solo, party.PlayerRole);
                first = party.Get(targets.First);
                farthest = party.Get(targets.Farthest);
                boss.SetTarget(first, follow: false);
                if (first != null) boss.Face(first);
                Release(boss, ActionId.AlphaOmegaAutoAttack, boss);
            });
            world.Events.Add(offset + hit, () =>
            {
                // Select both BEFORE damage: MT can bait both and die to the second hit.
                HitTarget(first, ActionId.Unknown7ddf, false, 0, true, firstHelper);
                HitTarget(farthest, ActionId.Unknown7ddf, false, 0, true, farthestHelper);
            });
        }
    }

    private void ScheduleCosmoDive()
    {
        ScheduleBossCast(29.592f, ActionId.CosmoDive, 5.3f, 35.189f);
        SimCharacter[] targets = [];
        world.Events.Add(35.189f, () =>
        {
            if (boss != null)
                targets = party.ActiveMembers().OrderBy(member => Vector3.DistanceSquared(member.Position, boss.Position)).Take(3).ToArray();
        });
        // 31654 is only the boss animation. Real 8y tank circles / 6y stack hit 2.4s later.
        world.Events.Add(37.592f, () =>
        {
            if (targets.Length < 3) return;
            HitTarget(targets[0], ActionId.CosmoDive_7BA7, true, 0, true);
            HitTarget(targets[1], ActionId.CosmoDive_7BA7, true, 0, true);
            HitTarget(targets[2], ActionId.CosmoDive_7BA8, false, 6, false);
        });
    }

    private void HitTarget(SimCharacter? target, uint action, bool tankbuster, int stack, bool applyVulnerability,
        SimEnemy? preparedHelper = null, bool killTargets = true, float? size = null)
    {
        if (failed || target == null || !target.IsAlive()) return;
        var placement = new Placement(target.Position, boss?.Rotation ?? 0f);
        var helper = preparedHelper ?? SpawnHelper(placement);
        if (helper == null) return;
        helper.SetPosition(placement);
        Release(helper, action, target);
        damage.Resolve(helper, action, tankbuster ? [DamageType.Magic, DamageType.TankBuster] : [DamageType.Magic],
            applyVulnerability ? [(StatusId.MagicVulnerabilityUp, 2f)] : [], stackMinTargets: stack, killTargets: killTargets, size: size);
        if (preparedHelper == null) world.Events.Add(2f, helper.Despawn);
    }

    private void ScheduleUnlimitedWaveCannon(bool second = false)
    {
        ScheduleBossCast(0f, ActionId.UnlimitedWaveCannon, 4.7f, 4.993f);
        var clockwise = rng.NextBool();
        var startAngle = Extended ? rng.NextInt(8) * MathF.PI / 4f : MathF.PI / 4f;
        Plugin.ChatGui.Print($"[AnoMech] {(second ? "第二次" : "首次")}波動砲：限制解除：{(clockwise ? "順時針" : "逆時針")}；前兩圈直走、第三圈轉斜向，第六圈放下即回八方。");
        for (var lane = 0; lane < ExaflareOffsets.Length; lane++)
        {
            // Recorded start NE -> N -> NW -> W (CCW). Mirror order around NE for CW.
            var angle = -startAngle + (clockwise ? -1 : 1) * lane * MathF.PI / 4;
            var inward = new Vector3(MathF.Sin(angle), 0f, MathF.Cos(angle));
            var at = ExaflareOffsets[lane];
            var firstHit = at + 12.001f;
            ScheduleHelper(at, firstHit, new Placement(-24f * inward, angle),
                ActionId.WaveCannon_7BAD, 11.7f, 4f, true);
            // Seven circles across the diameter: -24,-16,-8,0,8,16,24; 8y radius.
            // First repeat is ~1.1s after the arrow, subsequent hits ~1s apart.
            for (var step = 1; step <= 6; step++)
            {
                var hit = firstHit + 1.105f + (step - 1) * 1.004f;
                ScheduleHelper(hit, hit, new Placement((-24f + 8f * step) * inward, angle),
                    ActionId.WaveCannon_7BAE, 0f, 0f, true);
            }
        }
        for (var wave = 0; wave < PuddleCount; wave++)
            world.Events.Add(FirstPuddleAt + wave * PuddleInterval, DropPuddles);
        if (!solo && !MultiplayerContext.IsClient)
        {
            using var scope = SimRandom.HostOnly();
            if (Extended) TopP6FullAi.RunUnlimited(startAngle, clockwise, world, second);
            else TopP6AlphaOmegaAi.RunUnlimited(clockwise, world);
        }
        if (second)
        {
            ScheduleFullCosmoDive(SecondUnlimitedDiveAt, includeFollowUpAutos: true);
            world.Events.Add(SecondUnlimitedMeteorAt, ScheduleCosmoMeteorSequence);
        }
        else
        {
            ScheduleWaveCannon();
            world.Events.Add(WildChargeAt, ScheduleSecondCosmoArrow);
        }
    }

    private void ScheduleSecondCosmoArrow()
    {
        if (failed || boss == null) return;
        ScheduleAutoAttacks(FollowUpAutoAttacks);
        var inFirst = rng.NextBool();
        ScheduleBossCast(8.4f, ActionId.CosmoArrow, 5.7f, 14.4f);
        TopP6CosmoArrowSequence.Run(world, damage, inFirst, SecondArrowDelay);
        if (!solo && !MultiplayerContext.IsClient)
        {
            using var scope = SimRandom.HostOnly();
            if (Extended) TopP6FullAi.RunSecondArrow(inFirst, world);
            else TopP6AlphaOmegaAi.RunSecondArrow(inFirst, world);
        }
        if (Extended)
        {
            ScheduleWaveCannon(SecondCannonAt);
            world.Events.Add(SecondCannonAt + 11.37f, ScheduleSecondTail);
            return;
        }
        var lastArrowAt = SecondArrowDelay + (inFirst ? 23.91f : 21.91f);
        world.Events.Add(lastArrowAt + 2.1f, () =>
        {
            if (!failed) Plugin.ChatGui.Print("[AnoMech] P6 開場演練結束。");
        });
    }

    private void ScheduleWaveCannon(float cannonAt = CannonAt)
    {
        var order = RoleList.Random(party);
        if (Extended) ScheduleBossCast(cannonAt, ActionId.WaveCannon_7BA9, 10.6f, cannonAt + 10.882f);
        else world.Events.Add(cannonAt, () => boss?.Cast(ActionId.WaveCannon_7BA9,
            targetLocation: new Vector3(-0.008f, -0.015f, -0.008f), targetId: boss?.GameObjectId));
        for (var i = 0; i < 4; i++)
        {
            var index = i;
            var helper = SpawnHelper(new Placement(Vector3.Zero, 0f));
            for (var wave = 0; wave < 2; wave++)
            {
                var targetIndex = index + wave * 4;
                var hitAt = cannonAt + 3.04f + wave * 2f;
                world.Events.Add(hitAt - 0.07f, () =>
                {
                    if (party.Get(order[targetIndex]) is { } target) helper?.Face(target);
                });
                world.Events.Add(hitAt, () =>
                {
                    if (failed || helper == null || party.Get(order[targetIndex]) is not { } target) return;
                    helper.Cast(ActionId.WaveCannonProtean, castSeconds: 0f,
                        targetId: target.GameObjectId);
                    damage.Resolve(helper, ActionId.WaveCannonProtean, [DamageType.Magic],
                        [(StatusId.MagicVulnerabilityUp, 2.5f)]);
                });
            }
            world.Events.Add(cannonAt + 5.04f + 2f, () => helper?.Despawn());
        }
        var chargeTarget = solo ? party.PlayerRole : order[0];
        world.Events.Add(cannonAt + (Extended ? 10.782f : 11.27f), () =>
        {
            if (party.Get(chargeTarget) is { } target) boss?.Face(target);
        });
        world.Events.Add(cannonAt + 11.37f, () =>
        {
            if (failed || boss == null) return;
            boss.Cast(ActionId.WaveCannonWildCharge, castSeconds: 0f,
                targetId: party.Get(chargeTarget)?.GameObjectId);
            damage.Resolve(boss, ActionId.WaveCannonWildCharge, [DamageType.Magic], [],
                stackMinTargets: solo ? 1 : 8, wildChargeTargets: solo ? 0 : 2,
                wildChargeDamageType: [DamageType.TankBuster]);
        });
    }

    private void DropPuddles()
    {
        if (failed) return;
        // Snapshot ALL living players, not one farthest target and not their later positions.
        foreach (var member in party.ActiveMembers())
            ScheduleHelper(0f, PuddleDelay, new Placement(member.Position, 0f),
                ActionId.WaveCannon_7BAF, 2.7f, 0f, true);
    }

    private void ScheduleHelper(float at, float hit, Placement placement, uint action,
        float castSeconds, float omenDelay, bool lethal)
    {
        SimEnemy? helper = null;
        world.Events.Add(MathF.Max(0f, at - 0.1f), () => helper = SpawnHelper(placement));
        if (castSeconds > 0)
            world.Events.Add(at, () =>
            {
                if (!failed && helper != null)
                    helper.Cast(action, castSeconds: castSeconds, omenDelay: omenDelay,
                        targetId: helper.GameObjectId, fireDelay: MathF.Max(0f, hit - at - castSeconds));
            });
        world.Events.Add(hit, () =>
        {
            if (failed || helper == null) return;
            if (castSeconds <= 0) Release(helper, action, helper);
            if (lethal) damage.Resolve(helper, action, [DamageType.Lethal], []);
        });
        world.Events.Add(hit + 2f, () => helper?.Despawn());
    }

    private SimEnemy? SpawnHelper(Placement placement)
    {
        if (failed) return null;
        var helper = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.OmegaHelper, NameId: BNpcNameId.AlphaOmega, Level: 1,
            Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: placement));
        if (helper == null) Fail("P6 機制物件未能生成，停止本輪。");
        return helper;
    }

    private static void Release(SimEnemy caster, uint action, SimCharacter target)
    {
        caster.NativeActionEffect(action, 1.1f, checked((ushort)action), 0, ActionType.Action, 0,
            rotation: caster.Rotation, animationTargetId: target.GameObjectId, actionTargetId: target.GameObjectId);
    }

    private void Fail(string message)
    {
        failed = true;
        Plugin.ChatGui.PrintError(message);
        world.Party.WipeAllPlayers(message);
    }
}
