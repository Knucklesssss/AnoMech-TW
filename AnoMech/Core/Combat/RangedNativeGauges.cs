using System;
using FFXIVClientStructs.FFXIV.Client.Game.Gauge;

namespace AnoMech.Core.Combat;

internal sealed unsafe class BardNativeGauge : IJobGauge
{
    private readonly BardGauge* gauge;

    public BardNativeGauge() => gauge = (BardGauge*)HealerGauge.Current(23);
    public bool Matches => HealerGauge.Matches(23, gauge);

    public void Mirror(IJobCombat rules)
    {
        var brd = (BardCombat)rules;
        gauge->SongTimer = (ushort)Math.Ceiling(brd.SongRemaining * 1000);
        gauge->Repertoire = (byte)brd.Repertoire;
        gauge->SoulVoice = (byte)brd.SoulVoice;
        // Low bits: current song (1-3); codas: Ballad 16, Paeon 32, Minuet 64.
        gauge->SongFlags = (SongFlags)((int)brd.CurrentSong | (brd.Codas << 4));
    }
}

internal sealed unsafe class MachinistNativeGauge : IJobGauge
{
    private readonly MachinistGauge* gauge;

    public MachinistNativeGauge() => gauge = (MachinistGauge*)HealerGauge.Current(31);
    public bool Matches => HealerGauge.Matches(31, gauge);

    public void Mirror(IJobCombat rules)
    {
        var mch = (MachinistCombat)rules;
        gauge->Heat = (byte)mch.Heat;
        gauge->Battery = (byte)mch.Battery;
        gauge->OverheatTimeRemaining = (short)Math.Ceiling(mch.OverheatRemaining * 1000);
        gauge->SummonTimeRemaining = (short)Math.Ceiling(mch.QueenRemaining * 1000);
        gauge->LastSummonBatteryPower = (byte)mch.QueenBattery;
        // ponytail: TimerActive bit meaning is unverified; 1 overheat, 2 queen.
        gauge->TimerActive = (byte)((mch.OverheatRemaining > 0 ? 1 : 0) | (mch.QueenRemaining > 0 ? 2 : 0));
    }
}

internal sealed unsafe class DancerNativeGauge : IJobGauge
{
    private readonly DancerGauge* gauge;

    public DancerNativeGauge() => gauge = (DancerGauge*)HealerGauge.Current(38);
    public bool Matches => HealerGauge.Matches(38, gauge);

    public void Mirror(IJobCombat rules)
    {
        var dnc = (DancerCombat)rules;
        gauge->Feathers = (byte)dnc.Feathers;
        gauge->Esprit = (byte)dnc.EspritGauge;
        for (var i = 0; i < 4; i++) gauge->DanceSteps[i] = (byte)dnc.Steps[i];
        gauge->StepIndex = (byte)dnc.StepIndex;
    }
}

internal sealed unsafe class BlackMageNativeGauge : IJobGauge
{
    private readonly BlackMageGauge* gauge;

    public BlackMageNativeGauge() => gauge = (BlackMageGauge*)HealerGauge.Current(25);
    public bool Matches => HealerGauge.Matches(25, gauge);

    public void Mirror(IJobCombat rules)
    {
        var blm = (BlackMageCombat)rules;
        gauge->ElementStance = (sbyte)blm.Element;
        gauge->UmbralHearts = (byte)blm.UmbralHearts;
        gauge->PolyglotStacks = (byte)blm.Polyglot;
        // Countdown to the next Polyglot while an element is held.
        gauge->EnochianTimer = (short)(blm.Element == 0 ? 0 : Math.Ceiling((30 - blm.PolyglotTimer) * 1000));
        gauge->EnochianFlags = (EnochianFlags)((blm.Element != 0 ? 1 : 0) | (blm.Paradox ? 2 : 0));
    }
}

internal sealed unsafe class SummonerNativeGauge : IJobGauge
{
    private readonly SummonerGauge* gauge;

    public SummonerNativeGauge() => gauge = (SummonerGauge*)HealerGauge.Current(27);
    public bool Matches => HealerGauge.Matches(27, gauge);

    public void Mirror(IJobCombat rules)
    {
        var smn = (SummonerCombat)rules;
        gauge->SummonTimer = (ushort)Math.Ceiling(smn.TranceRemaining * 1000);
        gauge->AttunementTimer = (ushort)Math.Ceiling(smn.AttunementRemaining * 1000);
        gauge->ReturnSummon = (byte)smn.CurrentTrance;
        // ponytail: bit layout from FFXIVClientStructs (count << 2 | element); unverified in game.
        gauge->Attunement = (byte)((smn.AttunementStacks << 2) | (int)smn.Attunement);
        gauge->AetherFlags = (AetherFlags)(smn.Aetherflow | ((int)smn.Attunement << 2) | (smn.PhoenixNext ? 16 : 0)
            | (smn.Ruby ? 32 : 0) | (smn.Topaz ? 64 : 0) | (smn.Emerald ? 128 : 0));
    }
}
