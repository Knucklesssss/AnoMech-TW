using System;

namespace AnoMech.Core.Combat;

public readonly record struct BufferedCombatAction(uint ActionId, ulong TargetId);

public sealed class CombatInputBuffer
{
    private BufferedCombatAction? pending;
    private double age;

    public BufferedCombatAction? Pending => pending;

    public bool Queue(uint actionId, ulong targetId, double waitSeconds)
    {
        ValidateTime(waitSeconds);
        if (waitSeconds == 0 || waitSeconds > .5) return false;
        pending = new BufferedCombatAction(actionId, targetId);
        age = 0;
        return true;
    }

    public void Advance(double seconds)
    {
        ValidateTime(seconds);
        if (pending == null) return;
        age += seconds;
        if (age > .5) Reset();
    }

    public BufferedCombatAction? Take(bool canExecute)
    {
        if (!canExecute) return null;
        var result = pending;
        Reset();
        return result;
    }

    public void Reset()
    {
        pending = null;
        age = 0;
    }

    private static void ValidateTime(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0)
            throw new ArgumentOutOfRangeException(nameof(seconds));
    }
}
