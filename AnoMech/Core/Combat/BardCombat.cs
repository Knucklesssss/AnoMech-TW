using System;
using System.Collections.Generic;
using System.Numerics;

namespace AnoMech.Core.Combat;

// Level-90 songs, Repertoire, Soul Voice and Hawk's Eye procs. Rules from docs/client-data/BRD-90.md.
public sealed class BardCombat : RangedCombatBase
{
    private const ushort HawksEye = 3861, RagingStrikes = 125, Barrage = 128, MagesBallad = 2217, ArmysPaeon = 2218,
        WanderersMinuet = 2216, BattleVoice = 141, Troubadour = 1934, NaturesMinne = 1202, BlastArrowReady = 2692,
        RadiantFinale = 2722, WardensPaean = 866;
    private const double SongSeconds = 45, RepertoireSeconds = 3;
    private Song song;
    private double songRemaining;
    private double repertoireTick;
    private int repertoire;
    private int soulVoice;
    private int codas;

    public enum Song { None, Ballad, Paeon, Minuet }

    public BardCombat(double gcdSeconds = 2.5) : base(gcdSeconds) { }

    public Song CurrentSong => song;
    public double SongRemaining => songRemaining;
    public int Repertoire => repertoire;
    public int SoulVoice => soulVoice;
    public int Codas => codas;
    protected override IReadOnlyList<uint> JobActions { get; } =
        [16495, 7409, 7406, 7407, 25783, 16494, 3560, 16496, 25784, 101, 107, 110, 117, 112, 114, 116, 3559, 7404, 118, 3558,
         3561, 3562, 7405, 7408, 25785];
    protected override IReadOnlyList<ushort> JobStatusIds { get; } =
        [HawksEye, RagingStrikes, Barrage, MagesBallad, ArmysPaeon, WanderersMinuet, BattleVoice, Troubadour, NaturesMinne,
         BlastArrowReady, RadiantFinale, WardensPaean];
    protected override string JobDebugState => $"song={song}/{songRemaining:0.0} rep={repertoire} soul={soulVoice} codas={codas}";

    public override uint Adjust(uint actionId) => actionId switch
    {
        97 => 16495,
        98 => 7409,
        100 => 7406,
        113 => 7407,
        106 => 25783,
        36974 => 16494,
        36975 => 110, // Heartbreak Shot, level 92, stored on a level-100 hotbar
        3559 when song == Song.Minuet => 7404,
        16496 when HasBuff(BlastArrowReady) => 25784,
        _ => actionId,
    };

    public override bool IsGapCloser(uint actionId) => false;
    protected override uint ComboFrom(uint actionId) => 0;

    // Warden's Paean is cast on the player; party targets are not simulated.
    protected override bool IsJobSelfAction(uint actionId)
        => actionId is 101 or 107 or 114 or 116 or 3559 or 118 or 3561 or 7405 or 7408 or 25785;

    protected override bool JobHighlighted(uint actionId) => actionId switch
    {
        7409 or 16494 => HasBuff(HawksEye) || HasBuff(Barrage),
        7404 => repertoire > 0,
        25784 => true,
        _ => false,
    };

    protected override bool RangedCanUse(uint actionId, bool inCombat) => actionId switch
    {
        7409 or 16494 => HasBuff(HawksEye) || HasBuff(Barrage),
        114 or 116 or 3559 => inCombat,
        7404 => song == Song.Minuet && repertoire > 0,
        16496 => soulVoice >= 20,
        25784 => HasBuff(BlastArrowReady),
        25785 => codas != 0,
        _ => true,
    };

    protected override JobHit? ApplyJob(uint actionId, bool hasTarget)
    {
        switch (actionId)
        {
            case 16495 or 7406 or 7407 or 3560:
                if (Chance(0.35)) Buff(HawksEye, 30);
                return new JobHit(actionId, false, false);
            case 25783:
                if (Chance(0.35)) Buff(HawksEye, 30);
                return new JobHit(actionId, true, false);
            case 7409 or 16494:
                if (!ConsumeBuff(Barrage)) ConsumeBuff(HawksEye);
                return new JobHit(actionId, actionId == 16494, false);
            case 16496:
                if (soulVoice >= 80) Buff(BlastArrowReady, 10);
                soulVoice = 0;
                return new JobHit(actionId, true, false);
            case 25784:
                ClearBuff(BlastArrowReady);
                return new JobHit(actionId, true, false);
            case 7404:
                repertoire = 0;
                return new JobHit(actionId, true, false);
            case 117: return new JobHit(actionId, true, false);
            case 114: StartSong(Song.Ballad, MagesBallad, 1); return null;
            case 116: StartSong(Song.Paeon, ArmysPaeon, 2); return null;
            case 3559: StartSong(Song.Minuet, WanderersMinuet, 4); return null;
            case 25785:
                Buff(RadiantFinale, 20, (ushort)BitOperations.PopCount((uint)codas));
                codas = 0;
                return null;
            case 101: Buff(RagingStrikes, 20); return null;
            case 107: Buff(Barrage, 10); return null;
            case 118: Buff(BattleVoice, 20); return null;
            case 7405: Buff(Troubadour, 15); return null;
            case 7408: Buff(NaturesMinne, 15); return null;
            case 3561: Buff(WardensPaean, 30); return null;
        }
        return new JobHit(actionId, false, false);
    }

    private void StartSong(Song next, ushort status, int coda)
    {
        ClearBuff(MagesBallad);
        ClearBuff(ArmysPaeon);
        ClearBuff(WanderersMinuet);
        song = next;
        songRemaining = SongSeconds;
        repertoireTick = 0;
        repertoire = 0;
        codas |= coda;
        Buff(status, SongSeconds);
    }

    protected override void AdvanceJob(double seconds)
    {
        if (song == Song.None) return;
        songRemaining = Decrease(songRemaining, seconds);
        for (repertoireTick += seconds; repertoireTick >= RepertoireSeconds; repertoireTick -= RepertoireSeconds)
            if (Chance(0.8)) GainRepertoire();
        if (songRemaining > 0) return;
        song = Song.None;
        repertoire = 0;
        repertoireTick = 0;
    }

    // ponytail: Army's Paeon stacks only show on the gauge; the haste is not simulated.
    private void GainRepertoire()
    {
        soulVoice = Math.Min(100, soulVoice + 5);
        switch (song)
        {
            case Song.Ballad: Timing.Reduce(10, 7.5); break;
            case Song.Paeon: repertoire = Math.Min(4, repertoire + 1); break;
            case Song.Minuet: repertoire = Math.Min(3, repertoire + 1); break;
        }
    }

    protected override void ResetJob()
    {
        song = Song.None;
        songRemaining = 0;
        repertoireTick = 0;
        repertoire = 0;
        soulVoice = 0;
        codas = 0;
    }

    protected override (int Group, double Recast, int Charges) JobTimingContract(uint actionId) => actionId switch
    {
        101 => (15, 120, 1),
        107 => (20, 120, 1),
        110 or 117 => (10, 15, 3),
        112 => (6, 30, 1),
        114 => (16, 120, 1),
        116 => (17, 120, 1),
        3559 => (18, 120, 1),
        7404 => (1, 1, 1),
        118 => (19, 120, 1),
        3558 => (3, 15, 1),
        3561 => (11, 45, 1),
        3562 => (13, 60, 1),
        7405 => (21, 90, 1), // trait 447 (level 88) shortens Troubadour's recast from 120 s to 90 s
        7408 => (22, 120, 1),
        25785 => (14, 110, 1),
        _ => Gcd,
    };
}
