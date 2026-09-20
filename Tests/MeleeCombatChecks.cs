using AnoMech.Core.Combat;

internal static class MeleeCombatChecks
{
    public static void Run()
    {
        RoleActions();
        Movement();
        Monk();
        Dragoon();
        Ninja();
        Samurai();
        Console.WriteLine("PASS: melee role actions, movement requests, Monk, Dragoon, Ninja and Samurai.");
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


    private static void Dragoon()
    {
        Check(JobCombatRegistry.Find(22, 90) != null, "Dragoon level 90 must be registered.");
        var drg = new DragoonCombat();
        // Trait 438 (level 88) and trait 580 (level 84) each raise a one-charge ability to two.
        Check(drg.GetCooldown(83) == (15, 40, 2), "Life Surge must have 2 charges per trait 438.");
        Check(drg.GetCooldown(36951) == (20, 60, 2), "Winged Glide must have 2 charges per trait 580.");
        Check(drg.GetCooldown(25773) == (5, 10, 1), "Wyrmwind Thrust must keep its sheet contract.");
        Check(drg.Adjust(84) == 25771 && drg.Adjust(88) == 25772 && drg.Adjust(92) == 16478,
            "Trait 437 and 275 upgrades must map to the level-90 actions.");
        Check(drg.Adjust(75) == 75, "True Thrust must stay itself without Draconian Fire.");

        Hit(drg, 75); Gcd(drg);
        Check(drg.IsHighlighted(78) && drg.IsHighlighted(87), "True Thrust must start both combos.");
        Hit(drg, 87);
        Check(drg.Statuses().Any(st => st.Id == 2720 && st.Remaining == 30), "Disembowel must grant Power Surge on its combo.");
        Gcd(drg);
        Hit(drg, 25772); Gcd(drg); Hit(drg, 3556); Gcd(drg);

        // The AOE combo is what grants Draconian Fire, which turns True Thrust into Raiden Thrust.
        Hit(drg, 86, aoe: true); Gcd(drg); Hit(drg, 7397, aoe: true); Gcd(drg); Hit(drg, 16477, aoe: true);
        Check(drg.DraconianFireReady && drg.Adjust(75) == 16479 && drg.Adjust(86) == 25770,
            "A finished Coerthan Torment combo must grant Draconian Fire.");
        Gcd(drg);
        Hit(drg, 75);
        Check(drg.FirstmindsFocus == 1 && !drg.DraconianFireReady,
            "Raiden Thrust must spend Draconian Fire for one Firstminds Focus.");
        Check(!drg.CanUse(25773, true, true, true, checkTiming: false), "Wyrmwind Thrust needs two Firstminds Focus.");

        var fire = new DragoonCombat();
        Hit(fire, 86, aoe: true); Gcd(fire); Hit(fire, 7397, aoe: true); Gcd(fire); Hit(fire, 16477, aoe: true); Gcd(fire);
        Hit(fire, 75); Gcd(fire);
        Hit(fire, 86, aoe: true); Gcd(fire); Hit(fire, 7397, aoe: true); Gcd(fire); Hit(fire, 16477, aoe: true); Gcd(fire);
        Hit(fire, 86, aoe: true);
        Check(fire.FirstmindsFocus == 2 && fire.CanUse(25773, true, true, true, checkTiming: false),
            "Draconian Fury must also give Firstminds Focus, up to two.");
        Gcd(fire);
        Hit(fire, 25773, aoe: true);
        Check(fire.FirstmindsFocus == 0, "Wyrmwind Thrust must spend both Firstminds Focus.");

        var dive = new DragoonCombat();
        Check(!dive.CanUse(7400, true, true, true, checkTiming: false), "Nastrond needs Nastrond Ready.");
        Check(!dive.CanUse(16480, true, true, true, checkTiming: false), "Stardiver needs Life of the Dragon.");
        Check(dive.TryUse(3555, true, true, true) is { IsAoe: true }, "Geirskogul must hit.");
        Check(dive.LifeOfTheDragonRemaining == 20 && dive.CanUse(7400, true, true, true, checkTiming: false)
              && dive.CanUse(16480, true, true, true, checkTiming: false),
            "Trait 163 must turn the Geirskogul buff straight into Life of the Dragon plus Nastrond Ready.");
        dive.Advance(2);
        Check(dive.TryUse(7400, true, true, true) is { IsAoe: true } && !dive.CanUse(7400, true, true, true, checkTiming: false),
            "Nastrond must spend Nastrond Ready.");

        var jump = new DragoonCombat();
        Check(jump.TryUse(92, true, true, true) is { ActionId: 16478 }, "Jump must resolve as High Jump.");
        Check(jump.CanUse(7399, true, true, true, checkTiming: false), "High Jump must ready Mirage Dive.");
        jump.Advance(1);
        Check(jump.TryUse(94, false, false, true) == null && jump.TakeMove() == new JobMove(JobMoveKind.Backward, 15, false),
            "Elusive Jump must request a 15 y backward move.");
        Check(!jump.CanUse(94, false, false, true, bound: true, checkTiming: false), "Elusive Jump must be refused while bound.");
    }

    private static void Ninja()
    {
        Check(JobCombatRegistry.Find(30, 90) != null, "Ninja level 90 must be registered.");
        var nin = new NinjaCombat();
        Check(nin.GetCooldown(2262) == (16, 60, 2), "Shukuchi must have 2 charges per trait 279.");
        Check(nin.GetCooldown(2259) == (4, 20, 2), "The mudra group must have 2 charges.");
        Check(nin.Adjust(2246) == 3566 && nin.Adjust(2248) == 36957, "Trait 515 and 585 upgrades must map through.");

        // Ten then Chi is Raiton, which banks Raiju Ready.
        Use(nin, 2259); nin.Advance(0.6); Use(nin, 2261); nin.Advance(0.6);
        Check(nin.Adjust(2260) == 2267, "Ten then Chi must spell Raiton.");
        Hit(nin, 2260);
        Check(nin.Mudras.Count == 0 && nin.CanUse(25778, true, true, true, checkTiming: false),
            "Raiton must clear the mudras and ready Forked Raiju.");
        Gcd(nin);
        Hit(nin, 25778);
        Check(!nin.CanUse(25778, true, true, true, checkTiming: false), "Forked Raiju must spend its Raiju Ready stack.");

        var rabbit = new NinjaCombat();
        Use(rabbit, 2259); rabbit.Advance(0.6); Use(rabbit, 2259); rabbit.Advance(0.6);
        Check(rabbit.Adjust(2260) == 2272, "An illegal mudra sequence must become Rabbit Medium.");
        Check(rabbit.TryUse(2260, true, true, true) == null && rabbit.Mudras.Count == 0,
            "Rabbit Medium must resolve without a hit and clear the mudras.");

        var kassatsu = new NinjaCombat();
        Check(kassatsu.TryUse(2264, false, false, true) == null, "Kassatsu must start.");
        kassatsu.Advance(0.6);
        Use(kassatsu, 2261); kassatsu.Advance(0.6); Use(kassatsu, 2259); kassatsu.Advance(0.6);
        Check(kassatsu.Adjust(2260) == 16491, "Trait 250 must turn Katon into Goka Mekkyaku under Kassatsu.");
        Hit(kassatsu, 2260, aoe: true);
        Check(kassatsu.Adjust(2260) == 2272, "Kassatsu must be spent by the ninjutsu.");

        var ninki = new NinjaCombat();
        Hit(ninki, 2240);
        Check(ninki.Ninki == 5, "Spinning Edge must give 5 Ninki.");
        Gcd(ninki); Hit(ninki, 2242);
        Check(ninki.Ninki == 10, "Gust Slash must add 5 Ninki on its combo.");
        Gcd(ninki); Hit(ninki, 3563);
        Check(ninki.Ninki == 25 && ninki.Kazematoi == 2, "Armor Crush must give 15 Ninki and two Kazematoi on its combo.");
        Gcd(ninki); Hit(ninki, 2240); Gcd(ninki); Hit(ninki, 2242); Gcd(ninki); Hit(ninki, 2255);
        Check(ninki.Kazematoi == 1, "Aeolian Edge must spend one Kazematoi.");
        Check(ninki.Ninki == 50 && ninki.CanUse(16493, false, false, true, checkTiming: false),
            "That rotation must bank exactly 50 Ninki and unlock Bunshin.");
        Check(!new NinjaCombat().CanUse(16493, false, false, true, checkTiming: false), "Bunshin needs 50 Ninki.");

        var bunshin = new NinjaCombat();
        for (var i = 0; i < 12; i++) { Hit(bunshin, 2240); Gcd(bunshin); }
        Check(bunshin.Ninki >= 50 && bunshin.TryUse(16493, false, false, true) == null,
            "Bunshin must be usable once Ninki reaches 50.");
        var before = bunshin.Ninki;
        bunshin.Advance(0.6);
        Hit(bunshin, 2240);
        Check(bunshin.Ninki == Math.Min(100, before + 10),
            "A Bunshin clone hit must add 5 Ninki on top of the weaponskill's own 5.");

        var shukuchi = new NinjaCombat();
        Check(shukuchi.TryUse(2262, false, false, true) == null && shukuchi.TakeMove() == new JobMove(JobMoveKind.GroundPoint, 0, false),
            "Shukuchi must request a ground-targeted move.");
        Check(!shukuchi.CanUse(2262, false, false, true, bound: true, checkTiming: false), "Shukuchi must be refused while bound.");
    }

    private static void Samurai()
    {
        Check(JobCombatRegistry.Find(34, 90) != null, "Samurai level 90 must be registered.");
        var sam = new SamuraiCombat();
        Check(sam.GetCooldown(7499) == (19, 55, 2), "Meikyo Shisui must have 2 charges per trait 443.");
        Check(sam.GetCooldown(16487) == (8, 15, 1), "Shoha must keep its sheet contract.");
        Check(sam.Adjust(7483) == 25780 && sam.Adjust(7498) == 36962, "Trait 519 and 589 upgrades must map through.");
        Check(sam.CastTime(7487) == 1.3, "Trait 277 must shorten the iaijutsu cast to 1.3 s.");

        Hit(sam, 7477);
        Check(sam.Kenki == 5, "Hakaze must give the trait's 5 Kenki.");
        Gcd(sam); Hit(sam, 7478); Gcd(sam); Hit(sam, 7481);
        Check(sam.Sen == 2 && sam.Kenki == 25, "Gekko must give Getsu and 10 Kenki on its combo.");
        Gcd(sam); Hit(sam, 7477); Gcd(sam); Hit(sam, 7480);
        Check(sam.Sen == 3, "Yukikaze must add Setsu on its combo.");
        Check(sam.Adjust(7867) == 7488, "Two Sen must make Iaijutsu into Tenka Goken.");
        Gcd(sam); Cast(sam, 7867, aoe: true);
        Check(sam.Sen == 0 && sam.Meditation == 1 && sam.Kaeshi == 16485,
            "Tenka Goken must spend every Sen, bank Meditation and arm Kaeshi.");
        Check(sam.Adjust(16483) == 16485, "Tsubame-gaeshi must repeat the last iaijutsu.");
        Gcd(sam); Hit(sam, 16483, aoe: true);
        Check(sam.Kaeshi == 0, "Tsubame-gaeshi must disarm itself.");

        var three = new SamuraiCombat();
        Hit(three, 7477); Gcd(three); Hit(three, 7478); Gcd(three); Hit(three, 7481); Gcd(three);
        Hit(three, 7477); Gcd(three); Hit(three, 7479); Gcd(three); Hit(three, 7482); Gcd(three);
        Hit(three, 7477); Gcd(three); Hit(three, 7480); Gcd(three);
        Check(three.Adjust(7867) == 7487, "Three Sen must make Iaijutsu into Midare Setsugekka.");
        Cast(three, 7867);
        Check(three.Kaeshi == 16486 && three.Sen == 0, "Midare Setsugekka must arm Kaeshi Setsugekka.");

        var hagakure = new SamuraiCombat();
        Hit(hagakure, 7477); Gcd(hagakure); Hit(hagakure, 7478); Gcd(hagakure); Hit(hagakure, 7481); Gcd(hagakure);
        var kenki = hagakure.Kenki;
        Check(hagakure.TryUse(7495, false, false, true) == null && hagakure.Sen == 0 && hagakure.Kenki == kenki + 10,
            "Hagakure must turn each Sen into 10 Kenki.");
        Check(!hagakure.CanUse(7495, false, false, true, checkTiming: false), "Hagakure needs at least one Sen.");

        var shoha = new SamuraiCombat();
        Check(!shoha.CanUse(16487, true, true, true, checkTiming: false), "Shoha needs three Meditation.");

        var meikyo = new SamuraiCombat();
        Check(meikyo.TryUse(7499, false, false, true) == null, "Meikyo Shisui must start.");
        meikyo.Advance(0.6);
        Hit(meikyo, 7481);
        Check(meikyo.Sen == 2 && meikyo.Statuses().Any(st => st.Id == 1298),
            "Meikyo Shisui must satisfy the Gekko combo and grant Fugetsu.");

        var ogi = new SamuraiCombat();
        Check(!ogi.CanUse(25781, true, true, true, checkTiming: false), "Ogi Namikiri needs Ogi Namikiri Ready.");
        Check(ogi.TryUse(16482, false, false, true) == null && ogi.Kenki == 50,
            "Ikishoten must give 50 Kenki and ready Ogi Namikiri.");
        ogi.Advance(1);
        Cast(ogi, 25781, aoe: true);
        Check(ogi.Kaeshi == 25782 && ogi.Adjust(16483) == 25782, "Ogi Namikiri must arm Kaeshi Namikiri.");

        var yaten = new SamuraiCombat();
        yaten.TryUse(16482, false, false, true);
        yaten.Advance(1);
        Check(yaten.TryUse(7493, true, true, true) is not null && yaten.TakeMove() == new JobMove(JobMoveKind.Backward, 10, false),
            "Yaten must request a 10 y backward move.");
        Check(!yaten.CanUse(7493, true, true, true, bound: true, checkTiming: false), "Yaten must be refused while bound.");
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

    internal static void Cast(IJobCombat job, uint id, bool aoe = false)
    {
        var expected = job.Adjust(id);
        Check(job.BeginCast(id, true, true, true), $"{id} must begin casting. {job.DebugState}");
        job.Advance(2);
        Check(job.CompleteCast(true, true, true, out var hit), $"{id} must complete its cast. {job.DebugState}");
        if (hit is not { } h || h.ActionId != expected || h.IsAoe != aoe)
            throw new Exception($"Expected cast hit {expected} (from {id}) aoe={aoe}; got {hit?.ToString() ?? "null"}. {job.DebugState}");
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
