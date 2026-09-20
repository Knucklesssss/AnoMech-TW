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

internal sealed unsafe class DragoonNativeGauge : IJobGauge
{
    private readonly DragoonGauge* gauge;

    public DragoonNativeGauge() => gauge = (DragoonGauge*)HealerGauge.Current(22);
    public bool Matches => HealerGauge.Matches(22, gauge);

    public void Mirror(IJobCombat rules)
    {
        var drg = (DragoonCombat)rules;
        gauge->LotdTimer = (short)Math.Ceiling(drg.LifeOfTheDragonRemaining * 1000);
        gauge->LotdState = (byte)(drg.LifeOfTheDragonRemaining > 0 ? 2 : 0);
        // Eyes of the Dragon were removed before level 90; the field stays cleared.
        gauge->EyeCount = 0;
        gauge->FirstmindsFocusCount = (byte)drg.FirstmindsFocus;
    }
}

internal sealed unsafe class NinjaNativeGauge : IJobGauge
{
    private readonly NinjaGauge* gauge;

    public NinjaNativeGauge() => gauge = (NinjaGauge*)HealerGauge.Current(30);
    public bool Matches => HealerGauge.Matches(30, gauge);

    public void Mirror(IJobCombat rules)
    {
        var nin = (NinjaCombat)rules;
        gauge->Ninki = (byte)nin.Ninki;
        gauge->Kazematoi = (byte)nin.Kazematoi;
    }
}

internal sealed unsafe class SamuraiNativeGauge : IJobGauge
{
    private readonly SamuraiGauge* gauge;

    public SamuraiNativeGauge() => gauge = (SamuraiGauge*)HealerGauge.Current(34);
    public bool Matches => HealerGauge.Matches(34, gauge);

    public void Mirror(IJobCombat rules)
    {
        var sam = (SamuraiCombat)rules;
        gauge->Kenki = (byte)sam.Kenki;
        gauge->MeditationStacks = (byte)sam.Meditation;
        gauge->SenFlags = (SenFlags)sam.Sen;
        // ponytail: the Kaeshi byte's encoding is unverified; 0/1/2/3 follows the order the client replaces
        // 燕回返 in. Confirm against the in-game gauge.
        gauge->Kaeshi = (KaeshiAction)(byte)(sam.Kaeshi switch { 16485 => 1, 16486 => 2, 25782 => 3, _ => 0 });
    }
}
