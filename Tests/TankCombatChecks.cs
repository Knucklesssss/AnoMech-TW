using AnoMech.Core.Combat;

internal static class TankCombatChecks
{
    public static void Run()
    {
        Registry();
        Warrior();
        Paladin();
        Gunbreaker();
        Console.WriteLine("PASS: tank registry, Warrior, Paladin and Gunbreaker rotations, gauges, casts, substitutions and defensives.");
    }

    private static void Registry()
    {
        foreach (var classJob in new byte[] { 19, 21, 32, 37 })
            Check(JobCombatRegistry.Find(classJob, 90) != null, $"Tank job {classJob} level 90 must be registered.");
        Check(JobCombatRegistry.Find(21, 70) == null && JobCombatRegistry.Find(25, 90) == null, "Only registered job/level pairs may start local combat.");
        foreach (var entry in JobCombatRegistry.Entries)
        {
            var job = entry.CreateRules(2.5);
            Check(JobCombatRegistry.Entries.Count(e => e.ClassJob == entry.ClassJob && e.Level == entry.Level) == 1, "Duplicate job registry entry.");
            Check(job.Actions.Contains(entry.GcdProbeAction) && job.Actions.Distinct().Count() == job.Actions.Count, $"Job {entry.ClassJob} must list unique actions including its GCD probe.");
            Check(job.StatusIds.Distinct().Count() == job.StatusIds.Count, $"Job {entry.ClassJob} lists a status twice.");
            Check(job.Statuses().All(s => s.Remaining == 0), $"A fresh job {entry.ClassJob} must not mirror active statuses.");
            var contracts = new Dictionary<int, (double, int)>();
            foreach (var action in job.Actions)
            {
                var (group, recast, charges) = job.GetBindingCooldown(action);
                if (contracts.TryGetValue(group, out var known))
                    Check(known.Item2 == charges && (charges == 1 || known.Item1 == recast), $"Job {entry.ClassJob} group {group} has two cooldown contracts.");
                contracts[group] = (recast, charges);
            }
        }
    }

    private static void Warrior()
    {
        var war = new WarriorCombat();
        Hit(war, 31);
        Check(war.IsHighlighted(37) && !war.IsHighlighted(42), "Only Maim must glow after Heavy Swing.");
        war.Advance(2.5); Hit(war, 37); war.Advance(2.5); Hit(war, 42);
        Check(war.Beast == 30, "Storm's Path combo must grant 30 Beast.");
        Check(war.Adjust(49) == 3549 && !war.CanUse(49, true, true, true, checkTiming: false), "Fell Cleave must need 50 Beast.");
        war.Advance(2.5); Hit(war, 31); war.Advance(2.5); Hit(war, 37); war.Advance(2.5); Hit(war, 45);
        Check(war.Beast == 50 && war.TempestRemaining == 30, "Storm's Eye combo must grant 10 Beast and Surging Tempest.");
        Check(!war.CanUse(52, false, false, false), "Infuriate must need combat.");
        war.Advance(2.5);
        Check(war.TryUse(52, false, false, true) == null && war.Beast == 100 && war.Adjust(49) == 16465 && war.IsHighlighted(49),
            "Infuriate must grant Beast up to the cap and turn Fell Cleave into a glowing Inner Chaos.");
        war.Advance(0.6);
        Hit(war, 49);
        Check(war.Beast == 50 && !war.Chaos && Math.Abs(war.Timing.Remaining(20) - (60 - 0.6 - 5)) < 1e-6,
            "Inner Chaos must spend 50 Beast, clear Nascent Chaos and reduce Infuriate.");
        war.Advance(2.5);
        Check(war.TryUse(38, false, false, true) == null && war.InnerReleaseStacks == 3 && war.RendReady
            && Math.Abs(war.TempestRemaining - (30 - 2.5 - 0.6 - 2.5 + 10)) < 1e-6, "Inner Release must grant stacks, Primal Rend and extend Tempest.");
        war.Advance(0.6);
        Hit(war, 49);
        Check(war.Beast == 50 && war.InnerReleaseStacks == 2, "Inner Release must make Fell Cleave free.");
        Check(war.IsHighlighted(25753), "Primal Rend must glow while ready.");
        war.Advance(2.5);
        Hit(war, 25753, aoe: true, gapCloser: true);
        Check(!war.RendReady && !war.CanUse(25753, true, true, true, checkTiming: false), "Primal Rend must consume its readiness.");

        war.Reset();
        for (var i = 0; i < 3; i++) { Hit(war, 7386, gapCloser: true); war.Advance(0.6); }
        Check(!war.CanUse(7386, true, true, true), "Onslaught must have three charges at level 90.");

        war.Reset();
        war.TryUse(48, false, false, false);
        Check(war.Stance && war.Adjust(48) == 32066, "Defiance must turn into Release Defiance.");
        war.Advance(0.6); war.TryUse(40, false, false, true);
        war.Advance(0.6); war.TryUse(44, false, false, true);
        war.Advance(0.6); war.TryUse(3551, false, false, true);
        war.Advance(0.6);
        Check(!war.CanUse(16464, false, false, true), "Nascent Flash must share Bloodwhetting's cooldown.");
        war.TryUse(7388, false, false, true);
        var statuses = war.Statuses().ToDictionary(s => s.Id);
        Check(statuses[87].Remaining == 0 && statuses[89].Remaining == 0 && statuses[2678].Remaining == 0 && statuses[1457].Remaining == 30,
            "Shake It Off must remove Thrill, Vengeance and Bloodwhetting and show its shield.");
        war.Reset();
        Check(war.Stance, "Defiance must survive a duty reset.");
    }

    private static void Paladin()
    {
        var pld = new PaladinCombat();
        Check(pld.CastTime(7384) == 1.5 && pld.CastTime(3541) == 1.5, "Spells must have a cast without Divine Might or Requiescat.");
        Check(pld.TryUse(7384, true, true, true) == null, "A cast-time spell must not resolve instantly.");
        Check(pld.BeginCast(7384, true, true, true) && pld.CastingAction == 7384 && !pld.CanUse(20, false, false, true),
            "A cast must lock other actions.");
        pld.Advance(1.5);
        Check(pld.CompleteCast(true, true, true, out var holy) && holy?.ActionId == 7384 && pld.Mp == JobCombatBase.MaxMp - 1000,
            "Holy Spirit must resolve at cast end and cost 1000 MP.");
        pld.Advance(1.0);
        Check(pld.BeginCast(3541, false, false, true), "Clemency must start casting.");
        pld.CancelCast();
        Check(pld.CastingAction == 0 && pld.CanUse(20, false, false, true) && pld.Mp == JobCombatBase.MaxMp - 1000,
            "Cancelling a cast must release the lock without paying MP.");

        pld.Reset();
        Hit(pld, 9);
        Check(pld.IsHighlighted(15), "Riot Blade must glow after Fast Blade.");
        pld.Advance(2.5); Hit(pld, 15); pld.Advance(2.5); Hit(pld, 21);
        Check(pld.CastTime(7384) == 0 && pld.IsHighlighted(7384) && pld.IsHighlighted(16460), "Royal Authority must grant Divine Might and Atonement Ready.");
        pld.Advance(2.5);
        Hit(pld, 7384);
        Check(pld.CastTime(7384) == 1.5 && pld.Mp == JobCombatBase.MaxMp - 1000, "Instant Holy Spirit must consume Divine Might.");
        pld.Advance(2.5); Hit(pld, 16460);
        Check(pld.Adjust(16460) == 36918, "Atonement must turn into Supplication.");
        pld.Advance(2.5); Hit(pld, 16460);
        Check(pld.Adjust(16460) == 36919, "Supplication must turn into Sepulchre.");
        pld.Advance(2.5); Hit(pld, 16460);
        Check(pld.Adjust(16460) == 16460 && !pld.CanUse(16460, true, true, true, checkTiming: false), "The Atonement chain must end after Sepulchre.");

        pld.Advance(2.5);
        Check(pld.TryUse(20, false, false, true) == null && pld.Adjust(20) == 3538, "Fight or Flight must turn into Goring Blade.");
        pld.Advance(0.6);
        Hit(pld, 20);
        Check(pld.Adjust(20) == 20, "Goring Blade must consume its readiness.");

        pld.Advance(2.5);
        Hit(pld, 7383);
        Check(pld.RequiescatStacks == 4 && pld.CastTime(3541) == 0, "Requiescat must grant four stacks and instant spells.");
        pld.Advance(2.5); Hit(pld, 16459, aoe: true);
        Check(pld.Adjust(16459) == 25748, "Confiteor must turn into Blade of Faith.");
        pld.Advance(2.5); Hit(pld, 16459, aoe: true);
        pld.Advance(2.5); Hit(pld, 16459, aoe: true);
        pld.Advance(2.5); Hit(pld, 16459, aoe: true);
        Check(pld.ComboAction == 0 && pld.RequiescatStacks == 0 && !pld.CanUse(16459, true, true, true, checkTiming: false),
            "Blade of Valor must end the chain and spend the last Requiescat stack.");

        pld.Reset();
        Check(!pld.CanUse(3542, false, false, true), "Sheltron must need 50 Oath.");
        for (var i = 0; i < 10; i++) pld.AutoAttackHit();
        Check(pld.Oath == 50 && pld.Adjust(3542) == 25746, "Auto-attacks must build Oath and Sheltron must be Holy Sheltron.");
        Check(pld.TryUse(3542, false, false, true) == null && pld.Oath == 0
            && pld.Statuses().Single(s => s.Id == 2674).Remaining == 8, "Holy Sheltron must spend 50 Oath and show its status.");
        pld.Advance(0.6);
        pld.TryUse(28, false, false, false);
        Check(pld.Stance && pld.Adjust(28) == 32065, "Iron Will must turn into Release Iron Will.");
    }

    private static void Gunbreaker()
    {
        var adjusted = new GunbreakerCombat(2.47);
        adjusted.OverrideRecast(25760, 59.16);
        adjusted.OverrideRecast(16146, 10);
        Check(adjusted.GetBindingCooldown(25760) == (13, 59.16, 1) && adjusted.GetBindingCooldown(16146) == (8, 30, 1),
            "Client recasts must replace skill-speed differences only.");

        var gnb = new GunbreakerCombat();
        Check(!gnb.CanUse(16162, true, true, true), "Burst Strike must need a cartridge.");
        Hit(gnb, 16137); gnb.Advance(2.5); Hit(gnb, 16139); gnb.Advance(2.5); Hit(gnb, 16145);
        Check(gnb.Cartridges == 1 && gnb.Statuses().Single(s => s.Id == 1898).Remaining > 0, "Solid Barrel combo must load a cartridge after Brutal Shell.");
        Check(!gnb.CanUse(16146, true, true, true), "Gnashing Fang must wait for the GCD.");
        gnb.Advance(2.5);
        Hit(gnb, 16146);
        Check(gnb.Cartridges == 0 && gnb.Adjust(16146) == 16147 && gnb.Adjust(16155) == 16156 && Math.Abs(gnb.Timing.Remaining(58) - 2.5) < 1e-9,
            "Gnashing Fang must spend a cartridge, start the GCD and ready Jugular Rip.");
        gnb.Advance(0.6);
        Hit(gnb, 16155);
        Check(gnb.Adjust(16155) == 16155, "Jugular Rip must consume its readiness.");
        gnb.Advance(1.9);
        Hit(gnb, 16146);
        Check(gnb.Adjust(16146) == 16150 && gnb.Adjust(16155) == 16157, "Savage Claw must advance the chain and ready Abdomen Tear.");
        gnb.Advance(2.5);
        Hit(gnb, 16137);
        Check(gnb.Adjust(16155) == 16155 && gnb.Adjust(16146) == 16150, "Another weaponskill must drop Continuation but keep the Gnashing Fang chain.");
        gnb.Advance(2.5);
        Hit(gnb, 16146);
        Check(gnb.GnashStep == 0 && gnb.Adjust(16155) == 16158, "Wicked Talon must end the chain and ready Eye Gouge.");
        gnb.Advance(2.5);
        Hit(gnb, 16164);
        Check(gnb.Cartridges == 3, "Bloodfest must load three cartridges.");
        gnb.Advance(0.6);
        Hit(gnb, 16162);
        Check(gnb.Cartridges == 2 && gnb.Adjust(16155) == 25759, "Burst Strike must spend a cartridge and ready Hypervelocity.");
        gnb.Advance(2.5);
        Hit(gnb, 25760, aoe: true);
        Check(gnb.Cartridges == 1 && Math.Abs(gnb.Timing.Remaining(13) - 60) < 1e-9,
            "Double Down must spend a cartridge and go on its own cooldown.");
        gnb.Advance(0.6);
        Check(gnb.TryUse(16138, false, false, true) == null && gnb.Adjust(16138) == 16153, "No Mercy must turn into Sonic Break.");
        gnb.Advance(2.5);
        Hit(gnb, 16138);
        Check(gnb.Adjust(16138) == 16138, "Sonic Break must consume its readiness.");
        gnb.Advance(0.6);
        gnb.TryUse(16151, false, false, true); gnb.Advance(0.6);
        gnb.TryUse(16151, false, false, true); gnb.Advance(0.6);
        Check(!gnb.CanUse(16151, false, false, true), "Aurora must have two charges.");
        gnb.TryUse(16142, false, false, false);
        Check(gnb.Stance && gnb.Adjust(16142) == 32068, "Royal Guard must turn into Release Royal Guard.");
    }

    private static void Hit(IJobCombat job, uint id, bool aoe = false, bool gapCloser = false)
    {
        var expected = job.Adjust(id);
        var hit = job.TryUse(id, true, true, true);
        if (hit is not { } h || h.ActionId != expected || h.IsAoe != aoe || h.GapCloser != gapCloser)
            throw new Exception($"Expected hit {expected} (from {id}) aoe={aoe} gapCloser={gapCloser}; got {hit?.ToString() ?? "null"}. {job.DebugState}");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
