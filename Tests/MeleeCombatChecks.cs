using AnoMech.Core.Combat;

internal static class MeleeCombatChecks
{
    public static void Run()
    {
        RoleActions();
        Console.WriteLine("PASS: melee role actions.");
    }

    private static void RoleActions()
    {
        var melee = new TestMelee();
        Check(melee.Actions.SequenceEqual(new uint[] { 1, 7541, 7542, 7546, 7548, 7549, 7863 }), "Melee role actions must follow the job actions.");
        Check(melee.GetCooldown(7541) == (50, 120, 1) && melee.GetCooldown(7542) == (47, 90, 1) && melee.GetCooldown(7546) == (46, 45, 2)
              && melee.GetCooldown(7548) == (49, 120, 1) && melee.GetCooldown(7549) == (48, 90, 1) && melee.GetCooldown(7863) == (44, 40, 1),
            "Melee role action contracts must match the client sheet.");
        Check(melee.IsSelfAction(7541) && melee.IsSelfAction(7542) && melee.IsSelfAction(7546) && melee.IsSelfAction(7548)
              && !melee.IsSelfAction(7549) && !melee.IsSelfAction(7863), "Feint and Leg Sweep need an enemy; the rest are self actions.");
        Check(melee.TryUse(7542, false, false, true) == null && melee.Statuses().Any(s => s.Id == 84 && s.Remaining == 20), "Bloodbath must last 20 s.");
        melee.Advance(0.6);
        Check(melee.TryUse(7546, false, false, true) == null && melee.Statuses().Any(s => s.Id == 1250 && s.Remaining == 10), "True North must last 10 s.");
        melee.Advance(0.6);
        Check(melee.CanUse(7546, false, false, true), "True North must keep a second charge.");
        Check(melee.TryUse(7548, false, false, true) == null && melee.Statuses().Any(s => s.Id == 1209 && s.Remaining == 6), "Arm's Length must last 6 s.");
        Check(!melee.CanUse(7549, false, false, true, checkTiming: false) && !melee.CanUse(7863, false, false, true, checkTiming: false),
            "Feint and Leg Sweep must need an enemy.");
        Check(melee.StatusIds.SequenceEqual(new ushort[] { 84, 1250, 1209 }), "Melee role statuses must be Bloodbath, True North and Arm's Length.");
    }

    internal static void Hit(IJobCombat job, uint id, bool aoe = false)
    {
        var expected = job.Adjust(id);
        var hit = job.TryUse(id, true, true, true);
        if (hit is not { } h || h.ActionId != expected || h.IsAoe != aoe)
            throw new Exception($"Expected hit {expected} (from {id}) aoe={aoe}; got {hit?.ToString() ?? "null"}. {job.DebugState}");
    }

    internal static void Use(IJobCombat job, uint id)
    {
        Check(job.CanUse(id, true, true, true), $"{id} must be usable. {job.DebugState}");
        Check(job.TryUse(id, true, true, true) == null, $"{id} must resolve without a hit. {job.DebugState}");
    }

    internal static void Gcd(IJobCombat job) => job.Advance(2.5);

    internal static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private sealed class TestMelee() : MeleeCombatBase(2.5)
    {
        protected override IReadOnlyList<uint> JobActions { get; } = [1];
        protected override IReadOnlyList<ushort> JobStatusIds { get; } = [];
        protected override string JobDebugState => "";
        protected override uint ComboFrom(uint actionId) => 0;
        protected override bool IsJobSelfAction(uint actionId) => false;
        protected override (int Group, double Recast, int Charges) JobTimingContract(uint actionId) => Gcd;
        protected override JobHit? ApplyJob(uint actionId, bool hasTarget) => new JobHit(actionId, false, false);
        protected override void AdvanceJob(double seconds) { }
        protected override void ResetJob() { }
        public override bool IsGapCloser(uint actionId) => false;
    }
}
