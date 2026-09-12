using AnoMech.Core.Game.Party;

internal static class PartyListOrderChecks
{
    public static void Run()
    {
        var type = typeof(PartyRole).Assembly.GetType("AnoMech.Core.Game.Party.PartyListOrderRules");
        if (type == null) throw new Exception("Custom party display-order rules are missing.");
        PartyRole[] Normalize(PartyRole[]? order) => (PartyRole[])type.GetMethod("Normalize")!.Invoke(null, [order])!;
        int[]? Map(PartyRole[] order, uint[] roleIds, uint[] rowIds)
            => (int[]?)type.GetMethod("MapRows")!.Invoke(null, [order, roleIds, rowIds]);
        var roles = Enum.GetValues<PartyRole>();
        var reverse = roles.Reverse().ToArray();
        if (!Normalize(reverse).SequenceEqual(reverse)) throw new Exception("Keep the requested order, including the player's role.");
        if (!Normalize(null).SequenceEqual(roles)) throw new Exception("Missing settings use role order.");
        var malformed = Normalize([PartyRole.CasterDps, PartyRole.CasterDps, (PartyRole)99]);
        if (!malformed.SequenceEqual(new[] { PartyRole.CasterDps }.Concat(roles.Take(7))))
            throw new Exception("Invalid/duplicate settings must not lose or duplicate a role.");
        uint[] roleIds = [11,12,13,14,15,16,17,18];
        uint[] rowIds = [15,11,12,13,14,16,17,18];
        if (!Map(reverse, roleIds, rowIds)!.SequenceEqual(new[] { 3,7,6,5,4,2,1,0 }))
            throw new Exception("Map by identity, not by the player's fixed native slot or AI role index.");
        for (var player = 0; player < 8; player++)
        {
            var native = new[] { roleIds[player] }.Concat(roleIds.Where(id => id != roleIds[player])).ToArray();
            var map = Map(reverse, roleIds, native)!;
            if (map[0] != 7 - player || map.Distinct().Count() != 8)
                throw new Exception("All eight player roles can occupy their requested display position.");
        }
        if (Map(reverse, roleIds, [15,11,12,13,14,16,17,0]) != null
            || Map(reverse, roleIds, [15,11,12,13,14,16,17,99]) != null
            || Map(reverse, roleIds, [15,11,12,13,14,16,17,17]) != null)
            throw new Exception("Do not reorder incomplete, foreign or duplicate native rows.");
        Console.WriteLine("PASS: custom party row order, all player roles, invalid settings and incomplete native rows.");
    }
}
