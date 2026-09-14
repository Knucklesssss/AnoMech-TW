using AnoMech.Core.Combat;

internal static class RangedCombatChecks
{
    public static void Run()
    {
        RoleActions();
        Bard();
        Machinist();
        Dancer();
        BlackMage();
        Summoner();
        RedMage();
        Pictomancer();
        Console.WriteLine("PASS: ranged and caster role actions, Bard, Machinist, Dancer, Black Mage, Summoner, Red Mage and Pictomancer.");
    }

    private static void Pictomancer()
    {
        Check(JobCombatRegistry.Find(42, 90) != null, "Pictomancer level 90 must be registered.");
        var pct = new PictomancerCombat();
        // Trait 540 (level 86, "彩繪效果提高II") gives Striking Muse 2 charges instead of the sheet's base 1.
        Check(pct.GetCooldown(34674).Charges == 2, "Striking Muse must have 2 charges per trait 540.");
        // The muse hotbar placeholders (35347/35348/35349) sit before their targets in JobActions, so
        // CombatNativeState binds their native recast circles from these ids directly: they need their
        // own explicit timing contracts matching their real target's group/recast/charges.
        Check(pct.GetBindingCooldown(35347) == (19, 40, 2), "Creature Motif must bind group 19, 40 s, 2 charges.");
        Check(pct.GetBindingCooldown(35348) == (20, 60, 2), "Weapon Motif must bind group 20, 60 s, 2 charges per trait 540.");
        Check(pct.GetBindingCooldown(35349) == (21, 120, 1), "Landscape Motif must bind group 21, 120 s, 1 charge.");
        Cast(pct, 34650);
        Check(pct.Adjust(34650) == 34651, "Fire in Red must turn into Aero in Green.");
        pct.Advance(1);
        Cast(pct, 34650);
        pct.Advance(1);
        Cast(pct, 34650);
        Check(pct.Palette == 25 && pct.Paint == 1 && pct.Adjust(34650) == 34650, "Water in Blue must add 25 Palette and a White Paint.");
        pct.Advance(1);
        Hit(pct, 34662, aoe: true);
        Check(pct.Paint == 0, "Holy in White must spend White Paint.");
        pct.Advance(2.5);
        Cast(pct, 34689);
        Check(pct.CreatureCanvas == PictomancerCombat.Creature.Pom && pct.Adjust(35347) == 34670 && pct.Adjust(34689) == 34665,
            "Creature Motif must paint Pom first and turn the muse into Pom Muse.");
        pct.Advance(0.2); // cast release lock
        Hit(pct, 35347, aoe: true);
        Check(pct.CreatureCanvas == PictomancerCombat.Creature.None && pct.PomPortrait, "Pom Muse must clear the canvas and mark the pom.");
        Check(!pct.CanUse(34683, false, false, true, checkTiming: false), "Subtractive Palette needs 50 Palette.");
    }

    private static void RedMage()
    {
        Check(JobCombatRegistry.Find(35, 90) != null, "Red Mage level 90 must be registered.");
        var rdm = new RedMageCombat { Roll = () => 0 };
        Check(rdm.Adjust(7503) == 37004 && rdm.Adjust(7505) == 25855 && rdm.CastTime(37004) == 2,
            "Jolt and Verthunder must map to level-90 actions.");
        Cast(rdm, 37004);
        Check(rdm.Black == 2 && rdm.White == 2 && rdm.CastTime(25855) == 0, "A hardcast Jolt III must grant mana and Dualcast.");
        rdm.Advance(0.5);
        Hit(rdm, 25855);
        Check(rdm.Black == 8 && rdm.CastTime(25855) == 5 && rdm.CanUse(7510, true, true, true, checkTiming: false),
            "The Dualcast Verthunder III must spend Dualcast, add 6 Black Mana and proc Verfire.");
        rdm.Advance(2.5);
        // Trait 305 (level 78, "魔元化效果提高") shortens Manafication's recast from the sheet's 120s to 110s.
        Check(rdm.GetCooldown(7521).Recast == 110, "Manafication recast must be 110s per trait 305.");
        // Trait 306 (level 74, "赤魔法精通") shortens Contre Sixte's recast from the sheet's 45s to 35s.
        Check(rdm.GetCooldown(7519).Recast == 35, "Contre Sixte recast must be 35s per trait 306.");
        // Trait 485 (level 88, "促進效果提高") gives Acceleration 2 charges instead of the sheet's 1.
        Check(rdm.GetCooldown(7518).Charges == 2, "Acceleration must have 2 charges per trait 485.");
        Check(rdm.TryUse(7521, false, false, true) == null && rdm.Adjust(7504) == 7527, "Manafication must enable Enchanted Riposte.");
        // Trait 486 (level 90, "魔元化效果提高II") raises Manafication's max stacks from the sheet's base 3 to 6.
        Check(rdm.Statuses().Any(s => s.Id == 1971 && s.Param == 6), "Manafication must carry 6 stacks per trait 486.");
        rdm.Advance(0.6);
        Hit(rdm, 7504);
        Check(rdm.ManaStacks == 1 && rdm.Black == 8 && rdm.IsHighlighted(7512), "Magicked Swordplay must make Enchanted Riposte free and add a Mana Stack.");
        rdm.Advance(1.6);
        Hit(rdm, 7512);
        rdm.Advance(1.6);
        Hit(rdm, 7516);
        Check(rdm.ManaStacks == 3 && rdm.Adjust(25855) == 7525 && rdm.Adjust(7505) == 7525,
            "A third Enchanted hit must fill Mana Stacks and ready Verflare, including from the base Verthunder hotbar id.");
        rdm.Advance(2.3);
        Hit(rdm, 25855, aoe: true);
        Check(rdm.ManaStacks == 0 && rdm.Adjust(37004) == 16530 && rdm.Adjust(7503) == 16530,
            "Verflare must spend Mana Stacks and ready Scorch off Jolt III, including from the base Jolt hotbar id.");
        rdm.Advance(2.6);
        Hit(rdm, 37004, aoe: true);
        Check(rdm.Adjust(37004) == 25858 && rdm.Adjust(7509) == 25858,
            "Scorch must ready Resolution off Jolt III, including from the base Scatter hotbar id.");
        rdm.Advance(2.6);
        Hit(rdm, 37004, aoe: true);
        Check(rdm.ComboAction == 0, "Resolution must complete the Verflare finisher chain.");

        var both = new RedMageCombat { Roll = () => 0.99 };
        both.TryUse(7518, false, false, true);
        both.Advance(0.6);
        Cast(both, 37004);
        both.Advance(0.5);
        Check(both.Statuses().Any(s => s.Id == 1238 && s.Remaining > 0), "Acceleration must still be up before the Dualcast Verthunder III.");
        Hit(both, 25855);
        Check(both.Statuses().Any(s => s.Id == 1238 && s.Remaining > 0) && both.CastTime(25855) == 0,
            "A Dualcast Verthunder III must spend only Dualcast, leaving Acceleration up.");
        Check(!both.CanUse(7510, true, true, true, checkTiming: false),
            "The Dualcast (not Acceleration-sourced) Verthunder III must roll for Verfire normally and miss on a non-proc roll.");
    }

    private static void Summoner()
    {
        Check(JobCombatRegistry.Find(27, 90) != null, "Summoner level 90 must be registered.");
        var smn = new SummonerCombat();
        Check(smn.Adjust(163) == 3579 && smn.Adjust(25802) == 25838 && smn.Adjust(36990) == 181,
            "Ruin, Summon Ruby and Necrotize must map to level-90 actions.");
        // Trait 480 (level 88, "守護之光效果提高") raises Radiant Aegis from the sheet's 1 charge to 2.
        Check(smn.GetCooldown(25799).Charges == 2, "Radiant Aegis must have 2 charges per trait 480.");
        Check(!smn.CanUse(7427, true, true, false, checkTiming: false), "Summon Bahamut needs combat.");
        Hit(smn, 7427);
        Check(smn.CurrentTrance == SummonerCombat.Trance.Bahamut && smn.Adjust(3579) == 25820 && smn.Adjust(25822) == 3582
                && smn.Ruby && smn.Topaz && smn.Emerald,
            "Summon Bahamut must enter its trance, substitute Astral spells and grant three arcana.");
        smn.Advance(15);
        Check(smn.CurrentTrance == SummonerCombat.Trance.None && smn.Adjust(7427) == 25831, "Bahamut ends after 15 s and Phoenix comes next.");
        Hit(smn, 25802, aoe: true);
        Check(smn.Attunement == SummonerCombat.Primal.Ifrit && smn.AttunementStacks == 2 && smn.Adjust(25883) == 25823
                && smn.Adjust(25822) == 25835 && smn.CastTime(25823) == 2.8 && !smn.Ruby,
            "Summon Ifrit II must spend Ruby Arcanum, attune two Ruby Rites and ready Crimson Cyclone.");
        smn.Advance(2.5);
        Hit(smn, 25822, aoe: true);
        Check(smn.Adjust(25822) == 25885, "Crimson Cyclone must ready Crimson Strike.");
        smn.Advance(2.5);
        Hit(smn, 25822, aoe: true);
        Check(smn.Adjust(25822) != 25885, "Crimson Strike must be castable and spend its ready status.");
        smn.Advance(0.6);
        Hit(smn, 16508);
        Check(smn.Aetherflow == 2 && smn.CanUse(7426, true, true, true, checkTiming: false), "Energy Drain must grant Aetherflow and Further Ruin.");
    }

    private static void BlackMage()
    {
        Check(JobCombatRegistry.Find(25, 90) != null, "Black Mage level 90 must be registered.");
        var blm = new BlackMageCombat { Roll = () => 0.99 };
        Check(blm.Adjust(144) == 153 && blm.Adjust(36986) == 153 && blm.Adjust(147) == 25794, "Thunder, High Thunder and Fire II must map to level-90 actions.");
        Check(!blm.CanUse(3577, true, true, true, checkTiming: false), "Fire IV needs Astral Fire.");
        // Trait 463 (level 84, "魔泉效果提高") shortens Manafont's recast from the sheet's 120s to 100s.
        Check(blm.GetBindingCooldown(158).Recast == 100, "Manafont recast must be 100s per trait 463.");
        Cast(blm, 152);
        blm.Advance(0.1); // cast release lock
        Check(blm.Element == 3 && blm.Mp == JobCombatBase.MaxMp - 2000 && blm.CanUse(153, true, true, true, checkTiming: false),
            "Fire III must enter Astral Fire III, cost 2000 MP and grant Thunderhead.");
        Cast(blm, 3577);
        Check(blm.Mp == JobCombatBase.MaxMp - 2000 - 1600, "Fire IV costs double in Astral Fire without Umbral Hearts, with no natural regen.");
        blm.Advance(0.6);
        Check(Math.Abs(blm.CastTime(154) - 1.75) < 1e-9, "Blizzard III casts in half the time from Astral Fire III.");
        var mp = blm.Mp;
        Cast(blm, 154);
        Check(blm.Element == -3 && blm.Paradox && blm.Adjust(141) == 25797 && blm.Mp == mp,
            "Blizzard III from Astral Fire III must be free, enter Umbral Ice III and ready Paradox.");
        blm.Advance(0.8);
        Cast(blm, 3576);
        Check(blm.UmbralHearts == 3, "Blizzard IV must grant three Umbral Hearts.");
        blm.Advance(30);
        Check(blm.Polyglot >= 1 && blm.CanUse(16507, true, true, true, checkTiming: false), "An element held for 30 s must grant Polyglot.");
        // Trait 461 (level 80, "穢濁效果提高") makes Foul instant. Checked before Triplecast below, since
        // Triplecast/Swiftcast would make CastTime(7422) == 0 regardless of whether the trait is applied.
        Check(blm.CastTime(7422) == 0, "Foul must be instant per trait 461.");
        blm.TryUse(7421, false, false, true);
        Check(blm.CastTime(3576) == 0, "Triplecast must make Blizzard IV instant.");
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

    private static void Machinist()
    {
        Check(JobCombatRegistry.Find(31, 90) != null, "Machinist level 90 must be registered.");
        var mch = new MachinistCombat();
        Check(mch.Adjust(2866) == 7411 && mch.Adjust(36979) == 2874 && mch.Adjust(36980) == 2890,
            "Split Shot, Double Check and Checkmate must map to level-90 actions.");
        Hit(mch, 7411);
        mch.Advance(2.5);
        Check(mch.IsHighlighted(7412), "Heated Slug Shot must glow after Heated Split Shot.");
        Hit(mch, 7412);
        mch.Advance(2.5);
        Hit(mch, 7413);
        Check(mch.Heat == 15 && mch.Battery == 10, "The heated combo must build 15 Heat and 10 Battery.");
        mch.Advance(2.5);
        Hit(mch, 16500);
        Check(mch.Battery == 30 && !mch.CanUse(17209, false, false, true, checkTiming: false), "Air Anchor adds 20 Battery; Hypercharge needs 50 Heat.");
        mch.Advance(0.6);
        mch.TryUse(7414, false, false, true);
        mch.Advance(0.6);
        Check(mch.TryUse(17209, false, false, true) == null && mch.OverheatStacks == 5 && mch.Heat == 15,
            "Hypercharged must allow a free Hypercharge with five Overheated stacks.");
        mch.Advance(0.6);
        Hit(mch, 2874);
        mch.Advance(0.8);
        Hit(mch, 36978);
        Check(mch.OverheatStacks == 4 && Math.Abs(mch.Timing.Remaining(15) - 14.2) < 1e-6,
            "Blazing Shot must spend Overheated and cut Gauss Round by 15 s.");
        Check(!mch.CanUse(16501, false, false, true, checkTiming: false), "Automaton Queen needs 50 Battery.");
    }

    private static void Dancer()
    {
        Check(JobCombatRegistry.Find(38, 90) != null, "Dancer level 90 must be registered.");
        var dnc = new DancerCombat { Roll = () => 0 };
        Hit(dnc, 15989);
        Check(dnc.CanUse(15991, true, true, true, checkTiming: false) && dnc.IsHighlighted(15990),
            "Cascade must proc Silken Symmetry and light Fountain.");
        dnc.Advance(2.5);
        Hit(dnc, 15991);
        Check(dnc.Feathers == 1 && !dnc.CanUse(15991, true, true, true, checkTiming: false),
            "Reverse Cascade must spend Silken Symmetry and grant a feather.");
        dnc.Advance(0.6);
        Hit(dnc, 16007);
        Check(dnc.Feathers == 0 && dnc.IsHighlighted(16009), "Fan Dance must spend a feather and ready Fan Dance III.");
        dnc.Advance(1.9);
        Check(dnc.TryUse(15997, false, false, true) == null && dnc.StepCount == 2 && dnc.Adjust(15989) == 15999 && dnc.IsHighlighted(16000),
            "Standard Step must enter dance mode with its steps on the gauge.");
        Check(!dnc.CanUse(16007, true, true, true, checkTiming: false), "Only steps and a few skills work while dancing.");
        dnc.Advance(0.6);
        dnc.TryUse(15990, false, false, true);
        dnc.Advance(1.5);
        dnc.TryUse(15991, false, false, true);
        dnc.Advance(1.5);
        Check(dnc.StepIndex == 2 && dnc.Adjust(15997) == 16003 && dnc.IsHighlighted(16003), "Both steps must light Standard Finish.");
        Hit(dnc, 15997, aoe: true);
        Check(dnc.StepCount == 0 && dnc.Adjust(15989) == 15989, "Standard Finish must leave dance mode.");

        Check(!dnc.CanUse(16010, false, false, true, bound: true, checkTiming: false), "En Avant must be blocked while bound.");
        Check(dnc.CanUse(16010, false, false, true, bound: false, checkTiming: false), "En Avant must be usable while not bound.");

        Check(dnc.GetCooldown(16012).Recast == 90, "Shield Samba recast must be 90 s under trait 456.");

        dnc.Advance(2.5);
        Hit(dnc, 15989);
        Check(dnc.EspritGauge == 5, "Cascade must grant 5 Esprit under Standard Finish's Esprit status (trait 255).");
        dnc.Advance(2.5);
        Hit(dnc, 15991);
        Check(dnc.EspritGauge == 15, "Reverse Cascade must grant 10 Esprit under Esprit status (trait 454).");
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
