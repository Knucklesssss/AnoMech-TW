using System;
using System.Collections.Generic;

namespace AnoMech.Core.Combat;

// Level-90 rotation, Lily gauge, MP, casts and castable heals and mitigation
// (status icons and cooldowns only). Rules from docs/client-data/WHM-90.md.
public sealed class WhiteMageCombat : HealerCombatBase
{
    // ponytail: the lily timer also runs out of combat; the rules do not track combat start.
    private const double LilySeconds = 20;
    private const ushort Regen = 158, MedicaII = 150, PresenceOfMind = 157, ThinAir = 1217, DivineBenison = 1218,
        Confession = 1219, Temperance = 1872, Aquaveil = 2708, Liturgy = 2709;
    private int lilies;
    private int bloodLilies;
    private double lilyTimer;

    public WhiteMageCombat(double gcdSeconds = 2.5) : base(gcdSeconds) { }

    public int Lilies => lilies;
    public int BloodLilies => bloodLilies;
    public double LilyTimer => lilyTimer;
    protected override IReadOnlyList<uint> JobActions { get; } =
        [25859, 16532, 25860, 120, 135, 131, 124, 133, 137, 16531, 16534, 16535, 3571, 3570, 7432, 140, 3569, 7433, 16536,
         25861, 25862, 28509, 136, 7430, 37008];
    protected override IReadOnlyList<ushort> JobStatusIds { get; } = [158, 150, 157, 1217, 1218, 1219, 1872, 2708, 2709];

    protected override string JobDebugState => $"lily={lilies} blood={bloodLilies} timer={lilyTimer:0.0}";

    public override uint Adjust(uint actionId) => actionId switch
    {
        119 or 127 or 3568 or 7431 or 16533 => 25859,
        121 or 132 => 16532,
        139 => 25860,
        37010 => 133, // Medica III, level 96, stored on a level-100 hotbar
        25862 when HasBuff(Liturgy) => 28509,
        _ => actionId,
    };

    protected override bool IsJobSelfAction(uint actionId)
        => actionId is not (25859 or 16532 or 16535);

    protected override bool JobHighlighted(uint actionId) => actionId == 16535 && bloodLilies == 3;

    protected override double JobCastTime(uint actionId)
    {
        var scale = HasBuff(PresenceOfMind) ? 0.8 : 1;
        return actionId switch
        {
            25859 or 25860 or 120 => 1.5 * scale,
            135 or 131 or 124 or 133 => 2 * scale,
            _ => 0,
        };
    }

    protected override int JobMpCost(uint actionId) => HasBuff(ThinAir) ? 0 : SheetMpCost(actionId);

    private static int SheetMpCost(uint actionId) => actionId switch
    {
        25859 or 16532 or 25860 or 120 or 137 => 400,
        135 or 133 => 1000,
        131 => 1500,
        124 => 900,
        _ => 0,
    };

    protected override bool JobCanUse(uint actionId, bool inCombat) => actionId switch
    {
        16531 or 16534 => lilies >= 1,
        16535 => bloodLilies == 3,
        28509 => HasBuff(Liturgy),
        _ => true,
    };

    protected override JobHit? ApplyJob(uint actionId, bool hasTarget)
    {
        if (SheetMpCost(actionId) > 0) ConsumeBuff(ThinAir);
        switch (actionId)
        {
            case 133: Buff(MedicaII, 15); return null;
            case 137: Buff(Regen, 18); return null;
            case 16531 or 16534:
                lilies--;
                bloodLilies = Math.Min(3, bloodLilies + 1);
                return null;
            case 16535:
                bloodLilies = 0;
                return new JobHit(actionId, true, false);
            case 3571:
                GainMp(500);
                return hasTarget ? new JobHit(actionId, true, false) : null;
            case 7432: Buff(DivineBenison, 15); return null;
            case 7433: Buff(Confession, 10); return null;
            case 16536: Buff(Temperance, 20); return null;
            case 25861: Buff(Aquaveil, 8); return null;
            case 25862: Buff(Liturgy, 20, 5); return null;
            case 28509: ClearBuff(Liturgy); return null;
            case 136: Buff(PresenceOfMind, 15); return null;
            case 7430: Buff(ThinAir, 12); return null;
            case 25859 or 16532: return new JobHit(actionId, false, false);
            case 25860: return hasTarget ? new JobHit(actionId, true, false) : null;
        }
        return null;
    }

    protected override void AdvanceHealer(double seconds)
    {
        for (lilyTimer += seconds; lilyTimer >= LilySeconds; lilyTimer -= LilySeconds)
            lilies = Math.Min(3, lilies + 1);
    }

    protected override void ResetHealer()
    {
        lilies = 0;
        bloodLilies = 0;
        lilyTimer = 0;
    }

    protected override (int Group, double Recast, int Charges) JobTimingContract(uint actionId) => actionId switch
    {
        3571 => (8, 40, 1),
        3570 => (20, 60, 1),
        7432 => (10, 30, 2),
        140 => (24, 180, 1),
        3569 => (15, 90, 1),
        7433 => (11, 60, 1),
        16536 => (22, 120, 1),
        25861 => (12, 60, 1),
        25862 => (23, 180, 1),
        28509 => (1, 1, 1),
        136 => (21, 120, 1),
        7430 => (19, 60, 2),
        37008 => (13, 60, 1),
        _ => Gcd,
    };
}
