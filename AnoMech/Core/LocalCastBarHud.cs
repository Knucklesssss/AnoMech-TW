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
    private const uint CastNameNodeId = 4;
    private const uint CastIconNodeId = 8;

    private LocalCombatSession? session;
    private uint CastingAction => Plugin.GameInstance?.World.LimitBreaks?.CastingAction is > 0 and var lb ? lb : session?.CastingAction ?? 0;
    private bool shown;
    private uint loadedIcon;

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
        if (CastingAction != 0)
        {
            MarkArraysDirty();
            SetShown(true);
        }
        else
        {
            loadedIcon = 0;
            if (shown) SetShown(false);
        }
    }

    public void Clear()
    {
        session = null;
        loadedIcon = 0;
        if (shown) SetShown(false);
    }

    private void OnPreDraw(AddonEvent type, AddonArgs args)
    {
        if (CastingAction == 0) return;
        SetShown(true);
        // The addon keeps the name it got when a real cast opened it, ignoring the string array.
        var addon = (AtkUnitBase*)args.Addon.Address;
        var nameNode = addon == null ? null : addon->GetTextNodeById(CastNameNodeId);
        if (addon == null || !Plugin.DataManager.GetExcelSheet<Action>().TryGetRow(CastingAction, out var action)) return;
        if (nameNode != null) nameNode->SetText(action.Name.ExtractText());
        if (loadedIcon != action.Icon && LoadIcon(addon, action.Icon)) loadedIcon = action.Icon;
    }

    // The icon component keeps the texture of the last real cast; reload its image nodes with the simulated action's icon.
    private static bool LoadIcon(AtkUnitBase* addon, uint icon)
    {
        var node = addon->GetComponentNodeById(CastIconNodeId);
        if (node == null || node->Component == null) return false;
        var loaded = false;
        var manager = &node->Component->UldManager;
        for (var i = 0; i < manager->NodeListCount; i++)
        {
            var child = manager->NodeList[i];
            if (child == null || child->Type != NodeType.Image) continue;
            ((AtkImageNode*)child)->LoadIconTexture(icon, 0);
            loaded = true;
            break;
        }
        return loaded;
    }

    private void SetShown(bool visible)
    {
        shown = visible;
        var addon = (AtkUnitBase*)Plugin.GameGui.GetAddonByName(AddonName, 1).Address;
        if (addon == null) return;
        // Setting IsVisible alone left ShowHideFlags at 1 and the addon never drew; Show/Hide clear and set it.
        if (visible && (!addon->IsVisible || (addon->ShowHideFlags & 1) != 0)) addon->Show(true, 1);
        else if (!visible && addon->IsVisible) addon->Hide(false, false, 1);
        var root = addon->RootNode;
        if (root == null) return;
        if (root->IsVisible() != visible) root->ToggleVisibility(visible);
        if (visible) root->SetAlpha(255);
    }

    private void OnPreRequestedUpdate(AddonEvent type, AddonArgs args)
    {
        if (CastingAction == 0) return;
        if (args is not AddonRequestedUpdateArgs reqArgs) return;
        var numArrays = (NumberArrayData**)reqArgs.NumberArrayData;
        var strArrays = (StringArrayData**)reqArgs.StringArrayData;
        if (numArrays == null || strArrays == null) return;
        var numArr = numArrays[(int)NumberArrayType.CastBar];
        var strArr = strArrays[(int)StringArrayType.CastBar];
        if (numArr == null || strArr == null || numArr->IntArray == null) return;
        if (!Plugin.DataManager.GetExcelSheet<Action>().TryGetRow(CastingAction, out var action)) return;
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
