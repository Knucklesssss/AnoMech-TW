using System;
using System.Collections.Generic;

namespace AnoMech.Core.Combat;

// Level-90 Astral Fire / Umbral Ice, Umbral Hearts, Polyglot, Paradox and Thunderhead.
// Rules from docs/client-data/BLM-90.md.
public sealed class BlackMageCombat : CasterCombatBase
{
    private const ushort Firestarter = 165, Manaward = 168, LeyLines = 737, Triplecast = 1211, Thunderhead = 3870;
    private const double PolyglotSeconds = 30;
    private const int MaxPolyglot = 2;
    // Client trait text (296/458/459): ice spells cast in Umbral Ice restore MP, 2500/5000/10000 by stack.
    private static readonly int[] UmbralSoulMp = [0, 2500, 5000, 10000];
    private int element;
    private int hearts;
    private int polyglot;
    private double polyglotTimer;
    private bool paradox;

    public BlackMageCombat(double gcdSeconds = 2.5) : base(gcdSeconds) { }

    public int Element => element;
    public int UmbralHearts => hearts;
    public int Polyglot => polyglot;
    public double PolyglotTimer => polyglotTimer;
    public bool Paradox => paradox;
    protected override IReadOnlyList<uint> JobActions { get; } =
        [141, 142, 25794, 149, 152, 153, 154, 155, 156, 157, 158, 159, 162, 3573, 3576, 3577, 7419, 7420, 7421, 7422, 16505,
         16506, 16507, 25795, 25796, 25797];
    protected override IReadOnlyList<ushort> JobStatusIds { get; } = [Firestarter, Manaward, LeyLines, Triplecast, Thunderhead];
    protected override string JobDebugState => $"element={element} hearts={hearts} polyglot={polyglot}/{polyglotTimer:0.0} paradox={paradox}";

    protected override int MpTickAmount => element > 0 ? 0 : base.MpTickAmount;

    public override uint Adjust(uint actionId) => actionId switch
    {
        144 => 153,
        7447 => 7420,
        147 => 25794,
        25793 => 25795,
        36986 => 153, // High Thunder, level 92, stored on a level-100 hotbar
        36987 => 7420, // High Thunder II, level 92
        141 or 142 when paradox => 25797,
        _ => actionId,
    };

    public override bool IsGapCloser(uint actionId) => false;
    protected override uint ComboFrom(uint actionId) => 0;

    // Aetherial Manipulation targets a party member; the dash is not simulated.
    protected override bool IsJobSelfAction(uint actionId)
        => actionId is 149 or 155 or 157 or 158 or 3573 or 7419 or 7421 or 16506 or 25796;

    private static bool IsFire(uint actionId) => actionId is 141 or 25794 or 152 or 3577 or 162 or 16505;
    private static bool IsIce(uint actionId) => actionId is 142 or 25795 or 154 or 3576 or 159;

    protected override double JobCastTime(uint actionId)
    {
        if (actionId == 152 && HasBuff(Firestarter)) return 0;
        var seconds = actionId switch
        {
            141 or 142 or 159 or 162 or 3576 or 3577 or 16505 => 2.0,
            25794 or 25795 => 3.0,
            152 or 154 => 3.5,
            // Foul (7422) is instant per trait 461 ("穢濁效果提高", level 80: 發動穢濁不需要詠唱時間), so it stays out of this switch and falls to 0.
            _ => 0.0,
        };
        // Trait 459 ("極性精通III", level 35) states the opposite element's cast time is halved at full stacks.
        return element == 3 && IsIce(actionId) || element == -3 && IsFire(actionId) ? seconds / 2 : seconds;
    }

    protected override bool JobInstant(uint actionId) => HasBuff(Triplecast);
    protected override void SpendJobInstant(uint actionId) => ConsumeStack(Triplecast);

    protected override int JobMpCost(uint actionId)
    {
        if (actionId == 152 && HasBuff(Firestarter)) return 0;
        var cost = actionId switch
        {
            141 or 3577 or 156 or 154 or 3576 or 25795 => 800,
            142 => 400,
            159 => 1000,
            152 => 2000,
            25794 => 1500,
            25797 => 1600,
            162 => hearts > 0 ? Math.Max(800, Mp * 2 / 3) : Math.Max(800, Mp),
            16505 => Math.Max(800, Mp),
            _ => 0,
        };
        if (actionId == 25797) return element < 0 ? 0 : cost;
        if (IsFire(actionId))
        {
            if (element < 0) return 0; // opposite element (trait 296): free at any Umbral Ice stack
            return element > 0 && hearts == 0 && actionId is not (162 or 16505) ? cost * 2 : cost;
        }
        if (IsIce(actionId)) return element == 0 ? cost : 0;
        return cost;
    }

    protected override bool JobCanUse(uint actionId, bool inCombat) => actionId switch
    {
        3577 or 162 or 16505 or 158 => element > 0,
        3576 or 159 or 16506 => element < 0,
        153 or 7420 => HasBuff(Thunderhead),
        7422 or 16507 => polyglot > 0,
        25797 => paradox,
        149 or 25796 => element != 0,
        7419 => HasBuff(LeyLines),
        3573 => !HasBuff(LeyLines),
        _ => true,
    };

    protected override JobHit? ApplyJob(uint actionId, bool hasTarget, bool hardcast)
    {
        switch (actionId)
        {
            case 141:
                if (element > 0 && hearts > 0) hearts--;
                SetElement(element < 0 ? 0 : Math.Min(3, element + 1));
                if (Chance(0.4)) Buff(Firestarter, double.PositiveInfinity);
                return new JobHit(actionId, false, false);
            case 152:
                if (!ConsumeBuff(Firestarter) && element > 0 && hearts > 0) hearts--;
                SetElement(3);
                return new JobHit(actionId, false, false);
            case 25794:
                if (element > 0 && hearts > 0) hearts--;
                SetElement(3);
                return new JobHit(actionId, true, false);
            case 3577:
                if (hearts > 0) hearts--;
                return new JobHit(actionId, false, false);
            case 162:
                hearts = 0;
                SetElement(3);
                return new JobHit(actionId, true, false);
            case 16505:
                SetElement(3);
                return new JobHit(actionId, false, false);
            case 142:
                SetElement(element > 0 ? 0 : Math.Max(-3, element - 1));
                if (element < 0) GainMp(UmbralSoulMp[-element]);
                return new JobHit(actionId, false, false);
            case 154 or 25795:
                SetElement(-3);
                GainMp(UmbralSoulMp[-element]);
                return new JobHit(actionId, actionId == 25795, false);
            case 3576 or 159:
                hearts = 3;
                GainMp(UmbralSoulMp[-element]); // JobCanUse requires element < 0 here already
                return new JobHit(actionId, actionId == 159, false);
            case 16506:
                SetElement(Math.Max(-3, element - 1));
                hearts = Math.Min(3, hearts + 1);
                GainMp(UmbralSoulMp[-element]);
                return null;
            case 25797:
                paradox = false;
                if (element > 0) Buff(Firestarter, double.PositiveInfinity);
                return new JobHit(actionId, false, false);
            case 153 or 7420:
                ClearBuff(Thunderhead);
                return new JobHit(actionId, actionId == 7420, false);
            case 7422 or 16507:
                polyglot--;
                return new JobHit(actionId, actionId == 7422, false);
            case 25796: polyglot = Math.Min(MaxPolyglot, polyglot + 1); return null;
            case 158:
                GainMp(MaxMp);
                SetElement(3);
                hearts = 3;
                Buff(Thunderhead, double.PositiveInfinity);
                paradox = true;
                return null;
            case 149: SetElement(element > 0 ? -1 : 1); return null;
            case 7421: Buff(Triplecast, 15, 3); return null;
            case 3573: Buff(LeyLines, 20); return null;
            case 157: Buff(Manaward, 20); return null;
            case 156: return new JobHit(actionId, false, false);
        }
        return null;
    }

    private void SetElement(int next)
    {
        if (next == element) return;
        // ponytail: Paradox from switching at full stacks per the commonly cited 7.x rule; the client text only names the crystal.
        if (element == -3 && hearts == 3 && next > 0 || element == 3 && next < 0) paradox = true;
        if (next != 0 && Math.Sign(next) != Math.Sign(element)) Buff(Thunderhead, double.PositiveInfinity);
        if (next == 0) polyglotTimer = 0;
        element = next;
    }

    protected override void AdvanceCaster(double seconds)
    {
        if (element == 0) return;
        for (polyglotTimer += seconds; polyglotTimer >= PolyglotSeconds; polyglotTimer -= PolyglotSeconds)
            polyglot = Math.Min(MaxPolyglot, polyglot + 1);
    }

    protected override void ResetCaster()
    {
        element = 0;
        hearts = 0;
        polyglot = 0;
        polyglotTimer = 0;
        paradox = false;
        ClearBuff(Firestarter);
        ClearBuff(Thunderhead);
    }

    protected override (int Group, double Recast, int Charges) JobTimingContract(uint actionId) => actionId switch
    {
        149 => (2, 5, 1),
        155 => (3, 10, 1),
        157 => (22, 120, 1),
        // Trait 463 ("魔泉效果提高", level 84) shortens Manafont's recast from the sheet's 120s to 100s.
        158 => (24, 100, 1),
        3573 => (20, 120, 2), // 2 charges confirmed by client data ("累積次數：2"), not just the tooltip.
        7419 => (1, 3, 1),
        7421 => (19, 60, 2),
        25796 => (21, 120, 1),
        _ => Gcd,
    };
}
