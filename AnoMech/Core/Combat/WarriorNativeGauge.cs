using System;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Gauge;

namespace AnoMech.Core.Combat;

internal sealed unsafe class WarriorNativeGauge : IJobGauge
{
    private readonly WarriorGauge* gauge;
    private readonly byte beast;

    public WarriorNativeGauge()
    {
        var job = JobGaugeManager.Instance();
        if (job == null || job->ClassJobId != 21 || job->CurrentGauge == null)
            throw new InvalidOperationException("Warrior native gauge is unavailable.");
        gauge = (WarriorGauge*)job->CurrentGauge;
        beast = gauge->BeastGauge;
    }

    public bool Matches
    {
        get
        {
            var job = JobGaugeManager.Instance();
            return job != null && job->ClassJobId == 21 && job->CurrentGauge == (void*)gauge;
        }
    }

    public void Mirror(IJobCombat rules) => gauge->BeastGauge = (byte)((WarriorCombat)rules).Beast;

    public void Restore() => gauge->BeastGauge = beast;
}
