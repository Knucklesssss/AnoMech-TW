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
        Console.WriteLine("PASS: ranged and caster role actions.");
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
