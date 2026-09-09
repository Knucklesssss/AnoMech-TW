using System;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Top.P2PartySynergy;

public sealed class TopP2PartySynergyTuuuflessAi : TopP2PartySynergyAi
{
    public override string Name => "tuuufless";
    public override string? Group => "日服";

    private static readonly PartyRole[] WestColumnNorthToSouth =
        [PartyRole.RegenHealer, PartyRole.MainTank, PartyRole.MeleeDpsA, PartyRole.PhysRangedDps];

    private static readonly PartyRole[] EastColumnNorthToSouth =
        [PartyRole.ShieldHealer, PartyRole.OffTank, PartyRole.MeleeDpsB, PartyRole.CasterDps];

    protected override IAiMove CongaLine() => TwoColumnsHealerTankMeleeRanged();

    protected override void SwapForCongaOrder(IAiRoles s) => CrossOverTheSouthernOfASameColumnPair(s);

    protected override void AdjustForStacks(IAiRoles s) => CrossOverTheStackFartherFromTheEye(s);

    private static IAiMove TwoColumnsHealerTankMeleeRanged()
    {
        return AiMove.Create(
                         new(-6f, -7f), new(6f, -7f),
                         new(-6f, -2.5f), new(6f, -2.5f),
                         new(-6f, 2f), new(6f, 2f),
                         new(-6f, 6.5f), new(6f, 6.5f)
                     )
                     .Assignments([
                         WestColumnNorthToSouth[0], EastColumnNorthToSouth[0],
                         WestColumnNorthToSouth[1], EastColumnNorthToSouth[1],
                         WestColumnNorthToSouth[2], EastColumnNorthToSouth[2],
                         WestColumnNorthToSouth[3], EastColumnNorthToSouth[3],
                     ]);
    }

    private static void CrossOverTheSouthernOfASameColumnPair(IAiRoles s)
    {
        for (var i = 0; i < 4; i++)
        {
            var westSlot = 2 * i;
            var eastSlot = westSlot + 1;
            var atWest = s.RoleAt(westSlot);
            var atEast = s.RoleAt(eastSlot);
            var atWestStartsWest = StartsWest(atWest);

            if (atWestStartsWest != StartsWest(atEast))
            {
                if (!atWestStartsWest) s.ByPosition(westSlot, eastSlot);
                continue;
            }

            var southernIsAtWestSlot = SouthRank(atWest) > SouthRank(atEast);
            var southernBelongsAtWestSlot = !atWestStartsWest;
            if (southernIsAtWestSlot != southernBelongsAtWestSlot)
                s.ByPosition(westSlot, eastSlot);
        }
    }

    private void CrossOverTheStackFartherFromTheEye(IAiRoles s)
    {
        var pos0 = s.PositionOf(state.Stacks[0]);
        var pos1 = s.PositionOf(state.Stacks[1]);
        if ((pos0 + pos1) % 2 != 0) return;

        var farther = Math.Max(pos0, pos1);
        var partner = farther % 2 == 0 ? farther + 1 : farther - 1;
        if (state.Glitch == GlitchType.Far && farther is < 2 or > 5)
            partner = farther < 2 ? partner + 6 : partner - 6;
        s.ByPosition(farther, partner);
    }

    private static bool StartsWest(PartyRole role) => Array.IndexOf(WestColumnNorthToSouth, role) >= 0;

    private static int SouthRank(PartyRole role)
    {
        var west = Array.IndexOf(WestColumnNorthToSouth, role);
        return west >= 0 ? west : Array.IndexOf(EastColumnNorthToSouth, role);
    }
}
