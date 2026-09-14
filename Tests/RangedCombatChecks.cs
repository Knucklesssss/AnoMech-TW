using AnoMech.Core.Combat;

internal static class RangedCombatChecks
{
    public static void Run()
    {
        RoleActions();
        Console.WriteLine("PASS: ranged and caster role actions.");
    }

    private static void RoleActions()
    {
        var caster = new TestCaster();
        Check(caster.CastTime(1) == 2 && caster.TryUse(7561, false, false, true) == null && caster.CastTime(1) == 0,
            "Swiftcast must make a caster's next spell instant.");
        caster.Advance(0.6);
        Hit(caster, 1);
        Check(caster.CastTime(1) == 2 && caster.Mp == JobCombatBase.MaxMp - 400, "The instant spell must spend Swiftcast and its MP.");
        Check(!caster.CanUse(7560, false, false, true, checkTiming: false) && caster.CastTime(25880) == 2.5,
            "Addle needs an enemy; Sleep casts 2.5 s.");

        var ranged = new TestRanged();
        Check(ranged.CanUse(7557, false, false, false) && !ranged.CanUse(7557, false, false, true), "Peloton works only out of combat.");
        Check(ranged.TryUse(7548, false, false, true) == null && ranged.Statuses().Any(s => s.Id == 1209 && s.Remaining == 6),
            "Arm's Length must last 6 s.");
        Check(!ranged.CanUse(7551, false, false, true, checkTiming: false), "Head Graze needs an enemy.");
        var roll = new TestRanged { Roll = () => 0.2 };
        Check(roll.Proc(0.25) && !roll.Proc(0.2), "Chance must compare the roll against the probability.");
    }

    internal static void Hit(IJobCombat job, uint id, bool aoe = false)
    {
        var expected = job.Adjust(id);
        var hit = job.TryUse(id, true, true, true);
        if (hit is not { } h || h.ActionId != expected || h.IsAoe != aoe)
            throw new Exception($"Expected hit {expected} (from {id}) aoe={aoe}; got {hit?.ToString() ?? "null"}. {job.DebugState}");
    }

    internal static void Cast(IJobCombat job, uint id)
    {
        var seconds = job.CastTime(job.Adjust(id));
        Check(job.BeginCast(id, true, true, true), $"Cast {id} must begin. {job.DebugState}");
        job.Advance(seconds);
        Check(job.CompleteCast(true, true, true, out _), $"Cast {id} must complete. {job.DebugState}");
    }

    internal static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private sealed class TestCaster() : CasterCombatBase(2.5)
    {
        protected override IReadOnlyList<uint> JobActions { get; } = [1];
        protected override IReadOnlyList<ushort> JobStatusIds { get; } = [];
        protected override string JobDebugState => "";
        public override bool IsGapCloser(uint actionId) => false;
        protected override uint ComboFrom(uint actionId) => 0;
        protected override bool IsJobSelfAction(uint actionId) => false;
        protected override double JobCastTime(uint actionId) => actionId == 1 ? 2 : 0;
        protected override int JobMpCost(uint actionId) => actionId == 1 ? 400 : 0;
        protected override (int Group, double Recast, int Charges) JobTimingContract(uint actionId) => Gcd;
        protected override JobHit? ApplyJob(uint actionId, bool hasTarget, bool hardcast) => new JobHit(actionId, false, false);
        protected override void AdvanceCaster(double seconds) { }
        protected override void ResetCaster() { }
    }

    private sealed class TestRanged() : RangedCombatBase(2.5)
    {
        public bool Proc(double probability) => Chance(probability);
        protected override IReadOnlyList<uint> JobActions { get; } = [1];
        protected override IReadOnlyList<ushort> JobStatusIds { get; } = [];
        protected override string JobDebugState => "";
        public override bool IsGapCloser(uint actionId) => false;
        protected override uint ComboFrom(uint actionId) => 0;
        protected override bool IsJobSelfAction(uint actionId) => false;
        protected override (int Group, double Recast, int Charges) JobTimingContract(uint actionId) => Gcd;
        protected override JobHit? ApplyJob(uint actionId, bool hasTarget) => new JobHit(actionId, false, false);
        protected override void AdvanceJob(double seconds) { }
        protected override void ResetJob() { }
    }
}
