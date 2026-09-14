using AnoMech.Core.Combat;

internal static class DarkKnightCombatChecks
{
    public static void Run()
    {
        var drk = new DarkKnightCombat();
        Check(drk.Adjust(3625) == 7390 && drk.Adjust(16466) == 16469 && drk.Adjust(16467) == 16470 && drk.Supports(16467),
            "Blood Weapon, Flood and Edge of Darkness must translate to their level-90 actions.");

        // Single-target combo: Souleater grants 20 Blood only after Syphon Strike.
        Hit(drk, 3617);
        Check(drk.IsHighlighted(3623) && !drk.IsHighlighted(3632) && !drk.IsHighlighted(3617), "Only the next combo step must glow.");
        drk.Advance(2.5);
        Hit(drk, 3623); drk.Advance(2.5);
        Check(drk.ComboAction == 3623, "Syphon Strike must continue the combo.");
        Hit(drk, 3624); drk.Advance(2.5);
        Check(drk.ComboAction == 3623, "Unmend must preserve the combo.");
        Hit(drk, 3632); drk.Advance(2.5);
        Check(drk.Blood == 20 && drk.ComboAction == 0, "Souleater combo must grant 20 Blood and end the combo.");
        Hit(drk, 3632); drk.Advance(2.5);
        Check(drk.Blood == 20, "Souleater without combo must not grant Blood.");

        // AoE combo needs an enemy hit for gains.
        Check(drk.TryUse(3621, false, false, true) == null && drk.ComboAction == 0, "Empty Unleash must not start a combo.");
        drk.Advance(2.5);
        Hit(drk, 3621, aoe: true); drk.Advance(2.5);
        Hit(drk, 16468, aoe: true); drk.Advance(2.5);
        Check(drk.Blood == 40, "Stalwart Soul combo must grant 20 Blood.");

        // Spenders are gated by 50 Blood, cap at 100.
        Check(!drk.CanUse(7392, true, true, true), "Bloodspiller must need 50 Blood.");
        for (var i = 0; i < 4; i++) { Hit(drk, 3617); drk.Advance(2.5); Hit(drk, 3623); drk.Advance(2.5); Hit(drk, 3632); drk.Advance(2.5); }
        Check(drk.Blood == 100, "Blood must cap at 100.");
        Hit(drk, 7392); drk.Advance(2.5);
        Check(drk.Blood == 50, "Bloodspiller must cost 50 Blood.");

        // Delirium: three free spenders, Blood Weapon +10 per GCD hit.
        drk.Reset();
        Check(drk.TryUse(7390, false, false, true) == null && drk.DeliriumStacks == 3 && drk.BloodWeaponStacks == 3, "Delirium must grant both three-stack buffs.");
        drk.Advance(0.6);
        Hit(drk, 7392); drk.Advance(2.5);
        Check(drk.Blood == 10 && drk.DeliriumStacks == 2 && drk.BloodWeaponStacks == 2, "Free Bloodspiller must use a Delirium stack and gain 10 Blood.");
        Check(drk.TryUse(7391, false, false, true) == null && drk.DeliriumStacks == 1 && drk.BloodWeaponStacks == 2, "Empty Quietus spends Delirium but gains nothing.");
        drk.Advance(15);
        Check(drk.DeliriumStacks == 0 && drk.BloodWeaponStacks == 0 && drk.Statuses().All(s => s.Remaining == 0), "Delirium buffs must expire.");

        // Darkside extends by 30 up to 60; Shadowbringer needs Darkside and has two charges.
        drk.Reset();
        Check(!drk.CanUse(25757, true, true, true), "Shadowbringer must need Darkside.");
        Hit(drk, 16470); drk.Advance(1);
        Hit(drk, 16469, aoe: true); drk.Advance(1);
        Hit(drk, 16470);
        Check(drk.DarksideRemaining == 60, "Darkside must cap at 60 seconds.");
        Check(drk.Mp == 1000 && !drk.CanUse(16470, true, true, true, checkTiming: false), "Edge of Shadow must cost 3000 MP and need enough MP.");
        drk.Advance(3);
        Check(drk.Mp == 1200, "MP must regenerate 200 every 3 seconds.");
        drk.Reset();
        Check(drk.Mp == JobCombatBase.MaxMp, "A fresh run must start with full MP.");
        Hit(drk, 16470); drk.Advance(1);
        drk.Advance(1);
        Hit(drk, 25757, aoe: true); drk.Advance(0.6);
        Hit(drk, 25757, aoe: true); drk.Advance(0.6);
        Check(!drk.CanUse(25757, true, true, true), "Shadowbringer must have two charges.");

        // Salt and Darkness needs Salted Earth; Living Shadow sets the gauge timer.
        Check(!drk.CanUse(25755, true, true, true), "Salt and Darkness must need Salted Earth.");
        drk.TryUse(3639, false, false, true); drk.Advance(0.6);
        Hit(drk, 25755, aoe: true); drk.Advance(0.6);
        drk.TryUse(16472, false, false, true);
        Check(drk.ShadowRemaining == 20, "Living Shadow must start a 20 second timer.");

        // Shadowstride: two charges, no use while bound, Unmend reduces recast by 5 seconds.
        drk.Reset();
        Check(!drk.CanUse(36926, true, true, true, bound: true), "Shadowstride must be blocked while bound.");
        Hit(drk, 36926, gapCloser: true); drk.Advance(0.6);
        Hit(drk, 36926, gapCloser: true); drk.Advance(0.6);
        Check(!drk.CanUse(36926, true, true, true), "Shadowstride must have two charges.");
        drk.Advance(1.9);
        Hit(drk, 3624);
        Check(Math.Abs(drk.Timing.Remaining(8) - (30 - 0.6 - 0.6 - 1.9 - 5)) < 1e-6, "Unmend must reduce Shadowstride recast by 5 seconds.");

        // Shared cooldown and death rejection.
        drk.Reset();
        Hit(drk, 3643); drk.Advance(0.6);
        Check(!drk.CanUse(3641, true, true, true), "Carve and Spit and Abyssal Drain must share a cooldown.");
        Check(drk.Actions.All(a => !drk.CanUse(a, true, true, true, alive: false)), "Every action must be rejected while dead.");
        // Salted Earth substitutes into Salt and Darkness only while active.
        drk.Reset();
        Check(drk.Adjust(3639) == 3639, "Salted Earth must stay itself when not active.");
        drk.TryUse(3639, false, false, true); drk.Advance(0.6);
        Check(drk.Adjust(3639) == 25755, "Salted Earth must turn into Salt and Darkness while active.");
        Check(drk.GetBindingCooldown(3639) == (16, 90, 1), "Native binding must keep Salted Earth's own cooldown while substituted.");
        drk.Advance(15);
        Check(drk.Adjust(3639) == 3639, "Salted Earth must turn back after it expires.");

        // Grit toggles through Release Grit, is permanent, survives Reset and is seeded from the client.
        drk.Reset();
        drk.TryUse(3629, false, false, false); drk.Advance(2);
        Check(drk.Grit && drk.Adjust(3629) == 32067, "Grit must turn into Release Grit.");
        Check(drk.Statuses().Single(s => s.Id == 743).Remaining == double.PositiveInfinity, "Grit must mirror as a permanent status.");
        drk.Reset();
        Check(drk.Grit, "Grit must survive a duty reset.");
        drk.TryUse(3629, false, false, false);
        Check(!drk.Grit && drk.Statuses().Single(s => s.Id == 743).Remaining == 0, "Release Grit must remove the stance.");
        var seeded = new DarkKnightCombat();
        seeded.Seed(id => id == 743);
        Check(seeded.Grit, "Grit must be seeded from the client status.");

        // Defensive and role actions: castable, status and cooldown only.
        drk.Reset();
        Check(drk.TryUse(7393, false, false, true) == null && drk.Mp == JobCombatBase.MaxMp - 3000
            && drk.Statuses().Single(s => s.Id == 1178).Remaining == 7, "The Blackest Night must cost 3000 MP and show its shield.");
        drk.Advance(0.6);
        drk.TryUse(25754, false, false, true); drk.Advance(0.6);
        drk.TryUse(25754, false, false, true); drk.Advance(0.6);
        Check(!drk.CanUse(25754, false, false, true), "Oblation must have two charges.");
        drk.TryUse(7531, false, false, true);
        Check(drk.Statuses().Single(s => s.Id == 1191).Remaining == 20, "Rampart must show for 20 seconds.");
        drk.Advance(0.6);
        Check(!drk.CanUse(7533, false, false, true) && drk.CanUse(7533, true, true, true), "Provoke must need an enemy target.");
        Hit(drk, 7533);
        Check(!drk.CanUse(7533, true, true, true), "Provoke must go on cooldown.");
        Console.WriteLine("PASS: Dark Knight combos, Blood gauge, Delirium, Darkside, charges, derived, defensive and role actions.");
    }

    private static void Hit(DarkKnightCombat drk, uint id, bool aoe = false, bool gapCloser = false)
    {
        var hit = drk.TryUse(id, true, true, true);
        if (hit is not { } h || h.ActionId != drk.Adjust(id) || h.IsAoe != aoe || h.GapCloser != gapCloser)
            throw new Exception($"Expected Dark Knight hit {id} aoe={aoe} gapCloser={gapCloser}; got {hit}.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
