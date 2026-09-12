using System;
using System.Collections.Generic;
using System.Diagnostics;
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
public sealed unsafe class CombatNativeState : IDisposable
{
    private static readonly ushort[] StatusIds = [1177, 1897, 2677, 2624];
    private static readonly uint[] BindingActions = [31, 52, 7389, 7386, 7387];
    private readonly BattleChara* player;
    private readonly ActionManager* manager;
    private readonly WarriorGauge* gauge;
    private readonly UIState* ui;
    private readonly ulong objectId;
    private readonly long started = Stopwatch.GetTimestamp();
    private readonly uint comboAction;
    private readonly float comboTimer;
    private readonly float animationLock;
    private readonly byte beast;
    private readonly bool autoAttack;
    private readonly List<RecastSnapshot> recasts = [];
    private readonly List<StatusSnapshot> statuses = [];
    private bool disposed;
    private bool written;

    private sealed record RecastSnapshot(int NativeGroup, uint BindingAction, bool Additional,
        uint ActionId, bool Active, float Elapsed, float Total);
    private sealed record StatusSnapshot(ushort Id, ushort Param, float Remaining, GameObjectId Source);

    public CombatNativeState(SimPlayer simPlayer)
    {
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
        var job = JobGaugeManager.Instance();
        if (player == null || manager == null || ui == null || job == null || job->ClassJobId != 21 || job->CurrentGauge == null)
            throw new InvalidOperationException("Warrior native state is unavailable.");
        gauge = (WarriorGauge*)job->CurrentGauge;
        objectId = player->GetGameObjectId().ObjectId;
        comboAction = manager->Combo.Action;
        comboTimer = manager->Combo.Timer;
        animationLock = manager->AnimationLock;
        beast = gauge->BeastGauge;
        autoAttack = ui->WeaponState.AutoAttackState.IsAutoAttacking;
        if (!float.IsFinite(comboTimer) || !float.IsFinite(animationLock))
            throw new InvalidOperationException("Invalid native combo/animation timer.");
        foreach (var action in BindingActions)
        {
            CaptureRecast(manager->GetRecastGroup((int)ActionType.Action, action), action, false);
            var additional = manager->GetAdditionalRecastGroup(ActionType.Action, action);
            if (additional >= 0) CaptureRecast(additional, action, true);
        }
        foreach (var action in LocalCombatSession.OffensiveActions) ValidateBindings(action);
        var freeSlots = 0;
        foreach (var status in player->StatusManager.Status)
        {
            if (status.StatusId == 0) freeSlots++;
            if (Array.IndexOf(StatusIds, status.StatusId) < 0) continue;
            if (statuses.Exists(s => s.Id == status.StatusId))
                throw new InvalidOperationException("Duplicate offensive status sources cannot be safely owned.");
            if (simPlayer.HasStatus(status.StatusId))
                throw new InvalidOperationException("Scenario already owns an offensive Warrior status.");
            if (!float.IsFinite(status.RemainingTime)) throw new InvalidOperationException("Invalid native status timer.");
            statuses.Add(new(status.StatusId, status.Param, status.RemainingTime, status.SourceObject));
        }
        if (freeSlots + statuses.Count < StatusIds.Length)
            throw new InvalidOperationException("Insufficient native status slots for local Warrior buffs.");
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
        && player->GetGameObjectId().ObjectId == objectId && player->ClassJob == 21 && player->Level == 90
        && ActionManager.Instance() == manager && UIState.Instance() == ui
        && JobGaugeManager.Instance() != null && JobGaugeManager.Instance()->ClassJobId == 21
        && JobGaugeManager.Instance()->CurrentGauge == (void*)gauge;

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

    public void Mirror(WarriorCombat model, bool autos, bool resetAdditional = false)
    {
        if (!MatchesIdentity) throw new InvalidOperationException("Local combat player identity changed.");
        written = true;
        manager->ActionQueued = false;
        manager->Combo.Action = model.ComboAction;
        manager->Combo.Timer = (float)model.ComboRemaining;
        manager->AnimationLock = (float)model.Timing.LockRemaining;
        gauge->BeastGauge = (byte)model.Beast;
        ui->WeaponState.AutoAttackState.IsAutoAttacking = autos;
        foreach (var recast in recasts)
        {
            // Native Update advances additional groups initialized by
            // StartCooldown. Do not apply the charge duration or global lock to them.
            if (recast.Additional && !resetAdditional) continue;
            if (!MatchesIdentity) return;
            var detail = manager->GetRecastGroupDetail(recast.NativeGroup);
            if (detail == null) throw new InvalidOperationException("Native recast record disappeared.");
            var (group, seconds, charges) = model.GetCooldown(recast.BindingAction);
            var view = recast.Additional
                ? new CombatRecastView(false, 0, 0)
                : CombatRecastView.Project(seconds, charges, model.Timing.Charges(group, seconds, charges), model.Timing.Remaining(group));
            detail->ActionId = recast.BindingAction;
            detail->IsActive = view.IsActive;
            detail->Elapsed = (float)view.Elapsed;
            detail->Total = (float)view.Total;
        }
        MirrorStatus(1177, model.InnerReleaseRemaining, (ushort)model.InnerReleaseStacks);
        MirrorStatus(1897, model.ChaosRemaining, 0);
        MirrorStatus(2677, model.TempestRemaining, 0);
        MirrorStatus(2624, model.RendRemaining, 0);
    }

    private void MirrorStatus(ushort id, double remaining, ushort param)
    {
        if (!MatchesIdentity) return;
        if (remaining <= 0 || (id == 1177 && param == 0)) { Statuses.Remove((Character*)player, id); return; }
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
            gauge->BeastGauge = beast;
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
            foreach (var id in StatusIds)
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
