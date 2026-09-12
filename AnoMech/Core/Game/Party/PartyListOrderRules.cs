using System;
using System.Collections.Generic;
using System.Linq;

namespace AnoMech.Core.Game.Party;

public static class PartyListOrderRules
{
    public static PartyRole[] Normalize(PartyRole[]? order)
        => (order ?? []).Where(role => (uint)role < 8).Concat(Enum.GetValues<PartyRole>()).Distinct().ToArray();

    public static int[]? MapRows(PartyRole[]? order, uint[] roleIds, uint[] rowIds)
    {
        if (roleIds.Length != 8 || rowIds.Length != 8 || roleIds.Contains(0u)
            || roleIds.Distinct().Count() != 8 || rowIds.Distinct().Count() != 8)
            return null;
        var desired = Normalize(order).Select(role => roleIds[(int)role]).ToArray();
        var result = rowIds.Select(id => Array.IndexOf(desired, id)).ToArray();
        return result.Contains(-1) ? null : result;
    }
}
