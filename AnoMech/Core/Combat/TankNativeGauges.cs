using System;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Gauge;

namespace AnoMech.Core.Combat;

internal sealed unsafe class PaladinNativeGauge : IJobGauge
{
    private readonly PaladinGauge* gauge;

    public PaladinNativeGauge()
    {
        var job = JobGaugeManager.Instance();
        if (job == null || job->ClassJobId != 19 || job->CurrentGauge == null)
            throw new InvalidOperationException("Paladin native gauge is unavailable.");
        gauge = (PaladinGauge*)job->CurrentGauge;
    }

    public bool Matches
    {
        get
        {
            var job = JobGaugeManager.Instance();
            return job != null && job->ClassJobId == 19 && job->CurrentGauge == (void*)gauge;
        }
    }

    public void Mirror(IJobCombat rules) => gauge->OathGauge = (byte)((PaladinCombat)rules).Oath;
}

internal sealed unsafe class GunbreakerNativeGauge : IJobGauge
{
    private readonly GunbreakerGauge* gauge;

    public GunbreakerNativeGauge()
    {
        var job = JobGaugeManager.Instance();
        if (job == null || job->ClassJobId != 37 || job->CurrentGauge == null)
            throw new InvalidOperationException("Gunbreaker native gauge is unavailable.");
        gauge = (GunbreakerGauge*)job->CurrentGauge;
    }

    public bool Matches
    {
        get
        {
            var job = JobGaugeManager.Instance();
            return job != null && job->ClassJobId == 37 && job->CurrentGauge == (void*)gauge;
        }
    }

    public void Mirror(IJobCombat rules)
    {
        var gnb = (GunbreakerCombat)rules;
        gauge->Ammo = (byte)gnb.Cartridges;
        gauge->AmmoComboStep = (byte)gnb.GnashStep;
    }
}
