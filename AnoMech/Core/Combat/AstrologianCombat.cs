using System;
using System.Collections.Generic;

namespace AnoMech.Core.Combat;

// Level-90 rotation, Astral/Umbral draws and card plays, MP, casts, and castable heals and
// mitigation (status icons and cooldowns only). Rules from docs/client-data/AST-90.md.
// Card slots are tracked by the rules; the native card gauge is not written.
public sealed class AstrologianCombat : HealerCombatBase
{
    private const double StarSeconds = 20;
    private const double HoroscopeSeconds = 30;
    private const ushort AspectedBenefic = 835, AspectedHelios = 836, Lightspeed = 841, Divination = 1878, Opposition = 1879,
        Intersection = 1889, NeutralSect = 1892, Exaltation = 2717, Macrocosmos = 2718, CollectiveUnconscious = 849,
        Balance = 3887, Arrow = 3888, Spear = 3889, Bole = 3890, Ewer = 3891, Spire = 3892;
    private readonly bool[] cards = new bool[4];
    private bool umbralDrawn;
    private bool umbralNext;
    private double starRemaining;
    private double horoscopeRemaining;

    public AstrologianCombat(double gcdSeconds = 2.5) : base(gcdSeconds) { }

    public bool UmbralNext => umbralNext;
    public bool HasCard(int slot) => cards[slot];
    protected override IReadOnlyList<uint> JobActions { get; } =
        [25871, 16554, 25872, 3594, 3610, 3595, 3600, 3601, 3606, 3612, 3613, 3614, 7439, 8324, 16552, 16553, 16556, 16557,
         16558, 16559, 25873, 25874, 25875, 37017, 37018, 37019, 37020, 37021, 37022, 37023, 37024, 37025, 37026, 37027,
         37028, 7444, 7445];
    protected override IReadOnlyList<ushort> JobStatusIds { get; } =
        [835, 836, 841, 1878, 1879, 1889, 1892, 2717, 2718, 849, 3887, 3888, 3889, 3890, 3891, 3892];

    protected override string JobDebugState
        => $"cards={string.Join("", Array.ConvertAll(cards, c => c ? "1" : "0"))} umbral={umbralDrawn} next={(umbralNext ? "umbral" : "astral")} star={starRemaining:0.0} horoscope={horoscopeRemaining:0.0}";

    // Substitutions from the client's ReplaceAction sheet (技能變換設定).
    public override uint Adjust(uint actionId) => actionId switch
    {
        3596 or 3598 or 7442 or 16555 => 25871,
        3599 or 3608 => 16554,
        3615 => 25872,
        37030 => 3601, // Helios Conjunction, level 96, stored on a level-100 hotbar
        37017 when umbralNext => 37018,
        37019 when cards[0] => umbralDrawn ? 37026u : 37023u,
        37020 when cards[1] => umbralDrawn ? 37027u : 37024u,
        37021 when cards[2] => umbralDrawn ? 37028u : 37025u,
        37022 when cards[3] => umbralDrawn ? 7445u : 7444u,
        7439 when starRemaining > 0 => 8324,
        16557 when horoscopeRemaining > 0 => 16558,
        25874 when HasBuff(Macrocosmos) => 25875,
        _ => actionId,
    };

    // Card plays, Synastry and Exaltation are cast on the player; party targets are not simulated.
    protected override bool IsJobSelfAction(uint actionId) => actionId is not (25871 or 16554 or 25872);

    protected override bool AlsoUsesGcd(uint actionId) => actionId == 25874;

    protected override bool JobHighlighted(uint actionId) => actionId is 37023 or 37024 or 37025 or 37026 or 37027 or 37028 or 7444 or 7445;

    protected override double JobCastTime(uint actionId)
    {
        var seconds = actionId is 25871 or 25872 or 3594 or 3610 or 3600 or 3601 ? 1.5 : 0;
        return HasBuff(Lightspeed) ? Math.Max(0, seconds - 2.5) : seconds;
    }

    protected override int JobMpCost(uint actionId) => actionId switch
    {
        25871 or 16554 or 25872 or 3594 or 3595 => 400,
        3610 or 3600 => 700,
        3601 => 800,
        25874 => 600,
        _ => 0,
    };

    protected override bool JobCanUse(uint actionId, bool inCombat) => actionId switch
    {
        37017 => !umbralNext,
        37018 => umbralNext,
        37023 => cards[0] && !umbralDrawn,
        37026 => cards[0] && umbralDrawn,
        37024 => cards[1] && !umbralDrawn,
        37027 => cards[1] && umbralDrawn,
        37025 => cards[2] && !umbralDrawn,
        37028 => cards[2] && umbralDrawn,
        7444 => cards[3] && !umbralDrawn,
        7445 => cards[3] && umbralDrawn,
        37019 or 37020 or 37021 or 37022 => false,
        8324 => starRemaining > 0,
        16558 => horoscopeRemaining > 0,
        25875 => HasBuff(Macrocosmos),
        _ => true,
    };

    protected override JobHit? ApplyJob(uint actionId, bool hasTarget)
    {
        switch (actionId)
        {
            case 37017 or 37018:
                umbralDrawn = actionId == 37018;
                Array.Fill(cards, true);
                umbralNext = !umbralNext;
                GainMp(2000);
                return null;
            case 37023: PlayCard(0, Balance, 15); return null;
            case 37026: PlayCard(0, Spear, 15); return null;
            case 37024: PlayCard(1, Arrow, 15); return null;
            case 37027: PlayCard(1, Bole, 15); return null;
            case 37025: PlayCard(2, Spire, 30); return null;
            case 37028: PlayCard(2, Ewer, 15); return null;
            case 7444:
                cards[3] = false;
                return hasTarget ? new JobHit(actionId, true, false) : null;
            case 7445: cards[3] = false; return null;
            case 3595: Buff(AspectedBenefic, 15); return null;
            case 3601: Buff(AspectedHelios, 15); return null;
            case 3606: Buff(Lightspeed, 15); return null;
            case 3613: Buff(CollectiveUnconscious, 5); return null;
            case 16552: Buff(Divination, 20); return null;
            case 16553: Buff(Opposition, 15); return null;
            case 16556: Buff(Intersection, 30); return null;
            case 16559: Buff(NeutralSect, 20); return null;
            case 25873: Buff(Exaltation, 8); return null;
            case 7439: starRemaining = StarSeconds; return null;
            case 8324:
                starRemaining = 0;
                return hasTarget ? new JobHit(actionId, true, false) : null;
            case 16557: horoscopeRemaining = HoroscopeSeconds; return null;
            case 16558: horoscopeRemaining = 0; return null;
            case 25874:
                Buff(Macrocosmos, 15);
                return hasTarget ? new JobHit(actionId, true, false) : null;
            case 25875: ClearBuff(Macrocosmos); return null;
            case 25871 or 16554: return new JobHit(actionId, false, false);
            case 25872: return new JobHit(actionId, true, false);
        }
        return null;
    }

    private void PlayCard(int slot, ushort status, double seconds)
    {
        cards[slot] = false;
        Buff(status, seconds);
    }

    protected override void AdvanceHealer(double seconds)
    {
        starRemaining = Decrease(starRemaining, seconds);
        horoscopeRemaining = Decrease(horoscopeRemaining, seconds);
    }

    protected override void ResetHealer()
    {
        Array.Fill(cards, false);
        umbralDrawn = false;
        umbralNext = false;
        starRemaining = 0;
        horoscopeRemaining = 0;
    }

    protected override (int Group, double Recast, int Charges) JobTimingContract(uint actionId) => actionId switch
    {
        3606 => (19, 60, 2),
        3612 => (20, 120, 1),
        3613 => (12, 60, 1),
        3614 => (11, 40, 2),
        7439 => (13, 60, 1),
        8324 => (1013, 1, 1), // Stellar Detonation: its own 1 s recast beside Earthly Star's group
        16552 => (21, 120, 1),
        16553 => (14, 60, 1),
        16556 => (10, 30, 2),
        16557 => (15, 60, 1),
        16558 => (6, 1, 1),
        16559 => (22, 120, 1),
        25873 => (16, 60, 1),
        25874 => (23, 180, 1),
        25875 => (1, 1, 1),
        37017 or 37018 => (17, 55, 1),
        37019 or 37023 or 37026 => (2, 1, 1),
        37020 or 37024 or 37027 => (3, 1, 1),
        37021 or 37025 or 37028 => (4, 1, 1),
        37022 or 7444 or 7445 => (7, 1, 1),
        _ => Gcd,
    };
}
