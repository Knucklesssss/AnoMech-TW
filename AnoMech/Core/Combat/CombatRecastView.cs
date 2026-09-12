using System;

namespace AnoMech.Core.Combat;

public readonly record struct CombatRecastView(bool IsActive, double Elapsed, double Total)
{
    public static CombatRecastView Project(double recast, int maxCharges, int available, double nextRemaining)
    {
        Validate(recast);
        Validate(nextRemaining);
        if (maxCharges <= 0 || available < 0 || available > maxCharges || nextRemaining > recast)
            throw new ArgumentOutOfRangeException(nameof(available));
        var total = recast * maxCharges;
        Validate(total);
        return new(available < maxCharges, available == maxCharges ? total : available * recast + recast - nextRemaining, total);
    }

    public static CombatRecastView Restore(bool active, double elapsed, double total, double sessionSeconds)
    {
        Validate(elapsed);
        Validate(total);
        Validate(sessionSeconds);
        if (elapsed > total) throw new ArgumentOutOfRangeException(nameof(elapsed));
        var progressed = Math.Min(total, elapsed + sessionSeconds);
        return new(active && progressed < total, active ? progressed : elapsed, total);
    }

    private static void Validate(double value)
    {
        if (!double.IsFinite(value) || value < 0 || value > float.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(value));
    }
}
