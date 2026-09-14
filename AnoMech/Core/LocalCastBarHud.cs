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
// ponytail: CastTime/TotalCastTime written as centiseconds and index 1 left untouched — both
// unverified; the first frame of each cast logs what the game held so the units can be corrected.
internal sealed unsafe class LocalCastBarHud : IDisposable
{
    private const string AddonName = "_CastBar";

    private LocalCombatSession? session;
    private bool wasCasting;
    private uint loggedAction;

    public LocalCastBarHud()
    {
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PreRequestedUpdate, AddonName, OnPreRequestedUpdate);
    }

    public void Dispose()
    {
        Clear();
        Plugin.AddonLifecycle.UnregisterListener(AddonEvent.PreRequestedUpdate, AddonName, OnPreRequestedUpdate);
    }

    public void Refresh(LocalCombatSession? combat)
    {
        session = combat is { Active: true, CastingAction: not 0 } ? combat : null;
        var casting = session != null;
        if (casting || wasCasting)
        {
            MarkArraysDirty();
            SetVisible(casting);
        }
        if (!casting) loggedAction = 0;
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
        if (session is not { CastingAction: not 0 } combat) return;
        if (args is not AddonRequestedUpdateArgs reqArgs) return;
        var numArrays = (NumberArrayData**)reqArgs.NumberArrayData;
        var strArrays = (StringArrayData**)reqArgs.StringArrayData;
        if (numArrays == null || strArrays == null) return;
        var numArr = numArrays[(int)NumberArrayType.CastBar];
        var strArr = strArrays[(int)StringArrayType.CastBar];
        if (numArr == null || strArr == null || numArr->IntArray == null) return;

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

    private static void SetVisible(bool visible)
    {
        var addon = (AtkUnitBase*)Plugin.GameGui.GetAddonByName(AddonName, 1).Address;
        if (addon != null && addon->IsVisible != visible) addon->IsVisible = visible;
    }
}
