using System;
using System.Collections.Generic;

namespace AnoMech.Core.Combat;

// Level-90 mudras, ninjutsu, Ninki and Kazematoi. Rules from docs/client-data/NIN-90.md.
public sealed class NinjaCombat : MeleeCombatBase
{
    // PvE rows; the same-name CanStatusOff rows (2011, 2010) are PvP. Hide is 614, the row the client's own
    // "自身附加狀態" column lists for 隱遁. Meisui is 2689: row 3189 is the pre-Endwalker HP regen text.
    private const ushort Mudra = 496, Kassatsu = 497, Hide = 614, TenChiJin = 1186, ShadeShift = 488,
        Meisui = 2689, RaijuReady = 2690, PhantomKamaitachiReady = 2723, ShadowWalker = 3848, Bunshin = 1954;

    private const int Ten = 1, Chi = 2, Jin = 3;
    private const int MaxNinki = 100, MaxKazematoi = 5;

    private readonly List<int> mudras = new(3);
    private int ninki;
    private int kazematoi;
    private int tenChiJinUses;

    public NinjaCombat(double gcdSeconds = 2.5) : base(gcdSeconds) { }

    public int Ninki => ninki;
    public int Kazematoi => kazematoi;
    public IReadOnlyList<int> Mudras => mudras;

    protected override IReadOnlyList<uint> JobActions { get; } =
        [2240, 2242, 2255, 3563, 2254, 16488, 2247, 25774, 25777, 25778,
         2259, 2261, 2263, 2260, 2265, 2266, 2267, 2268, 2269, 2270, 2271, 16491, 16492, 2272,
         2241, 2245, 3566, 36957, 2258, 2262, 2264, 7401, 7402, 7403, 16489, 16493];
    protected override IReadOnlyList<ushort> JobStatusIds { get; } =
        [Mudra, Kassatsu, Hide, TenChiJin, ShadeShift, Meisui, RaijuReady, PhantomKamaitachiReady, ShadowWalker, Bunshin];
    protected override string JobDebugState
        => $"ninki={ninki} kazematoi={kazematoi} mudras={string.Join("", mudras)} tcj={tenChiJinUses}";

    public override uint Adjust(uint actionId) => actionId switch
    {
        2246 => 3566,   // trait 515: 斷絕 -> 夢幻三段
        2248 => 36957,  // trait 585: 奪取 -> 介毒之術
        2260 => Ninjutsu(mudras),
        // Ten Chi Jin turns each mudra button into the ninjutsu its running sequence spells.
        2259 => HasBuff(TenChiJin) ? Ninjutsu([.. mudras, Ten]) : 2259,
        2261 => HasBuff(TenChiJin) ? Ninjutsu([.. mudras, Chi]) : 2261,
        2263 => HasBuff(TenChiJin) ? Ninjutsu([.. mudras, Jin]) : 2263,
        _ => actionId,
    };

    // Trait 250 upgrades Katon and Hyoton while Kassatsu is up.
    private uint Ninjutsu(IReadOnlyList<int> sequence) => sequence.Count switch
    {
        1 => 2265,
        2 => (sequence[0], sequence[1]) switch
        {
            (Chi, Ten) or (Jin, Ten) => HasBuff(Kassatsu) ? 16491u : 2266,
            (Ten, Chi) or (Jin, Chi) => 2267,
            (Ten, Jin) or (Chi, Jin) => HasBuff(Kassatsu) ? 16492u : 2268,
            _ => 2272,
        },
        3 => (sequence[0], sequence[1], sequence[2]) switch
        {
            (Jin, Chi, Ten) or (Chi, Jin, Ten) => 2269,
            (Ten, Jin, Chi) or (Jin, Ten, Chi) => 2270,
            (Ten, Chi, Jin) or (Chi, Ten, Jin) => 2271,
            _ => 2272,
        },
        _ => 2272,
    };

    public override bool IsGapCloser(uint actionId) => actionId == 25777;
    protected override bool BlockedWhileBound(uint actionId) => actionId is 2262 or 25777;
    protected override bool AlsoUsesGcd(uint actionId) => actionId is 2259 or 2261 or 2263;
    // The mudra buttons lock the GCD for their own half second rather than a full GCD.
    protected override double AlsoUsesGcdSeconds(uint actionId) => 0.5;

    protected override uint ComboFrom(uint actionId) => actionId switch
    {
        2242 => 2240,
        2255 or 3563 => 2242,
        16488 => 2254,
        _ => 0,
    };

    protected override bool IsJobSelfAction(uint actionId)
        => actionId is 2241 or 2245 or 2254 or 16488 or 2259 or 2261 or 2263 or 2260 or 2270 or 2262 or 2264 or 7403 or 16489 or 16493 or 2272;

    protected override bool JobCanUse(uint actionId, bool inCombat)
    {
        // Ten Chi Jin refuses everything but its own mudras.
        if (HasBuff(TenChiJin) && actionId is not (2259 or 2261 or 2263) && !IsNinjutsu(actionId)) return false;
        return actionId switch
        {
            2245 => !inCombat,
            2258 => HasBuff(Hide) || HasBuff(ShadowWalker),
            16489 => inCombat && HasBuff(ShadowWalker),
            7403 => !HasBuff(Kassatsu),
            7401 or 7402 or 16493 => ninki >= 50,
            25774 => HasBuff(PhantomKamaitachiReady),
            25777 or 25778 => HasBuff(RaijuReady),
            2260 => HasBuff(Mudra),
            _ => true,
        };
    }

    protected override bool JobHighlighted(uint actionId) => actionId switch
    {
        25774 => HasBuff(PhantomKamaitachiReady),
        25777 or 25778 => HasBuff(RaijuReady),
        7401 or 7402 or 16493 => ninki >= 50,
        16489 => HasBuff(ShadowWalker),
        2258 => HasBuff(Hide) || HasBuff(ShadowWalker),
        _ => false,
    };

    private static bool IsNinjutsu(uint actionId)
        => actionId is 2265 or 2266 or 2267 or 2268 or 2269 or 2270 or 2271 or 16491 or 16492 or 2272;

    protected override JobHit? ApplyJob(uint actionId, bool hasTarget)
    {
        // Anything that is not a mudra or a ninjutsu breaks a half-finished sequence.
        if (!IsNinjutsu(actionId) && actionId is not (2259 or 2261 or 2263)) DropMudras();

        switch (actionId)
        {
            case 2259 or 2261 or 2263:
                mudras.Add(actionId == 2259 ? Ten : actionId == 2261 ? Chi : Jin);
                Buff(Mudra, 6);
                return null;

            case 2265 or 2266 or 2267 or 2268 or 2269 or 2270 or 2271 or 16491 or 16492 or 2272:
                return Ninjutsu(actionId, hasTarget);

            case 2240: GainNinki(5); SetCombo(2240); return Weaponskill(actionId, false, hasTarget);
            case 2242:
                if (Continue(ComboAction == 2240, 2242)) GainNinki(5);
                return Weaponskill(actionId, false, hasTarget);
            case 2255:
            {
                var combo = ComboAction == 2242;
                ClearCombo();
                if (combo) GainNinki(15);
                if (kazematoi > 0) kazematoi--;
                return Weaponskill(actionId, false, hasTarget);
            }
            case 3563:
            {
                var combo = ComboAction == 2242;
                ClearCombo();
                if (combo) { GainNinki(15); kazematoi = Math.Min(MaxKazematoi, kazematoi + 2); }
                return Weaponskill(actionId, false, hasTarget);
            }
            case 2254: GainNinki(5); SetCombo(2254); return Weaponskill(actionId, true, hasTarget);
            case 16488:
                if (Continue(ComboAction == 2254, 0)) GainNinki(5);
                return Weaponskill(actionId, true, hasTarget);
            case 2247: GainNinki(5); return Weaponskill(actionId, false, hasTarget);
            case 25774:
                ClearBuff(PhantomKamaitachiReady);
                GainNinki(10);
                return Weaponskill(actionId, true, hasTarget);
            case 25777 or 25778:
                ConsumeStack(RaijuReady);
                GainNinki(5);
                return Weaponskill(actionId, false, hasTarget);
            case 3566: return hasTarget ? new JobHit(actionId, false, false) : null;
            case 36957:
                GainNinki(40);
                return hasTarget ? new JobHit(actionId, true, false) : null;

            case 2241: Buff(ShadeShift, 20); return null;
            case 2245:
                Buff(Hide, 0);
                // Out of combat only, so both mudra charges are always the ones being restored.
                Timing.Reduce(4, 40);
                return null;
            case 2258:
                if (!ConsumeBuff(ShadowWalker)) ClearBuff(Hide);
                return hasTarget ? new JobHit(actionId, false, false) : null;
            case 2262: Move(JobMoveKind.GroundPoint); return null;
            case 2264: Buff(Kassatsu, 15); return null;
            case 7403:
                Buff(TenChiJin, 6);
                tenChiJinUses = 0;
                return null;
            case 16489:
                ClearBuff(ShadowWalker);
                GainNinki(50);
                Buff(Meisui, 30);
                return null;
            case 7401 or 7402:
                ninki -= 50;
                if (actionId == 7402) ClearBuff(Meisui);
                return hasTarget ? new JobHit(actionId, actionId == 7401, false) : null;
            case 16493:
                ninki -= 50;
                Buff(Bunshin, 30, 5);
                Buff(PhantomKamaitachiReady, 30);
                return null;
        }
        return null;
    }

    private JobHit? Ninjutsu(uint actionId, bool hasTarget)
    {
        var underTenChiJin = HasBuff(TenChiJin);
        if (underTenChiJin)
        {
            // Each mudra press under Ten Chi Jin spends no charge; three of them end the window.
            mudras.Add(LastTenChiJinMudra(actionId));
            if (++tenChiJinUses >= 3) { ClearBuff(TenChiJin); DropMudras(); }
        }
        else
        {
            // ponytail: Kassatsu's "ignore the mudra charges" half is not modelled — the buttons
            // still pay their charges; only the upgrade and the buff consumption are.
            ConsumeBuff(Kassatsu);
            DropMudras();
        }

        if (actionId == 2272) return null;  // Rabbit Medium: the sequence was not a ninjutsu.

        // Trait 441: Raiton grants up to three Raiju Ready.
        if (actionId == 2267) Buff(RaijuReady, 30, (ushort)Math.Min(3, BuffParam(RaijuReady) + 1));
        if (actionId is 2269 or 2271) Buff(ShadowWalker, 20);
        // Trait 166: Katon, Raiton and Hyoton reset Shukuchi.
        if (actionId is 2266 or 2267 or 2268 or 16491 or 16492) Timing.Reduce(16, 120);

        if (actionId == 2270) return new JobHit(actionId, true, false);
        if (!hasTarget) return null;
        return new JobHit(actionId, actionId is 2266 or 2269 or 16491, false);
    }

    // Under Ten Chi Jin the resolved ninjutsu tells us which mudra was pressed last.
    private int LastTenChiJinMudra(uint actionId) => mudras.Count switch
    {
        0 => Ten,
        1 => actionId is 2266 or 16491 ? Ten : actionId == 2267 ? Chi : Jin,
        _ => actionId == 2269 ? Ten : actionId == 2270 ? Chi : Jin,
    };

    // Bunshin's clone follows every weaponskill; solo, its hit is what feeds the 5 Ninki.
    private JobHit? Weaponskill(uint actionId, bool aoe, bool hasTarget)
    {
        if (ConsumeStack(Bunshin)) GainNinki(5);
        if (!hasTarget) return null;
        return new JobHit(actionId, aoe, actionId == 25777);
    }

    private bool Continue(bool success, uint next)
    {
        if (success && next != 0) SetCombo(next);
        else ClearCombo();
        return success;
    }

    private void DropMudras()
    {
        mudras.Clear();
        ClearBuff(Mudra);
    }

    private void GainNinki(int amount) => ninki = Math.Min(MaxNinki, ninki + amount);

    protected override void AdvanceJob(double seconds)
    {
        // The mudra window expiring abandons the sequence.
        if (mudras.Count > 0 && !HasBuff(Mudra) && !HasBuff(TenChiJin)) mudras.Clear();
    }

    protected override void ResetJob()
    {
        mudras.Clear();
        ninki = 0;
        kazematoi = 0;
        tenChiJinUses = 0;
    }

    protected override (int Group, double Recast, int Charges) JobTimingContract(uint actionId) => actionId switch
    {
        2259 or 2261 or 2263 => (4, 20, 2),
        2260 or 2265 or 2266 or 2267 or 2268 or 2269 or 2270 or 2271 or 16491 or 16492 or 2272 => (GlobalCooldownGroup, 1.5, 1),
        2241 => (21, 120, 1),
        2245 => (3, 20, 1),
        2258 => (9, 60, 1),
        2262 => (16, 60, 2),   // trait 279 (level 74): 2 charges
        2264 => (11, 60, 1),
        3566 => (12, 60, 1),
        36957 => (22, 120, 1),
        7401 or 7402 => (1, 1, 1),
        7403 => (20, 120, 1),
        16489 => (19, 120, 1),
        16493 => (15, 90, 1),
        _ => Gcd,
    };
}
