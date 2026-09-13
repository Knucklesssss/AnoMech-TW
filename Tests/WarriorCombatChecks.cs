using AnoMech.Core.Combat;

internal static class WarriorCombatChecks
{
    public static void Run()
    {
        if (typeof(WarriorCombatChecks).Assembly.GetType("AnoMech.Core.Combat.WarriorCombat") == null)
            throw new Exception("Warrior offensive state is missing.");
        PublicCooldownContractsAreAdjustedAndReadOnly();
        ManualPreflightSkipsOnlyTiming();
        SingleTargetComboAdvancesAndGrantsBeast();
        ComboOnlyChangesOnSuccessfulRelevantHits();
        TempestCombosRefreshBuffDuration();
        AoeCombosRequireAnEnemyHitForGains();
        InfuriateUsesSerialChargesAndChaosUpgrade();
        BeastGainsNeverExceedTheGaugeCap();
        BeastSpendersPayCostsAndReduceInfuriateOnHit();
        InnerReleaseDrivesFreeSpendersAndBurstTimers();
        PrimalRendRequiresReadyTargetRangeAndFreedom();
        OnslaughtUsesThreeSerialChargesAndGapCloserRules();
        UpheavalAndOrogenyShareOneCooldownWithoutReadMutation();
        RejectedUsesDoNotMutateStateOrStartLock();
        EverySupportedActionRejectsWhileDead();
        InvalidTimeAndResetAreAtomic();
        OldActionsTranslateAndUnsupportedActionsDoNothing();
        Console.WriteLine("PASS: Warrior offensive action state, resources, cooldowns and rejection boundaries.");
    }

    private static void ManualPreflightSkipsOnlyTiming()
    {
        var type = typeof(WarriorCombat);
        var canUse = type.GetMethod("CanUse", [typeof(uint), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(bool)])
            ?? throw new Exception("Warrior manual-input timing bypass is missing.");
        bool Check(WarriorCombat warrior, uint actionId, bool hasTarget, bool inRange, bool inCombat, bool alive = true, bool bound = false, bool checkTiming = true)
            => (bool)canUse.Invoke(warrior, [actionId, hasTarget, inRange, inCombat, alive, bound, checkTiming])!;

        var warrior = new WarriorCombat();
        AssertHit(warrior.TryUse(31, true, true, false), 31);
        if (Check(warrior, 37, true, true, false) || !Check(warrior, 37, true, true, false, checkTiming: false))
            throw new Exception("Manual preflight must skip the active GCD while normal readiness still enforces it.");
        if (Check(warrior, 37, false, true, false, checkTiming: false) ||
            Check(warrior, 37, true, true, false, alive: false, checkTiming: false) ||
            Check(warrior, 3549, true, true, true, checkTiming: false) ||
            Check(warrior, 52, false, false, false, checkTiming: false) ||
            Check(warrior, 7386, true, true, false, bound: true, checkTiming: false) ||
            Check(warrior, 999999, true, true, true, checkTiming: false))
            throw new Exception("Manual preflight must retain support, target, death, resource, combat and bind gates.");
        if (warrior.TryUse(37, true, true, false) != null || warrior.ComboAction != 31 || warrior.Beast != 0 || warrior.Timing.Remaining(58) != 2.5)
            throw new Exception("Preflight queries and a timing-rejected use must not mutate combo, Beast or cooldown state.");
    }

    private static void PublicCooldownContractsAreAdjustedAndReadOnly()
    {
        var type = typeof(WarriorCombat);
        var supports = type.GetMethod("Supports", [typeof(uint)])
            ?? throw new Exception("Warrior action support query is missing.");
        var getCooldown = type.GetMethod("GetCooldown", [typeof(uint)])
            ?? throw new Exception("Warrior cooldown contract query is missing.");
        bool Supports(WarriorCombat warrior, uint actionId) => (bool)supports.Invoke(warrior, [actionId])!;
        (int Group, double Recast, int Charges) Cooldown(WarriorCombat warrior, uint actionId)
            => ((int Group, double Recast, int Charges))getCooldown.Invoke(warrior, [actionId])!;

        var warrior = new WarriorCombat(2.35);
        if (!Supports(warrior, 49) || Supports(warrior, 999999))
            throw new Exception("Support queries must adjust legacy actions and reject unknown actions.");
        if (Cooldown(warrior, 49) != Cooldown(warrior, 3549) || Cooldown(warrior, 49) != (58, 2.35, 1) ||
            Cooldown(warrior, 31) != (58, 2.35, 1) || Cooldown(warrior, 7386) != (8, 30, 3) ||
            Cooldown(warrior, 52) != (20, 60, 2))
            throw new Exception("Cooldown queries must expose adjusted spender, chosen GCD, Onslaught and Infuriate contracts.");
        try
        {
            _ = Cooldown(warrior, 999999);
            throw new Exception("Unsupported cooldown queries must be rejected.");
        }
        catch (System.Reflection.TargetInvocationException exception) when (exception.InnerException is ArgumentOutOfRangeException)
        {
        }
        if (!warrior.Timing.TryUse(8, 20, 2, 0))
            throw new Exception("Support and cooldown queries must not register cooldown groups.");

        warrior = new WarriorCombat();
        if (Cooldown(warrior, 7387) != (9, 30, 1) || Cooldown(warrior, 25752) != Cooldown(warrior, 7387))
            throw new Exception("Upheaval and Orogeny must expose the same cooldown contract.");
        AssertHit(warrior.TryUse(7387, true, true, false), 7387);
        warrior.Advance(0.6);
        if (warrior.CanUse(25752, false, false, false))
            throw new Exception("Consuming Upheaval must make Orogeny unavailable through their exposed shared group.");
        warrior.Reset();
        AssertHit(warrior.TryUse(25752, true, false, false), 25752, aoe: true);
        warrior.Advance(0.6);
        if (warrior.CanUse(7387, true, true, false))
            throw new Exception("Consuming Orogeny must make Upheaval unavailable through their exposed shared group.");
    }

    private static void BeastGainsNeverExceedTheGaugeCap()
    {
        var warrior = new WarriorCombat();
        warrior.TryUse(52, false, false, true);
        warrior.Advance(0.6);
        warrior.TryUse(52, false, false, true);
        warrior.Advance(2.5);
        warrior.TryUse(31, true, true, true);
        warrior.Advance(2.5);
        warrior.TryUse(37, true, true, true);
        warrior.Advance(2.5);
        warrior.TryUse(42, true, true, true);
        if (warrior.Beast != 100)
            throw new Exception("Combo Beast gains must respect the 100-point gauge cap.");
    }

    private static void EverySupportedActionRejectsWhileDead()
    {
        uint[] actions = [31, 37, 42, 45, 41, 16462, 46, 3549, 3550, 16465, 16463, 25753, 7386, 7387, 25752, 52, 7389];
        foreach (var action in actions)
        {
            var warrior = new WarriorCombat();
            if (warrior.CanUse(action, true, true, true, alive: false) ||
                warrior.TryUse(action, true, true, true, alive: false) != null || warrior.Timing.LockRemaining != 0)
                throw new Exception($"Supported action {action} must reject a dead actor without mutation.");
        }
    }

    private static void InvalidTimeAndResetAreAtomic()
    {
        foreach (var gcd in new[] { -1d, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            ExpectRejected(() => _ = new WarriorCombat(gcd), "invalid Warrior GCD");

        var warrior = new WarriorCombat();
        warrior.TryUse(31, true, true, false);
        foreach (var seconds in new[] { -1d, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            ExpectRejected(() => warrior.Advance(seconds), "invalid Warrior elapsed time");
        if (warrior.ComboAction != 31 || warrior.ComboRemaining != 30 || warrior.Timing.LockRemaining != 0.6)
            throw new Exception("Rejected Warrior time advances must not mutate timers or lock state.");

        warrior.Reset();
        warrior.TryUse(7386, true, true, false);
        warrior.Advance(0.6);
        warrior.TryUse(7389, false, false, false);
        warrior.Advance(0.6);
        warrior.TryUse(52, false, false, true);
        warrior.Reset();
        if (warrior.Beast != 0 || warrior.ComboAction != 0 || warrior.ComboRemaining != 0 ||
            warrior.InnerReleaseStacks != 0 || warrior.InnerReleaseRemaining != 0 || warrior.ChaosRemaining != 0 ||
            warrior.TempestRemaining != 0 || warrior.RendRemaining != 0 || warrior.Timing.LockRemaining != 0)
            throw new Exception("Reset must remove all Warrior offensive state and shared animation lock.");
        if (!warrior.CanUse(7386, true, true, false) || !warrior.CanUse(7389, false, false, false) ||
            !warrior.CanUse(52, false, false, true))
            throw new Exception("Reset must restore all Warrior cooldown charges.");
    }

    private static void OnslaughtUsesThreeSerialChargesAndGapCloserRules()
    {
        var warrior = new WarriorCombat();
        if (warrior.CanUse(7386, false, true, false) || warrior.CanUse(7386, true, false, false) ||
            warrior.CanUse(7386, true, true, false, bound: true))
            throw new Exception("Onslaught must require a target in range and reject use while bound.");
        AssertHit(warrior.TryUse(7386, true, true, false), 7386, gapCloser: true);
        if (warrior.TryUse(7386, true, true, false) != null)
            throw new Exception("The shared animation lock must prevent a second activation from consuming another Onslaught charge.");
        warrior.Advance(0.6);
        AssertHit(warrior.TryUse(7386, true, true, false), 7386, gapCloser: true);
        warrior.Advance(0.6);
        AssertHit(warrior.TryUse(7386, true, true, false), 7386, gapCloser: true);
        warrior.Advance(0.6);
        if (warrior.CanUse(7386, true, true, false))
            throw new Exception("Onslaught must be unavailable after all three serial charges are spent.");
        warrior.Advance(28.199);
        if (warrior.CanUse(7386, true, true, false))
            throw new Exception("Onslaught must remain unavailable immediately before the first 30-second recovery boundary.");
        warrior.Advance(0.001);
        if (!warrior.CanUse(7386, true, true, false))
            throw new Exception("The first Onslaught charge must recover exactly 30 seconds after the first use.");
    }

    private static void UpheavalAndOrogenyShareOneCooldownWithoutReadMutation()
    {
        var warrior = new WarriorCombat();
        AssertHit(warrior.TryUse(7387, true, true, false), 7387);
        warrior.Advance(0.6);
        if (warrior.CanUse(25752, false, false, false) || warrior.CanUse(25752, false, false, false) ||
            warrior.TryUse(25752, false, false, false) != null || warrior.CanUse(25752, false, false, false))
            throw new Exception("Orogeny must share Upheaval's unavailable 30-second cooldown across repeated reads and a failed use.");
        warrior.Advance(29.399);
        if (warrior.CanUse(25752, true, false, false))
            throw new Exception("The shared Upheaval/Orogeny cooldown must remain unavailable immediately before 30 seconds.");
        warrior.Advance(0.001);
        if (!warrior.CanUse(25752, false, false, false))
            throw new Exception("Self-AoE Orogeny readiness must not require a selected target at the exact cooldown boundary.");
        AssertHit(warrior.TryUse(25752, true, false, false), 25752, aoe: true);
    }

    private static void RejectedUsesDoNotMutateStateOrStartLock()
    {
        var warrior = new WarriorCombat();
        if (warrior.TryUse(31, true, true, false, alive: false) != null ||
            warrior.TryUse(31, false, true, false) != null || warrior.TryUse(31, true, false, false) != null)
            throw new Exception("Dead, targetless or out-of-range targeted attacks must be rejected.");
        if (warrior.Beast != 0 || warrior.ComboAction != 0 || warrior.Timing.LockRemaining != 0)
            throw new Exception("Rejected attacks must not mutate resources, combo or animation lock.");
        AssertHit(warrior.TryUse(31, true, true, false), 31);
        if (warrior.Timing.LockRemaining != 0.6)
            throw new Exception("Every successful instant activation must apply the common 0.6-second animation lock once.");
    }

    private static void InnerReleaseDrivesFreeSpendersAndBurstTimers()
    {
        var warrior = new WarriorCombat();
        if (!warrior.CanUse(7389, false, false, false) || warrior.TryUse(7389, false, false, false) != null)
            throw new Exception("Inner Release must be a targetless self activation, not a damage hit.");
        if (warrior.InnerReleaseStacks != 3 || warrior.InnerReleaseRemaining != 15 || warrior.RendRemaining != 30 || warrior.TempestRemaining != 0)
            throw new Exception("Inner Release must grant three 15-second stacks and 30 seconds of Rend Ready without creating Tempest.");
        warrior.Advance(0.6);
        AssertHit(warrior.TryUse(3549, true, true, false), 3549);
        if (warrior.Beast != 0 || warrior.InnerReleaseStacks != 2)
            throw new Exception("Inner Release Fell Cleave must consume a stack instead of Beast and guarantee crit/direct hit.");
        warrior.Advance(2.5);
        AssertHit(warrior.TryUse(3550, true, false, false), 3550, aoe: true);
        if (warrior.Beast != 0 || warrior.InnerReleaseStacks != 1)
            throw new Exception("Inner Release Decimate must consume a stack instead of Beast and guarantee crit/direct hit.");

        warrior.Reset();
        warrior.TryUse(7389, false, false, false);
        warrior.Advance(0.6);
        warrior.TryUse(52, false, false, true);
        warrior.Advance(0.6);
        AssertHit(warrior.TryUse(3549, true, true, true), 16465);
        if (warrior.InnerReleaseStacks != 3 || warrior.Beast != 0 || warrior.ChaosRemaining != 0)
            throw new Exception("A Chaos spender must retain its 50-Beast cost and must not consume an Inner Release stack.");

        warrior.Reset();
        warrior.TryUse(7389, false, false, false);
        warrior.Advance(14.999);
        if (warrior.InnerReleaseStacks != 3 || warrior.InnerReleaseRemaining <= 0)
            throw new Exception("Inner Release must remain active immediately before 15 seconds.");
        warrior.Advance(0.001);
        if (warrior.InnerReleaseStacks != 0 || warrior.InnerReleaseRemaining != 0)
            throw new Exception("Inner Release stacks must expire exactly at 15 seconds.");
    }

    private static void PrimalRendRequiresReadyTargetRangeAndFreedom()
    {
        var warrior = new WarriorCombat();
        if (warrior.CanUse(25753, true, true, false) || warrior.TryUse(25753, true, true, false) != null)
            throw new Exception("Primal Rend must require Rend Ready.");
        warrior.TryUse(7389, false, false, false);
        warrior.Advance(0.6);
        if (warrior.CanUse(25753, false, true, false) || warrior.CanUse(25753, true, false, false) ||
            warrior.CanUse(25753, true, true, false, bound: true))
            throw new Exception("Primal Rend must require a target in range and reject use while bound.");
        if (warrior.RendRemaining != 29.4)
            throw new Exception("Rejected Primal Rend readiness checks must not consume or refresh Rend Ready.");
        AssertHit(warrior.TryUse(25753, true, true, false), 25753, aoe: true, gapCloser: true);
        if (warrior.RendRemaining != 0)
            throw new Exception("A successful Primal Rend must consume Rend Ready.");

        warrior.Reset();
        warrior.TryUse(31, true, true, false);
        warrior.Advance(2.5);
        warrior.TryUse(37, true, true, false);
        warrior.Advance(2.5);
        warrior.TryUse(45, true, true, false);
        warrior.Advance(2.5);
        warrior.TryUse(31, true, true, false);
        warrior.Advance(2.5);
        warrior.TryUse(37, true, true, false);
        warrior.Advance(2.5);
        warrior.TryUse(45, true, true, false);
        warrior.Advance(0.6);
        warrior.TryUse(7389, false, false, false);
        if (warrior.TempestRemaining != 60)
            throw new Exception("Inner Release must extend only an existing Tempest by 10 seconds and cap it at 60.");
    }

    private static void InfuriateUsesSerialChargesAndChaosUpgrade()
    {
        var warrior = new WarriorCombat();
        if (warrior.CanUse(52, false, false, false) || warrior.TryUse(52, false, false, false) != null || warrior.Beast != 0)
            throw new Exception("Infuriate must require combat and reject without mutation outside combat.");
        if (!warrior.CanUse(52, false, false, true))
            throw new Exception("Infuriate must not require a selected target in combat.");
        warrior.TryUse(52, false, false, true);
        if (warrior.Beast != 50 || warrior.ChaosRemaining != 30 || warrior.Timing.Charges(20, 60, 2) != 1)
            throw new Exception("Infuriate must grant 50 Beast, 30 seconds of Chaos and consume one of two serial charges.");
        warrior.Advance(0.6);
        warrior.TryUse(52, false, false, true);
        if (warrior.Beast != 100 || warrior.Timing.Charges(20, 60, 2) != 0)
            throw new Exception("A second Infuriate must cap Beast at 100 and consume its second charge.");
        warrior.Advance(9.4);
        if (warrior.Adjust(49) != 16465 || warrior.Adjust(3549) != 16465)
            throw new Exception("Active Chaos with 50 Beast must upgrade legacy and current Fell Cleave IDs to Inner Chaos.");
        AssertHit(warrior.TryUse(3549, true, true, true), 16465);
        if (warrior.Beast != 50 || warrior.ChaosRemaining != 0 || warrior.Timing.Remaining(20) != 45)
            throw new Exception("Inner Chaos must cost 50 Beast, consume Chaos and reduce Infuriate recharge by five seconds.");

        warrior.Reset();
        warrior.TryUse(52, false, false, true);
        warrior.Advance(0.6);
        AssertHit(warrior.TryUse(16463, true, false, true), 16463, aoe: true);
        if (warrior.Beast != 0 || warrior.ChaosRemaining != 0 || warrior.Timing.Remaining(20) != 54.4)
            throw new Exception("Chaotic Cyclone must use the same Chaos cost, consumption and Infuriate reduction rules.");
    }

    private static void BeastSpendersPayCostsAndReduceInfuriateOnHit()
    {
        var warrior = new WarriorCombat();
        if (warrior.CanUse(3549, true, true, true) || warrior.CanUse(3549, true, true, true) ||
            warrior.TryUse(3549, true, true, true) != null)
            throw new Exception("A spender without Beast must remain unavailable across repeated read checks and a failed use.");
        if (!warrior.Timing.TryUse(58, 9, 2, 0))
            throw new Exception("Rejected spender checks must not register an unseen cooldown group.");

        warrior.Reset();
        warrior.TryUse(52, false, false, true);
        warrior.Advance(30);
        AssertHit(warrior.TryUse(3549, true, true, true), 3549);
        if (warrior.Beast != 0 || warrior.Timing.Remaining(20) != 25)
            throw new Exception("Fell Cleave must cost 50 Beast and reduce Infuriate recharge by five seconds on hit.");

        warrior.Reset();
        warrior.TryUse(52, false, false, true);
        warrior.Advance(30);
        AssertHit(warrior.TryUse(3550, true, false, true), 3550, aoe: true);
        if (warrior.Beast != 0 || warrior.Timing.Remaining(20) != 25)
            throw new Exception("Decimate must cost 50 Beast and reduce Infuriate recharge by five seconds on hit.");
    }

    private static void TempestCombosRefreshBuffDuration()
    {
        var warrior = new WarriorCombat();
        warrior.TryUse(31, true, true, false);
        warrior.Advance(2.5);
        warrior.TryUse(37, true, true, false);
        warrior.Advance(2.5);
        AssertHit(warrior.TryUse(45, true, true, false), 45);
        if (warrior.Beast != 20 || warrior.TempestRemaining != 30)
            throw new Exception("Combo Storm's Eye must grant 10 Beast and activate 30 seconds of Tempest after its own hit.");
        warrior.Advance(2.5);
        AssertHit(warrior.TryUse(31, true, true, false), 31);
        warrior.Advance(2.5);
        warrior.TryUse(37, true, true, false);
        warrior.Advance(2.5);
        AssertHit(warrior.TryUse(45, true, true, false), 45);
        if (warrior.TempestRemaining != 52.5)
            throw new Exception("Repeated Storm's Eye must extend the existing Tempest by 30 seconds up to 60.");
    }

    private static void AoeCombosRequireAnEnemyHitForGains()
    {
        var warrior = new WarriorCombat();
        AssertHit(warrior.TryUse(41, true, false, false), 41, aoe: true);
        if (warrior.ComboAction != 41) throw new Exception("An Overpower hit must start the AoE combo.");
        warrior.Advance(2.5);
        AssertHit(warrior.TryUse(16462, true, false, false), 16462, aoe: true);
        if (warrior.Beast != 20 || warrior.TempestRemaining != 30 || warrior.ComboAction != 0)
            throw new Exception("Combo Mythril Tempest must grant 20 Beast, activate Tempest and finish the combo.");

        warrior.Reset();
        if (warrior.TryUse(41, false, false, false) != null || warrior.Timing.Remaining(58) != 2.5)
            throw new Exception("An empty self-AoE must activate once without producing a hit.");
        if (warrior.Beast != 0 || warrior.ComboAction != 0 || warrior.TempestRemaining != 0)
            throw new Exception("An empty Overpower must not grant hit-dependent combo, Beast or Tempest state.");
        warrior.Advance(2.5);
        if (warrior.TryUse(16462, false, false, false) != null || warrior.Beast != 0 || warrior.ComboAction != 0 || warrior.TempestRemaining != 0)
            throw new Exception("An empty Mythril Tempest must not grant hit-dependent combo, Beast or Tempest state.");

        warrior.Reset();
        warrior.TryUse(31, true, true, false);
        warrior.Advance(2.5);
        if (warrior.TryUse(41, false, false, false) != null || warrior.ComboAction != 0 || warrior.Beast != 0 || warrior.TempestRemaining != 0)
            throw new Exception("An empty Overpower must clear a nonmatching combo without starting its own combo or granting hit state.");

        warrior.Reset();
        warrior.TryUse(31, true, true, false);
        warrior.Advance(2.5);
        if (warrior.TryUse(16462, false, false, false) != null || warrior.ComboAction != 0 || warrior.Beast != 0 || warrior.TempestRemaining != 0)
            throw new Exception("An empty Mythril Tempest must clear a nonmatching combo without advancing or granting hit state.");
    }

    private static void SingleTargetComboAdvancesAndGrantsBeast()
    {
        var warrior = new WarriorCombat();
        AssertHit(warrior.TryUse(31, true, true, false), 31);
        if (warrior.ComboAction != 31 || warrior.ComboRemaining != 30 || warrior.Beast != 0)
            throw new Exception("Heavy Swing must start a 30-second combo without Beast.");
        warrior.Advance(2.5);
        AssertHit(warrior.TryUse(37, true, true, false), 37);
        if (warrior.ComboAction != 37 || warrior.ComboRemaining != 30 || warrior.Beast != 10)
            throw new Exception("Combo Maim must advance the combo, refresh it to 30 seconds and grant 10 Beast.");
        warrior.Advance(2.5);
        AssertHit(warrior.TryUse(42, true, true, false), 42);
        if (warrior.ComboAction != 0 || warrior.ComboRemaining != 0 || warrior.Beast != 30)
            throw new Exception("Combo Storm's Path must finish the combo and grant 20 Beast.");

        warrior.Reset();
        AssertHit(warrior.TryUse(37, true, true, false), 37);
        if (warrior.Beast != 0 || warrior.ComboAction != 0)
            throw new Exception("Maim without Heavy Swing must not advance the combo or grant Beast.");
    }

    private static void ComboOnlyChangesOnSuccessfulRelevantHits()
    {
        var warrior = new WarriorCombat();
        AssertHit(warrior.TryUse(31, true, true, false), 31);
        warrior.Advance(2.5);
        if (warrior.TryUse(37, false, true, false) != null || warrior.ComboAction != 31 || warrior.ComboRemaining != 27.5)
            throw new Exception("A rejected combo step must not clear or refresh the existing combo.");
        AssertHit(warrior.TryUse(46, true, true, false), 46);
        if (warrior.ComboAction != 31 || warrior.ComboRemaining != 27.5)
            throw new Exception("Tomahawk must preserve the combo without refreshing it.");
        warrior.Advance(2.5);
        AssertHit(warrior.TryUse(42, true, true, false), 42);
        if (warrior.ComboAction != 0)
            throw new Exception("A successful wrong combo weaponskill must clear the prior combo.");

        warrior.Reset();
        warrior.TryUse(31, true, true, false);
        warrior.Advance(0.6);
        warrior.TryUse(7389, false, false, false);
        if (warrior.ComboAction != 31 || warrior.ComboRemaining != 29.4)
            throw new Exception("An off-global-cooldown activation must preserve the combo without refreshing it.");
        warrior.Advance(1.9);
        warrior.TryUse(3549, true, true, false);
        if (warrior.ComboAction != 31 || warrior.ComboRemaining != 27.5)
            throw new Exception("A Beast spender must preserve the combo without refreshing it.");

        warrior.Reset();
        warrior.TryUse(31, true, true, false);
        warrior.Advance(29.999);
        if (warrior.ComboAction != 31 || warrior.ComboRemaining <= 0)
            throw new Exception("A combo must remain available immediately before 30 seconds.");
        warrior.Advance(0.001);
        if (warrior.ComboAction != 0 || warrior.ComboRemaining != 0)
            throw new Exception("A combo must expire exactly at 30 seconds.");
    }

    private static void OldActionsTranslateAndUnsupportedActionsDoNothing()
    {
        var warrior = new WarriorCombat();
        if (warrior.Adjust(49) != 3549 || warrior.Adjust(51) != 3550 || warrior.Adjust(38) != 7389 || warrior.Adjust(31) != 31)
            throw new Exception("Level-90 action upgrades must translate only the verified legacy IDs.");
        warrior.TryUse(31, true, true, false);
        var beforeCombo = warrior.ComboRemaining;
        if (warrior.TryUse(999999, true, true, true) != null || warrior.Beast != 0 || warrior.ComboRemaining != beforeCombo)
            throw new Exception("Unsupported actions must return no hit without changing state.");
    }

    private static void AssertHit(JobHit? actual, uint actionId, bool aoe = false, bool gapCloser = false)
    {
        if (actual is not { } hit || hit.ActionId != actionId ||
            hit.IsAoe != aoe || hit.GapCloser != gapCloser)
            throw new Exception($"Expected Warrior hit {actionId}  aoe={aoe}, gapCloser={gapCloser}; got {actual}.");
    }

    private static void ExpectRejected(Action action, string description)
    {
        try
        {
            action();
        }
        catch (ArgumentOutOfRangeException)
        {
            return;
        }
        throw new Exception($"Expected rejection for {description}.");
    }
}
