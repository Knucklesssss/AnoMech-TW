using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AnoMech.Core.Native;
using AnoMech.Core.SimObjects;
using AnoMech.Pointers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Gauge;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace AnoMech.Core.Combat;

// Field-level ownership only. Never owned by SimPlayer's SimStatus list: its
// subsequent Despawn must not remove the originals restored here.
internal interface IJobGauge
{
    bool Matches { get; }
    void Mirror(IJobCombat rules);
    void Restore();
}

// Native gauge adapters stay out of JobCombatRegistry: the registry is compiled
// into the pure console tests, which cannot reference unsafe game types.
internal static class JobNativeGauge
{
    public static IJobGauge Create(byte classJob) => classJob switch
    {
        21 => new WarriorNativeGauge(),
        32 => new DarkKnightNativeGauge(),
        _ => throw new InvalidOperationException($"No native gauge adapter for job {classJob}."),
    };
}

public sealed unsafe class CombatNativeState : IDisposable
{
    private readonly JobCombatEntry job;
    private readonly IJobCombat rules;
    private readonly IJobGauge gauge;
    private readonly BattleChara* player;
    private readonly ActionManager* manager;
    private readonly UIState* ui;
    private readonly ulong objectId;
    private readonly long started = Stopwatch.GetTimestamp();
    private readonly uint comboAction;
    private readonly float comboTimer;
    private readonly float animationLock;
    private readonly bool autoAttack;
    private readonly List<RecastSnapshot> recasts = [];
    private readonly List<StatusSnapshot> statuses = [];
    private bool disposed;
    private bool written;

    private sealed record RecastSnapshot(int NativeGroup, uint BindingAction, bool Additional,
        uint ActionId, bool Active, float Elapsed, float Total);
    private sealed record StatusSnapshot(ushort Id, ushort Param, float Remaining, GameObjectId Source);

    internal CombatNativeState(SimPlayer simPlayer, JobCombatEntry job, IJobCombat rules)
    {
        this.job = job;
        this.rules = rules;
        var signaturesReady = true;
        foreach (var (name, address) in new (string, nint)[]
        {
            ("ActionManager.GetRecastGroup", ActionManager.Addresses.GetRecastGroup.Value),
            ("ActionManager.GetAdditionalRecastGroup", ActionManager.Addresses.GetAdditionalRecastGroup.Value),
            ("ActionManager.GetRecastGroupDetail", ActionManager.Addresses.GetRecastGroupDetail.Value),
            ("ActionManager.GetActionRange", ActionManager.Addresses.GetActionRange.Value),
            ("ActionManager.StartCooldown", ActionManager.Addresses.StartCooldown.Value),
            ("StatusManager.AddStatus", StatusManager.Addresses.AddStatus.Value),
            ("StatusManager.RemoveStatus", StatusManager.Addresses.RemoveStatus.Value),
            ("StatusManager.GetStatusIndex", StatusManager.Addresses.GetStatusIndex.Value),
            ("ActionEffectHandler.Receive", ActionEffectHandler.Addresses.Receive.Value),
        }) signaturesReady &= SignatureReport.TrackAddress(name, address) != 0;
        if (!Plugin.PlayerInputHooks.CombatHooksReady || StatusManagerPointers.OnGainStatus == null || !signaturesReady)
            throw new InvalidOperationException("Required combat native signatures are unavailable.");
        player = simPlayer.BattleCharaPtr;
        manager = ActionManager.Instance();
        ui = UIState.Instance();
        if (player == null || manager == null || ui == null)
            throw new InvalidOperationException("Local combat native state is unavailable.");
        gauge = JobNativeGauge.Create(job.ClassJob);
        objectId = player->GetGameObjectId().ObjectId;
        comboAction = manager->Combo.Action;
        comboTimer = manager->Combo.Timer;
        animationLock = manager->AnimationLock;
        autoAttack = ui->WeaponState.AutoAttackState.IsAutoAttacking;
        if (!float.IsFinite(comboTimer) || !float.IsFinite(animationLock))
            throw new InvalidOperationException("Invalid native combo/animation timer.");
        // Groups are de-duplicated, so the first listed action binds each group.
        foreach (var action in rules.Actions)
        {
            CaptureRecast(manager->GetRecastGroup((int)ActionType.Action, action), action, false);
            var additional = manager->GetAdditionalRecastGroup(ActionType.Action, action);
            if (additional >= 0) CaptureRecast(additional, action, true);
        }
        foreach (var action in rules.Actions) ValidateBindings(action);
        var freeSlots = 0;
        foreach (var status in player->StatusManager.Status)
        {
            if (status.StatusId == 0) freeSlots++;
            if (!rules.StatusIds.Contains(status.StatusId)) continue;
            if (statuses.Exists(s => s.Id == status.StatusId))
                throw new InvalidOperationException("Duplicate offensive status sources cannot be safely owned.");
            if (simPlayer.HasStatus(status.StatusId))
                throw new InvalidOperationException("Scenario already owns a local combat job status.");
            if (!float.IsFinite(status.RemainingTime)) throw new InvalidOperationException("Invalid native status timer.");
            statuses.Add(new(status.StatusId, status.Param, status.RemainingTime, status.SourceObject));
        }
        if (freeSlots + statuses.Count < rules.StatusIds.Count)
            throw new InvalidOperationException("Insufficient native status slots for local job buffs.");
        // All bindings and snapshots are validated before the first write.
    }

    private void CaptureRecast(int group, uint action, bool additional)
    {
        if (group < 0) throw new InvalidOperationException($"Missing native recast binding for {action}.");
        if (recasts.Exists(r => r.NativeGroup == group)) return;
        var detail = manager->GetRecastGroupDetail(group);
        if (detail == null) throw new InvalidOperationException($"Missing native recast record for {action}.");
        _ = CombatRecastView.Restore(detail->IsActive, detail->Elapsed, detail->Total, 0);
        recasts.Add(new(group, action, additional, detail->ActionId, detail->IsActive, detail->Elapsed, detail->Total));
    }

    public bool MatchesIdentity => !disposed && Plugin.ClientState.IsLoggedIn && Plugin.ObjectTable.LocalPlayer?.Address == (nint)player
        && player->GetGameObjectId().ObjectId == objectId && player->ClassJob == job.ClassJob && player->Level == job.Level
        && ActionManager.Instance() == manager && UIState.Instance() == ui
        && gauge.Matches;

    public void SuppressNativeAutoAttack()
    {
        if (!MatchesIdentity) return;
        ui->WeaponState.AutoAttackState.IsAutoAttacking = false;
    }

    private void ValidateBindings(uint action)
    {
        var main = manager->GetRecastGroup((int)ActionType.Action, action);
        var additional = manager->GetAdditionalRecastGroup(ActionType.Action, action);
        if (!recasts.Exists(r => r.NativeGroup == main && !r.Additional)
            || (additional >= 0 && !recasts.Exists(r => r.NativeGroup == additional && r.Additional)))
            throw new InvalidOperationException($"Action {action} has an unowned native recast binding.");
    }

    public double AdditionalRemaining(uint action)
    {
        if (!MatchesIdentity) throw new InvalidOperationException("Local combat player identity changed.");
        var group = manager->GetAdditionalRecastGroup(ActionType.Action, action);
        if (group < 0) return 0;
        var detail = manager->GetRecastGroupDetail(group);
        if (detail == null) throw new InvalidOperationException("Additional recast record disappeared.");
        _ = CombatRecastView.Restore(detail->IsActive, detail->Elapsed, detail->Total, 0);
        return detail->IsActive ? Math.Max(0, detail->Total - detail->Elapsed) : 0;
    }

    public void StartCooldown(uint action)
    {
        if (!MatchesIdentity) throw new InvalidOperationException("Local combat player identity changed.");
        ValidateBindings(action);
        manager->StartCooldown(ActionType.Action, action);
    }

    public void Mirror(bool autos, bool resetAdditional = false)
    {
        if (!MatchesIdentity) throw new InvalidOperationException("Local combat player identity changed.");
        written = true;
        manager->ActionQueued = false;
        manager->Combo.Action = rules.ComboAction;
        manager->Combo.Timer = (float)rules.ComboRemaining;
        manager->AnimationLock = (float)rules.Timing.LockRemaining;
        gauge.Mirror(rules);
        ui->WeaponState.AutoAttackState.IsAutoAttacking = autos;
        foreach (var recast in recasts)
        {
            // Native Update advances additional groups initialized by
            // StartCooldown. Do not apply the charge duration or global lock to them.
            if (recast.Additional && !resetAdditional) continue;
            if (!MatchesIdentity) return;
            var detail = manager->GetRecastGroupDetail(recast.NativeGroup);
            if (detail == null) throw new InvalidOperationException("Native recast record disappeared.");
            var (group, seconds, charges) = rules.GetCooldown(recast.BindingAction);
            var view = recast.Additional
                ? new CombatRecastView(false, 0, 0)
                : CombatRecastView.Project(seconds, charges, rules.Timing.Charges(group, seconds, charges), rules.Timing.Remaining(group));
            detail->ActionId = recast.BindingAction;
            detail->IsActive = view.IsActive;
            detail->Elapsed = (float)view.Elapsed;
            detail->Total = (float)view.Total;
        }
        foreach (var status in rules.Statuses())
            MirrorStatus(status.Id, status.Remaining, status.Param);
    }

    private void MirrorStatus(ushort id, double remaining, ushort param)
    {
        if (!MatchesIdentity) return;
        if (remaining <= 0) { Statuses.Remove((Character*)player, id); return; }
        if (player->StatusManager.GetStatusIndex(id) < 0)
            Statuses.AddStatusInit((Character*)player, id, param);
        if (MatchesIdentity) Statuses.Apply((Character*)player, id, (float)remaining, param, player->GetGameObjectId());
    }

    public void Dispose()
    {
        if (disposed) return;
        // Identity is checked before every write; no stale pointer is restored to
        // a new entity. Session marks itself inactive before calling this method.
        try
        {
            if (!written || !MatchesIdentity) return;
            var elapsed = Stopwatch.GetElapsedTime(started).TotalSeconds;
            manager->ActionQueued = false;
            manager->Combo.Timer = (float)Math.Max(0, comboTimer - elapsed);
            manager->Combo.Action = manager->Combo.Timer > 0 ? comboAction : 0;
            manager->AnimationLock = (float)Math.Max(0, animationLock - elapsed);
            gauge.Restore();
            ui->WeaponState.AutoAttackState.IsAutoAttacking = autoAttack;
            foreach (var recast in recasts)
            {
                if (!MatchesIdentity) return;
                var detail = manager->GetRecastGroupDetail(recast.NativeGroup);
                if (detail == null) continue;
                var view = CombatRecastView.Restore(recast.Active, recast.Elapsed, recast.Total, elapsed);
                detail->ActionId = recast.ActionId;
                detail->IsActive = view.IsActive;
                detail->Elapsed = (float)view.Elapsed;
                detail->Total = (float)view.Total;
            }
            foreach (var id in rules.StatusIds)
            {
                if (!MatchesIdentity) return;
                Statuses.Remove((Character*)player, id);
            }
            foreach (var status in statuses)
            {
                if (!MatchesIdentity) return;
                var remaining = status.Remaining - elapsed;
                if (remaining <= 0) continue;
                Statuses.AddStatusInit((Character*)player, status.Id, status.Param);
                if (MatchesIdentity) Statuses.Apply((Character*)player, status.Id, (float)remaining, status.Param, status.Source);
            }
        }
        finally { disposed = true; }
    }
}
