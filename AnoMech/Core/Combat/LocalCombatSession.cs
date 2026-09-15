using System;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Native;
using AnoMech.Core.SimObjects;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using SheetAction = Lumina.Excel.Sheets.Action;

namespace AnoMech.Core.Combat;

public sealed unsafe class LocalCombatSession : IDisposable
{
    // The game lets a cast finish when movement starts in its last half second.
    private const double SlidecastSeconds = 0.5;
    // ponytail: dash length is not in client data; a quarter second looked like the game's gap closers, tune in game.
    private const float DashSeconds = 0.25f;
    private readonly SimWorld world;
    private readonly SimPlayer player;
    private readonly IJobCombat model;
    private readonly CombatInputBuffer buffer = new();
    private readonly CombatNativeState native;
    private readonly SimCast cast;
    private readonly double weaponDelay;
    private long lastTick = Stopwatch.GetTimestamp();
    private long lastMessage;
    private bool inCombat;
    private double snapshotClock;
    private double castRemaining;
    private Vector3 castStartPosition;
    private ulong castTarget;
    private double castTotal;
    private bool castProbeLogged;
    private GameObjectId castTargetObject;
    private Vector3? returnPoint;
    private double autoAttackTimer;
    private readonly System.Collections.Generic.Dictionary<(uint Id, bool Timing), uint> statusSeen = [];
    private void Log(string text) { if (Plugin.LogManager.Enabled) Plugin.LogManager.LogSkill(text); }
    private void LogState(string label)
    {
        if (!Plugin.LogManager.Enabled) return;
        try { Log($"{label} rules: {model.DebugState} | {native.NativeDebugState()}"); }
        catch (Exception ex) { Log($"{label} state read failed: {ex.Message}"); }
    }
    public bool Active { get; private set; }
    public uint CastingAction => model.CastingAction;
    public double CastRemaining => Math.Max(0, castRemaining);
    public double CastTotal => castTotal;
    public float CastProgress => castTotal <= 0 ? 0 : (float)Math.Clamp(1 - castRemaining / castTotal, 0, 1);
    public bool AutoAttacking { get; private set; }
    public string Reason { get; private set; } = "本機技能循環進行中";

    private LocalCombatSession(SimWorld world, SimPlayer player, JobCombatEntry job)
    {
        this.world = world;
        this.player = player;
        if (SignatureReport.TrackAddress("ActionManager.GetAdjustedRecastTime", ActionManager.Addresses.GetAdjustedRecastTime.Value) == 0)
            throw new InvalidOperationException("技能冷卻讀取介面未就緒。");
        var gcd = ActionManager.GetAdjustedRecastTime(ActionType.Action, job.GcdProbeAction, true) / 1000d;
        if (gcd <= 0) throw new InvalidOperationException("無法讀取技能冷卻。");
        model = job.CreateRules(gcd);
        foreach (var id in model.Actions)
            model.OverrideRecast(id, ActionManager.GetAdjustedRecastTime(ActionType.Action, id, true) / 1000d);
        var sheet = Plugin.DataManager.GetExcelSheet<SheetAction>();
        foreach (var id in model.Actions.Append(7u))
            if (!sheet.TryGetRow(id, out _)) throw new InvalidOperationException($"Missing installed Action {id}.");
        weaponDelay = ReadWeaponDelay();
        autoAttackTimer = weaponDelay;
        cast = new SimCast(player, world.Coordinates);
        native = new CombatNativeState(player, job, model);
        Active = true;
        Log($"Start job={job.ClassJob} level={job.Level} gcd={gcd:0.00} weaponDelay={weaponDelay:0.00}");
        try { native.Mirror(false, resetAdditional: true); LogState("AfterReset"); }
        catch { Stop("本機戰鬥初始化失敗"); throw; }
    }

    // Main-hand delay drives auto-attack hits (Paladin Oath); 3 s when unreadable.
    private static double ReadWeaponDelay()
    {
        var inventory = InventoryManager.Instance();
        var equipped = inventory == null ? null : inventory->GetInventoryContainer(InventoryType.EquippedItems);
        var slot = equipped == null ? null : equipped->GetInventorySlot(0);
        if (slot == null || slot->ItemId == 0) return 3;
        var item = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>().GetRowOrDefault(slot->ItemId);
        return item is { } row && row.Delayms > 0 ? row.Delayms / 1000d : 3;
    }

    public static LocalCombatSession? Start(SimWorld world, byte level, out string reason)
    {
        reason = "本機技能循環未啟動";
        if (!world.Map.IsZoneLoaded || world.Party.Player is not { } player || player.BattleCharaPtr == null)
        { reason = "需要已載入的模擬場景"; return null; }
        var job = JobCombatRegistry.Find(player.BattleCharaPtr->ClassJob, player.BattleCharaPtr->Level);
        if (job == null || job.Level != level)
        {
            reason = "目前職業或同步等級尚未支援本機技能循環";
            Plugin.ChatGui.PrintError($"[AnoMech] {reason}，技能維持原本行為。");
            return null;
        }
        if (!Plugin.PlayerInputHooks.CombatHooksReady)
        { reason = "本機戰鬥必要 hook 未全部就緒"; return null; }
        try
        {
            var session = new LocalCombatSession(world, player, job);
            reason = session.Reason;
            return session;
        }
        catch (Exception ex)
        { reason = $"本機戰鬥未啟動：{ex.Message}"; Plugin.Log.Error(ex, "Local combat activation failed"); ErrorLog.Record("本機戰鬥未啟動", ex.Message, ex); return null; }
    }

    public bool CheckIdentity()
    {
        if (!Active) return false;
        if (Plugin.ClientState.IsLoggedIn && world.Map.IsZoneLoaded && Plugin.GameInstance?.ActiveScenario != null && native.MatchesIdentity) return true;
        Stop("本機戰鬥已停止：角色、職業或模擬區域已改變");
        return false;
    }

    public void Tick()
    {
        if (!CheckIdentity()) return;
        var now = Stopwatch.GetTimestamp();
        var seconds = Stopwatch.GetElapsedTime(lastTick, now).TotalSeconds;
        lastTick = now;
        model.Advance(seconds);
        buffer.Advance(seconds);
        cast.Tick((float)seconds);
        snapshotClock += seconds;
        if (snapshotClock >= 1)
        {
            snapshotClock = 0;
            LogState("Tick");
            if (Plugin.LogManager.Enabled) Log($"Hotbar {native.HotbarRecastDebugState()}");
        }
        if (!Alive)
        {
            CancelCast("dead");
            buffer.Reset(); AutoAttacking = false; inCombat = false;
            return;
        }
        if (Plugin.GameInstance!.Paused) { CancelCast("paused"); return; }
        if (model.CastingAction != 0) TickCast(seconds);
        TickAutoAttack(seconds);
        if (buffer.Pending is { } pending)
        {
            if (!Validate(pending.ActionId, pending.TargetId, false)) { Log($"QueueDropped id={pending.ActionId} {WhyNot(pending.ActionId, pending.TargetId)}"); buffer.Reset(); }
            else if (Validate(pending.ActionId, pending.TargetId, true))
            { buffer.Take(true); Log($"QueueFired id={pending.ActionId}"); Use(pending.ActionId, pending.TargetId); }
        }
    }

    private void TickCast(double seconds)
    {
        castRemaining -= seconds;
        // Real displacement, not the input sample: MovementInputActive keeps its last value on frames
        // without movement input and cancelled casts the moment they began.
        var moved = Vector2.Distance(new(Position(player).X, Position(player).Z), new(castStartPosition.X, castStartPosition.Z)) > 0.1f;
        if (castRemaining > SlidecastSeconds && (moved || Plugin.PlayerInputHooks.IsJumping))
        {
            CancelCast("moved");
            return;
        }
        if (castRemaining > 0) return;
        var id = model.CastingAction;
        var target = ResolveTarget(id, castTarget);
        var hasTarget = model.IsSelfAction(id) ? Enemies().Any(e => InEffectRange(id, Position(player), e)) : target != null;
        var done = model.CompleteCast(hasTarget, target != null && InRange(id, target), Alive, out var hit);
        if (hit != null) inCombat = true;
        if (done)
        {
            // A full cast bar for one write lets SimCast see the cast finish and play the release.
            native.SetCast(id, castTotal, castTotal, castTargetObject);
            native.Mirror(AutoAttacking);
            cast.Tick(0);
        }
        else
        {
            cast.Despawn();
        }
        native.SetCast(0, 0, 0, default);
        native.Mirror(AutoAttacking);
        Log($"CastComplete id={id} done={done} hit={hit?.ToString() ?? "null"}");
    }

    private void CancelCast(string reason)
    {
        if (model.CastingAction == 0) return;
        Log($"CastCancel id={model.CastingAction} reason={reason}");
        model.CancelCast();
        cast.Despawn();
        native.SetCast(0, 0, 0, default);
        native.Mirror(AutoAttacking);
    }

    private void TickAutoAttack(double seconds)
    {
        if (!AutoAttacking || model.CastingAction != 0 || ResolveTarget(7, CurrentTargetId()) is not { } target || !InRange(7, target))
        {
            autoAttackTimer = Math.Min(weaponDelay, autoAttackTimer + seconds);
            return;
        }
        autoAttackTimer += seconds;
        if (autoAttackTimer < weaponDelay) return;
        autoAttackTimer -= weaponDelay;
        model.AutoAttackHit();
        inCombat = true;
    }

    private bool Alive => !player.Dead && player.BattleCharaPtr != null && player.BattleCharaPtr->Health > 0;
    private bool Bound => RestrictedStatus(movement: true);
    private bool RestrictedStatus(bool movement)
    {
        var sheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Status>();
        foreach (var status in player.BattleCharaPtr->StatusManager.Status)
        {
            if (status.StatusId == 0 || !sheet.TryGetRow(status.StatusId, out var row)) continue;
            if (movement ? row.LockMovement || row.LockControl : row.LockActions || row.LockControl) return true;
        }
        return false;
    }
    public uint Adjust(uint id) => model.Adjust(id);
    public bool Supports(uint id) => model.Supports(id);
    public bool IsGroundTargeted(uint id) => Plugin.DataManager.GetExcelSheet<SheetAction>().GetRow(Adjust(id)).TargetArea;
    public bool IsHighlighted(uint id) => model.IsHighlighted(id);

    public uint ActionStatus(uint id, ulong targetId, bool checkTiming)
    {
        if (!CheckIdentity()) return 572;
        if (targetId == 0xE0000000 || targetId == 0) targetId = CurrentTargetId();
        if (id == 7)
            return Alive && !Plugin.GameInstance!.Paused && !RestrictedStatus(movement: false)
                && (AutoAttacking || ResolveTarget(7, targetId) is { } target && InRange(7, target)) ? 0u : 572u;
        var status = !Supports(id) ? 573u
            : !Validate(id, targetId, false) ? 572u
            : !checkTiming || Validate(id, targetId, true) ? 0u : 582u;
        // The hotbar greys an icon from this status; log only changes so the file stays readable.
        if (Plugin.LogManager.Enabled && (!statusSeen.TryGetValue((id, checkTiming), out var last) || last != status))
        {
            statusSeen[(id, checkTiming)] = status;
            Log($"Status id={id} adjusted={Adjust(id)} timing={checkTiming} status={status} {WhyNot(id, targetId)}");
        }
        return status;
    }

    public bool TryInput(ActionType type, uint id, ulong targetId, out bool accepted)
    {
        accepted = false;
        var handled = TryInputCore(type, id, targetId, out accepted);
        Log($"Input type={type} id={id} adjusted={(type == ActionType.Action ? Adjust(id) : id)} handled={handled} accepted={accepted}");
        return handled;
    }

    public bool TryInputAt(ActionType type, uint id, ulong targetId, Vector3 worldLocation, out bool accepted)
    {
        accepted = false;
        if (!CheckIdentity() || type != ActionType.Action || !Supports(id) || !IsGroundTargeted(id)) return false;
        id = Adjust(id);
        var point = world.Coordinates.ToLocal(worldLocation);
        var range = Plugin.DataManager.GetExcelSheet<SheetAction>().GetRow(id).Range;
        if (Vector3.Distance(Position(player), point) > range || world.IsOutsideArena(point) || !Validate(id, 0, true))
        {
            Log($"RejectedGround id={id} point={point} {WhyNot(id, 0)}");
            return true;
        }
        buffer.Reset();
        Execute(id, 0, point);
        accepted = true;
        Log($"InputGround id={id} point={point}");
        return true;
    }

    private bool TryInputCore(ActionType type, uint id, ulong targetId, out bool accepted)
    {
        accepted = false;
        if (!CheckIdentity()) return false;
        if ((type == ActionType.GeneralAction && id == 1) || (type == ActionType.Action && id == 7))
        {
            if (!Alive || Plugin.GameInstance!.Paused || RestrictedStatus(movement: false)) { AutoAttacking = false; return true; }
            if (!AutoAttacking && ResolveTarget(7, targetId == 0 || targetId == 0xE0000000 ? CurrentTargetId() : targetId) == null)
            { Explain("自動攻擊需要目前可選取的模擬敵人。"); return true; }
            AutoAttacking = !AutoAttacking;
            accepted = true;
            return true;
        }
        if (Plugin.GameInstance!.Paused && (type is ActionType.Action or ActionType.Item
            || (type == ActionType.GeneralAction && id == 4))) return true;
        if (type != ActionType.Action && type != ActionType.Item) return false;
        if (type == ActionType.Action && id == LocalPlayerInputHooks.SprintActionId) return false;
        if (type != ActionType.Action || !Supports(id))
        {
            if (type == ActionType.Action) ErrorLog.Record("按到不支援的技能", $"id={id}");
            Explain("目前僅模擬此職業已支援的技能循環與派生，不處理此技能／道具效果。");
            return true;
        }
        // Ground-targeted actions (Shukuchi) let the client open its placement circle; the click arrives in TryInputAt.
        if (IsGroundTargeted(id)) return false;
        // Resolve default target once at button press; queued input keeps this ID.
        targetId = targetId == 0xE0000000 || targetId == 0 ? CurrentTargetId() : targetId;
        id = Adjust(id);
        if (!Validate(id, targetId, false)) { Log($"Rejected id={id} {WhyNot(id, targetId)}"); return true; }
        if (Validate(id, targetId, true)) { buffer.Reset(); Use(id, targetId); accepted = true; }
        else
        {
            var (group, recast, charges) = model.GetCooldown(id);
            var wait = Math.Max(model.Timing.LockRemaining,
                model.Timing.Charges(group, recast, charges) > 0 ? 0 : model.Timing.Remaining(group));
            wait = Math.Max(wait, native.AdditionalRemaining(id));
            accepted = buffer.Queue(id, targetId, wait);
            Log($"Queue id={id} wait={wait:0.00} queued={accepted} {WhyNot(id, targetId)}");
        }
        return true;
    }

    private bool Validate(uint id, ulong targetId, bool timing)
    {
        if (!Alive || Plugin.GameInstance!.Paused || RestrictedStatus(movement: false)) return false;
        id = Adjust(id);
        var target = ResolveTarget(id, targetId);
        var hasTarget = model.IsSelfAction(id) ? Enemies().Any(e => InEffectRange(id, Position(player), e)) : target != null;
        if (model.IsGapCloser(id) && target != null && world.IsOutsideArena(GapEndpoint(target))) return false;
        return model.CanUse(id, hasTarget, target != null && InRange(id, target), inCombat, Alive, Bound, timing)
            && (!timing || native.AdditionalRemaining(id) <= 0);
    }

    private string WhyNot(uint id, ulong targetId)
    {
        if (!Plugin.LogManager.Enabled) return "";
        try { return WhyNotCore(Adjust(id), targetId); }
        catch (Exception ex) { return $"reason read failed: {ex.Message}"; }
    }

    private string WhyNotCore(uint id, ulong targetId)
    {
        var target = ResolveTarget(id, targetId);
        var self = model.IsSelfAction(id);
        var hasTarget = self ? Enemies().Any(e => InEffectRange(id, Position(player), e)) : target != null;
        var inRange = target != null && InRange(id, target);
        return $"alive={Alive} paused={Plugin.GameInstance!.Paused} locked={RestrictedStatus(movement: false)} target={(target != null)} self={self} hasTarget={hasTarget} inRange={inRange} "
            + $"rules={model.CanUse(id, hasTarget, inRange, inCombat, Alive, Bound, false)} rulesTimed={model.CanUse(id, hasTarget, inRange, inCombat, Alive, Bound, true)} extra={native.AdditionalRemaining(id):0.00}";
    }

    private void Use(uint id, ulong targetId)
    {
        if (model.CastTime(Adjust(id)) > 0) BeginCast(id, targetId);
        else Execute(id, targetId);
    }

    private void BeginCast(uint id, ulong targetId)
    {
        id = Adjust(id);
        var target = ResolveTarget(id, targetId);
        var self = model.IsSelfAction(id);
        var hasTarget = self ? Enemies().Any(e => InEffectRange(id, Position(player), e)) : target != null;
        var seconds = model.CastTime(id);
        if (!model.BeginCast(id, hasTarget, target != null && InRange(id, target), inCombat, Alive, Bound)) return;
        native.StartCooldown(id);
        Plugin.PlayerInputHooks.RecordLocalAction();
        castRemaining = seconds;
        castStartPosition = Position(player);
        castTotal = seconds;
        castProbeLogged = false;
        castTarget = targetId;
        var presentationTarget = self ? null : target;
        castTargetObject = presentationTarget?.GameObjectId ?? player.GameObjectId;
        // Before Start: SimCast reads its total from CastInfo, which the ActorCast packet doesn't fill for the
        // local player; left at 0 from the previous cast it fired the release as soon as the cast began.
        native.SetCast(id, 0, seconds, castTargetObject);
        native.Mirror(AutoAttacking);
        cast.Start(id, presentationTarget == null ? Position(player) : Position(presentationTarget), (float)seconds,
            castTargetObject, 0, 0, 0, .1f);
        native.Mirror(AutoAttacking);
        Log($"CastBegin id={id} seconds={seconds:0.00}");
    }

    private void Execute(uint id, ulong targetId, Vector3? groundPoint = null)
    {
        id = Adjust(id);
        var target = ResolveTarget(id, targetId);
        var self = model.IsSelfAction(id);
        var hasTarget = self ? Enemies().Any(e => InEffectRange(id, Position(player), e)) : target != null;
        var hit = model.TryUse(id, hasTarget, target != null && InRange(id, target), inCombat, Alive, Bound);
        // This client-only timer initializer supplies the actual additional
        // recast duration. Main groups are replaced by the pure model below.
        native.StartCooldown(id);
        // CanUse was already true: null also means a successful buff or empty AoE.
        Plugin.PlayerInputHooks.RecordLocalAction();
        if (hit is { } action)
        {
            if (action.GapCloser && target != null)
            {
                var end = GapEndpoint(target);
                player.Dash(end, MathF.Max(1f, Vector3.Distance(Position(player), end) / DashSeconds));
            }
            inCombat = true;
        }
        if (model.TakeMove() is { } move) PerformMove(move, groundPoint);
        var presentationTarget = self ? null : target;
        cast.Start(id, presentationTarget == null ? Position(player) : Position(presentationTarget), 0,
            presentationTarget?.GameObjectId ?? player.GameObjectId, 0, 0, 0, .6f);
        native.Mirror(AutoAttacking);
        Log($"Execute id={id} hit={hit?.ToString() ?? "null"}");
        LogState("AfterExecute");
    }

    private void PerformMove(JobMove move, Vector3? groundPoint)
    {
        var from = Position(player);
        var rotation = player.BattleCharaPtr->Rotation;
        var facing = new Vector3(MathF.Sin(rotation), 0, MathF.Cos(rotation));
        Vector3? end = move.Kind switch
        {
            JobMoveKind.Backward => InsideArena(from, -facing * move.Distance),
            JobMoveKind.Forward => InsideArena(from, facing * move.Distance),
            JobMoveKind.ReturnPoint => returnPoint,
            JobMoveKind.GroundPoint => groundPoint,
            _ => null,
        };
        if (move.MarksReturn) returnPoint = from;
        if (end is { } point)
            player.Dash(point, MathF.Max(1f, Vector3.Distance(from, point) / DashSeconds));
        Log($"Move kind={move.Kind} distance={move.Distance} end={end?.ToString() ?? "none"}");
    }

    // A self displacement stops short of the arena fence instead of carrying the player out of it.
    private Vector3 InsideArena(Vector3 from, Vector3 offset)
    {
        for (var step = 20; step > 0; step--)
            if (!world.IsOutsideArena(from + offset * (step / 20f))) return from + offset * (step / 20f);
        return from;
    }

    private System.Collections.Generic.IEnumerable<SimEnemy> Enemies()
        => world.Children.OfType<SimEnemy>().Where(Eligible);
    private static bool Eligible(SimEnemy enemy)
    {
        var p = enemy.BattleCharaPtr;
        return enemy.IsActive && p != null && p->Health > 0 && p->DrawObject != null && p->DrawObject->IsVisible
            && (p->TargetableStatus & ObjectTargetableFlags.IsTargetable) != 0;
    }
    // Enemy actions take a simulated enemy; party-only actions (Aetherial Manipulation) a party member; actions that take
    // either (Thunderclap, Slither) prefer the enemy and fall back to a party member.
    private SimCharacter? ResolveTarget(uint actionId, ulong id)
    {
        var row = Plugin.DataManager.GetExcelSheet<SheetAction>().GetRow(actionId);
        if (!row.CanTargetParty || row.CanTargetHostile)
        {
            var enemy = Enemies().FirstOrDefault(e => e.GameObjectId.ObjectId == id);
            if (enemy != null || !row.CanTargetParty) return enemy;
        }
        for (var role = 0; role < 8; role++)
            if (world.Party.Get(role) is { } member && member != player && member.BattleCharaPtr != null && member.IsAlive()
                && member.GameObjectId.ObjectId == id)
                return member;
        return null;
    }
    private static ulong CurrentTargetId() => Plugin.TargetManager.Target?.GameObjectId ?? 0xE0000000;
    private Vector3 Position(SimCharacter actor) => world.Coordinates.ToLocal(actor.BattleCharaPtr->Position);
    private bool InRange(uint id, SimCharacter target)
    {
        var row = Plugin.DataManager.GetExcelSheet<SheetAction>().GetRow(id);
        var distance = Vector3.Distance(Position(player), Position(target)) - player.HitboxRadius - target.HitboxRadius;
        var range = row.Range < 0 ? ActionManager.GetActionRange(id) : row.Range;
        return float.IsFinite(range) && range >= 0 && distance <= range;
    }
    private bool InEffectRange(uint id, Vector3 center, SimEnemy enemy)
        => Vector3.Distance(center, Position(enemy)) - enemy.HitboxRadius
            <= Plugin.DataManager.GetExcelSheet<SheetAction>().GetRow(id).EffectRange;
    private Vector3 GapEndpoint(SimCharacter target)
    {
        var from = Position(player);
        var to = Position(target);
        var delta = from - to;
        var distance = delta.Length();
        return distance <= player.HitboxRadius + target.HitboxRadius ? from
            : to + delta / distance * (player.HitboxRadius + target.HitboxRadius);
    }

    private void Explain(string text)
    {
        var now = Stopwatch.GetTimestamp();
        if (lastMessage != 0 && Stopwatch.GetElapsedTime(lastMessage, now).TotalSeconds < 2) return;
        lastMessage = now;
        Plugin.ChatGui.PrintError($"[AnoMech] {text}");
    }
    public void BeforeNativeUpdate()
    {
        if (!CheckIdentity()) return;
        native.SuppressNativeAutoAttack();
        if (Plugin.LogManager.Enabled) native.SampleHotbarRecast();
    }
    public void AfterNativeUpdate()
    {
        if (!CheckIdentity()) return;
        var probe = model.CastingAction != 0 && !castProbeLogged && castRemaining <= castTotal / 2 && Plugin.LogManager.Enabled;
        if (probe) Log($"CastProbe afterNativeUpdate {native.CastDebugState()}");
        if (model.CastingAction != 0) native.SetCast(model.CastingAction, castTotal - castRemaining, castTotal, castTargetObject);
        native.Mirror(AutoAttacking);
        if (Plugin.LogManager.Enabled) native.SampleHotbarRecast();
        if (probe) { castProbeLogged = true; Log($"CastProbe afterMirror {native.CastDebugState()}"); }
    }
    public bool SurviveLethal()
    {
        if (!Active || !model.SurviveLethal()) return false;
        Log("SurvivedLethal");
        native.Mirror(AutoAttacking);
        return true;
    }

    public void Stop(string reason)
    {
        if (!Active) return;
        // Inactive before logging: the state read must not re-enter Stop through the hooks.
        Active = false;
        Log($"Stop reason={reason}");
        if (reason != "本機戰鬥已結束" && reason != "本機戰鬥已停止：角色、職業或模擬區域已改變")
            ErrorLog.Record("本機戰鬥異常停止", reason);
        LogState("BeforeStop");
        Reason = reason;
        buffer.Reset();
        AutoAttacking = false;
        try
        {
            // A cast bar left on the player would outlive the simulation.
            if (model.CastingAction != 0 && native.MatchesIdentity) cast.Despawn();
            native.SetCast(0, 0, 0, default);
            native.Dispose();
            LogState("AfterStop");
        }
        catch (Exception ex)
        {
            Reason = $"本機戰鬥已停止；還原失敗：{ex.Message}";
            Plugin.Log.Error(ex, "Local combat restoration failed");
            ErrorLog.Record("本機戰鬥還原失敗", ex.Message, ex);
        }
    }
    public void Dispose() => Stop("本機戰鬥已結束");
}
