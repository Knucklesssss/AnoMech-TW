using System;
using AnoMech.Core.Combat;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.System.Input;
using AnoMech.Core.Native;

namespace AnoMech.Core.Native;

// Hooks the native action and movement input paths so the simulator can stun
// the local player when a mechanic kills them. Status-row writes don't enforce
// anything (the server overwrites StatusManager._status[] on every packet); the
// real lockout is two booleans this class exposes — the detours read them every
// frame and short-circuit the original calls. Owned by Plugin (session-lifetime);
// SimPlayer is the sole writer of the two flags, reconciling them each tick from
// its own Dead / Movement.IsMoving state.
//
// Signatures and detour shapes lifted from FFXIV-RaidsRewritten's
// PlayerMovementOverride.cs / ActionManagerEx.cs (which themselves credit
// awgil's vnavmesh + bossmod). If a future patch breaks a sig, both projects
// will need to rev them together.
public sealed unsafe class LocalPlayerInputHooks : IDisposable
{
    internal const uint SprintActionId = 3;
    private const ushort SprintStatusId = 50;
    private const float SprintDuration = 10f; 
    internal const ushort SprintStatusParam = 30;

    public bool DisableAllActions { get; set; }
    public bool ZeroMovement { get; set; }

    // --- Player activity signals (read by SimPlayer to drive Party.Player.IsMoving/IsActing) ---
    // Movement is the engine's own per-frame movement sample taken in RMIWalkDetour — the same
    // signal bossmod's MovementOverride.IsMoving() reads — captured as the player's true input
    // intent *before* the stun-zeroing. This is intent/input based (matches cast-cancel semantics),
    // not a position delta. Holds its last value on frames where RMIWalk doesn't fire.
    public bool MovementInputActive { get; private set; }

    // True while the player's weapon auto-attack is swinging.
    public bool IsAutoAttacking => UIState.Instance()->WeaponState.AutoAttackState.IsAutoAttacking;

    // True while the player is in a jump arc (CONDITION_JUMP). State poll like
    // IsAutoAttacking — a jump is always self-initiated, so the state flag is
    // equivalent to input intent here (nothing can force the player airborne).
    public bool IsJumping => Plugin.Condition[ConditionFlag.Jumping];

    // Latched whenever the player actually fires a real action; drained once per frame by SimPlayer
    // (PollActionUsed) so a same-frame action press is still observable to a snapshot mechanic.
    private bool actionUsedSincePoll;
    public bool PollActionUsed()
    {
        var used = actionUsedSincePoll;
        actionUsedSincePoll = false;
        return used;
    }

    private delegate void RMIWalkDelegate(void* self, float* sumLeft, float* sumForward, float* sumTurnLeft, byte* haveBackwardOrStrafe, byte* a6, byte bAdditiveUnk);
    [Signature("E8 ?? ?? ?? ?? 80 7B 3E 00 48 8D 3D", Fallibility = Fallibility.Fallible)]
    private Hook<RMIWalkDelegate> rmiWalkHook = null!;

    private enum KeybindType
    {
        StrafeLeft = 325,
        StrafeRight = 326,
    }

    [return: MarshalAs(UnmanagedType.U1)]
    private delegate bool CheckStrafeKeybindDelegate(IntPtr ptr, KeybindType keybind);
    [Signature("E8 ?? ?? ?? ?? 84 C0 74 04 41 C6 06 01 BA 44 01 00 00", Fallibility = Fallibility.Fallible)]
    private Hook<CheckStrafeKeybindDelegate> checkStrafeKeybindHook = null!;

    private readonly Hook<InputData.Delegates.IsInputIdPressed>? isInputIdPressedHook;
    private readonly Hook<ActionManager.Delegates.Update>? updateHook;
    private readonly Hook<ActionManager.Delegates.UseAction>? useActionHook;
    private readonly Hook<ActionManager.Delegates.UseActionLocation>? useActionLocationHook;
    private readonly Hook<ActionManager.Delegates.GetAdjustedActionId>? adjustedActionHook;
    private readonly Hook<ActionManager.Delegates.GetActionStatus>? actionStatusHook;
    private readonly Hook<ActionManager.Delegates.IsActionHighlighted>? highlightHook;
    public bool CombatHooksReady => updateHook?.IsEnabled == true && useActionHook?.IsEnabled == true
        && useActionLocationHook?.IsEnabled == true && adjustedActionHook?.IsEnabled == true && actionStatusHook?.IsEnabled == true;
    private static LocalCombatSession? Combat => Plugin.GameInstance?.World.Combat is { Active: true } session ? session : null;
    internal void RecordLocalAction() => actionUsedSincePoll = true;

    public LocalPlayerInputHooks(IGameInteropProvider hook)
    {
        hook.InitializeFromAttributes(this);

        // ClientStructs resolves these four itself. Where its own signatures miss on
        // the TC binary, Value comes back zero and HookFromAddress would throw, taking
        // the whole plugin load down with it. Track and skip instead.
        var isInputIdPressedAddr = SignatureReport.TrackAddress("InputData.IsInputIdPressed", InputData.Addresses.IsInputIdPressed.Value);
        if (isInputIdPressedAddr != 0)
            isInputIdPressedHook = hook.HookFromAddress<InputData.Delegates.IsInputIdPressed>(isInputIdPressedAddr, IsInputIdPressedDetour);

        var updateAddr = SignatureReport.TrackAddress("ActionManager.Update", ActionManager.Addresses.Update.Value);
        if (updateAddr != 0)
            updateHook = hook.HookFromAddress<ActionManager.Delegates.Update>(updateAddr, UpdateDetour);

        var useActionAddr = SignatureReport.TrackAddress("ActionManager.UseAction", ActionManager.Addresses.UseAction.Value);
        if (useActionAddr != 0)
            useActionHook = hook.HookFromAddress<ActionManager.Delegates.UseAction>(useActionAddr, UseActionDetour);

        var useActionLocationAddr = SignatureReport.TrackAddress("ActionManager.UseActionLocation", ActionManager.Addresses.UseActionLocation.Value);
        if (useActionLocationAddr != 0)
            useActionLocationHook = hook.HookFromAddress<ActionManager.Delegates.UseActionLocation>(useActionLocationAddr, UseActionLocationDetour);

        var adjustedAddr = SignatureReport.TrackAddress("ActionManager.GetAdjustedActionId", ActionManager.Addresses.GetAdjustedActionId.Value);
        if (adjustedAddr != 0)
            adjustedActionHook = hook.HookFromAddress<ActionManager.Delegates.GetAdjustedActionId>(adjustedAddr, AdjustedActionDetour);
        var statusAddr = SignatureReport.TrackAddress("ActionManager.GetActionStatus", ActionManager.Addresses.GetActionStatus.Value);
        if (statusAddr != 0)
            actionStatusHook = hook.HookFromAddress<ActionManager.Delegates.GetActionStatus>(statusAddr, ActionStatusDetour);
        var highlightAddr = SignatureReport.TrackAddress("ActionManager.IsActionHighlighted", ActionManager.Addresses.IsActionHighlighted.Value);
        if (highlightAddr != 0)
            highlightHook = hook.HookFromAddress<ActionManager.Delegates.IsActionHighlighted>(highlightAddr, HighlightDetour);
        highlightHook?.Enable();
        adjustedActionHook?.Enable();
        actionStatusHook?.Enable();
        rmiWalkHook?.Enable();
        checkStrafeKeybindHook?.Enable();
        isInputIdPressedHook?.Enable();
        updateHook?.Enable();
        useActionHook?.Enable();
        useActionLocationHook?.Enable();
    }

    public void Dispose()
    {
        rmiWalkHook?.Dispose();
        checkStrafeKeybindHook?.Dispose();
        isInputIdPressedHook?.Dispose();
        updateHook?.Dispose();
        useActionHook?.Dispose();
        useActionLocationHook?.Dispose();
        adjustedActionHook?.Dispose();
        actionStatusHook?.Dispose();
        highlightHook?.Dispose();
    }

    private void RMIWalkDetour(void* self, float* sumLeft, float* sumForward, float* sumTurnLeft, byte* haveBackwardOrStrafe, byte* a6, byte bAdditiveUnk)
    {
        rmiWalkHook.Original(self, sumLeft, sumForward, sumTurnLeft, haveBackwardOrStrafe, a6, bAdditiveUnk);
        // Capture the engine's movement sample as the player's true movement intent, before any
        // stun-zeroing below. (self is a MoveControllerSubMemberForMine*; the sums are its move vector.)
        MovementInputActive = *sumLeft != 0 || *sumForward != 0;
        if (!ZeroMovement) return;
        *sumLeft = 0;
        *sumForward = 0;
        *haveBackwardOrStrafe = 0;
    }

    private bool CheckStrafeKeybindDetour(IntPtr ptr, KeybindType keybind)
    {
        if (ZeroMovement && (keybind == KeybindType.StrafeLeft || keybind == KeybindType.StrafeRight))
            return false;
        return checkStrafeKeybindHook.Original(ptr, keybind);
    }

    private bool IsInputIdPressedDetour(InputData* inputData, InputId inputId)
    {
        if (ZeroMovement && (inputId == InputId.JUMP || inputId == InputId.PAD_JUMPANDCANCELCAST))
            return false;
        return isInputIdPressedHook!.Original(inputData, inputId);
    }

    // Drains queued auto-attacks while DisableAllActions is set so the player
    // doesn't keep swinging mid-stun; mirrors raid-rewritten's UpdateDetour.
    private void UpdateDetour(ActionManager* self)
    {
        var session = Combat;
        try
        {
            session?.Tick();
            session?.BeforeNativeUpdate();
            updateHook!.Original(self);
            if (session?.Active != true && DisableAllActions)
            {
                var autosOn = UIState.Instance()->WeaponState.AutoAttackState.IsAutoAttacking;
                if (autosOn) self->UseAction(ActionType.GeneralAction, 1);
            }
        }
        catch (Exception ex) { StopCombat(session, ex); }
        finally
        {
            try { session?.AfterNativeUpdate(); }
            catch (Exception ex) { StopCombat(session, ex); }
        }
    }

    private bool UseActionDetour(ActionManager* self, ActionType actionType, uint actionId, ulong targetId, uint extraParam, ActionManager.UseActionMode mode, uint comboRouteId, bool* outOptAreaTargeted)
    {
        var session = Combat;
        try
        {
            if (session != null && session.TryInput(actionType, actionId, targetId, out var accepted))
            {
                if (outOptAreaTargeted != null) *outOptAreaTargeted = false;
                return accepted;
            }
            if (DisableAllActions && !IsStopAutosAction(actionType, actionId)) return false;
            var result = useActionHook!.Original(self, actionType, actionId, targetId, extraParam, mode, comboRouteId, outOptAreaTargeted);
            // Preserve the existing Sprint path and real-action activity latch.
            if (result && !IsStopAutosAction(actionType, actionId)) actionUsedSincePoll = true;
            if (result && actionType == ActionType.Action && actionId == SprintActionId)
                Plugin.GameInstance?.Player?.AddStatus(SprintStatusId, SprintDuration, SprintStatusParam);
            return result;
        }
        catch (Exception ex) { StopCombat(session, ex); return false; }
    }

    private bool UseActionLocationDetour(ActionManager* self, ActionType actionType, uint actionId, ulong targetId, Vector3* location, uint extraParam, byte a7)
    {
        var session = Combat;
        try
        {
            if (session != null && session.TryInput(actionType, actionId, targetId, out var accepted)) return accepted;
            if (DisableAllActions && !IsStopAutosAction(actionType, actionId)) return false;
            var result = useActionLocationHook!.Original(self, actionType, actionId, targetId, location, extraParam, a7);
            if (result) actionUsedSincePoll = true;
            return result;
        }
        catch (Exception ex) { StopCombat(session, ex); return false; }
    }

    private uint AdjustedActionDetour(ActionManager* self, uint id)
    {
        var session = Combat;
        try
        {
            return session != null && session.CheckIdentity() && session.Supports(id)
                ? session.Adjust(id) : adjustedActionHook!.Original(self, id);
        }
        catch (Exception ex) { StopCombat(session, ex); return id; }
    }

    // Combo glow follows the simulated combo; the server-driven native state is frozen.
    private bool HighlightDetour(ActionManager* self, ActionType type, uint id)
    {
        var session = Combat;
        try
        {
            return session != null && type == ActionType.Action && session.CheckIdentity() && session.Supports(id)
                ? session.IsHighlighted(id) : highlightHook!.Original(self, type, id);
        }
        catch (Exception ex) { StopCombat(session, ex); return false; }
    }

    private uint ActionStatusDetour(ActionManager* self, ActionType type, uint id, ulong target, bool checkRecast, bool checkCasting, uint* extra)
    {
        var session = Combat;
        try
        {
            if (session != null && (type == ActionType.Item || (type == ActionType.Action && id != SprintActionId)))
            {
                if (extra != null) *extra = 0;
                if (type == ActionType.Item) return 573u;
                var status = session.ActionStatus(id, target, checkRecast);
                if (status == 0 && session.Supports(id))
                {
                    // The client can still refuse an action the simulation allows; it then never calls UseAction.
                    uint nativeExtra = 0;
                    var nativeStatus = actionStatusHook!.Original(self, type, id, target, checkRecast, checkCasting, &nativeExtra);
                    if (nativeStatus != 0)
                        ErrorLog.Record("遊戲判定不能用", $"id={id} adjusted={session.Adjust(id)} 遊戲狀態碼={nativeStatus} 附加={nativeExtra}");
                }
                return status;
            }
            return actionStatusHook!.Original(self, type, id, target, checkRecast, checkCasting, extra);
        }
        catch (Exception ex) { StopCombat(session, ex); if (extra != null) *extra = 0; return 572; }
    }

    private static void StopCombat(LocalCombatSession? session, Exception ex)
    {
        try
        {
            Plugin.Log.Error(ex, "Local combat native detour failed");
            ErrorLog.Record("戰鬥攔截錯誤", ex.Message, ex);
            session?.Stop($"本機戰鬥已停止：{ex.Message}");
        }
        catch (Exception cleanup)
        {
            Plugin.Log.Error(cleanup, "Local combat restoration failed");
            ErrorLog.Record("戰鬥還原錯誤", cleanup.Message, cleanup);
        }
    }

    // Lets the auto-cancel UseAction from UpdateDetour through; everything else
    // bounces while autos are still firing.
    private static bool IsStopAutosAction(ActionType actionType, uint actionId)
    {
        if (!UIState.Instance()->WeaponState.AutoAttackState.IsAutoAttacking) return false;
        return actionType == ActionType.GeneralAction && actionId == 1;
    }
}
