using System;
using System.Collections.Generic;
using System.Linq;

namespace AnoMech.Core.Combat;

// Shared per-run combat state: timing, combo, MP and highlight. A job only
// supplies its action table and the rules the client data cannot express.
// Every simulation starts from Reset() — a fresh instance, like entering a duty.
public abstract class JobCombatBase : IJobCombat
{
    public const int MaxMp = 10000;
    protected const int GlobalCooldownGroup = 58;
    private const double AnimationLock = 0.6;
    // ponytail: server regen is not in client data; 200 MP per 3 s is the
    // commonly cited value, pending the user's in-game calibration.
    private const double MpTickSeconds = 3;
    private const int MpPerTick = 200;
    private uint comboAction;
    private double comboRemaining;
    private int mp = MaxMp;
    private double mpTick;

    protected JobCombatBase(double gcdSeconds)
    {
        if (!double.IsFinite(gcdSeconds) || gcdSeconds < 0) throw new ArgumentOutOfRangeException(nameof(gcdSeconds));
        GcdSeconds = gcdSeconds;
    }

    protected double GcdSeconds { get; }
    public CombatTiming Timing { get; } = new();
    public uint ComboAction => comboAction;
    public double ComboRemaining => comboRemaining;
    public int Mp => mp;
    public abstract IReadOnlyList<uint> Actions { get; }
    public abstract IReadOnlyList<ushort> StatusIds { get; }
    public virtual uint Adjust(uint actionId) => actionId;
    public bool Supports(uint actionId) => Actions.Contains(Adjust(actionId));
    public abstract IEnumerable<JobStatus> Statuses();
    public abstract bool IsSelfAction(uint actionId);
    public abstract bool IsGapCloser(uint actionId);

    // Combo predecessor from the Action sheet's ActionCombo column; 0 = none.
    protected abstract uint ComboFrom(uint actionId);
    protected virtual int MpCost(uint actionId) => 0;
    protected abstract (int Group, double Recast, int Charges) TimingContract(uint actionId);
    protected virtual bool JobCanUse(uint actionId, bool inCombat) => true;
    // Runs after timing and MP were paid. Null = successful buff or empty AoE.
    protected abstract JobHit? Apply(uint actionId, bool hasTarget);
    protected abstract void AdvanceJob(double seconds);
    protected abstract void ResetJob();

    public (int Group, double Recast, int Charges) GetCooldown(uint actionId)
    {
        actionId = Adjust(actionId);
        if (!Actions.Contains(actionId)) throw new ArgumentOutOfRangeException(nameof(actionId));
        return TimingContract(actionId);
    }

    public (int Group, double Recast, int Charges) GetBindingCooldown(uint actionId)
    {
        if (!Actions.Contains(actionId)) throw new ArgumentOutOfRangeException(nameof(actionId));
        return TimingContract(actionId);
    }

    public virtual void Seed(Func<ushort, bool> hasStatus) { }

    public string DebugState
        => $"combo={comboAction}/{comboRemaining:0.0}s mp={mp} lock={Timing.LockRemaining:0.00} gcd={Timing.Remaining(GlobalCooldownGroup):0.00} {JobDebugState}";
    protected abstract string JobDebugState { get; }

    public bool IsHighlighted(uint actionId)
    {
        var from = ComboFrom(Adjust(actionId));
        return from != 0 && from == comboAction;
    }

    public bool CanUse(uint actionId, bool hasTarget, bool inRange, bool inCombat, bool alive = true, bool bound = false, bool checkTiming = true)
    {
        actionId = Adjust(actionId);
        if (!Actions.Contains(actionId) || !alive) return false;
        if (IsGapCloser(actionId) && bound) return false;
        if (!IsSelfAction(actionId) && (!hasTarget || !inRange)) return false;
        if (mp < MpCost(actionId) || !JobCanUse(actionId, inCombat)) return false;
        var (group, recast, charges) = TimingContract(actionId);
        return !checkTiming || Timing.IsAvailable(group, recast, charges);
    }

    public JobHit? TryUse(uint actionId, bool hasTarget, bool inRange, bool inCombat, bool alive = true, bool bound = false)
    {
        if (!CanUse(actionId, hasTarget, inRange, inCombat, alive, bound)) return null;
        actionId = Adjust(actionId);
        var (group, recast, charges) = TimingContract(actionId);
        if (!Timing.TryUse(group, recast, charges, AnimationLock)) return null;
        mp -= MpCost(actionId);
        return Apply(actionId, hasTarget);
    }

    public void Advance(double seconds)
    {
        Timing.Advance(seconds);
        comboRemaining = Decrease(comboRemaining, seconds);
        if (comboRemaining == 0) comboAction = 0;
        for (mpTick += seconds; mpTick >= MpTickSeconds; mpTick -= MpTickSeconds) GainMp(MpPerTick);
        AdvanceJob(seconds);
    }

    public void Reset()
    {
        Timing.Reset();
        ClearCombo();
        mp = MaxMp;
        mpTick = 0;
        ResetJob();
    }

    protected void SetCombo(uint actionId)
    {
        comboAction = actionId;
        comboRemaining = 30;
    }

    protected void ClearCombo()
    {
        comboAction = 0;
        comboRemaining = 0;
    }

    protected void GainMp(int amount) => mp = Math.Min(MaxMp, mp + amount);

    protected static double Decrease(double remaining, double seconds)
        => seconds + 1e-9 >= remaining ? 0 : remaining - seconds;
}
