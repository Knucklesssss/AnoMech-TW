using System;
using System.Collections.Generic;

namespace AnoMech.Core.Combat;

// Level-90 Black/White Mana, Dualcast, Verfire/Verstone procs, enchanted melee and finishers.
// Rules from docs/client-data/RDM-90.md.
public sealed class RedMageCombat : CasterCombatBase
{
    private const ushort VerfireReady = 1234, VerstoneReady = 1235, Acceleration = 1238, Embolden = 1239, Dualcast = 1249,
        Manafication = 1971, MagickBarrier = 2707, MagickedSwordplay = 3875;
    private int black;
    private int white;
    private int stacks;
    private bool accelerated;

    public RedMageCombat(double gcdSeconds = 2.5) : base(gcdSeconds) { }

    public int Black => black;
    public int White => white;
    public int ManaStacks => stacks;
    protected override IReadOnlyList<uint> JobActions { get; } =
        [37004, 7504, 7527, 7512, 7528, 7516, 7529, 7513, 7530, 37002, 37003, 16529, 16528, 25855, 25856, 7510, 7511, 16526,
         16524, 16525, 7525, 7526, 16530, 25858, 7506, 7515, 16527, 7517, 7518, 7519, 7520, 7521, 25857, 7514, 7523];
    protected override IReadOnlyList<ushort> JobStatusIds { get; } =
        [VerfireReady, VerstoneReady, Acceleration, Embolden, Dualcast, Manafication, MagickBarrier, MagickedSwordplay];
    protected override string JobDebugState => $"black={black} white={white} stacks={stacks}";

    private bool Enchanted(int cost) => HasBuff(MagickedSwordplay) || black >= cost && white >= cost;

    public override uint Adjust(uint actionId) => actionId switch
    {
        7503 or 7524 => Adjust(37004),
        7505 => Adjust(25855),
        7507 => Adjust(25856),
        7509 => Adjust(16526),
        25855 or 16524 when stacks == 3 => 7525,
        25856 or 16525 when stacks == 3 => 7526,
        37004 or 16526 when ComboAction is 7525 or 7526 => 16530,
        37004 or 16526 when ComboAction == 16530 => 25858,
        7504 when Enchanted(20) => 7527,
        7512 when Enchanted(15) => 7528,
        7516 when Enchanted(15) => 7529,
        7513 when ComboAction == 7530 && Enchanted(15) => 37002,
        7513 when ComboAction == 37002 && Enchanted(15) => 37003,
        7513 when Enchanted(20) => 7530,
        16529 when black >= 5 && white >= 5 => 16528,
        _ => actionId,
    };

    public override bool IsGapCloser(uint actionId) => actionId == 7506;
    protected override uint ComboFrom(uint actionId) => actionId switch
    {
        7512 => 7504,
        7516 => 7512,
        7528 => 7527,
        7529 => 7528,
        37002 => 7530,
        37003 => 37002,
        16530 => 7525,
        25858 => 16530,
        _ => 0,
    };

    // Vercure and Verraise are cast on the player; party targets are not simulated.
    protected override bool IsJobSelfAction(uint actionId) => actionId is 7518 or 7520 or 7521 or 25857 or 7514 or 7523;
    protected override bool JobHighlighted(uint actionId) => actionId switch
    {
        16530 => ComboAction == 7526,
        7510 => HasBuff(VerfireReady),
        7511 => HasBuff(VerstoneReady),
        7525 or 7526 => stacks == 3,
        _ => false,
    };

    protected override double JobCastTime(uint actionId) => actionId switch
    {
        37004 or 7510 or 7511 or 16524 or 16525 or 7514 => 2,
        25855 or 25856 or 16526 => 5,
        7523 => 10,
        _ => 0,
    };

    private static bool Accelerable(uint actionId) => actionId is 25855 or 25856 or 16526;
    protected override bool JobInstant(uint actionId) => HasBuff(Dualcast) || HasBuff(Acceleration) && Accelerable(actionId);
    protected override void SpendJobInstant(uint actionId)
    {
        if (ConsumeBuff(Dualcast)) return;
        ClearBuff(Acceleration);
        accelerated = true;
    }

    protected override int JobMpCost(uint actionId) => actionId switch
    {
        37004 or 7510 or 7511 => 200,
        25855 or 25856 => 300,
        16526 or 16524 or 16525 or 16530 or 25858 => 400,
        7514 => 500,
        7523 => 2400,
        _ => 0,
    };

    protected override bool JobCanUse(uint actionId, bool inCombat) => actionId switch
    {
        7527 or 7530 => Enchanted(20),
        7528 or 7529 or 37002 or 37003 => Enchanted(15),
        16528 => black >= 5 && white >= 5,
        7510 => HasBuff(VerfireReady),
        7511 => HasBuff(VerstoneReady),
        7525 or 7526 => stacks == 3,
        16530 => ComboAction is 7525 or 7526,
        25858 => ComboAction == 16530,
        7521 => inCombat,
        _ => true,
    };

    protected override JobHit? ApplyJob(uint actionId, bool hasTarget, bool hardcast)
    {
        // accelerated is set only by SpendJobInstant, and only when Acceleration (not Dualcast) made the
        // cast instant; a hardcast can't have Acceleration up (it would have been instant), so deriving
        // accel from HasBuff(Acceleration) here would wrongly consume and guarantee-proc an Acceleration
        // that Dualcast, not this cast, actually spent.
        var accel = accelerated;
        accelerated = false;
        // Trait 216: Dualcast is removed by anything but an auto-attack or an ability. A GCD action clears
        // it here; if this cast just consumed Dualcast via SpendJobInstant it is already gone (no-op).
        if (JobTimingContract(actionId).Group == GlobalCooldownGroup) ClearBuff(Dualcast);
        if (hardcast) Buff(Dualcast, 15);
        switch (actionId)
        {
            case 37004: Mana(2, 2); return Single(actionId);
            case 25855:
                Mana(6, 0);
                if (accel || Chance(0.5)) Buff(VerfireReady, 30);
                return Single(actionId);
            case 25856:
                Mana(0, 6);
                if (accel || Chance(0.5)) Buff(VerstoneReady, 30);
                return Single(actionId);
            case 7510:
                ClearBuff(VerfireReady);
                Mana(5, 0);
                return Single(actionId);
            case 7511:
                ClearBuff(VerstoneReady);
                Mana(0, 5);
                return Single(actionId);
            case 16526: Mana(3, 3); return Area(actionId);
            case 16524: Mana(7, 0); return Area(actionId);
            case 16525: Mana(0, 7); return Area(actionId);
            case 7527:
                Swing(20);
                SetCombo(7527);
                return Single(actionId);
            case 7528:
                Swing(15);
                if (ComboAction == 7527) SetCombo(7528); else ClearCombo();
                return Single(actionId);
            case 7529:
                Swing(15);
                ClearCombo();
                return Single(actionId);
            case 7530:
                Swing(20);
                SetCombo(7530);
                return Area(actionId);
            case 37002:
                Swing(15);
                SetCombo(37002);
                return Area(actionId);
            case 37003:
                Swing(15);
                ClearCombo();
                return Area(actionId);
            case 16528:
                black -= 5;
                white -= 5;
                return Single(actionId);
            case 7504:
                SetCombo(7504);
                return Single(actionId);
            case 7512:
                if (ComboAction == 7504) SetCombo(7512); else ClearCombo();
                return Single(actionId);
            case 7516 or 16529:
                ClearCombo();
                return Single(actionId);
            case 7513: return Area(actionId);
            case 7525:
            {
                var guaranteed = white > black;
                stacks = 0;
                Mana(11, 0);
                if (guaranteed || Chance(0.2)) Buff(VerfireReady, 30);
                SetCombo(7525);
                return Area(actionId);
            }
            case 7526:
            {
                var guaranteed = black > white;
                stacks = 0;
                Mana(0, 11);
                if (guaranteed || Chance(0.2)) Buff(VerstoneReady, 30);
                SetCombo(7526);
                return Area(actionId);
            }
            case 16530:
                Mana(4, 4);
                SetCombo(16530);
                return Area(actionId);
            case 25858:
                Mana(4, 4);
                ClearCombo();
                return Area(actionId);
            case 7506: return new JobHit(actionId, false, true);
            case 7515 or 16527 or 7517: return Single(actionId);
            case 7519: return Area(actionId);
            case 7518: Buff(Acceleration, 20); return null;
            case 7520: Buff(Embolden, 20); return null;
            case 7521:
                Buff(MagickedSwordplay, 30, 3);
                Buff(Manafication, 30, 6);
                ClearCombo();
                return null;
            case 25857: Buff(MagickBarrier, 10); return null;
        }
        return null;
    }

    private static JobHit Single(uint actionId) => new(actionId, false, false);
    private static JobHit Area(uint actionId) => new(actionId, true, false);

    private void Mana(int blackGain, int whiteGain)
    {
        black = Math.Min(100, black + blackGain);
        white = Math.Min(100, white + whiteGain);
    }

    private void Swing(int cost)
    {
        if (!ConsumeStack(MagickedSwordplay))
        {
            black -= cost;
            white -= cost;
        }
        stacks = Math.Min(3, stacks + 1);
    }

    protected override void AdvanceCaster(double seconds) { }

    protected override void ResetCaster()
    {
        black = 0;
        white = 0;
        stacks = 0;
        accelerated = false;
    }

    protected override (int Group, double Recast, int Charges) JobTimingContract(uint actionId) => actionId switch
    {
        7527 or 7528 or 7530 or 37002 or 37003 => (GlobalCooldownGroup, 1.5, 1),
        7529 => (GlobalCooldownGroup, 2.2, 1),
        7506 => (11, 35, 2),
        7515 or 16527 => (10, 35, 2),
        7517 => (5, 25, 1),
        // Trait 485 (level 88, "促進效果提高") turns Acceleration into a 2-charge skill.
        7518 => (20, 55, 2),
        // Trait 306 (level 74, "赤魔法精通") shortens Contre Sixte's recast from the sheet's 45s to 35s.
        7519 => (8, 35, 1),
        7520 => (21, 120, 1),
        // Trait 305 (level 78, "魔元化效果提高") shortens the sheet's 120s to 110s.
        7521 => (22, 110, 1),
        25857 => (19, 120, 1),
        _ => Gcd,
    };
}
