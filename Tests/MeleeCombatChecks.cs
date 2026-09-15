using AnoMech.Core.Combat;

internal static class MeleeCombatChecks
{
    public static void Run()
    {
        RoleActions();
        Movement();
        Monk();
        Console.WriteLine("PASS: melee role actions, movement requests and Monk.");
    }

    private static void Monk()
    {
        Check(JobCombatRegistry.Find(20, 90) != null, "Monk level 90 must be registered.");
        var mnk = new MonkCombat { Roll = () => 0.99 };
        // Trait 431 (level 84, "輕身步法效果提高") raises Thunderclap from the sheet's 2 charges to 3.
        Check(mnk.GetCooldown(25762) == (15, 30, 3), "Thunderclap must have 3 charges per trait 431.");
        Check(mnk.GetCooldown(69) == (14, 40, 2), "Perfect Balance must have 2 charges.");
        Check(mnk.Adjust(36945) == 53 && mnk.Adjust(36946) == 54 && mnk.Adjust(36947) == 56 && mnk.Adjust(62) == 25767
              && mnk.Adjust(25761) == 3547 && mnk.Adjust(25763) == 16474 && mnk.Adjust(36940) == 36942 && mnk.Adjust(36941) == 36943,
            "Level-100 hotbar ids and trait upgrades must map to level-90 actions.");
        Check(!mnk.CanUse(54, true, true, true, checkTiming: false), "True Strike needs Raptor Form.");
        Hit(mnk, 53);
        Check(mnk.Chakra == 0 && mnk.CanUse(54, true, true, true, checkTiming: false) && mnk.IsHighlighted(54),
            "A non-critical Bootshine outside Opo-opo Form must give no chakra and Raptor Form.");
        Gcd(mnk); Hit(mnk, 61);
        Check(mnk.RaptorFury == 1 && mnk.CanUse(66, true, true, true, checkTiming: false), "Twin Snakes must give Raptor's Fury and Coeurl Form.");
        Gcd(mnk); Hit(mnk, 66);
        Check(mnk.CoeurlFury == 2, "Demolish must give two Coeurl's Fury.");
        Gcd(mnk); Hit(mnk, 74);
        Check(mnk.OpoOpoFury == 1 && mnk.Chakra == 0, "Dragon Kick in Opo-opo Form must give Opo-opo's Fury.");
        Gcd(mnk); Hit(mnk, 54);
        Check(mnk.RaptorFury == 0, "True Strike must spend Raptor's Fury.");
        Gcd(mnk); Hit(mnk, 56);
        Check(mnk.CoeurlFury == 1, "Snap Punch must spend one Coeurl's Fury.");
        Gcd(mnk); Hit(mnk, 53);
        Check(mnk.Chakra == 1 && mnk.OpoOpoFury == 0, "Bootshine in Opo-opo Form must always crit for chakra and spend Opo-opo's Fury.");

        var crit = new MonkCombat { Roll = () => 0.39 };
        Hit(crit, 74);
        var miss = new MonkCombat { Roll = () => 0.4 };
        Hit(miss, 74);
        Check(crit.Chakra == 1 && miss.Chakra == 0, "Weaponskill chakra must use a fixed 40% critical chance.");

        var meditation = new MonkCombat();
        Check(meditation.TryUse(36942, false, false, false) == null && meditation.Chakra == 5, "Meditation out of combat must fill 5 chakra.");
        Check(!meditation.CanUse(36942, false, false, true, checkTiming: false), "Meditation needs fewer than 5 chakra.");
        Check(meditation.CanUse(3547, true, true, true, checkTiming: false) && !meditation.CanUse(3547, true, true, false, checkTiming: false),
            "The Forbidden Chakra needs 5 chakra and combat.");
        meditation.Advance(0.6);
        Hit(meditation, 3547);
        Check(meditation.Chakra == 0, "The Forbidden Chakra must spend 5 chakra.");
        meditation.Advance(1);
        Check(meditation.TryUse(36942, false, false, true) == null && meditation.Chakra == 1, "Meditation in combat must give 1 chakra.");

        var pb = new MonkCombat { Roll = () => 0.99 };
        Check(!pb.CanUse(69, false, false, false, checkTiming: false), "Perfect Balance needs combat.");
        Check(pb.TryUse(69, false, false, true) == null, "Perfect Balance must start.");
        pb.Advance(0.6);
        Hit(pb, 53); Gcd(pb); Hit(pb, 74); Gcd(pb); Hit(pb, 25767, aoe: true);
        Check(pb.BeastChakra.SequenceEqual(new[] { 3, 3, 3 }) && pb.Adjust(25764) == 3545, "Three Opo-opo weaponskills must ready Elixir Field.");
        Gcd(pb); Hit(pb, 25764, aoe: true);
        Check(pb.LunarNadi && !pb.SolarNadi && pb.BeastChakra.All(b => b == 0) && pb.CanUse(56, true, true, true, checkTiming: false),
            "Elixir Field must give Lunar Nadi and Formless Fist.");
        Gcd(pb);
        Check(pb.TryUse(69, false, false, true) == null, "The second Perfect Balance charge must start.");
        pb.Advance(0.6);
        Hit(pb, 53); Gcd(pb); Hit(pb, 54); Gcd(pb); Hit(pb, 56);
        Check(pb.Adjust(25764) == 25768, "Three different Beast Chakra must ready Rising Phoenix.");
        Gcd(pb); Hit(pb, 25764, aoe: true);
        Check(pb.LunarNadi && pb.SolarNadi, "Rising Phoenix must give Solar Nadi.");
        pb.Advance(40);
        Check(pb.TryUse(69, false, false, true) == null, "Perfect Balance must recharge.");
        pb.Advance(0.6);
        Hit(pb, 53); Gcd(pb); Hit(pb, 53); Gcd(pb); Hit(pb, 54);
        Check(pb.Adjust(25764) == 25769, "Both Nadi must turn the blitz into Phantom Rush.");
        Gcd(pb); Hit(pb, 25764, aoe: true);
        Check(!pb.LunarNadi && !pb.SolarNadi, "Phantom Rush must clear both Nadi.");

        var celestial = new MonkCombat { Roll = () => 0.99 };
        celestial.TryUse(69, false, false, true);
        celestial.Advance(0.6);
        Hit(celestial, 53); Gcd(celestial); Hit(celestial, 53); Gcd(celestial); Hit(celestial, 54);
        Check(celestial.Adjust(25764) == 25765, "Two kinds of Beast Chakra must ready Celestial Revolution.");
        Gcd(celestial); Hit(celestial, 25764);
        Check(celestial.LunarNadi && !celestial.SolarNadi, "Celestial Revolution without Lunar Nadi must give Lunar Nadi.");

        var brotherhood = new MonkCombat { Roll = () => 0.99 };
        brotherhood.TryUse(7396, false, false, true);
        brotherhood.Advance(0.6);
        for (var i = 0; i < 6; i++) { Hit(brotherhood, 74); Gcd(brotherhood); }
        Check(brotherhood.Chakra == 6, "Meditative Brotherhood must give chakra every weaponskill and allow more than 5.");

        var earth = new MonkCombat();
        Check(!earth.CanUse(36944, false, false, true, checkTiming: false), "Earth's Reply needs Earth's Rumination Ready.");
        earth.TryUse(7394, false, false, true);
        earth.Advance(0.6);
        Check(earth.TryUse(36944, false, false, true) == null && !earth.CanUse(36944, false, false, true, checkTiming: false),
            "Riddle of Earth must ready Earth's Reply once.");

        var wind = new MonkCombat();
        Check(wind.TryUse(25766, false, false, true) == null && wind.Statuses().Any(s => s.Id == 2687 && s.Remaining == 15),
            "Riddle of Wind must grant status 2687.");

        mnk.Advance(0.6);
        Check(mnk.TryUse(25762, true, true, true) is { GapCloser: true }, "Thunderclap must slide to its target.");
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

    private static void Movement()
    {
        var mover = new TestMover();
        Check(mover.TakeMove() == null, "A fresh job must not request a move.");
        Check(mover.TryUse(2, false, false, true) == null && mover.TakeMove() == new JobMove(JobMoveKind.Backward, 15, true),
            "A backward jump must request a 15 y backward move that marks the return point.");
        Check(mover.TakeMove() == null, "A move request must be taken only once.");
        Check(!mover.CanUse(2, false, false, true, bound: true, checkTiming: false), "Displacements must be refused while bound.");
        mover.Advance(0.6);
        Check(mover.TryUse(3, false, false, true) == null && Math.Abs(mover.Timing.Remaining(58) - 0.5) < 1e-9,
            "A short-lock action must hold the GCD group for its own 0.5 s, not a full GCD.");
        var reset = new TestMover();
        reset.TryUse(2, false, false, true);
        reset.Reset();
        Check(reset.TakeMove() == null, "Reset must drop a pending move.");
    }

    private sealed class TestMover() : MeleeCombatBase(2.5)
    {
        protected override IReadOnlyList<uint> JobActions { get; } = [1, 2, 3];
        protected override IReadOnlyList<ushort> JobStatusIds { get; } = [];
        protected override string JobDebugState => "";
        protected override uint ComboFrom(uint actionId) => 0;
        protected override bool IsJobSelfAction(uint actionId) => true;
        protected override bool BlockedWhileBound(uint actionId) => actionId == 2;
        protected override bool AlsoUsesGcd(uint actionId) => actionId == 3;
        protected override double AlsoUsesGcdSeconds(uint actionId) => 0.5;
        protected override (int Group, double Recast, int Charges) JobTimingContract(uint actionId) => actionId switch
        {
            2 => (6, 30, 1),
            3 => (4, 20, 2),
            _ => Gcd,
        };
        protected override JobHit? ApplyJob(uint actionId, bool hasTarget)
        {
            if (actionId == 2) Move(JobMoveKind.Backward, 15, marksReturn: true);
            return null;
        }
        protected override void AdvanceJob(double seconds) { }
        protected override void ResetJob() { }
        public override bool IsGapCloser(uint actionId) => false;
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
