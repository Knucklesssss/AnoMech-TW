using AnoMech.Core.Combat;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.UI.Arrays;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using Action = Lumina.Excel.Sheets.Action;

namespace AnoMech.Core;

// With the native cast state written (CombatNativeState) the game shows and advances _CastBar itself, but
// it keeps a stale name and icon from an earlier cast; this overwrites only those while a simulated cast runs.
internal sealed unsafe class LocalCastBarHud : IDisposable
{
    private const string AddonName = "_CastBar";

    private LocalCombatSession? session;

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
        if (session != null) MarkArraysDirty();
    }

    public void Clear() => session = null;

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
