using System;
using FFXIVClientStructs.FFXIV.Client.Game.Gauge;

namespace AnoMech.Core.Combat;

internal sealed unsafe class MonkNativeGauge : IJobGauge
{
    private readonly MonkGauge* gauge;

    public MonkNativeGauge() => gauge = (MonkGauge*)HealerGauge.Current(20);
    public bool Matches => HealerGauge.Matches(20, gauge);

    public void Mirror(IJobCombat rules)
    {
        var mnk = (MonkCombat)rules;
        gauge->Chakra = (byte)mnk.Chakra;
        gauge->BeastChakra1 = (BeastChakraType)mnk.BeastChakra[0];
        gauge->BeastChakra2 = (BeastChakraType)mnk.BeastChakra[1];
        gauge->BeastChakra3 = (BeastChakraType)mnk.BeastChakra[2];
        // One byte holds all three fury counts (FFXIVClientStructs getters): Opo-opo bits 0-1, Raptor 2-3, Coeurl 4-5.
        gauge->BeastChakraStacks = (byte)(mnk.OpoOpoFury | mnk.RaptorFury << 2 | mnk.CoeurlFury << 4);
        gauge->Nadi = (NadiFlags)((mnk.LunarNadi ? 1 : 0) | (mnk.SolarNadi ? 2 : 0));
        gauge->BlitzTimeRemaining = (ushort)Math.Ceiling(mnk.PerfectBalanceRemaining * 1000);
    }
}
