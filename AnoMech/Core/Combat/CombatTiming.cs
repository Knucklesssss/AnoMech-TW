using System;
using System.Collections.Generic;

namespace AnoMech.Core.Combat;

public sealed class CombatTiming
{
    private readonly Dictionary<int, ChargeGroup> groups = [];
    private double now;
    private double lockedUntil;

    public double LockRemaining => Math.Max(0, lockedUntil - now);

    public void Advance(double seconds)
    {
        Validate(seconds, nameof(seconds));
        if (!double.IsFinite(now + seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
        now += seconds;
        foreach (var group in groups.Values) Recover(group);
    }

    public bool TryUse(int group, double recast, int maxCharges, double animationLock)
    {
        Validate(group, nameof(group));
        Validate(recast, nameof(recast));
        Validate(maxCharges, nameof(maxCharges));
        Validate(animationLock, nameof(animationLock));
        ValidateContract(group, recast, maxCharges);
        if (now < lockedUntil) return false;
        var state = GetGroup(group, recast, maxCharges);
        Recover(state);
        if (state.Charges == 0) return false;
        // A single-charge group takes each use's recast (Sage Eukrasian spells run a 1.5 s GCD).
        if (state.MaxCharges == 1) state.Recast = recast;

        if (state.Charges == state.MaxCharges) state.NextChargeAt = now + state.Recast;
        state.Charges--;
        lockedUntil = now + animationLock;
        return true;
    }

    public int Charges(int group, double recast, int maxCharges)
    {
        Validate(group, nameof(group));
        Validate(recast, nameof(recast));
        Validate(maxCharges, nameof(maxCharges));
        var state = GetGroup(group, recast, maxCharges);
        Recover(state);
        return state.Charges;
    }

    public double Remaining(int group)
    {
        Validate(group, nameof(group));
        if (!groups.TryGetValue(group, out var state) || state.Charges == state.MaxCharges) return 0;
        // (now + recast) - now can exceed recast by one ulp.
        return Math.Clamp(state.NextChargeAt - now, 0, state.Recast);
    }

    public bool IsAvailable(int group, double recast, int maxCharges)
    {
        Validate(group, nameof(group));
        Validate(recast, nameof(recast));
        Validate(maxCharges, nameof(maxCharges));
        ValidateContract(group, recast, maxCharges);
        return now >= lockedUntil && (!groups.TryGetValue(group, out var state) ? maxCharges > 0 : state.Charges > 0);
    }

    public void Reduce(int group, double seconds)
    {
        Validate(group, nameof(group));
        Validate(seconds, nameof(seconds));
        if (!groups.TryGetValue(group, out var state) || state.Charges == state.MaxCharges) return;
        state.NextChargeAt -= seconds;
        Recover(state);
    }

    public void ClearLock() => lockedUntil = Math.Min(lockedUntil, now);

    public void Reset()
    {
        groups.Clear();
        now = 0;
        lockedUntil = 0;
    }

    private ChargeGroup GetGroup(int group, double recast, int maxCharges)
    {
        if (!groups.TryGetValue(group, out var state))
            groups[group] = state = new ChargeGroup(recast, maxCharges);
        else
            ValidateContract(group, recast, maxCharges);
        return state;
    }

    private void ValidateContract(int group, double recast, int maxCharges)
    {
        if (groups.TryGetValue(group, out var state) && (state.MaxCharges != maxCharges || (maxCharges > 1 && state.Recast != recast)))
            throw new InvalidOperationException($"Charge group {group} already uses recast {state.Recast} and {state.MaxCharges} max charges.");
    }

    private void Recover(ChargeGroup state)
    {
        if (state.Recast == 0)
        {
            state.Charges = state.MaxCharges;
            state.NextChargeAt = 0;
            return;
        }
        while (state.Charges < state.MaxCharges && now >= state.NextChargeAt)
        {
            state.Charges++;
            state.NextChargeAt += state.Recast;
        }
    }

    private static void Validate(int value, string name)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(name);
    }

    private static void Validate(double value, string name)
    {
        if (!double.IsFinite(value) || value < 0) throw new ArgumentOutOfRangeException(name);
    }

    private sealed class ChargeGroup(double recast, int maxCharges)
    {
        public double Recast { get; set; } = recast;
        public int MaxCharges { get; } = maxCharges;
        public int Charges { get; set; } = maxCharges;
        public double NextChargeAt { get; set; }
    }
}
