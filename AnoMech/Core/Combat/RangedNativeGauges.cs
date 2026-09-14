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
