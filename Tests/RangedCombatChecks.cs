using AnoMech.Core.Combat;

internal static class RangedCombatChecks
{
    public static void Run()
    {
        RoleActions();
        Bard();
        Console.WriteLine("PASS: ranged and caster role actions.");
    }

    private static void Bard()
    {
        Check(JobCombatRegistry.Find(23, 90) != null, "Bard level 90 must be registered.");
        var brd = new BardCombat { Roll = () => 0 };
        Check(brd.Adjust(97) == 16495 && brd.Adjust(36975) == 110, "Heavy Shot and Heartbreak Shot must map to level-90 actions.");
        Check(!brd.CanUse(7409, true, true, true, checkTiming: false), "Refulgent Arrow needs Hawk's Eye.");
        Hit(brd, 16495);
        Check(brd.CanUse(7409, true, true, true, checkTiming: false) && brd.IsHighlighted(7409), "A proc must enable and light Refulgent Arrow.");
        brd.Advance(2.5);
        Hit(brd, 7409);
        Check(!brd.CanUse(7409, true, true, true, checkTiming: false), "Refulgent Arrow must spend Hawk's Eye.");
        Check(!brd.CanUse(3559, false, false, false), "Songs need combat.");
        brd.Advance(0.6);
        brd.TryUse(3559, false, false, true);
        brd.Advance(3);
        Check(brd.CurrentSong == BardCombat.Song.Minuet && brd.Repertoire == 1 && brd.SoulVoice == 5 && brd.Adjust(3559) == 7404,
            "Minuet must build Repertoire and Soul Voice every 3 s and turn into Pitch Perfect.");
        Hit(brd, 3559, aoe: true);
        Check(brd.Repertoire == 0, "Pitch Perfect must spend Repertoire.");
        brd.Advance(0.6);
        brd.TryUse(114, false, false, true);
        Check(brd.CurrentSong == BardCombat.Song.Ballad && brd.Codas == 5, "Changing songs must end the old one and keep both codas.");
        brd.Advance(0.6);
        Hit(brd, 110);
        brd.Advance(3);
        Check(Math.Abs(brd.Timing.Remaining(10) - 4.5) < 1e-6, "Mage's Ballad Repertoire must cut Bloodletter's recast by 7.5 s.");
        brd.Advance(9);
        Hit(brd, 16496, aoe: true);
        Check(brd.SoulVoice == 0 && brd.Adjust(16496) == 16496, "Apex Arrow must spend Soul Voice; under 80 gives no Blast Arrow.");

        var barrage = new BardCombat { Roll = () => 0 };
        barrage.TryUse(107, false, false, true);
        barrage.Advance(0.6);
        Hit(barrage, 16495);
        barrage.Advance(2.5);
        Hit(barrage, 7409);
        Check(barrage.CanUse(7409, true, true, true, checkTiming: false), "Refulgent Arrow must consume Barrage before Hawk's Eye, leaving Hawk's Eye up.");

        var expiry = new BardCombat { Roll = () => 0 };
        expiry.TryUse(114, false, false, true);
        expiry.Advance(45);
        Check(expiry.CurrentSong == BardCombat.Song.None && expiry.Repertoire == 0, "A song must end and clear Repertoire after 45 s.");

        var paeon = new BardCombat { Roll = () => 0 };
        paeon.TryUse(116, false, false, true);
        paeon.Advance(15);
        Check(paeon.Repertoire == 4, "Army's Paeon Repertoire must cap at 4.");

        var finale = new BardCombat { Roll = () => 0 };
        Check(!finale.CanUse(25785, true, true, true, checkTiming: false), "Radiant Finale needs at least one coda.");
        finale.TryUse(114, false, false, true);
        finale.Advance(0.6);
        Check(finale.CanUse(25785, true, true, true, checkTiming: false), "One coda must enable Radiant Finale.");
        finale.TryUse(25785, false, false, true);
        Check(finale.Codas == 0, "Radiant Finale must spend every coda.");
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
