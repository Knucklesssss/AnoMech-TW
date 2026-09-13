using System;
using System.Linq;
using System.Numerics;
using AnoMech.Core.SimObjects;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AnoMech.Core.Game.Party;

// Move intact row components so their status icons, hit targets and native bindings
// stay attached to the same actor. Never reorder MainGroup or its local-player slot.
internal sealed unsafe class PartyListLayout : IDisposable
{
    private const string AddonName = "_PartyList";
    private SimParty? party;
    private nint savedAddon;
    private readonly nint[] nodes = new nint[11];
    private readonly Vector2[] positions = new Vector2[11];
    private bool applied;

    public PartyListLayout()
    {
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, AddonName, ApplyLayout);
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostUpdate, AddonName, ApplyLayout);
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PreDraw, AddonName, ApplyLayout);
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, AddonName, RestoreBeforeUpdate);
    }

    public void Refresh(SimParty current) => party = current;

    public void Clear()
    {
        party = null;
        Restore((AddonPartyList*)Plugin.GameGui.GetAddonByName(AddonName, 1).Address);
    }

    public void Dispose()
    {
        Clear();
        Plugin.AddonLifecycle.UnregisterListener(AddonEvent.PostRequestedUpdate, AddonName, ApplyLayout);
        Plugin.AddonLifecycle.UnregisterListener(AddonEvent.PostUpdate, AddonName, ApplyLayout);
        Plugin.AddonLifecycle.UnregisterListener(AddonEvent.PreDraw, AddonName, ApplyLayout);
        Plugin.AddonLifecycle.UnregisterListener(AddonEvent.PreFinalize, AddonName, RestoreBeforeUpdate);
    }

    private void RestoreBeforeUpdate(AddonEvent type, AddonArgs args) => Restore((AddonPartyList*)args.Addon.Address);

    private void ApplyLayout(AddonEvent type, AddonArgs args)
    {
        var addon = (AddonPartyList*)args.Addon.Address;
        if (!Plugin.Config.CustomPartyListOrder || party == null)
        {
            Restore(addon);
            return;
        }
        if (addon == null || addon->MemberCount != 8) return;
        var hud = AgentHUD.Instance();
        if (hud == null || hud->PartyMemberCount != 8) return;
        var rowIds = new uint[8];
        for (var i = 0; i < 8; i++)
        {
            var member = hud->PartyMembers[i];
            if (member.Index >= 8) return;
            rowIds[member.Index] = member.EntityId;
        }
        var roleIds = new uint[8];
        for (var i = 0; i < 8; i++)
        {
            var character = party.Get(i);
            if (character != null && character.BattleCharaPtr != null)
                roleIds[i] = character.BattleCharaPtr->EntityId;
        }
        var mapping = PartyListOrderRules.MapRows(Plugin.Config.PartyListOrder, roleIds, rowIds);
        if (mapping == null) return;
        for (var i = 0; i < 8; i++)
            if (NodeAt(addon, i) == null) return;
        // Restore only when a replacement is ready, within the same callback.
        // Native status/cast updates must never leave our rows at default positions
        // until a later draw, nor may an incomplete identity snapshot undo them.
        Restore(addon);
        for (var i = 0; i < 11; i++)
        {
            var node = NodeAt(addon, i);
            nodes[i] = (nint)node;
            positions[i] = node == null ? default : new(node->X, node->Y);
        }
        var destinations = positions.Take(8).OrderBy(p => p.Y).ThenBy(p => p.X).ToArray();
        // A not-yet-laid-out addon must not collapse every actor into one row.
        if (destinations.Distinct().Count() != 8) return;
        savedAddon = (nint)addon;
        applied = true;
        for (var i = 0; i < 8; i++)
            ((AtkResNode*)nodes[i])->SetPositionFloat(destinations[mapping[i]].X, destinations[mapping[i]].Y);

        var selfRow = hud->PartyMembers[0].Index;
        var selfDelta = destinations[mapping[selfRow]] - positions[selfRow];
        // These player/leader adornments may live outside the row component.
        // Do not offset children of a moved node twice.
        for (var i = 8; i < 11; i++)
        {
            var node = (AtkResNode*)nodes[i];
            if (node == null) continue;
            var parent = node->ParentNode;
            while (parent != null && !nodes.Take(i).Contains((nint)parent)) parent = parent->ParentNode;
            if (parent == null)
                node->SetPositionFloat(positions[i].X + selfDelta.X, positions[i].Y + selfDelta.Y);
        }
    }

    private void Restore(AddonPartyList* addon)
    {
        if (!applied) return;
        if (addon != null && (nint)addon == savedAddon)
            for (var i = 0; i < nodes.Length; i++)
            {
                var node = NodeAt(addon, i);
                if (node != null && (nint)node == nodes[i])
                    node->SetPositionFloat(positions[i].X, positions[i].Y);
            }
        applied = false;
    }

    private static AtkResNode* NodeAt(AddonPartyList* addon, int index)
    {
        if (index < 8)
        {
            var component = addon->PartyMembers[index].PartyMemberComponent;
            return component == null ? null : (AtkResNode*)component->OwnerNode;
        }
        return index switch
        {
            8 => addon->LeaderMarkResNode,
            9 => addon->MpBarSpecialResNode,
            _ => (AtkResNode*)addon->MpBarSpecialTextNode,
        };
    }
}
