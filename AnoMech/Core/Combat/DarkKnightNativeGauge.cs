using System;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Gauge;

namespace AnoMech.Core.Combat;

internal sealed unsafe class DarkKnightNativeGauge : IJobGauge
{
    private readonly DarkKnightGauge* gauge;
    private readonly byte blood;
    private readonly ushort darkside;
    private readonly ushort shadow;

    public DarkKnightNativeGauge()
    {
        var job = JobGaugeManager.Instance();
        if (job == null || job->ClassJobId != 32 || job->CurrentGauge == null)
            throw new InvalidOperationException("Dark Knight native gauge is unavailable.");
        gauge = (DarkKnightGauge*)job->CurrentGauge;
        blood = gauge->Blood;
        darkside = gauge->DarksideTimer;
        shadow = gauge->ShadowTimer;
    }

    public bool Matches
    {
        get
        {
            var job = JobGaugeManager.Instance();
            return job != null && job->ClassJobId == 32 && job->CurrentGauge == (void*)gauge;
        }
    }

    public void Mirror(IJobCombat rules)
    {
        var drk = (DarkKnightCombat)rules;
        gauge->Blood = (byte)drk.Blood;
        gauge->DarksideTimer = (ushort)Math.Ceiling(drk.DarksideRemaining * 1000);
        gauge->ShadowTimer = (ushort)Math.Ceiling(drk.ShadowRemaining * 1000);
    }

    public void Restore()
    {
        gauge->Blood = blood;
        gauge->DarksideTimer = darkside;
        gauge->ShadowTimer = shadow;
    }
}
