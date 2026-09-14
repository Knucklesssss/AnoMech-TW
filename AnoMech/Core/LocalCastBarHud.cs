using AnoMech.Core.Combat;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.UI.Arrays;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using Action = Lumina.Excel.Sheets.Action;

namespace AnoMech.Core;

// With the native cast state written (CombatNativeState) the game fills _CastBar's numbers itself, but it only
// opens the addon for a cast started the normal way and keeps the last real cast's name and icon. This opens the
// addon for a simulated cast and overwrites the name and icon.
internal sealed unsafe class LocalCastBarHud : IDisposable
{
    private const string AddonName = "_CastBar";

    private LocalCombatSession? session;
    private bool shown;

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
        if (session != null)
        {
            MarkArraysDirty();
            SetShown(true);
        }
        else if (shown) SetShown(false);
    }

    public void Clear()
    {
        session = null;
        if (shown) SetShown(false);
    }

    private void OnPreDraw(AddonEvent type, AddonArgs args)
    {
        if (session is { CastingAction: not 0 }) SetShown(true);
    }

    private void SetShown(bool visible)
    {
        shown = visible;
        var addon = (AtkUnitBase*)Plugin.GameGui.GetAddonByName(AddonName, 1).Address;
        if (addon == null) return;
        if (addon->IsVisible != visible) addon->IsVisible = visible;
        var root = addon->RootNode;
        if (root == null) return;
        if (root->IsVisible() != visible) root->ToggleVisibility(visible);
        if (visible) root->SetAlpha(255);
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
        if (!Plugin.DataManager.GetExcelSheet<Action>().TryGetRow(combat.CastingAction, out var action)) return;
        ((CastBarNumberArray*)numArr->IntArray)->CastIconId = action.Icon;
        strArr->SetValue(0, action.Name.ExtractText(), managed: true);
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
}
