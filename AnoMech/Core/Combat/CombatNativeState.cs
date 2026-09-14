using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.Native;
using AnoMech.Core.SimObjects;
using AnoMech.Pointers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace AnoMech.Core.Combat;

internal interface IJobGauge
{
    bool Matches { get; }
    void Mirror(IJobCombat rules);
}

// Native gauge adapters stay out of JobCombatRegistry: the registry is compiled
// into the pure console tests, which cannot reference unsafe game types.
internal static class JobNativeGauge
{
    public static IJobGauge Create(byte classJob) => classJob switch
    {
        19 => new PaladinNativeGauge(),
        21 => new WarriorNativeGauge(),
        32 => new DarkKnightNativeGauge(),
        37 => new GunbreakerNativeGauge(),
        24 => new WhiteMageNativeGauge(),
        28 => new ScholarNativeGauge(),
        33 => new AstrologianNativeGauge(),
        40 => new SageNativeGauge(),
        _ => throw new InvalidOperationException($"No native gauge adapter for job {classJob}."),
    };
}

// Writes the rules' state into the client every frame. No snapshot is kept:
// a run starts from a fresh rules instance and Dispose writes a reset one, like
// entering and leaving a duty. Each write is guarded by the pointer it touches,
// so a changed player cannot block the ActionManager-wide cooldown reset.
public sealed unsafe class CombatNativeState : IDisposable
{
    private readonly JobCombatEntry job;
    private readonly IJobCombat rules;
    private readonly IJobGauge gauge;
    private readonly BattleChara* player;
    private readonly ActionManager* manager;
    private readonly UIState* ui;
    private readonly ulong objectId;
    private readonly List<(int NativeGroup, uint BindingAction, bool Additional)> recasts = [];
    private bool disposed;

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
        // Main groups first: an additional group can be another action's main group
        // (Gnashing Fang and Double Down also start the GCD).
        foreach (var action in rules.Actions)
            AddRecast(manager->GetRecastGroup((int)ActionType.Action, action), action, false);
        foreach (var action in rules.Actions)
        {
            var additional = manager->GetAdditionalRecastGroup(ActionType.Action, action);
            if (additional >= 0) AddRecast(additional, action, true);
        }
        foreach (var action in rules.Actions) ValidateBindings(action);
        var freeSlots = 0;
        foreach (var status in player->StatusManager.Status)
        {
            if (status.StatusId == 0) freeSlots++;
            else if (rules.StatusIds.Contains(status.StatusId))
            {
                if (simPlayer.HasStatus(status.StatusId))
                    throw new InvalidOperationException("Scenario already owns a local combat job status.");
                freeSlots++; // removed by the first Mirror
            }
        }
        if (freeSlots < rules.StatusIds.Count)
            throw new InvalidOperationException("Insufficient native status slots for local job buffs.");
        var statusManager = &player->StatusManager;
        rules.Seed(id => statusManager->GetStatusIndex(id) >= 0);
    }

    private void AddRecast(int group, uint action, bool additional)
    {
        if (group < 0) throw new InvalidOperationException($"Missing native recast binding for {action}.");
        if (recasts.Exists(r => r.NativeGroup == group)) return;
        if (manager->GetRecastGroupDetail(group) == null) throw new InvalidOperationException($"Missing native recast record for {action}.");
        recasts.Add((group, action, additional));
    }

    private bool PlayerMatches => Plugin.ClientState.IsLoggedIn && Plugin.ObjectTable.LocalPlayer?.Address == (nint)player
        && player->GetGameObjectId().ObjectId == objectId;

    public bool MatchesIdentity => !disposed && PlayerMatches && player->ClassJob == job.ClassJob && player->Level == job.Level
        && ActionManager.Instance() == manager && UIState.Instance() == ui && gauge.Matches;

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
            || (additional >= 0 && !recasts.Exists(r => r.NativeGroup == additional)))
            throw new InvalidOperationException($"Action {action} has an unowned native recast binding.");
    }

    // What the client actually holds, for comparing against the rules in the skill log.
    public string NativeDebugState()
    {
        if (ActionManager.Instance() != manager) return "native=ActionManager changed";
        var text = $"native combo={manager->Combo.Action}/{manager->Combo.Timer:0.0}s lock={manager->AnimationLock:0.00}";
        var gcd = manager->GetRecastGroupDetail(manager->GetRecastGroup((int)ActionType.Action, job.GcdProbeAction));
        if (gcd != null) text += $" gcdRecast={(gcd->IsActive ? "on" : "off")}/{gcd->Elapsed:0.00}/{gcd->Total:0.00}";
        if (PlayerMatches) text += $" mp={player->Mana}/{player->MaxMana} statuses=[{string.Join(",", rules.StatusIds.Where(id => player->StatusManager.GetStatusIndex(id) >= 0))}]";
        else text += " player=changed";
        // group:actionId:active:elapsed/total for every owned record; a = additional group.
        var records = new System.Text.StringBuilder(" recasts=");
        foreach (var recast in recasts)
        {
            var record = manager->GetRecastGroupDetail(recast.NativeGroup);
            if (record == null) continue;
            records.Append($"{recast.NativeGroup}{(recast.Additional ? "a" : "")}:{record->ActionId}:{(record->IsActive ? 1 : 0)}:{record->Elapsed:0.00}/{record->Total:0.00} ");
        }
        return text + records;
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
        Write(autos, resetAdditional);
    }

    private void Write(bool autos, bool resetAdditional)
    {
        if (ActionManager.Instance() == manager)
        {
            manager->ActionQueued = false;
            manager->Combo.Action = rules.ComboAction;
            manager->Combo.Timer = (float)rules.ComboRemaining;
            manager->AnimationLock = (float)rules.Timing.LockRemaining;
            foreach (var recast in recasts)
            {
                // Native Update advances additional groups initialized by
                // StartCooldown; only a reset clears them.
                if (recast.Additional && !resetAdditional) continue;
                var detail = manager->GetRecastGroupDetail(recast.NativeGroup);
                if (detail == null) continue;
                var (group, seconds, charges) = rules.GetBindingCooldown(recast.BindingAction);
                var view = recast.Additional
                    ? new CombatRecastView(false, 0, 0)
                    // The GCD group mixes skill-speed and spell-speed recasts (Paladin: 2.49 s Fast Blade, 2.50 s Holy Spirit);
                    // the bound action's recast may be shorter than what the last use left.
                    : CombatRecastView.Project(seconds, charges, rules.Timing.Charges(group, seconds, charges), Math.Min(seconds, rules.Timing.Remaining(group)));
                detail->ActionId = recast.BindingAction;
                detail->IsActive = view.IsActive;
                detail->Elapsed = (float)view.Elapsed;
                detail->Total = (float)view.Total;
            }
        }
        if (UIState.Instance() == ui) ui->WeaponState.AutoAttackState.IsAutoAttacking = autos;
        if (gauge.Matches) gauge.Mirror(rules);
        if (!PlayerMatches) return;
        player->Mana = (uint)Math.Min(rules.Mp, player->MaxMana);
        foreach (var status in rules.Statuses())
            MirrorStatus(status.Id, status.Remaining, status.Param);
    }

    private void MirrorStatus(ushort id, double remaining, ushort param)
    {
        if (!PlayerMatches) return;
        if (remaining <= 0) { Statuses.Remove((Character*)player, id); return; }
        if (player->StatusManager.GetStatusIndex(id) < 0)
            Statuses.AddStatusInit((Character*)player, id, param);
        // Permanent stances show no timer; the client uses -1 like PinnedStatus.
        var duration = double.IsPositiveInfinity(remaining) ? -1f : (float)remaining;
        if (PlayerMatches) Statuses.Apply((Character*)player, id, duration, param, player->GetGameObjectId());
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        rules.Reset();
        Write(autos: false, resetAdditional: true);
        // A full-but-inactive record (Elapsed == Total == sheet recast) made speed-adjusted
        // weaponskills (Gnashing Fang, Double Down) flash on the hotbar after leaving.
        if (ActionManager.Instance() != manager) return;
        foreach (var recast in recasts)
        {
            var detail = manager->GetRecastGroupDetail(recast.NativeGroup);
            if (detail == null) continue;
            detail->IsActive = false;
            detail->Elapsed = 0;
            detail->Total = 0;
        }
    }
}
