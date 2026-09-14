using System;
using System.Collections.Generic;
using System.Linq;

namespace AnoMech.Core.Combat;

// Shared per-run combat state: timing, combo, MP, casts, statuses and highlight.
// A job only supplies its action table and the rules the client data cannot express.
// Every simulation starts from Reset() — a fresh instance, like entering a duty.
public abstract class JobCombatBase : IJobCombat
{
    public const int MaxMp = 10000;
    protected const int GlobalCooldownGroup = 58;
    private const double AnimationLock = 0.6;
    private const double CastReleaseLock = 0.1;
    // ponytail: server regen is not in client data; 200 MP per 3 s is the
    // commonly cited value, pending the user's in-game calibration.
    private const double MpTickSeconds = 3;
    private const int MpPerTick = 200;
    private readonly Dictionary<ushort, (double Remaining, ushort Param)> buffs = [];
    private uint comboAction;
    private double comboRemaining;
    private int mp = MaxMp;
    private double mpTick;
    private uint castingAction;

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
    public uint CastingAction => castingAction;
    public abstract IReadOnlyList<uint> Actions { get; }
    public abstract IReadOnlyList<ushort> StatusIds { get; }
    public virtual uint Adjust(uint actionId) => actionId;
    public bool Supports(uint actionId) => Actions.Contains(Adjust(actionId));
    public abstract bool IsSelfAction(uint actionId);
    public abstract bool IsGapCloser(uint actionId);
    public virtual void Seed(Func<ushort, bool> hasStatus) { }
    public virtual double CastTime(uint actionId) => 0;
    public virtual void AutoAttackHit() { }

    // Combo predecessor from the Action sheet's ActionCombo column; 0 = none.
    protected abstract uint ComboFrom(uint actionId);
    protected virtual int MpCost(uint actionId) => 0;
    protected abstract (int Group, double Recast, int Charges) TimingContract(uint actionId);
    // Weaponskills with their own recast that also start the GCD (sheet AdditionalCooldownGroup 58).
    protected virtual bool AlsoUsesGcd(uint actionId) => false;
    protected virtual bool JobCanUse(uint actionId, bool inCombat) => true;
    protected virtual bool JobHighlighted(uint actionId) => false;
    // Runs after recast and MP were paid. Null = successful buff or empty AoE.
    protected abstract JobHit? Apply(uint actionId, bool hasTarget);
    protected abstract void AdvanceJob(double seconds);
    protected abstract void ResetJob();
    protected abstract string JobDebugState { get; }

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

    public string DebugState
        => $"combo={comboAction}/{comboRemaining:0.0}s mp={mp} cast={castingAction} lock={Timing.LockRemaining:0.00} gcd={Timing.Remaining(GlobalCooldownGroup):0.00} "
            + $"buffs=[{string.Join(",", buffs.Select(b => $"{b.Key}:{b.Value.Remaining:0.0}/{b.Value.Param}"))}] {JobDebugState}";

    public IEnumerable<JobStatus> Statuses()
        => StatusIds.Select(id => buffs.TryGetValue(id, out var b) ? new JobStatus(id, b.Remaining, b.Param) : new JobStatus(id, 0, 0));

    public bool IsHighlighted(uint actionId)
    {
        actionId = Adjust(actionId);
        var from = ComboFrom(actionId);
        return (from != 0 && from == comboAction) || JobHighlighted(actionId);
    }

    public bool CanUse(uint actionId, bool hasTarget, bool inRange, bool inCombat, bool alive = true, bool bound = false, bool checkTiming = true)
    {
        actionId = Adjust(actionId);
        if (!Actions.Contains(actionId) || !alive) return false;
        if (IsGapCloser(actionId) && bound) return false;
        if (!IsSelfAction(actionId) && (!hasTarget || !inRange)) return false;
        if (mp < MpCost(actionId) || !JobCanUse(actionId, inCombat)) return false;
        if (!checkTiming) return true;
        if (castingAction != 0) return false;
        var (group, recast, charges) = TimingContract(actionId);
        return Timing.IsAvailable(group, recast, charges)
            && (!AlsoUsesGcd(actionId) || Timing.IsAvailable(GlobalCooldownGroup, GcdSeconds, 1));
    }

    public JobHit? TryUse(uint actionId, bool hasTarget, bool inRange, bool inCombat, bool alive = true, bool bound = false)
    {
        if (!CanUse(actionId, hasTarget, inRange, inCombat, alive, bound)) return null;
        actionId = Adjust(actionId);
        if (CastTime(actionId) > 0 || !PayRecast(actionId, AnimationLock)) return null;
        mp -= MpCost(actionId);
        return Apply(actionId, hasTarget);
    }

    public bool BeginCast(uint actionId, bool hasTarget, bool inRange, bool inCombat, bool alive = true, bool bound = false)
    {
        if (!CanUse(actionId, hasTarget, inRange, inCombat, alive, bound)) return false;
        actionId = Adjust(actionId);
        var seconds = CastTime(actionId);
        if (seconds <= 0 || !PayRecast(actionId, seconds + CastReleaseLock)) return false;
        castingAction = actionId;
        return true;
    }

    public bool CompleteCast(bool hasTarget, bool inRange, bool alive, out JobHit? hit)
    {
        hit = null;
        var actionId = castingAction;
        castingAction = 0;
        if (actionId == 0 || !alive || (!IsSelfAction(actionId) && (!hasTarget || !inRange)) || mp < MpCost(actionId)) return false;
        mp -= MpCost(actionId);
        hit = Apply(actionId, hasTarget);
        return true;
    }

    // The GCD already started stays spent, as in game; only the cast lock is released.
    public void CancelCast()
    {
        if (castingAction == 0) return;
        castingAction = 0;
        Timing.ClearLock();
    }

    public void Advance(double seconds)
    {
        Timing.Advance(seconds);
        comboRemaining = Decrease(comboRemaining, seconds);
        if (comboRemaining == 0) comboAction = 0;
        for (mpTick += seconds; mpTick >= MpTickSeconds; mpTick -= MpTickSeconds) GainMp(MpPerTick);
        foreach (var id in buffs.Keys.ToList())
        {
            var (remaining, param) = buffs[id];
            remaining = Decrease(remaining, seconds);
            if (remaining == 0) buffs.Remove(id);
            else buffs[id] = (remaining, param);
        }
        AdvanceJob(seconds);
    }

    public void Reset()
    {
        Timing.Reset();
        ClearCombo();
        mp = MaxMp;
        mpTick = 0;
        castingAction = 0;
        // Permanent statuses are stances: they survive entering and leaving a duty.
        foreach (var id in buffs.Where(b => !double.IsPositiveInfinity(b.Value.Remaining)).Select(b => b.Key).ToList())
            buffs.Remove(id);
        ResetJob();
    }

    private bool PayRecast(uint actionId, double lockSeconds)
    {
        var (group, recast, charges) = TimingContract(actionId);
        if (AlsoUsesGcd(actionId) && !Timing.TryUse(GlobalCooldownGroup, GcdSeconds, 1, 0)) return false;
        return Timing.TryUse(group, recast, charges, lockSeconds);
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

    protected void Buff(ushort statusId, double seconds, ushort param = 0) => buffs[statusId] = (seconds, param);

    protected void ExtendBuff(ushort statusId, double seconds, double cap)
        => Buff(statusId, Math.Min(cap, BuffRemaining(statusId) + seconds), BuffParam(statusId));

    protected void ClearBuff(ushort statusId) => buffs.Remove(statusId);

    // Consumes a ready status; false when it was absent.
    protected bool ConsumeBuff(ushort statusId) => buffs.Remove(statusId);

    protected bool HasBuff(ushort statusId) => buffs.ContainsKey(statusId);

    protected double BuffRemaining(ushort statusId) => buffs.TryGetValue(statusId, out var b) ? b.Remaining : 0;

    protected ushort BuffParam(ushort statusId) => buffs.TryGetValue(statusId, out var b) ? b.Param : (ushort)0;

    // Uses one stack; the status disappears with its last stack.
    protected bool ConsumeStack(ushort statusId)
    {
        if (!buffs.TryGetValue(statusId, out var b) || b.Param == 0) return false;
        if (b.Param == 1) buffs.Remove(statusId);
        else buffs[statusId] = (b.Remaining, (ushort)(b.Param - 1));
        return true;
    }

    protected static double Decrease(double remaining, double seconds)
        => seconds + 1e-9 >= remaining ? 0 : remaining - seconds;
}
