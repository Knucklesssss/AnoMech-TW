using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Top;

// Rebuilds a strat's slot order from the party signs the user placed by hand, so a
// static can rehearse its own call: whoever wears Attack 1 runs to whatever spot the
// strat gave slot 0, and so on.
//
// A sign that was never placed leaves its slot as the plugin assigned it, so a
// half-marked party still resolves instead of collapsing. Roles named in
// `keepInPlace` are never moved — those are the debuff holders, whose spot is
// dictated by the mechanic rather than by a sign, and marking one would otherwise
// drag it out of the position the fight requires.
internal static class HandPlacedSigns
{
    public static RoleList Reorder(
        SimParty party,
        RoleList fallback,
        IReadOnlyList<(Sign Sign, int Slot)> plan,
        IReadOnlyCollection<PartyRole> keepInPlace)
    {
        var order = fallback.List.ToList();
        foreach (var (sign, slot) in plan)
        {
            if (RoleWearing(party, sign) is not { } role) continue;
            if (keepInPlace.Contains(role)) continue;
            var current = order.IndexOf(role);
            if (current < 0 || current == slot) continue;
            (order[slot], order[current]) = (order[current], order[slot]);
        }

        return new RoleList(party, order);
    }

    private static PartyRole? RoleWearing(SimParty party, Sign sign)
    {
        var marked = Markings.Get(sign).ObjectId;
        if (marked == 0) return null;
        foreach (var role in Enum.GetValues<PartyRole>())
            if (party.Get(role) is { } member && member.GameObjectId.ObjectId == marked)
                return role;
        return null;
    }
}
