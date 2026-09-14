using System;
using System.Collections.Generic;

namespace AnoMech.Core.Combat;

// Level-90 hues, Palette, paint, canvases, muses, hammer combo and Starry Muse. Rules from docs/client-data/PCT-90.md.
public sealed class PictomancerCombat : CasterCombatBase
{
    private const ushort SubtractivePalette = 3674, Aetherhues = 3675, AetherhuesII = 3676, HammerTime = 3680, Smudge = 3684,
        StarryMuse = 3685, TemperaCoat = 3686, TemperaGrassa = 3687, Hyperphantasia = 3688, SubtractiveSpectrum = 3690,
        MonochromeTones = 3691, MoogleReadyStatus = 4103;
    private int palette;
    private int paint;
    private Creature creatureCanvas;
    private bool wingNext;
    private bool weaponCanvas;
    private bool landscapeCanvas;
    private bool pomPortrait;
    private bool wingPortrait;

    public enum Creature { None, Pom, Wing }

    public PictomancerCombat(double gcdSeconds = 2.5) : base(gcdSeconds) { }

    public int Palette => palette;
    public int Paint => paint;
    public Creature CreatureCanvas => creatureCanvas;
    public bool WeaponCanvas => weaponCanvas;
    public bool LandscapeCanvas => landscapeCanvas;
    public bool PomPortrait => pomPortrait;
    public bool WingPortrait => wingPortrait;
    public bool MoogleReady => HasBuff(MoogleReadyStatus);
    protected override IReadOnlyList<uint> JobActions { get; } =
        [34650, 34651, 34652, 34653, 34654, 34655, 34656, 34657, 34658, 34659, 34660, 34661, 34662, 34663, 34689, 34664, 34665,
         34690, 34668, 34691, 34669, 35347, 34670, 34671, 35348, 34674, 35349, 34675, 34676, 34678, 34679, 34680, 34683, 34684,
         34685, 34686];
    protected override IReadOnlyList<ushort> JobStatusIds { get; } =
        [SubtractivePalette, Aetherhues, AetherhuesII, HammerTime, Smudge, StarryMuse, TemperaCoat, TemperaGrassa, Hyperphantasia,
         SubtractiveSpectrum, MonochromeTones, MoogleReadyStatus];
    protected override string JobDebugState
        => $"palette={palette} paint={paint} creature={creatureCanvas} wingNext={wingNext} weapon={weaponCanvas} landscape={landscapeCanvas} portraits={pomPortrait}{wingPortrait}";

    private uint Hue(uint red, uint green, uint blue, uint cyan, uint yellow, uint magenta)
    {
        var stage = HasBuff(AetherhuesII) ? 2 : HasBuff(Aetherhues) ? 1 : 0;
        return HasBuff(SubtractivePalette) ? stage switch { 0 => cyan, 1 => yellow, _ => magenta } : stage switch { 0 => red, 1 => green, _ => blue };
    }

    public override uint Adjust(uint actionId) => actionId switch
    {
        34650 => Hue(34650, 34651, 34652, 34653, 34654, 34655),
        34656 => Hue(34656, 34657, 34658, 34659, 34660, 34661),
        34662 when HasBuff(MonochromeTones) => 34663,
        34689 => wingNext ? 34665u : 34664u,
        34690 => 34668,
        34691 => 34669,
        35347 when creatureCanvas == Creature.Pom => 34670,
        35347 when creatureCanvas == Creature.Wing => 34671,
        35348 => 34674,
        35349 => 34675,
        34678 when ComboAction == 34678 => 34679,
        34678 when ComboAction == 34679 => 34680,
        _ => actionId,
    };

    public override bool IsGapCloser(uint actionId) => false;
    protected override uint ComboFrom(uint actionId) => actionId switch { 34679 => 34678, 34680 => 34679, _ => 0 };

    protected override bool IsJobSelfAction(uint actionId)
        => actionId is 34689 or 34664 or 34665 or 34690 or 34668 or 34691 or 34669 or 35347 or 35348 or 34674 or 35349 or 34675
            or 34683 or 34684 or 34685 or 34686;

    protected override bool JobHighlighted(uint actionId) => actionId switch
    {
        34662 or 34663 => paint > 0,
        34676 => HasBuff(MoogleReadyStatus),
        34678 or 34679 or 34680 => HasBuff(HammerTime),
        34683 => palette >= 50 || HasBuff(SubtractiveSpectrum),
        _ => false,
    };

    // ponytail: motifs cast 3 s even out of combat; the rules do not track combat when asked for a cast time.
    protected override double JobCastTime(uint actionId) => actionId switch
    {
        34650 or 34651 or 34652 or 34656 or 34657 or 34658 => 1.5,
        34653 or 34654 or 34655 or 34659 or 34660 or 34661 => 2.3,
        34664 or 34665 or 34668 or 34669 => 3,
        _ => 0,
    };

    protected override int JobMpCost(uint actionId) => actionId switch
    {
        34650 or 34651 or 34652 or 34656 or 34657 or 34658 or 34662 => 300,
        34653 or 34654 or 34655 or 34659 or 34660 or 34661 or 34663 => 400,
        _ => 0,
    };

    protected override bool JobCanUse(uint actionId, bool inCombat)
    {
        var subtractive = HasBuff(SubtractivePalette);
        return actionId switch
        {
            34650 or 34656 => !subtractive,
            34651 or 34657 => !subtractive && HasBuff(Aetherhues),
            34652 or 34658 => !subtractive && HasBuff(AetherhuesII),
            34653 or 34659 => subtractive,
            34654 or 34660 => subtractive && HasBuff(Aetherhues),
            34655 or 34661 => subtractive && HasBuff(AetherhuesII),
            34662 => paint > 0 && !HasBuff(MonochromeTones),
            34663 => paint > 0 && HasBuff(MonochromeTones),
            34664 => creatureCanvas == Creature.None && !wingNext,
            34665 => creatureCanvas == Creature.None && wingNext,
            34668 => !weaponCanvas && !HasBuff(HammerTime),
            34669 => !landscapeCanvas && !HasBuff(StarryMuse),
            34689 or 34690 or 34691 or 35347 or 35348 or 35349 => false, // usable only through a substitution
            34670 => creatureCanvas == Creature.Pom,
            34671 => creatureCanvas == Creature.Wing,
            34674 => weaponCanvas && inCombat,
            34675 => landscapeCanvas && inCombat,
            34676 => HasBuff(MoogleReadyStatus),
            34678 or 34679 or 34680 => HasBuff(HammerTime),
            34683 => !subtractive && (palette >= 50 || HasBuff(SubtractiveSpectrum)),
            34686 => HasBuff(TemperaCoat),
            _ => true,
        };
    }

    protected override JobHit? ApplyJob(uint actionId, bool hasTarget, bool hardcast)
    {
        if (actionId is >= 34650 and <= 34663 && HasBuff(StarryMuse)) ConsumeStack(Hyperphantasia);
        if (actionId is 34653 or 34654 or 34655 or 34659 or 34660 or 34661) ConsumeStack(SubtractivePalette);
        switch (actionId)
        {
            case 34650 or 34656 or 34653 or 34659:
                Buff(Aetherhues, 30);
                ClearBuff(AetherhuesII);
                return Hit(actionId);
            case 34651 or 34657 or 34654 or 34660:
                ClearBuff(Aetherhues);
                Buff(AetherhuesII, 30);
                return Hit(actionId);
            case 34652 or 34658:
                ClearBuff(AetherhuesII);
                palette = Math.Min(100, palette + 25);
                paint = Math.Min(5, paint + 1);
                return Hit(actionId);
            case 34655 or 34661:
                ClearBuff(AetherhuesII);
                paint = Math.Min(5, paint + 1);
                return Hit(actionId);
            case 34662:
                paint--;
                return new JobHit(actionId, true, false);
            case 34663:
                paint--;
                ClearBuff(MonochromeTones);
                return new JobHit(actionId, true, false);
            case 34664:
                creatureCanvas = Creature.Pom;
                wingNext = true;
                return null;
            case 34665:
                creatureCanvas = Creature.Wing;
                wingNext = false;
                return null;
            case 34668: weaponCanvas = true; return null;
            case 34669: landscapeCanvas = true; return null;
            case 34670 or 34671:
                if (actionId == 34670) pomPortrait = true; else wingPortrait = true;
                creatureCanvas = Creature.None;
                if (pomPortrait && wingPortrait)
                {
                    Buff(MoogleReadyStatus, double.PositiveInfinity);
                    pomPortrait = wingPortrait = false;
                }
                return new JobHit(actionId, true, false);
            case 34674:
                weaponCanvas = false;
                Buff(HammerTime, 30, 3);
                return null;
            case 34675:
                landscapeCanvas = false;
                Buff(StarryMuse, 20);
                Buff(SubtractiveSpectrum, 30);
                Buff(Hyperphantasia, 30, 5);
                return null;
            case 34676:
                ClearBuff(MoogleReadyStatus);
                return new JobHit(actionId, true, false);
            case 34678 or 34679 or 34680:
                ConsumeStack(HammerTime);
                if (actionId == 34680) ClearCombo(); else SetCombo(actionId);
                return new JobHit(actionId, true, false);
            case 34683:
                if (!ConsumeBuff(SubtractiveSpectrum)) palette -= 50;
                Buff(SubtractivePalette, double.PositiveInfinity, 3);
                Buff(MonochromeTones, double.PositiveInfinity);
                return null;
            case 34684: Buff(Smudge, 5); return null;
            case 34685: Buff(TemperaCoat, 10); return null;
            case 34686:
                ClearBuff(TemperaCoat);
                Buff(TemperaGrassa, 10);
                return null;
        }
        return null;
    }

    private static JobHit Hit(uint actionId) => new(actionId, actionId is >= 34656 and <= 34661, false);

    protected override void AdvanceCaster(double seconds) { }

    protected override void ResetCaster()
    {
        palette = 0;
        paint = 0;
        creatureCanvas = Creature.None;
        wingNext = false;
        weaponCanvas = false;
        landscapeCanvas = false;
        pomPortrait = false;
        wingPortrait = false;
        ClearBuff(SubtractivePalette);
        ClearBuff(MonochromeTones);
        ClearBuff(MoogleReadyStatus);
    }

    // Trait 540 (level 86, "彩繪效果提高II") gives Striking Muse (34674) 2 charges instead of the sheet's base 1.
    protected override (int Group, double Recast, int Charges) JobTimingContract(uint actionId) => actionId switch
    {
        34653 or 34654 or 34655 or 34659 or 34660 or 34661 or 34663 => (GlobalCooldownGroup, Scaled(3.3), 1),
        34664 or 34665 or 34668 or 34669 => (GlobalCooldownGroup, Scaled(4), 1),
        34670 or 34671 or 35347 => (19, 40, 2),
        34674 or 35348 => (20, 60, 2),
        34675 or 35349 => (21, 120, 1),
        34676 => (7, 30, 1),
        34683 => (1, 1, 1),
        34684 => (6, 20, 1),
        34685 => (22, 120, 1),
        34686 => (2, 1, 1),
        _ => Gcd,
    };
}
