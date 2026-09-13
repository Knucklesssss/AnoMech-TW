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
    private readonly SimWorld world;
    private readonly SimPlayer player;
    private readonly IJobCombat model;
    private readonly CombatInputBuffer buffer = new();
    private readonly CombatNativeState native;
    private readonly SimCast cast;
    private long lastTick = Stopwatch.GetTimestamp();
    private long lastMessage;
    private bool inCombat;
    public bool Active { get; private set; }
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
        var sheet = Plugin.DataManager.GetExcelSheet<SheetAction>();
        foreach (var id in model.Actions.Append(7u))
            if (!sheet.TryGetRow(id, out _)) throw new InvalidOperationException($"Missing installed Action {id}.");
        cast = new SimCast(player, world.Coordinates);
        native = new CombatNativeState(player, job, model);
        Active = true;
        try { native.Mirror(false, resetAdditional: true); }
        catch { Stop("本機戰鬥初始化失敗"); throw; }
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
        { reason = $"本機戰鬥未啟動：{ex.Message}"; Plugin.Log.Error(ex, "Local combat activation failed"); return null; }
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
        if (!Alive)
        {
            buffer.Reset(); AutoAttacking = false; inCombat = false;
            return;
        }
        if (Plugin.GameInstance!.Paused) return;
        if (buffer.Pending is { } pending)
        {
            if (!Validate(pending.ActionId, pending.TargetId, false)) buffer.Reset();
            else if (Validate(pending.ActionId, pending.TargetId, true))
            { buffer.Take(true); Execute(pending.ActionId, pending.TargetId); }
        }
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

    public uint ActionStatus(uint id, ulong targetId, bool checkTiming)
    {
        if (!CheckIdentity()) return 572;
        if (targetId == 0xE0000000 || targetId == 0) targetId = CurrentTargetId();
        if (id == 7)
            return Alive && !Plugin.GameInstance!.Paused && !RestrictedStatus(movement: false)
                && (AutoAttacking || ResolveTarget(targetId) is { } target && InRange(7, target)) ? 0u : 572u;
        if (!Supports(id)) return 573;
        if (!Validate(id, targetId, false)) return 572;
        return !checkTiming || Validate(id, targetId, true) ? 0u : 582u;
    }

    public bool TryInput(ActionType type, uint id, ulong targetId, out bool accepted)
    {
        accepted = false;
        if (!CheckIdentity()) return false;
        if ((type == ActionType.GeneralAction && id == 1) || (type == ActionType.Action && id == 7))
        {
            if (!Alive || Plugin.GameInstance!.Paused || RestrictedStatus(movement: false)) { AutoAttacking = false; return true; }
            if (!AutoAttacking && ResolveTarget(targetId == 0 || targetId == 0xE0000000 ? CurrentTargetId() : targetId) == null)
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
        { Explain("目前僅模擬此職業已支援的技能循環與派生，不處理此技能／道具效果。"); return true; }
        // Resolve default target once at button press; queued input keeps this ID.
        targetId = targetId == 0xE0000000 || targetId == 0 ? CurrentTargetId() : targetId;
        id = Adjust(id);
        if (!Validate(id, targetId, false)) { Explain("技能條件不符：請檢查目標、距離、存活狀態與資源。"); return true; }
        if (Validate(id, targetId, true)) { buffer.Reset(); Execute(id, targetId); accepted = true; }
        else
        {
            var (group, recast, charges) = model.GetCooldown(id);
            var wait = Math.Max(model.Timing.LockRemaining,
                model.Timing.Charges(group, recast, charges) > 0 ? 0 : model.Timing.Remaining(group));
            wait = Math.Max(wait, native.AdditionalRemaining(id));
            accepted = buffer.Queue(id, targetId, wait);
        }
        return true;
    }

    private bool Validate(uint id, ulong targetId, bool timing)
    {
        if (!Alive || Plugin.GameInstance!.Paused || RestrictedStatus(movement: false)) return false;
        id = Adjust(id);
        var target = ResolveTarget(targetId);
        var hasTarget = model.IsSelfAction(id) ? Enemies().Any(e => InEffectRange(id, Position(player), e)) : target != null;
        if (model.IsGapCloser(id) && target != null && world.IsOutsideArena(GapEndpoint(target))) return false;
        return model.CanUse(id, hasTarget, target != null && InRange(id, target), inCombat, Alive, Bound, timing)
            && (!timing || native.AdditionalRemaining(id) <= 0);
    }

    private void Execute(uint id, ulong targetId)
    {
        id = Adjust(id);
        var target = ResolveTarget(targetId);
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
            if (action.GapCloser && target != null) player.SetPosition(GapEndpoint(target));
            inCombat = true;
        }
        var presentationTarget = self ? null : target;
        cast.Start(id, presentationTarget == null ? Position(player) : Position(presentationTarget), 0,
            presentationTarget?.GameObjectId ?? player.GameObjectId, 0, 0, 0, .6f);
        native.Mirror(AutoAttacking);
    }

    private System.Collections.Generic.IEnumerable<SimEnemy> Enemies()
        => world.Children.OfType<SimEnemy>().Where(Eligible);
    private static bool Eligible(SimEnemy enemy)
    {
        var p = enemy.BattleCharaPtr;
        return enemy.IsActive && p != null && p->Health > 0 && p->DrawObject != null && p->DrawObject->IsVisible
            && (p->TargetableStatus & ObjectTargetableFlags.IsTargetable) != 0;
    }
    private SimEnemy? ResolveTarget(ulong id) => Enemies().FirstOrDefault(e => e.GameObjectId.ObjectId == id);
    private static ulong CurrentTargetId() => Plugin.TargetManager.Target?.GameObjectId ?? 0xE0000000;
    private Vector3 Position(SimCharacter actor) => world.Coordinates.ToLocal(actor.BattleCharaPtr->Position);
    private bool InRange(uint id, SimEnemy enemy)
    {
        var row = Plugin.DataManager.GetExcelSheet<SheetAction>().GetRow(id);
        var distance = Vector3.Distance(Position(player), Position(enemy)) - player.HitboxRadius - enemy.HitboxRadius;
        var range = row.Range < 0 ? ActionManager.GetActionRange(id) : row.Range;
        return float.IsFinite(range) && range >= 0 && distance <= range;
    }
    private bool InEffectRange(uint id, Vector3 center, SimEnemy enemy)
        => Vector3.Distance(center, Position(enemy)) - enemy.HitboxRadius
            <= Plugin.DataManager.GetExcelSheet<SheetAction>().GetRow(id).EffectRange;
    private Vector3 GapEndpoint(SimEnemy target)
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
    public void BeforeNativeUpdate() { if (CheckIdentity()) native.SuppressNativeAutoAttack(); }
    public void AfterNativeUpdate() { if (CheckIdentity()) native.Mirror(AutoAttacking); }
    public void Stop(string reason)
    {
        if (!Active) return;
        Active = false;
        Reason = reason;
        buffer.Reset();
        AutoAttacking = false;
        try { native.Dispose(); }
        catch (Exception ex)
        {
            Reason = $"本機戰鬥已停止；還原失敗：{ex.Message}";
            Plugin.Log.Error(ex, "Local combat restoration failed");
        }
    }
    public void Dispose() => Stop("本機戰鬥已結束");
}
