using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace AnoMech.Core.Combat;

// For practice jobs without a LocalCombatSession. Same cast fields as CombatNativeState;
// this object owns them only between LB start and release/cancellation.
internal sealed unsafe class PracticeLimitBreakNativeCast
{
    private BattleChara* owner;
    internal void Mirror(BattleChara* caster, uint action, float elapsed, float total, GameObjectId target)
    {
        var manager = ActionManager.Instance();
        if (manager == null || caster == null) return;
        owner = caster;
        manager->CastActionType = ActionType.Action;
        manager->CastActionId = action;
        manager->CastSpellId = action;
        manager->CastTargetId = target;
        manager->CastTimeElapsed = elapsed;
        manager->CastTimeTotal = total;
        var conditions = Conditions.Instance();
        if (conditions != null) conditions->Flags[(int)ConditionFlag.Casting] = true;
        ((byte*)manager)[0x7DC] = 1; // Same TC casting flag used by CombatNativeState.
        caster->CastInfo.IsCasting = true;
        caster->CastInfo.ActionType = ActionType.Action;
        caster->CastInfo.ActionId = action;
        caster->CastInfo.TargetId = target;
        caster->CastInfo.CurrentCastTime = elapsed;
        caster->CastInfo.BaseCastTime = total;
        caster->CastInfo.TotalCastTime = total;
        caster->CastInfo.SourceSequence = manager->LastUsedActionSequence;
    }

    internal void Clear()
    {
        if (owner == null) return;
        // Never dereference a character pointer after identity changes.
        if ((nint)owner == (Plugin.ObjectTable.LocalPlayer?.Address ?? 0))
        {
            owner->CastInfo.IsCasting = false;
            owner->CastInfo.ActionId = 0;
            owner->CastInfo.CurrentCastTime = 0;
            owner->CastInfo.BaseCastTime = 0;
            owner->CastInfo.TotalCastTime = 0;
            var manager = ActionManager.Instance();
            if (manager != null)
            {
                manager->CastActionId = 0;
                manager->CastSpellId = 0;
                manager->CastTimeElapsed = 0;
                manager->CastTimeTotal = 0;
                ((byte*)manager)[0x7DC] = 0;
            }
            var conditions = Conditions.Instance();
            if (conditions != null) conditions->Flags[(int)ConditionFlag.Casting] = false;
        }
        owner = null;
    }
}
