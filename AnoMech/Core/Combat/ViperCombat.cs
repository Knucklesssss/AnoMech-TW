using System;
using System.Collections.Generic;

namespace AnoMech.Core.Combat;

// Level-90 fang chains, Rattling Coil, Serpent Offering and Reawakened. Rules from docs/client-data/VPR-90.md.
// Both opener buttons carry their whole chain through Adjust, the way the client's replace table does.
public sealed class ViperCombat : MeleeCombatBase
{
    private const ushort HonedReavers = 3672, HonedSteel = 3772, HuntersInstinct = 3668, Swiftscaled = 3669,
        FlankstingStrike = 3645, FlanksbaneFang = 3646, HindstingStrike = 3647, HindsbaneFang = 3648,
        SteelFangs = 3649, ReavingFangs = 3650,
        HuntersVenom = 3657, SwiftskinsVenom = 3658, FellhuntersVenom = 3659, FellskinsVenom = 3660,
        Reawakened = 3670, ReadyToReawaken = 3671;

    private const int MaxOffering = 100, MaxCoil = 3, MaxTribute = 4;

    private int offering;
    private int coil;
    private int tribute;

    public ViperCombat(double gcdSeconds = 2.5) : base(gcdSeconds) { }

    public int SerpentOffering => offering;
    public int RattlingCoil => coil;
    public int AnguineTribute => tribute;
    public double ReawakenedRemaining => BuffRemaining(Reawakened);

    protected override IReadOnlyList<uint> JobActions { get; } =
        [34606, 34607, 34608, 34609, 34610, 34611, 34612, 34613,
         34614, 34615, 34616, 34617, 34618, 34619,
         34620, 34621, 34622, 34623, 34624, 34625,
         34626, 34627, 34628, 34629, 34630, 34632, 34633,
         35920, 34634, 34635, 35921, 34636, 34638, 35922, 34637, 34639,
         34646, 34647];
    protected override IReadOnlyList<ushort> JobStatusIds { get; } =
        [HonedReavers, HonedSteel, HuntersInstinct, Swiftscaled,
         FlankstingStrike, FlanksbaneFang, HindstingStrike, HindsbaneFang, SteelFangs, ReavingFangs,
         HuntersVenom, SwiftskinsVenom, FellhuntersVenom, FellskinsVenom, Reawakened, ReadyToReawaken];
    protected override string JobDebugState
        => $"offering={offering} coil={coil} tribute={tribute} combo={ComboAction}";

    public override uint Adjust(uint actionId) => actionId switch
    {
        // Reawakened rewrites the four chain buttons into the Legacy fangs.
        34606 when tribute > 0 => 34627,
        34607 when tribute > 0 => 34628,
        34621 when tribute > 0 => 34629,
        34622 when tribute > 0 => 34630,

        34606 => ComboAction == 34608 ? (HasBuff(FlanksbaneFang) ? 34611u : 34610)
               : ComboAction is 34606 or 34607 ? 34608u : 34606,
        34607 => ComboAction == 34609 ? (HasBuff(HindsbaneFang) ? 34613u : 34612)
               : ComboAction is 34606 or 34607 ? 34609u : 34607,
        34614 => ComboAction == 34616 ? (HasBuff(ReavingFangs) ? 34619u : 34618)
               : ComboAction is 34614 or 34615 ? 34616u : 34614,
        34615 => ComboAction == 34617 ? (HasBuff(ReavingFangs) ? 34619u : 34618)
               : ComboAction is 34614 or 34615 ? 34617u : 34615,

        35920 => hasTwinTail ? (tailIsAoe ? 34635u : 34634) : 35920,
        35921 => twinBlood == 0 ? 35921 : twinBloodIsAoe ? 34638u : 34636,
        35922 => twinBlood == 0 ? 35922 : twinBloodIsAoe ? 34639u : 34637,
        _ => actionId,
    };

    private bool hasTwinTail;
    private bool tailIsAoe;
    private int twinBlood;
    private bool twinBloodIsAoe;

    public override bool IsGapCloser(uint actionId) => actionId == 34646;
    protected override bool BlockedWhileBound(uint actionId) => actionId == 34646;
    // The Vicewinder pair keep their own cooldown and still spend the GCD.
    protected override bool AlsoUsesGcd(uint actionId) => actionId is 34620 or 34623;
    protected override double AlsoUsesGcdSeconds(uint actionId) => GcdSeconds;

    protected override uint ComboFrom(uint actionId) => actionId switch
    {
        34608 or 34609 => 34606,
        34610 or 34611 => 34608,
        34612 or 34613 => 34609,
        34616 or 34617 => 34614,
        34618 or 34619 => 34616,
        34621 or 34622 => 34620,
        34624 or 34625 => 34623,
        _ => 0,
    };

    protected override bool IsJobSelfAction(uint actionId)
        => actionId is 34614 or 34615 or 34616 or 34617 or 34618 or 34619 or 34623 or 34624 or 34625
            or 34626 or 35920 or 34635 or 35921 or 34638 or 35922 or 34639 or 34647;

    protected override bool JobCanUse(uint actionId, bool inCombat) => actionId switch
    {
        34626 => HasBuff(ReadyToReawaken) || offering >= 50,
        34627 or 34628 or 34629 or 34630 => tribute > 0,
        34633 => coil >= 1,
        34634 or 34635 => hasTwinTail,
        34636 or 34637 or 34638 or 34639 => twinBlood > 0,
        35920 => hasTwinTail,
        35921 or 35922 => twinBlood > 0,
        34647 => inCombat,
        _ => true,
    };

    protected override bool JobHighlighted(uint actionId) => actionId switch
    {
        34626 => HasBuff(ReadyToReawaken) || offering >= 50,
        34627 or 34628 or 34629 or 34630 => tribute > 0,
        34633 => coil >= 1,
        34634 or 34635 or 35920 => hasTwinTail,
        34636 or 34637 or 34638 or 34639 or 35921 or 35922 => twinBlood > 0,
        _ => false,
    };

    protected override JobHit? ApplyJob(uint actionId, bool hasTarget)
    {
        switch (actionId)
        {
            // Each opener grants the other one's Honed buff; they cannot coexist.
            case 34606:
                Buff(HonedSteel, 60);
                ClearBuff(HonedReavers);
                SetCombo(34606);
                return Strike(actionId, false, hasTarget);
            case 34607:
                Buff(HonedReavers, 60);
                ClearBuff(HonedSteel);
                SetCombo(34607);
                return Strike(actionId, false, hasTarget);
            case 34608:
                Continue(ComboAction is 34606 or 34607, 34608);
                Buff(HuntersInstinct, 40);
                return Strike(actionId, false, hasTarget);
            case 34609:
                Continue(ComboAction is 34606 or 34607, 34609);
                Buff(Swiftscaled, 40);
                return Strike(actionId, false, hasTarget);
            case 34610 or 34611 or 34612 or 34613:
                if (Continue(ComboAction == (actionId is 34610 or 34611 ? 34608u : 34609), 0))
                {
                    GainOffering(10);
                    ArmTail(aoe: false);
                }
                // Trait 675: the third fang arms the opposite fang of its own pair.
                ClearBuff(FlankstingStrike); ClearBuff(FlanksbaneFang);
                ClearBuff(HindstingStrike); ClearBuff(HindsbaneFang);
                Buff(actionId switch
                {
                    34610 => HindstingStrike,
                    34611 => HindsbaneFang,
                    34612 => FlankstingStrike,
                    _ => FlanksbaneFang,
                }, 60);
                return Strike(actionId, false, hasTarget);

            case 34614:
                Buff(HonedSteel, 60);
                ClearBuff(HonedReavers);
                SetCombo(34614);
                return Strike(actionId, true, hasTarget);
            case 34615:
                Buff(HonedReavers, 60);
                ClearBuff(HonedSteel);
                SetCombo(34615);
                return Strike(actionId, true, hasTarget);
            case 34616:
                Continue(ComboAction is 34614 or 34615, 34616);
                Buff(HuntersInstinct, 40);
                return Strike(actionId, true, hasTarget);
            case 34617:
                Continue(ComboAction is 34614 or 34615, 34617);
                Buff(Swiftscaled, 40);
                return Strike(actionId, true, hasTarget);
            case 34618 or 34619:
                if (Continue(ComboAction is 34616 or 34617, 0))
                {
                    GainOffering(10);
                    ArmTail(aoe: true);
                }
                ClearBuff(SteelFangs); ClearBuff(ReavingFangs);
                Buff(actionId == 34618 ? ReavingFangs : SteelFangs, 60);
                return Strike(actionId, true, hasTarget);

            case 34620 or 34623:
                coil = Math.Min(MaxCoil, coil + 1);
                SetCombo(actionId);
                return Strike(actionId, actionId == 34623, hasTarget);
            case 34621 or 34622 or 34624 or 34625:
            {
                var aoe = actionId is 34624 or 34625;
                if (Continue(ComboAction == (aoe ? 34623u : 34620), 0))
                {
                    GainOffering(5);
                    ArmTwinBlood(aoe);
                }
                Buff(actionId is 34621 or 34624 ? HuntersInstinct : Swiftscaled, 40);
                Buff(actionId switch
                {
                    34621 => HuntersVenom,
                    34622 => SwiftskinsVenom,
                    34624 => FellhuntersVenom,
                    _ => FellskinsVenom,
                }, 30);
                return Strike(actionId, aoe, hasTarget);
            }

            case 34626:
                if (!ConsumeBuff(ReadyToReawaken)) offering -= 50;
                Buff(Reawakened, 30);
                tribute = MaxTribute;
                ClearCombo();
                return Strike(actionId, true, hasTarget);
            case 34627 or 34628 or 34629 or 34630:
                tribute = Math.Max(0, tribute - 1);
                if (tribute == 0) ClearBuff(Reawakened);
                return Strike(actionId, true, hasTarget);

            case 34632: ClearCombo(); return Strike(actionId, false, hasTarget);
            case 34633:
                coil -= 1;
                ClearCombo();
                return Strike(actionId, true, hasTarget);

            case 34634 or 34635:
                hasTwinTail = false;
                return hasTarget ? new JobHit(actionId, actionId == 34635, false) : null;
            case 34636 or 34637 or 34638 or 34639:
                twinBlood = Math.Max(0, twinBlood - 1);
                return hasTarget ? new JobHit(actionId, actionId is 34638 or 34639, false) : null;

            case 34646: return hasTarget ? new JobHit(actionId, false, true) : null;
            case 34647:
                // Trait 531 readies Reawakened; the Offering it also grants is not quantified in the client text.
                Buff(ReadyToReawaken, 30);
                return null;
        }
        return null;
    }

    private void ArmTail(bool aoe)
    {
        hasTwinTail = true;
        tailIsAoe = aoe;
    }

    // Traits 526 and 527: each Vicewinder follow-up allows two uses of its Twinblood pair.
    private void ArmTwinBlood(bool aoe)
    {
        twinBlood = 2;
        twinBloodIsAoe = aoe;
    }

    private static JobHit? Strike(uint actionId, bool aoe, bool hasTarget)
        => hasTarget ? new JobHit(actionId, aoe, false) : null;

    private bool Continue(bool success, uint next)
    {
        if (success && next != 0) SetCombo(next);
        else ClearCombo();
        return success;
    }

    private void GainOffering(int amount) => offering = Math.Min(MaxOffering, offering + amount);

    protected override void AdvanceJob(double seconds)
    {
        if (!HasBuff(Reawakened) && tribute > 0) tribute = 0;
    }

    protected override void ResetJob()
    {
        offering = 0;
        coil = 0;
        tribute = 0;
        hasTwinTail = false;
        tailIsAoe = false;
        twinBlood = 0;
        twinBloodIsAoe = false;
    }

    protected override (int Group, double Recast, int Charges) JobTimingContract(uint actionId) => actionId switch
    {
        34620 or 34623 => (15, 40, 2),
        // Sheet GCD recasts that skill speed shortens alongside the 2.5 s GCD.
        34621 or 34622 or 34624 or 34625 => (GlobalCooldownGroup, Scaled(3), 1),
        34626 => (GlobalCooldownGroup, Scaled(2.2), 1),
        34627 or 34628 or 34629 or 34630 => (GlobalCooldownGroup, Scaled(2), 1),
        34633 => (GlobalCooldownGroup, Scaled(3.5), 1),
        35920 or 34634 or 34635 => (1, 1, 1),
        35921 or 34636 or 34638 => (2, 1, 1),
        35922 or 34637 or 34639 => (3, 1, 1),
        34646 => (14, 30, 3),  // trait 529 (level 84): 3 charges
        34647 => (20, 120, 1),
        _ => Gcd,
    };
}
