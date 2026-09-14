using System;
using System.Collections.Generic;

namespace AnoMech.Core.Combat;

// Level-90 Heat, Battery, Overheat and Automaton Queen. Rules from docs/client-data/MCH-90.md.
public sealed class MachinistCombat : RangedCombatBase
{
    private const ushort Reassembled = 851, Overheated = 2688, Wildfire = 1946, Flamethrower = 1205, Hypercharged = 3864, Tactician = 1951;
    // Client text: Automaton Queen's runtime is a flat 12 s, regardless of banked Battery.
    private const double QueenSeconds = 12;
    private int heat;
    private int battery;
    private int lastBattery;
    private double queenRemaining;

    public MachinistCombat(double gcdSeconds = 2.5) : base(gcdSeconds) { }

    public int Heat => heat;
    public int Battery => battery;
    public int QueenBattery => lastBattery;
    public double QueenRemaining => queenRemaining;
    public double OverheatRemaining => BuffRemaining(Overheated);
    public int OverheatStacks => BuffParam(Overheated);
    protected override IReadOnlyList<uint> JobActions { get; } =
        [7411, 7412, 7413, 25786, 16500, 16498, 16499, 25788, 36978, 16497, 2874, 2890, 2876, 2878, 16766, 17209, 7414, 16501,
         16502, 7418, 16889, 2887];
    protected override IReadOnlyList<ushort> JobStatusIds { get; } = [Reassembled, Overheated, Wildfire, Flamethrower, Hypercharged, Tactician];
    protected override string JobDebugState => $"heat={heat} battery={battery} queen={queenRemaining:0.0}/{lastBattery}";

    public override uint Adjust(uint actionId) => actionId switch
    {
        2866 => 7411,
        2868 => 7412,
        2873 => 7413,
        2870 => 25786,
        2872 => 16500,
        7410 => 36978,
        2864 => 16501,
        7415 => 16502,
        36979 => 2874, // Double Check, level 92, stored on a level-100 hotbar
        36980 => 2890, // Checkmate, level 92
        2878 when HasBuff(Wildfire) => 16766,
        _ => actionId,
    };

    public override bool IsGapCloser(uint actionId) => false;
    protected override uint ComboFrom(uint actionId) => actionId switch { 7412 => 7411, 7413 => 7412, _ => 0 };
    protected override bool IsJobSelfAction(uint actionId)
        => actionId is 2876 or 17209 or 7414 or 16501 or 16502 or 7418 or 16889 or 16766;
    protected override bool AlsoUsesGcd(uint actionId) => actionId is 16498 or 16499 or 16500 or 25788 or 7418;
    protected override bool JobHighlighted(uint actionId) => actionId switch
    {
        36978 or 16497 => HasBuff(Overheated),
        17209 => HasBuff(Hypercharged),
        _ => false,
    };

    protected override bool RangedCanUse(uint actionId, bool inCombat) => actionId switch
    {
        36978 or 16497 => HasBuff(Overheated),
        17209 => !HasBuff(Overheated) && (heat >= 50 || HasBuff(Hypercharged)),
        16501 => battery >= 50 && queenRemaining == 0,
        16502 => queenRemaining > 0,
        16766 => HasBuff(Wildfire),
        7414 => inCombat,
        _ => true,
    };

    protected override JobHit? ApplyJob(uint actionId, bool hasTarget)
    {
        if (actionId is 7411 or 7412 or 7413 or 25786 or 16500 or 16498 or 16499 or 25788 or 36978 or 16497) ConsumeBuff(Reassembled);
        switch (actionId)
        {
            case 7411:
                GainHeat(5);
                SetCombo(7411);
                return new JobHit(actionId, false, false);
            case 7412:
                if (ComboAction == 7411) { GainHeat(5); SetCombo(7412); }
                else ClearCombo();
                return new JobHit(actionId, false, false);
            case 7413:
                if (ComboAction == 7412) { GainHeat(5); GainBattery(10); }
                ClearCombo();
                return new JobHit(actionId, false, false);
            case 25786:
                GainHeat(10);
                return new JobHit(actionId, true, false);
            case 16500 or 25788:
                GainBattery(20);
                return new JobHit(actionId, actionId == 25788, false);
            case 36978:
                ConsumeStack(Overheated);
                Timing.Reduce(15, 15);
                Timing.Reduce(16, 15);
                return new JobHit(actionId, false, false);
            case 16497:
                ConsumeStack(Overheated);
                return new JobHit(actionId, true, false);
            case 16499 or 2890: return new JobHit(actionId, true, false);
            case 2876: Buff(Reassembled, 5); return null;
            case 2878:
                Buff(Wildfire, 10);
                return new JobHit(actionId, false, false);
            case 16766: ClearBuff(Wildfire); return null;
            case 17209:
                if (!ConsumeBuff(Hypercharged)) heat -= 50;
                Buff(Overheated, 10, 5);
                return null;
            case 7414: Buff(Hypercharged, 30); return null;
            case 16501:
                lastBattery = battery;
                battery = 0;
                queenRemaining = QueenSeconds;
                return null;
            case 16502: queenRemaining = 0; return null;
            case 7418: Buff(Flamethrower, 10); return null;
            case 16889: Buff(Tactician, 15); return null;
        }
        return new JobHit(actionId, false, false);
    }

    private void GainHeat(int amount) => heat = Math.Min(100, heat + amount);
    private void GainBattery(int amount) => battery = Math.Min(100, battery + amount);

    protected override void AdvanceJob(double seconds) => queenRemaining = Decrease(queenRemaining, seconds);

    protected override void ResetJob()
    {
        heat = 0;
        battery = 0;
        lastBattery = 0;
        queenRemaining = 0;
    }

    protected override (int Group, double Recast, int Charges) JobTimingContract(uint actionId) => actionId switch
    {
        16500 => (9, 40, 1),
        16498 or 16499 => (5, 20, 1),
        25788 => (12, 60, 1),
        36978 or 16497 => (GlobalCooldownGroup, 1.5, 1),
        2874 => (15, 30, 3),
        2890 => (16, 30, 3),
        2876 => (18, 55, 2),
        2878 => (20, 120, 1),
        16766 => (1, 1, 1),
        17209 => (2, 10, 1),
        7414 => (21, 120, 1),
        16501 => (3, 6, 1),
        16502 => (4, 15, 1),
        7418 => (13, 60, 1),
        16889 => (22, 90, 1), // trait 452 (lv88): Tactician recast cut to 90s
        2887 => (19, 120, 1),
        _ => Gcd,
    };
}
