using System;
using System.Collections.Generic;

namespace AnoMech.Core.Combat;

// Level-90 rotation, Addersgall gauge, Eukrasia, MP, casts, and castable heals, shields and
// mitigation (status icons and cooldowns only). Addersting comes from broken shields, which are
// not simulated, so Toxikon stays unusable (same scope as Dark Arts). Rules from docs/client-data/SGE-90.md.
public sealed class SageCombat : HealerCombatBase
{
    private const double AddersgallSeconds = 20;
    private const ushort Eukrasia = 2606, Kardia = 2604, Soteria = 2610, Zoe = 2611, Physis = 2620, Kerachole = 2618,
        Taurochole = 2619, Holos = 3003, Krasis = 2622, EukrasianDiagnosis = 2607, EukrasianPrognosis = 2609;
    private int addersgall;
    private double addersgallTimer;

    public SageCombat(double gcdSeconds = 2.5) : base(gcdSeconds) { }

    public int Addersgall => addersgall;
    public double AddersgallTimer => addersgallTimer;
    public bool EukrasiaActive => HasBuff(Eukrasia);
    protected override IReadOnlyList<uint> JobActions { get; } =
        [24312, 24313, 24315, 24316, 24284, 24286, 24290, 24291, 24292, 24314, 37032, 24285, 24294, 24295, 24296, 24298,
         24299, 24300, 24301, 24302, 24303, 24305, 24309, 24310, 24311, 24317, 24318];
    protected override IReadOnlyList<ushort> JobStatusIds { get; } = [2606, 2604, 2610, 2611, 2620, 2618, 2619, 3003, 2622, 2607, 2609];

    protected override string JobDebugState => $"addersgall={addersgall} timer={addersgallTimer:0.0} eukrasia={HasBuff(Eukrasia)}";

    // Substitutions from the client's ReplaceAction sheet (技能變換設定).
    public override uint Adjust(uint actionId)
    {
        actionId = actionId switch
        {
            24283 or 24306 => 24312,
            24289 or 24307 => 24313,
            24297 => 24315,
            24304 => 24316,
            24288 => 24302,
            _ => actionId,
        };
        if (!HasBuff(Eukrasia)) return actionId;
        return actionId switch
        {
            24284 => 24291,
            24286 => 24292,
            24312 => 24314,
            24315 => 37032,
            _ => actionId,
        };
    }

    // Kardia, Druochole, Taurochole, Haima and Krasis are cast on the player; party targets are not simulated.
    protected override bool IsJobSelfAction(uint actionId)
        => actionId is not (24312 or 24313 or 24316 or 24314 or 24295 or 24318);

    public override bool IsGapCloser(uint actionId) => actionId == 24295;

    protected override bool AlsoUsesGcd(uint actionId) => actionId is 24313 or 24318;

    protected override bool JobHighlighted(uint actionId) => actionId is 24291 or 24292 or 24314 or 37032;

    protected override double JobCastTime(uint actionId) => actionId switch
    {
        24312 or 24284 or 24318 => 1.5,
        24286 => 2,
        _ => 0,
    };

    protected override int JobMpCost(uint actionId) => actionId switch
    {
        24312 or 24313 or 24315 or 24284 => 400,
        24286 or 24318 => 700,
        _ => 0,
    };

    protected override bool JobCanUse(uint actionId, bool inCombat) => actionId switch
    {
        24316 => false,
        24296 or 24298 or 24299 or 24303 => addersgall >= 1,
        24291 or 24292 or 24314 or 37032 => HasBuff(Eukrasia),
        24290 => !HasBuff(Eukrasia),
        _ => true,
    };

    protected override JobHit? ApplyJob(uint actionId, bool hasTarget)
    {
        switch (actionId)
        {
            case 24290: Buff(Eukrasia, double.PositiveInfinity); return null;
            case 24291:
                ClearBuff(Eukrasia);
                Buff(EukrasianDiagnosis, 30);
                return null;
            case 24292:
                ClearBuff(Eukrasia);
                Buff(EukrasianPrognosis, 30);
                return null;
            case 24314:
                ClearBuff(Eukrasia);
                return new JobHit(actionId, false, false);
            case 37032:
                ClearBuff(Eukrasia);
                return hasTarget ? new JobHit(actionId, true, false) : null;
            case 24285: Buff(Kardia, double.PositiveInfinity); return null;
            case 24294: Buff(Soteria, 15, 4); return null;
            case 24300: Buff(Zoe, 30); return null;
            case 24302: Buff(Physis, 15); return null;
            case 24310: Buff(Holos, 20); return null;
            case 24317: Buff(Krasis, 10); return null;
            case 24296 or 24299:
                SpendAddersgall();
                return null;
            case 24298:
                SpendAddersgall();
                ClearBuff(Taurochole);
                Buff(Kerachole, 15);
                return null;
            case 24303:
                SpendAddersgall();
                ClearBuff(Kerachole);
                Buff(Taurochole, 15);
                return null;
            case 24309: addersgall = Math.Min(3, addersgall + 1); return null;
            case 24295: return new JobHit(actionId, false, true);
            case 24312: return new JobHit(actionId, false, false);
            case 24313 or 24318: return new JobHit(actionId, true, false);
            case 24315: return hasTarget ? new JobHit(actionId, true, false) : null;
        }
        return null;
    }

    private void SpendAddersgall()
    {
        addersgall--;
        GainMp(700);
    }

    protected override void AdvanceHealer(double seconds)
    {
        for (addersgallTimer += seconds; addersgallTimer >= AddersgallSeconds; addersgallTimer -= AddersgallSeconds)
            addersgall = Math.Min(3, addersgall + 1);
    }

    protected override void ResetHealer()
    {
        addersgall = 0;
        addersgallTimer = 0;
        // Eukrasia is permanent in game but is not a stance: a fresh duty starts without it.
        ClearBuff(Eukrasia);
    }

    protected override (int Group, double Recast, int Charges) JobTimingContract(uint actionId) => actionId switch
    {
        // Eukrasian spells share the GCD group but recast in 1.5 s.
        24291 or 24292 or 24314 or 37032 => (GlobalCooldownGroup, GcdSeconds * 0.6, 1),
        24313 => (14, 40, 2),
        24285 => (2, 5, 1),
        24294 => (15, 90, 1),
        24295 => (7, 45, 1),
        24296 => (1, 1, 1),
        24298 => (4, 30, 1),
        24299 => (5, 30, 1),
        24300 => (20, 90, 1),
        24301 => (3, 30, 1),
        24302 => (12, 60, 1),
        24303 => (8, 45, 1),
        24305 => (21, 120, 1),
        24309 => (16, 90, 1),
        24310 => (19, 120, 1),
        24311 => (22, 120, 1),
        24317 => (13, 60, 1),
        24318 => (23, 120, 1),
        _ => Gcd,
    };
}
