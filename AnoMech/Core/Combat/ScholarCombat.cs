using System;
using System.Collections.Generic;

namespace AnoMech.Core.Combat;

// Level-90 rotation, Aetherflow and Faerie gauges, MP, casts, and castable heals, shields and
// fairy orders (status icons and cooldowns only; the fairy itself is not spawned).
// Rules from docs/client-data/SCH-90.md.
public sealed class ScholarCombat : HealerCombatBase
{
    private const double SeraphSeconds = 22;
    // ponytail: Aetherpact drains 10 Faerie Gauge per 3 s tick; the interval is not in the client text.
    private const double PactTickSeconds = 3;
    private const ushort Galvanize = 297, SacredSoil = 299, EmergencyTactics = 792, Recitation = 1896, WhisperingDawn = 315,
        FeyIllumination = 317, Protraction = 2710, Expedient = 2711, DesperateMeasures = 2712;
    private int aetherflow;
    private int fairy;
    private double seraphRemaining;
    private bool aetherpact;
    private double pactTick;

    public ScholarCombat(double gcdSeconds = 2.5) : base(gcdSeconds) { }

    public int Aetherflow => aetherflow;
    public int FairyGauge => fairy;
    public double SeraphRemaining => seraphRemaining;
    public bool Aetherpact => aetherpact;
    protected override IReadOnlyList<uint> JobActions { get; } =
        [25865, 16540, 17870, 25866, 190, 185, 186, 166, 167, 189, 3583, 188, 7434, 3585, 3586, 3587, 7436, 7437, 7869,
         16537, 16538, 16542, 16543, 16545, 16546, 17215, 25867, 25868];
    protected override IReadOnlyList<ushort> JobStatusIds { get; } = [297, 299, 792, 1896, 315, 317, 2710, 2711, 2712];

    protected override string JobDebugState => $"aetherflow={aetherflow} fairy={fairy} seraph={seraphRemaining:0.0} pact={aetherpact}";

    public override uint Adjust(uint actionId) => actionId switch
    {
        17864 or 17865 => 16540,
        17869 or 3584 or 7435 or 16541 => 25865,
        16539 => 25866,
        37013 => 186, // Concitation, level 96, stored on a level-100 hotbar
        7437 when aetherpact => 7869,
        16545 when seraphRemaining > 0 => 16546,
        _ => actionId,
    };

    protected override bool IsJobSelfAction(uint actionId)
        => actionId is not (25865 or 16540 or 17870 or 167 or 7436);

    protected override double JobCastTime(uint actionId) => actionId switch
    {
        25865 or 190 or 17215 => 1.5,
        185 or 186 => 2,
        _ => 0,
    };

    protected override int JobMpCost(uint actionId) => actionId switch
    {
        25865 or 25866 or 190 => 400,
        16540 or 17870 => 300,
        185 or 186 => HasBuff(Recitation) ? 0 : 900,
        17215 => 200,
        _ => 0,
    };

    protected override bool JobCanUse(uint actionId, bool inCombat) => actionId switch
    {
        167 or 189 or 188 => aetherflow >= 1,
        3583 or 7434 => aetherflow >= 1 || HasBuff(Recitation),
        166 or 3587 => inCombat,
        7437 => fairy >= 10,
        7869 => aetherpact,
        16545 => seraphRemaining <= 0,
        16546 => seraphRemaining > 0,
        _ => true,
    };

    protected override JobHit? ApplyJob(uint actionId, bool hasTarget)
    {
        switch (actionId)
        {
            case 167:
                SpendAetherflow();
                return new JobHit(actionId, false, false);
            case 189:
                SpendAetherflow();
                return null;
            case 188:
                SpendAetherflow();
                Buff(SacredSoil, 15);
                return null;
            case 3583 or 7434:
                // Faerie Gauge only fills when Aetherflow is actually spent; Recitation skips both.
                if (!ConsumeBuff(Recitation)) SpendAetherflow();
                return null;
            case 185 or 186:
                ConsumeBuff(Recitation);
                Buff(Galvanize, 30);
                return null;
            case 166:
                aetherflow = 3;
                GainMp(2000);
                return null;
            case 3587: aetherflow = 3; return null;
            case 3586: Buff(EmergencyTactics, 15); return null;
            case 7436: return new JobHit(actionId, false, false);
            case 7437:
                aetherpact = true;
                pactTick = 0;
                return null;
            case 7869: aetherpact = false; return null;
            case 16537: Buff(WhisperingDawn, 21); return null;
            case 16538: Buff(FeyIllumination, 20); return null;
            case 16542: Buff(Recitation, 15); return null;
            case 16545: seraphRemaining = SeraphSeconds; return null;
            case 25867: Buff(Protraction, 10); return null;
            case 25868:
                Buff(Expedient, 20);
                Buff(DesperateMeasures, 10);
                return null;
            case 25865 or 16540 or 17870: return new JobHit(actionId, false, false);
            case 25866: return hasTarget ? new JobHit(actionId, true, false) : null;
        }
        return null;
    }

    private void SpendAetherflow()
    {
        aetherflow--;
        fairy = Math.Min(100, fairy + 10);
    }

    protected override void AdvanceHealer(double seconds)
    {
        seraphRemaining = Decrease(seraphRemaining, seconds);
        if (!aetherpact) return;
        for (pactTick += seconds; pactTick >= PactTickSeconds && aetherpact; pactTick -= PactTickSeconds)
        {
            fairy = Math.Max(0, fairy - 10);
            if (fairy < 10) aetherpact = false;
        }
    }

    protected override void ResetHealer()
    {
        aetherflow = 0;
        fairy = 0;
        seraphRemaining = 0;
        aetherpact = false;
        pactTick = 0;
    }

    protected override (int Group, double Recast, int Charges) JobTimingContract(uint actionId) => actionId switch
    {
        166 => (14, 60, 1),
        167 => (4, 1, 1),
        189 => (1, 1, 1),
        3583 => (9, 30, 1),
        188 => (8, 30, 1),
        7434 => (10, 45, 1),
        3585 => (20, 90, 1),
        3586 => (7, 15, 1),
        3587 => (25, 180, 1),
        7436 => (21, 120, 1),
        7437 => (6, 3, 1),
        7869 => (1006, 1, 1), // Dissolve Union: its own 1 s recast beside Aetherpact's group
        16537 => (15, 60, 1),
        16538 => (22, 120, 1),
        16542 => (16, 90, 1),
        16543 => (12, 60, 1),
        16545 => (23, 120, 1),
        16546 => (11, 30, 2),
        25867 => (13, 60, 1),
        25868 => (19, 120, 1),
        _ => Gcd,
    };
}
