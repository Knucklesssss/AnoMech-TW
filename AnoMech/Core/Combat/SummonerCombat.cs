using System;
using System.Collections.Generic;

namespace AnoMech.Core.Combat;

// Level-90 Bahamut/Phoenix trances, arcana, primal attunement, favors and Aetherflow; no pet models.
// Rules from docs/client-data/SMN-90.md.
public sealed class SummonerCombat : CasterCombatBase
{
    private const ushort FurtherRuin = 2701, RadiantAegis = 2702, SearingLight = 2703, IfritsFavor = 2724, GarudasFavor = 2725,
        TitansFavor = 2853, CrimsonStrikeReady = 4403;
    private const double TranceSeconds = 15, AttunementSeconds = 30;
    private Trance trance;
    private double tranceRemaining;
    private bool phoenixNext;
    private bool ruby, topaz, emerald;
    private Primal attunement;
    private int attunementStacks;
    private double attunementRemaining;
    private int aetherflow;
    // ponytail: Carbuncle is treated as already summoned when a simulation starts; unverified.
    private bool carbuncle = true;

    public enum Trance { None, Bahamut, Phoenix }
    public enum Primal { None, Ifrit, Titan, Garuda }

    public SummonerCombat(double gcdSeconds = 2.5) : base(gcdSeconds) { }

    public Trance CurrentTrance => trance;
    public double TranceRemaining => tranceRemaining;
    public bool PhoenixNext => phoenixNext;
    public bool Ruby => ruby;
    public bool Topaz => topaz;
    public bool Emerald => emerald;
    public Primal Attunement => attunement;
    public int AttunementStacks => attunementStacks;
    public double AttunementRemaining => attunementRemaining;
    public int Aetherflow => aetherflow;
    protected override IReadOnlyList<uint> JobActions { get; } =
        [3579, 25826, 7427, 25831, 7429, 16516, 3582, 25830, 25835, 25836, 25837, 25820, 25821, 16514, 16515, 25838,
         25839, 25840, 25883, 25884, 25823, 25824, 25825, 25832, 25833, 25834, 16508, 16510, 181, 3578, 7426, 25798, 25799,
         25801, 25822, 173, 16230];
    protected override IReadOnlyList<ushort> JobStatusIds { get; } =
        [FurtherRuin, RadiantAegis, SearingLight, IfritsFavor, GarudasFavor, TitansFavor, CrimsonStrikeReady];
    protected override string JobDebugState
        => $"trance={trance}/{tranceRemaining:0.0} phoenixNext={phoenixNext} arcana={ruby}{topaz}{emerald} attune={attunement}x{attunementStacks}/{attunementRemaining:0.0} aetherflow={aetherflow}";

    public override uint Adjust(uint actionId) => actionId switch
    {
        163 or 172 => Adjust(3579),
        16511 => Adjust(25826),
        36990 => 181, // Necrotize, level 92, stored on a level-100 hotbar
        25802 or 25805 => 25838,
        25803 or 25806 => 25839,
        25804 or 25807 => 25840,
        3581 or 25800 or 7427 => phoenixNext ? 25831u : 7427u,
        3579 when trance == Trance.Bahamut => 25820,
        3579 when trance == Trance.Phoenix => 16514,
        25826 when trance == Trance.Bahamut => 25821,
        25826 when trance == Trance.Phoenix => 16515,
        7429 when trance == Trance.Phoenix => 16516,
        25822 when trance == Trance.Bahamut => 3582,
        25822 when trance == Trance.Phoenix => 25830,
        25822 or 25835 when HasBuff(CrimsonStrikeReady) => 25885,
        25822 when HasBuff(IfritsFavor) => 25835,
        25822 when HasBuff(TitansFavor) => 25836,
        25822 when HasBuff(GarudasFavor) => 25837,
        25883 when attunement == Primal.Ifrit => 25823,
        25883 when attunement == Primal.Titan => 25824,
        25883 when attunement == Primal.Garuda => 25825,
        25884 when attunement == Primal.Ifrit => 25832,
        25884 when attunement == Primal.Titan => 25833,
        25884 when attunement == Primal.Garuda => 25834,
        _ => actionId,
    };

    public override bool IsGapCloser(uint actionId) => actionId == 25835;
    protected override uint ComboFrom(uint actionId) => 0;

    // Physick, Resurrection and Rekindle are cast on the player; party targets are not simulated.
    protected override bool IsJobSelfAction(uint actionId) => actionId is 25798 or 25799 or 25801 or 25822 or 25830 or 173 or 16230;
    protected override bool AlsoUsesGcd(uint actionId) => actionId is 7427 or 25831;

    protected override double JobCastTime(uint actionId) => actionId switch
    {
        3579 or 25826 or 25798 or 16230 => 1.5,
        25823 or 25832 => 2.8,
        25837 => 3,
        173 => 8,
        _ => 0,
    };

    protected override int JobMpCost(uint actionId) => actionId switch
    {
        3579 or 25826 or 25820 or 25821 or 16514 or 16515 => 300,
        7426 or 25798 or 16230 => 400,
        173 => 2400,
        _ => 0,
    };

    protected override bool JobCanUse(uint actionId, bool inCombat) => actionId switch
    {
        7427 or 25831 => carbuncle && inCombat && trance == Trance.None && attunement == Primal.None,
        25820 or 25821 or 3582 or 7429 => trance == Trance.Bahamut,
        16514 or 16515 or 25830 or 16516 => trance == Trance.Phoenix,
        25838 => ruby && carbuncle && trance == Trance.None,
        25839 => topaz && carbuncle && trance == Trance.None,
        25840 => emerald && carbuncle && trance == Trance.None,
        25823 or 25832 => attunement == Primal.Ifrit,
        25824 or 25833 => attunement == Primal.Titan,
        25825 or 25834 => attunement == Primal.Garuda,
        25883 or 25884 or 25822 => false, // usable only through a substitution
        25835 => HasBuff(IfritsFavor),
        25885 => HasBuff(CrimsonStrikeReady),
        25836 => HasBuff(TitansFavor),
        25837 => HasBuff(GarudasFavor),
        181 or 3578 => aetherflow > 0,
        7426 => HasBuff(FurtherRuin),
        25801 => inCombat,
        25799 => carbuncle,
        _ => true,
    };

    protected override JobHit? ApplyJob(uint actionId, bool hasTarget, bool hardcast)
    {
        switch (actionId)
        {
            case 7427: StartTrance(Trance.Bahamut); return new JobHit(actionId, false, false);
            case 25831: StartTrance(Trance.Phoenix); return new JobHit(actionId, false, false);
            case 25838:
                ruby = false;
                StartAttunement(Primal.Ifrit, 2);
                Buff(IfritsFavor, double.PositiveInfinity);
                return new JobHit(actionId, true, false);
            case 25839:
                topaz = false;
                StartAttunement(Primal.Titan, 4);
                return new JobHit(actionId, true, false);
            case 25840:
                emerald = false;
                StartAttunement(Primal.Garuda, 4);
                Buff(GarudasFavor, double.PositiveInfinity);
                return new JobHit(actionId, true, false);
            case 25823 or 25824 or 25825 or 25832 or 25833 or 25834:
                if (actionId is 25824 or 25833) Buff(TitansFavor, double.PositiveInfinity);
                if (--attunementStacks == 0) EndAttunement();
                return new JobHit(actionId, actionId is 25832 or 25833 or 25834, false);
            case 25835:
                ClearBuff(IfritsFavor);
                Buff(CrimsonStrikeReady, double.PositiveInfinity);
                return new JobHit(actionId, true, true);
            case 25885: ClearBuff(CrimsonStrikeReady); return new JobHit(actionId, true, false);
            case 25836: ClearBuff(TitansFavor); return new JobHit(actionId, true, false);
            case 25837: ClearBuff(GarudasFavor); return new JobHit(actionId, true, false);
            case 16508 or 16510:
                aetherflow = 2;
                Buff(FurtherRuin, 60);
                return new JobHit(actionId, actionId == 16510, false);
            case 181 or 3578:
                aetherflow--;
                return new JobHit(actionId, actionId == 3578, false);
            case 7426: ClearBuff(FurtherRuin); return new JobHit(actionId, true, false);
            case 25798: carbuncle = true; return null;
            case 25799: Buff(RadiantAegis, 30); return null;
            case 25801: Buff(SearingLight, 20); return null;
            case 25830 or 173 or 16230: return null;
            case 3579 or 25820 or 16514: return new JobHit(actionId, false, false);
        }
        return new JobHit(actionId, true, false);
    }

    // Summoning anything removes the favors a previous primal left.
    private void ClearFavors()
    {
        ClearBuff(IfritsFavor);
        ClearBuff(TitansFavor);
        ClearBuff(GarudasFavor);
        ClearBuff(CrimsonStrikeReady);
    }

    private void StartTrance(Trance next)
    {
        ClearFavors();
        trance = next;
        tranceRemaining = TranceSeconds;
        phoenixNext = next == Trance.Bahamut;
        ruby = topaz = emerald = true;
    }

    private void StartAttunement(Primal primal, int stacks)
    {
        ClearFavors();
        attunement = primal;
        attunementStacks = stacks;
        attunementRemaining = AttunementSeconds;
    }

    private void EndAttunement()
    {
        attunement = Primal.None;
        attunementStacks = 0;
        attunementRemaining = 0;
    }

    protected override void AdvanceCaster(double seconds)
    {
        if (trance != Trance.None && (tranceRemaining = Decrease(tranceRemaining, seconds)) == 0) trance = Trance.None;
        if (attunement != Primal.None && (attunementRemaining = Decrease(attunementRemaining, seconds)) == 0) EndAttunement();
    }

    protected override void ResetCaster()
    {
        trance = Trance.None;
        tranceRemaining = 0;
        phoenixNext = false;
        ruby = topaz = emerald = false;
        EndAttunement();
        aetherflow = 0;
        carbuncle = true;
        ClearFavors();
    }

    protected override (int Group, double Recast, int Charges) JobTimingContract(uint actionId) => actionId switch
    {
        7427 or 25831 => (10, 60, 1),
        7429 or 16516 => (6, 20, 1),
        3582 or 25830 => (5, 20, 1),
        25836 => (3, 1, 1),
        16508 or 16510 => (12, 60, 1),
        181 => (1, 1, 1),
        3578 => (2, 1, 1),
        25799 => (21, 60, 2),
        25801 => (20, 120, 1),
        25823 or 25832 => (GlobalCooldownGroup, Scaled(3), 1),
        25825 or 25834 => (GlobalCooldownGroup, Scaled(1.5), 1),
        25837 => (GlobalCooldownGroup, Scaled(3.5), 1),
        _ => Gcd,
    };
}
