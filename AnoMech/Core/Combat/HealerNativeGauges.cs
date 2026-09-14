using System;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Gauge;

namespace AnoMech.Core.Combat;

internal sealed unsafe class WhiteMageNativeGauge : IJobGauge
{
    private readonly WhiteMageGauge* gauge;

    public WhiteMageNativeGauge() => gauge = (WhiteMageGauge*)HealerGauge.Current(24);
    public bool Matches => HealerGauge.Matches(24, gauge);

    public void Mirror(IJobCombat rules)
    {
        var whm = (WhiteMageCombat)rules;
        gauge->Lily = (byte)whm.Lilies;
        gauge->BloodLily = (byte)whm.BloodLilies;
        gauge->LilyTimer = (short)(whm.LilyTimer * 1000);
    }
}

internal sealed unsafe class ScholarNativeGauge : IJobGauge
{
    private readonly ScholarGauge* gauge;

    public ScholarNativeGauge() => gauge = (ScholarGauge*)HealerGauge.Current(28);
    public bool Matches => HealerGauge.Matches(28, gauge);

    public void Mirror(IJobCombat rules)
    {
        var sch = (ScholarCombat)rules;
        gauge->Aetherflow = (byte)sch.Aetherflow;
        gauge->FairyGauge = (byte)sch.FairyGauge;
        gauge->SeraphTimer = (short)Math.Ceiling(sch.SeraphRemaining * 1000);
    }
}

internal sealed unsafe class SageNativeGauge : IJobGauge
{
    private readonly SageGauge* gauge;

    public SageNativeGauge() => gauge = (SageGauge*)HealerGauge.Current(40);
    public bool Matches => HealerGauge.Matches(40, gauge);

    public void Mirror(IJobCombat rules)
    {
        var sge = (SageCombat)rules;
        gauge->Addersgall = (byte)sge.Addersgall;
        gauge->Addersting = 0;
        gauge->AddersgallTimer = (short)(sge.AddersgallTimer * 1000);
        gauge->Eukrasia = (byte)(sge.EukrasiaActive ? 1 : 0);
    }
}

// The 7.x card gauge layout is not verified in the installed client structs; card state stays in the rules.
internal sealed unsafe class AstrologianNativeGauge : IJobGauge
{
    private readonly void* gauge = HealerGauge.Current(33);
    public bool Matches => HealerGauge.Matches(33, gauge);
    public void Mirror(IJobCombat rules) { }
}

internal static unsafe class HealerGauge
{
    public static void* Current(byte classJob)
    {
        var job = JobGaugeManager.Instance();
        if (job == null || job->ClassJobId != classJob || job->CurrentGauge == null)
            throw new InvalidOperationException($"Native gauge for job {classJob} is unavailable.");
        return job->CurrentGauge;
    }

    public static bool Matches(byte classJob, void* gauge)
    {
        var job = JobGaugeManager.Instance();
        return job != null && job->ClassJobId == classJob && job->CurrentGauge == gauge;
    }
}
