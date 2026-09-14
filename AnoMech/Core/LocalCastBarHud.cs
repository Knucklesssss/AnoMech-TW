using AnoMech.Core.Combat;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.UI.Arrays;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using Action = Lumina.Excel.Sheets.Action;

namespace AnoMech.Core;

// The game clears the local player's cast state every frame, so it never fills _CastBar for a
// simulated cast; this writes the addon's arrays the way EnmityHud does for _EnemyList.
// CastTime/TotalCastTime are centiseconds (a real 2.0 s cast left 196 there).
// ponytail: index 1 is unnamed in ClientStructs; written as a casting flag on the guess the addon hides without it.
internal sealed unsafe class LocalCastBarHud : IDisposable
{
    private const string AddonName = "_CastBar";

    private LocalCombatSession? session;
    private bool wasCasting;
    private uint loggedAction;
    private bool clearPending;
    private bool visibilityLogged;

    public LocalCastBarHud()
    {
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PreRequestedUpdate, AddonName, OnPreRequestedUpdate);
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PreDraw, AddonName, OnPreDraw);
    }

    public void Dispose()
    {
        Clear();
        Plugin.AddonLifecycle.UnregisterListener(AddonEvent.PreRequestedUpdate, AddonName, OnPreRequestedUpdate);
        Plugin.AddonLifecycle.UnregisterListener(AddonEvent.PreDraw, AddonName, OnPreDraw);
    }

    public void Refresh(LocalCombatSession? combat)
    {
        session = combat is { Active: true, CastingAction: not 0 } ? combat : null;
        var casting = session != null;
        if (!casting && wasCasting) clearPending = true;
        if (casting || wasCasting)
        {
            MarkArraysDirty();
            SetVisible(casting);
        }
        if (casting && !visibilityLogged && session!.CastProgress >= 0.5f && Plugin.LogManager.Enabled)
        {
            visibilityLogged = true;
            Plugin.LogManager.LogSkill($"CastBarHud mid {VisibilityState()}");
        }
        if (!casting) { loggedAction = 0; visibilityLogged = false; }
        wasCasting = casting;
    }

    public void Clear()
    {
        if (wasCasting) SetVisible(false);
        session = null;
        wasCasting = false;
        loggedAction = 0;
    }

    private void OnPreRequestedUpdate(AddonEvent type, AddonArgs args)
    {
        var combat = session is { CastingAction: not 0 } casting ? casting : null;
        if (combat == null && !clearPending) return;
        if (args is not AddonRequestedUpdateArgs reqArgs) return;
        var numArrays = (NumberArrayData**)reqArgs.NumberArrayData;
        var strArrays = (StringArrayData**)reqArgs.StringArrayData;
        if (numArrays == null || strArrays == null) return;
        var numArr = numArrays[(int)NumberArrayType.CastBar];
        var strArr = strArrays[(int)StringArrayType.CastBar];
        if (numArr == null || strArr == null || numArr->IntArray == null) return;
        if (combat == null)
        {
            numArr->IntArray[1] = 0;
            clearPending = false;
            return;
        }

        if (loggedAction != combat.CastingAction && Plugin.LogManager.Enabled)
        {
            loggedAction = combat.CastingAction;
            var raw = numArr->IntArray;
            Plugin.LogManager.LogSkill($"CastBarHud before id={combat.CastingAction} ints=[{raw[0]},{raw[1]},{raw[2]},{raw[3]},{raw[4]},{raw[5]}] size={numArr->Size}");
        }

        var name = string.Empty;
        uint icon = 0;
        if (Plugin.DataManager.GetExcelSheet<Action>().TryGetRow(combat.CastingAction, out var action))
        {
            name = action.Name.ExtractText();
            icon = action.Icon;
        }

        var bar = (CastBarNumberArray*)numArr->IntArray;
        bar->CastIconId = icon;
        bar->CastTime = (int)Math.Round((combat.CastTotal - combat.CastRemaining) * 100);
        bar->TotalCastTime = (int)Math.Round(combat.CastTotal * 100);
        bar->CompletionPercentage = (int)(combat.CastProgress * 100);
        bar->Interupted = false;
        numArr->IntArray[1] = 1;
        strArr->SetValue(0, name, managed: true);
    }

    private static void MarkArraysDirty()
    {
        var holder = AtkStage.Instance()->AtkArrayDataHolder;
        if (holder == null) return;
        var numArr = holder->GetNumberArrayData((int)NumberArrayType.CastBar);
        var strArr = holder->GetStringArrayData((int)StringArrayType.CastBar);
        if (numArr != null) numArr->UpdateState = 1;
        if (strArr != null) strArr->UpdateState = 1;
    }

    // The addon stays visible but the game hides and fades its root node when the client isn't casting; re-show it last.
    private void OnPreDraw(AddonEvent type, AddonArgs args)
    {
        if (session is not { CastingAction: not 0 }) return;
        SetVisible(true);
        // The game also fades the root out when the client isn't casting.
        var addon = (AtkUnitBase*)args.Addon.Address;
        if (addon != null && addon->RootNode != null) addon->RootNode->SetAlpha(255);
    }

    private static string VisibilityState()
    {
        var addon = (AtkUnitBase*)Plugin.GameGui.GetAddonByName(AddonName, 1).Address;
        if (addon == null) return "addon=null";
        var holder = AtkStage.Instance()->AtkArrayDataHolder;
        var numArr = holder == null ? null : holder->GetNumberArrayData((int)NumberArrayType.CastBar);
        var ints = numArr == null || numArr->IntArray == null ? "null"
            : $"[{numArr->IntArray[0]},{numArr->IntArray[1]},{numArr->IntArray[2]},{numArr->IntArray[3]},{numArr->IntArray[4]},{numArr->IntArray[5]}]";
        var root = addon->RootNode;
        return $"visible={addon->IsVisible} root={(root == null ? "null" : $"{root->IsVisible()}/alpha={root->Alpha_2}")} ints={ints}";
    }

    private static void SetVisible(bool visible)
    {
        var addon = (AtkUnitBase*)Plugin.GameGui.GetAddonByName(AddonName, 1).Address;
        if (addon == null) return;
        if (addon->IsVisible != visible) addon->IsVisible = visible;
        if (addon->RootNode != null && addon->RootNode->IsVisible() != visible) addon->RootNode->ToggleVisibility(visible);
    }
}
