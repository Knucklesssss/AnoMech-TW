using AnoMech.Core.Combat;

internal static class CombatTimingChecks
{
    public static void Run()
    {
        var type = typeof(CombatTimingChecks).Assembly.GetType("AnoMech.Core.Combat.CombatTiming");
        if (type == null) throw new Exception("Shared combat timing state is missing.");
        SerialChargeRecovery();
        RejectedUseDuringSharedLockDoesNotMutate();
        ResetRestoresChargesAndClearsLock();
        LargeAdvanceRecoversEveryDueCharge();
        InvalidInputsAreRejectedBeforeMutation();
        ChargeGroupContractCannotChange();
        Console.WriteLine("PASS: shared combat timing serial charges, lock boundaries, reset, validation and large steps.");
    }

    private static void SerialChargeRecovery()
    {
        var timing = new CombatTiming();
        if (timing.Charges(7, 30, 3) != 3 || timing.Remaining(7) != 0)
            throw new Exception("An unseen charge group must start full with no remaining recharge time.");
        if (!timing.TryUse(7, 30, 3, 0.6) || timing.Charges(7, 30, 3) != 2 || timing.Remaining(7) != 30)
            throw new Exception("The first use must consume one charge and start its recharge.");
        timing.Advance(0.6);
        if (!timing.TryUse(7, 30, 3, 0.6)) throw new Exception("The second charge must be usable after the lock.");
        timing.Advance(0.6);
        if (!timing.TryUse(7, 30, 3, 0.6)) throw new Exception("The third charge must be usable after the lock.");
        timing.Advance(28.8);
        if (timing.Charges(7, 30, 3) != 1 || timing.Remaining(7) != 30)
            throw new Exception("Serial recovery must grant the first charge at 30 seconds and queue the next for 60.");
        timing.Advance(30);
        if (timing.Charges(7, 30, 3) != 2 || timing.Remaining(7) != 30)
            throw new Exception("Serial recovery must grant the second charge at 60 seconds.");
        timing.Advance(30);
        if (timing.Charges(7, 30, 3) != 3 || timing.Remaining(7) != 0)
            throw new Exception("Serial recovery must grant the final charge at 90 seconds.");
    }

    private static void RejectedUseDuringSharedLockDoesNotMutate()
    {
        var timing = new CombatTiming();
        if (!timing.TryUse(7, 30, 3, 0.6)) throw new Exception("The first use must succeed.");
        timing.Advance(0.1);
        if (timing.TryUse(8, 5, 2, 0.2)) throw new Exception("Animation lock must be shared across charge groups.");
        if (timing.Charges(7, 30, 3) != 2 || timing.Charges(8, 10, 4) != 4 || Math.Abs(timing.Remaining(7) - 29.9) > 1e-9)
            throw new Exception("A lock-rejected use must not register a group, consume a charge or delay recovery.");
        timing.Advance(29.9);
        if (timing.Charges(7, 30, 3) != 3)
            throw new Exception("A rejected use must leave the original 30-second recovery boundary unchanged.");
    }

    private static void ResetRestoresChargesAndClearsLock()
    {
        var timing = new CombatTiming();
        timing.TryUse(7, 30, 3, 0.6);
        timing.Advance(0.1);
        timing.Reset();
        if (timing.Charges(7, 30, 3) != 3 || timing.Remaining(7) != 0)
            throw new Exception("Reset must restore every charge group to full.");
        if (!timing.TryUse(8, 30, 3, 0.6))
            throw new Exception("Reset must clear the shared animation lock.");
    }

    private static void LargeAdvanceRecoversEveryDueCharge()
    {
        var timing = new CombatTiming();
        for (var i = 0; i < 3; i++)
            if (!timing.TryUse(7, 30, 3, 0)) throw new Exception("All three charges must be usable without animation lock.");
        timing.Advance(90);
        if (timing.Charges(7, 30, 3) != 3 || timing.Remaining(7) != 0)
            throw new Exception("A large time step must recover every serial charge that became due.");
    }

    private static void InvalidInputsAreRejectedBeforeMutation()
    {
        var timing = new CombatTiming();
        timing.TryUse(7, 30, 3, 0.6);
        foreach (var seconds in new[] { -1d, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            ExpectRejected(() => timing.Advance(seconds), "invalid elapsed time");
        if (timing.Remaining(7) != 30) throw new Exception("Rejected time advances must not move the clock.");

        ExpectRejected(() => timing.TryUse(-1, 20, 4, 0), "negative group");
        foreach (var recast in new[] { -1d, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            ExpectRejected(() => timing.TryUse(8, recast, 4, 0), "invalid recast");
        ExpectRejected(() => timing.TryUse(8, 20, -1, 0), "negative max charges");
        foreach (var animationLock in new[] { -1d, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            ExpectRejected(() => timing.TryUse(8, 20, 4, animationLock), "invalid animation lock");
        if (timing.Charges(8, 20, 4) != 4) throw new Exception("Rejected uses must not create or consume a charge group.");

        ExpectRejected(() => timing.Charges(-1, 20, 4), "negative queried group");
        ExpectRejected(() => timing.Charges(9, double.NaN, 4), "non-finite queried recast");
        ExpectRejected(() => timing.Charges(9, 20, -1), "negative queried max charges");
        if (timing.Charges(9, 10, 2) != 2) throw new Exception("Rejected queries must not create a charge group.");
        ExpectRejected(() => timing.Remaining(-1), "negative remaining group");
    }

    private static void ChargeGroupContractCannotChange()
    {
        var timing = new CombatTiming();
        timing.TryUse(7, 30, 3, 0);
        ExpectContractRejected(() => timing.Charges(7, 29, 3));
        ExpectContractRejected(() => timing.Charges(7, 30, 4));
        ExpectContractRejected(() => timing.TryUse(7, 29, 3, 0));
        if (timing.Charges(7, 30, 3) != 2 || timing.Remaining(7) != 30)
            throw new Exception("A rejected group contract change must not reset or mutate its recovery.");
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
        throw new Exception($"Combat timing must reject {description}.");
    }

    private static void ExpectContractRejected(Action action)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }
        throw new Exception("A charge group's recast and maximum charges must remain immutable.");
    }
}
