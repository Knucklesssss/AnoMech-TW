using System;
using System.Collections.Generic;
using System.Linq;

namespace AnoMech.Core.Combat;

// Level-90 forms, Perfect Balance, Beast Chakra blitzes, Nadi, fury and chakra. Rules from docs/client-data/MNK-90.md.
public sealed class MonkCombat : MeleeCombatBase
{
    private const ushort OpoOpoForm = 107, RaptorForm = 108, CoeurlForm = 109, PerfectBalance = 110, FormlessFist = 2513, Mantra = 102,
        RiddleOfEarth = 1179, EarthsRuminationReady = 3841, RiddleOfFire = 1181, Brotherhood = 1185, MeditativeBrotherhood = 1182,
        RiddleOfWind = 2687, SixSidedStar = 2514;
    // Spec decision: the simulation has no stats, so a weaponskill critical is a fixed 40% roll.
    private const double CritChance = 0.4;
    private readonly int[] beastChakra = new int[3];
    private int chakra;
    private bool lunar, solar;
    private int opoFury, raptorFury, coeurlFury;
    // Meditation reads combat inside ApplyJob, which has no combat flag; CanUse always runs right before it.
    private bool combat;

    public MonkCombat(double gcdSeconds = 2.5) : base(gcdSeconds) { }

    public int Chakra => chakra;
    public IReadOnlyList<int> BeastChakra => beastChakra;
    public bool LunarNadi => lunar;
    public bool SolarNadi => solar;
    public int OpoOpoFury => opoFury;
    public int RaptorFury => raptorFury;
    public int CoeurlFury => coeurlFury;
    public double PerfectBalanceRemaining => BuffRemaining(PerfectBalance);

    protected override IReadOnlyList<uint> JobActions { get; } =
        [53, 54, 56, 61, 66, 70, 74, 16473, 25767, 4262, 25764, 3545, 25765, 25768, 25769, 16476, 36942, 36943, 65, 69, 7394, 36944,
         7395, 7396, 25766, 3547, 16474, 25762];
    protected override IReadOnlyList<ushort> JobStatusIds { get; } =
        [OpoOpoForm, RaptorForm, CoeurlForm, PerfectBalance, FormlessFist, Mantra, RiddleOfEarth, EarthsRuminationReady, RiddleOfFire,
         Brotherhood, MeditativeBrotherhood, RiddleOfWind, SixSidedStar];
    protected override string JobDebugState
        => $"chakra={chakra} beast={string.Join("", beastChakra)} nadi={(lunar ? "L" : "-")}{(solar ? "S" : "-")} fury={opoFury}/{raptorFury}/{coeurlFury}";

    public override uint Adjust(uint actionId) => actionId switch
    {
        36945 => 53, // Leaping Opo, level 92, stored on a level-100 hotbar
        36946 => 54, // Rising Raptor, level 92
        36947 => 56, // Pouncing Coeurl, level 92
        62 => 25767,
        25761 => 3547,
        25763 => 16474,
        36940 => 36942,
        36941 => 36943,
        25764 => Blitz(),
        _ => actionId,
    };

    private uint Blitz()
    {
        if (beastChakra.Any(b => b == 0)) return 25764;
        if (lunar && solar) return 25769;
        return beastChakra.Distinct().Count() switch { 1 => 3545, 3 => 25768, _ => 25765 };
    }

    public override bool IsGapCloser(uint actionId) => actionId == 25762;
    protected override uint ComboFrom(uint actionId) => 0;
    protected override bool IsJobSelfAction(uint actionId)
        => actionId is 70 or 16473 or 25767 or 4262 or 25764 or 3545 or 25768 or 36942 or 36943 or 65 or 69 or 7394 or 36944
            or 7395 or 7396 or 25766;

    protected override bool JobCanUse(uint actionId, bool inCombat)
    {
        combat = inCombat;
        return actionId switch
        {
            54 or 61 or 16473 => InForm(RaptorForm),
            56 or 66 or 70 => InForm(CoeurlForm),
            25764 => false,
            3545 or 25765 or 25768 or 25769 => Blitz() == actionId,
            69 => inCombat && beastChakra.All(b => b == 0),
            3547 or 16474 => inCombat && chakra >= 5,
            36942 or 36943 => chakra < 5,
            36944 => HasBuff(EarthsRuminationReady),
            _ => true,
        };
    }

    private bool InForm(ushort form) => HasBuff(form) || HasBuff(FormlessFist) || HasBuff(PerfectBalance);

    protected override bool JobHighlighted(uint actionId) => actionId switch
    {
        53 or 74 or 25767 => HasBuff(OpoOpoForm),
        54 or 61 or 16473 => HasBuff(RaptorForm),
        56 or 66 or 70 => HasBuff(CoeurlForm),
        3545 or 25765 or 25768 or 25769 => Blitz() == actionId,
        3547 or 16474 => chakra >= 5,
        36944 => HasBuff(EarthsRuminationReady),
        _ => false,
    };

    protected override JobHit? ApplyJob(uint actionId, bool hasTarget)
    {
        switch (actionId)
        {
            case 53:
                FormSkill(OpoOpoForm, 3, RaptorForm, critInForm: true);
                opoFury = 0;
                return new JobHit(actionId, false, false);
            case 74:
                if (FormSkill(OpoOpoForm, 3, RaptorForm, critInForm: false)) opoFury = 1;
                return new JobHit(actionId, false, false);
            case 25767:
                FormSkill(OpoOpoForm, 3, RaptorForm, critInForm: true);
                return Area(actionId, hasTarget);
            case 54:
                FormSkill(RaptorForm, 1, CoeurlForm, critInForm: false);
                raptorFury = 0;
                return new JobHit(actionId, false, false);
            case 61:
                FormSkill(RaptorForm, 1, CoeurlForm, critInForm: false);
                raptorFury = 1;
                return new JobHit(actionId, false, false);
            case 16473:
                FormSkill(RaptorForm, 1, CoeurlForm, critInForm: false);
                return Area(actionId, hasTarget);
            case 56:
                FormSkill(CoeurlForm, 2, OpoOpoForm, critInForm: false);
                coeurlFury = Math.Max(0, coeurlFury - 1);
                return new JobHit(actionId, false, false);
            case 66:
                FormSkill(CoeurlForm, 2, OpoOpoForm, critInForm: false);
                coeurlFury = 2;
                return new JobHit(actionId, false, false);
            case 70:
                FormSkill(CoeurlForm, 2, OpoOpoForm, critInForm: false);
                return Area(actionId, hasTarget);
            case 3545 or 25765 or 25768 or 25769:
                if (actionId == 3545) lunar = true;
                else if (actionId == 25768) solar = true;
                else if (actionId == 25765) { if (lunar) solar = true; else lunar = true; }
                else lunar = solar = false;
                Array.Clear(beastChakra);
                Buff(FormlessFist, 30);
                GainChakra(guaranteedCrit: false);
                return actionId is 3545 or 25768 ? Area(actionId, hasTarget) : new JobHit(actionId, actionId == 25769, false);
            case 4262: Buff(FormlessFist, 30); return null;
            case 16476:
                chakra = 0;
                Buff(SixSidedStar, 5);
                return new JobHit(actionId, false, false);
            case 36942 or 36943:
                chakra = combat ? Math.Min(5, chakra + 1) : 5;
                return null;
            case 3547 or 16474:
                chakra -= 5;
                return new JobHit(actionId, actionId == 16474, false);
            case 65: Buff(Mantra, 15); return null;
            case 69: Buff(PerfectBalance, 20, 3); return null;
            case 7394:
                Buff(RiddleOfEarth, 10);
                Buff(EarthsRuminationReady, 30);
                return null;
            case 36944: ClearBuff(EarthsRuminationReady); return null;
            case 7395: Buff(RiddleOfFire, 20); return null;
            case 7396:
                Buff(Brotherhood, 20);
                Buff(MeditativeBrotherhood, 20);
                return null;
            case 25766: Buff(RiddleOfWind, 15); return null;
            case 25762: return new JobHit(actionId, false, true);
        }
        return null;
    }

    // A form weaponskill: Perfect Balance adds Beast Chakra instead of changing form; the form itself or Formless Fist
    // triggers the form's effects. Returns whether those effects apply.
    // ponytail: Beast Chakra kinds follow the client text's mapping (Opo-opo weaponskill → 猛虎脈輪); unverified on the gauge.
    private bool FormSkill(ushort form, int beastChakraKind, ushort next, bool critInForm)
    {
        var formless = ConsumeBuff(FormlessFist);
        var effects = formless || HasBuff(form);
        if (ConsumeStack(PerfectBalance))
        {
            var slot = Array.IndexOf(beastChakra, 0);
            if (slot >= 0) beastChakra[slot] = beastChakraKind;
        }
        else
        {
            ClearBuff(OpoOpoForm);
            ClearBuff(RaptorForm);
            ClearBuff(CoeurlForm);
            Buff(next, 30);
        }
        GainChakra(guaranteedCrit: critInForm && effects);
        return effects;
    }

    private void GainChakra(bool guaranteedCrit)
    {
        var brotherhood = HasBuff(MeditativeBrotherhood);
        var gain = (guaranteedCrit || Chance(CritChance) ? 1 : 0) + (brotherhood ? 1 : 0);
        chakra = Math.Max(chakra, Math.Min(brotherhood ? 10 : 5, chakra + gain));
    }

    private static JobHit? Area(uint actionId, bool hasTarget) => hasTarget ? new JobHit(actionId, true, false) : null;

    protected override void AdvanceJob(double seconds) { }

    protected override void ResetJob()
    {
        Array.Clear(beastChakra);
        chakra = 0;
        lunar = solar = false;
        opoFury = raptorFury = coeurlFury = 0;
    }

    protected override (int Group, double Recast, int Charges) JobTimingContract(uint actionId) => actionId switch
    {
        16476 => (GlobalCooldownGroup, Scaled(5), 1),
        36942 or 36943 => (GlobalCooldownGroup, Scaled(1), 1),
        65 => (16, 90, 1),
        69 => (14, 40, 2),
        7394 => (21, 120, 1),
        36944 => (2, 1, 1),
        7395 => (12, 60, 1),
        7396 => (20, 120, 1),
        25766 => (17, 90, 1),
        3547 => (1, 1, 1),
        16474 => (3, 1, 1),
        25762 => (15, 30, 3), // trait 431 (level 84): 3 charges
        _ => Gcd,
    };
}
