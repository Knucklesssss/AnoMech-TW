using AnoMech.Core.Combat;

internal static class HealerCombatChecks
{
    public static void Run()
    {
        foreach (var classJob in new byte[] { 24, 28, 33, 40 })
            Check(JobCombatRegistry.Find(classJob, 90) != null, $"Healer job {classJob} level 90 must be registered.");
        RoleActions();
        WhiteMage();
        Scholar();
        Astrologian();
        Sage();
        Console.WriteLine("PASS: healer registry, role actions, White Mage, Scholar, Astrologian and Sage gauges, casts and substitutions.");
    }

    private static void RoleActions()
    {
        var whm = new WhiteMageCombat();
        Check(whm.CastTime(25859) == 1.5 && whm.TryUse(7561, false, false, true) == null && whm.CastTime(25859) == 0,
            "Swiftcast must make the next spell instant.");
        whm.Advance(0.6);
        Hit(whm, 25859);
        Check(whm.CastTime(25859) == 1.5 && whm.Mp == JobCombatBase.MaxMp - 400, "The instant spell must spend Swiftcast and its MP.");
        whm.Advance(2.5);
        whm.TryUse(7562, false, false, true);
        whm.Advance(3);
        Check(whm.Mp == JobCombatBase.MaxMp - 400 + 200 + 550 || whm.Mp == JobCombatBase.MaxMp, "Lucid Dreaming must restore MP each tick.");
        Check(!whm.CanUse(16560, false, false, true) && whm.CastTime(16560) == 2.5, "Repose needs an enemy and casts 2.5 s.");
    }

    private static void WhiteMage()
    {
        var whm = new WhiteMageCombat();
        Check(whm.Adjust(119) == 25859 && whm.Adjust(139) == 25860 && whm.Adjust(121) == 16532, "Stone, Holy and Aero must upgrade to level 90.");
        Check(!whm.CanUse(16531, false, false, true), "Afflatus Solace must need a lily.");
        whm.Advance(20);
        Check(whm.Lilies == 1 && whm.TryUse(16531, false, false, true) == null && whm.Lilies == 0 && whm.BloodLilies == 1,
            "A lily every 20 s; Solace spends it and grows the blood lily.");
        whm.Advance(40);
        whm.TryUse(16534, false, false, true); whm.Advance(2.5);
        whm.TryUse(16531, false, false, true); whm.Advance(2.5);
        Check(whm.BloodLilies == 3 && whm.IsHighlighted(16535), "Three lily heals must bloom Afflatus Misery.");
        Hit(whm, 16535, aoe: true);
        Check(whm.BloodLilies == 0, "Misery must consume the blood lily.");
        whm.Advance(2.5);
        whm.TryUse(7430, false, false, true);
        whm.Advance(0.6);
        var mp = whm.Mp;
        Check(whm.TryUse(137, false, false, true) == null && whm.Mp == mp, "Thin Air must make the next spell free.");
        whm.Advance(2.5);
        Check(whm.TryUse(136, false, false, true) == null && Math.Abs(whm.CastTime(135) - 1.6) < 1e-9, "Presence of Mind must shorten casts by 20%.");
        whm.Advance(0.6);
        whm.TryUse(25862, false, false, true);
        Check(whm.Adjust(25862) == 28509, "Liturgy of the Bell must turn into its release.");
    }

    private static void Scholar()
    {
        var sch = new ScholarCombat();
        Check(!sch.CanUse(166, false, false, false) && !sch.CanUse(167, true, true, true), "Aetherflow needs combat; Energy Drain needs a stack.");
        Check(sch.TryUse(166, false, false, true) == null && sch.Aetherflow == 3, "Aetherflow must grant three stacks.");
        sch.Advance(0.6);
        Hit(sch, 167);
        Check(sch.Aetherflow == 2 && sch.FairyGauge == 10, "Energy Drain must spend a stack and fill the Faerie Gauge.");
        sch.Advance(1);
        sch.TryUse(16542, false, false, true);
        sch.Advance(0.6);
        Check(sch.TryUse(7434, false, false, true) == null && sch.Aetherflow == 2, "Recitation must make Excogitation free.");
        sch.Advance(0.6);
        sch.TryUse(16545, false, false, true);
        Check(sch.Adjust(16545) == 16546 && sch.CanUse(16546, false, false, true, checkTiming: false), "Summon Seraph must turn into Consolation.");
        sch.Advance(22);
        Check(sch.Adjust(16545) == 16545, "Consolation must end with Seraph.");
        sch.TryUse(7437, false, false, true);
        Check(sch.Aetherpact && sch.Adjust(7437) == 7869, "Aetherpact must turn into Dissolve Union.");
        sch.Advance(3);
        Check(!sch.Aetherpact, "Aetherpact must stop when the Faerie Gauge drops below 10.");
    }

    private static void Astrologian()
    {
        var ast = new AstrologianCombat();
        Check(ast.Adjust(37019) == 37019 && !ast.CanUse(37019, false, false, true), "Play I does nothing before a draw.");
        ast.TryUse(37017, false, false, true);
        Check(ast.Adjust(37019) == 37023 && ast.Adjust(37022) == 7444 && ast.Adjust(37017) == 37018, "Astral Draw must deal Balance and Lord and turn into Umbral Draw.");
        ast.Advance(0.6);
        ast.TryUse(37019, false, false, true);
        Check(ast.Adjust(37019) == 37019 && ast.Statuses().Single(s => s.Id == 3887).Remaining == 15, "Playing Balance must consume it and buff.");
        ast.Advance(55);
        ast.TryUse(37017, false, false, true);
        Check(ast.Adjust(37019) == 37026 && ast.Adjust(37021) == 37028, "Umbral Draw must deal Spear and Ewer.");
        ast.Advance(0.6);
        ast.TryUse(3606, false, false, true);
        Check(ast.CastTime(3601) == 0, "Lightspeed must remove 1.5 s casts.");
        ast.Advance(0.6);
        ast.TryUse(7439, false, false, true);
        Check(ast.Adjust(7439) == 8324, "Earthly Star must turn into Stellar Detonation.");
        ast.Advance(0.6);
        Hit(ast, 25874, aoe: true);
        Check(ast.Adjust(25874) == 25875 && Math.Abs(ast.Timing.Remaining(58) - 2.5) < 1e-9, "Macrocosmos must start the GCD and turn into Microcosmos.");
    }

    private static void Sage()
    {
        var sge = new SageCombat();
        Check(sge.Adjust(24283) == 24312 && sge.Adjust(24289) == 24313, "Dosis and Phlegma must upgrade to level 90.");
        sge.TryUse(24290, false, false, true);
        Check(sge.Adjust(24283) == 24314 && sge.Adjust(24284) == 24291, "Eukrasia must turn Dosis and Diagnosis into their Eukrasian forms.");
        sge.Advance(2.5);
        Hit(sge, 24283);
        Check(!sge.EukrasiaActive && Math.Abs(sge.Timing.Remaining(58) - 1.5) < 1e-9, "Eukrasian Dosis must end Eukrasia on a 1.5 s GCD.");
        Check(!sge.CanUse(24298, false, false, true), "Kerachole needs Addersgall.");
        sge.Advance(40);
        Check(sge.Addersgall == 2, "Addersgall must fill every 20 s.");
        sge.TryUse(24298, false, false, true); sge.Advance(0.6);
        sge.TryUse(24303, false, false, true);
        var statuses = sge.Statuses().ToDictionary(s => s.Id);
        Check(sge.Addersgall == 0 && statuses[2618].Remaining == 0 && statuses[2619].Remaining == 15, "Taurochole must replace Kerachole.");
        Check(!sge.CanUse(24316, true, true, true, checkTiming: false), "Toxikon stays unusable without simulated Addersting.");
        sge.Advance(0.6);
        sge.TryUse(24285, false, false, true);
        sge.Reset();
        Check(sge.Statuses().Single(s => s.Id == 2604).Remaining == double.PositiveInfinity && !sge.EukrasiaActive,
            "Kardia must survive a duty reset; Eukrasia must not.");
        Hit(sge, 24313, aoe: true); sge.Advance(2.5);
        Hit(sge, 24313, aoe: true); sge.Advance(2.5);
        Check(!sge.CanUse(24313, true, true, true), "Phlegma must have two charges.");
    }

    private static void Hit(IJobCombat job, uint id, bool aoe = false)
    {
        var expected = job.Adjust(id);
        var hit = job.TryUse(id, true, true, true);
        if (hit is not { } h || h.ActionId != expected || h.IsAoe != aoe)
            throw new Exception($"Expected hit {expected} (from {id}) aoe={aoe}; got {hit?.ToString() ?? "null"}. {job.DebugState}");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
