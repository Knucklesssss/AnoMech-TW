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

    // An idle record is 0/0 like the client's own: Elapsed == Total made icons of actions that also start the GCD
    // flash every time the GCD rolled. The client splits a record by its own charge count (a level-100 character
    // keeps its level-100 charges when synced; Drill read as two 9.59 s charges), so the record spans that count.
    public static CombatRecastView Mirror(double recast, int charges, int clientCharges, int available, double nextRemaining)
        => available == charges ? new(false, 0, 0) : Project(recast, Math.Max(charges, clientCharges), available, nextRemaining);

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
