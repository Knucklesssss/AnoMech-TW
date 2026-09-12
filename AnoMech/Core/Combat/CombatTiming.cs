using System;
using System.Collections.Generic;

namespace AnoMech.Core.Combat;

public sealed class CombatTiming
{
    private readonly Dictionary<int, ChargeGroup> groups = [];
    private double now;
    private double lockedUntil;

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
        return state.NextChargeAt - now;
    }

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
        if (groups.TryGetValue(group, out var state) && (state.Recast != recast || state.MaxCharges != maxCharges))
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
        public double Recast { get; } = recast;
        public int MaxCharges { get; } = maxCharges;
        public int Charges { get; set; } = maxCharges;
        public double NextChargeAt { get; set; }
    }
}
