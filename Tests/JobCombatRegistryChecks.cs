using AnoMech.Core.Combat;

internal static class JobCombatRegistryChecks
{
    public static void Run()
    {
        var entry = JobCombatRegistry.Find(21, 90) ?? throw new Exception("Warrior 90 must be registered.");
        if (JobCombatRegistry.Find(32, 90) == null || JobCombatRegistry.Find(32, 70) != null || JobCombatRegistry.Find(21, 70) != null || JobCombatRegistry.Find(19, 90) != null || JobCombatRegistry.Find(0, 90) != null)
            throw new Exception("Only registered job/level pairs may start local combat.");
        foreach (var registered in JobCombatRegistry.Entries)
        {
            var job = registered.CreateRules(2.5);
            if (job.Actions.Count == 0 || !job.Actions.Contains(registered.GcdProbeAction) || job.Actions.Distinct().Count() != job.Actions.Count)
                throw new Exception($"Job {registered.ClassJob} must list unique actions including its GCD probe.");
            if (job.Actions.Any(a => !job.Supports(a)))
                throw new Exception($"Job {registered.ClassJob} lists an unsupported action.");
            if (JobCombatRegistry.Entries.Count(e => e.ClassJob == registered.ClassJob && e.Level == registered.Level) != 1)
                throw new Exception("Duplicate job registry entry.");
        }

        uint[] actions = [31, 37, 42, 45, 41, 16462, 46, 3549, 3550, 16465, 16463, 25753, 7386, 7387, 25752, 52, 7389];
        var rules = entry.CreateRules(2.5);
        if (entry.GcdProbeAction != 31 || !rules.Actions.SequenceEqual(actions))
            throw new Exception("Warrior action list or GCD probe changed.");
        if (!actions.Where(rules.IsSelfAction).SequenceEqual(new uint[] { 41, 16462, 3550, 16463, 25752, 52, 7389 }))
            throw new Exception("Warrior self actions must match the previous hard-coded session lists.");
        if (!actions.Where(rules.IsGapCloser).SequenceEqual(new uint[] { 25753, 7386 }))
            throw new Exception("Warrior gap closers must match the previous hard-coded session list.");
        if (!rules.StatusIds.SequenceEqual(new ushort[] { 1177, 1897, 2677, 2624 }))
            throw new Exception("Warrior mirrored status IDs changed.");
        if (rules.Statuses().Any(s => s.Remaining > 0))
            throw new Exception("A fresh Warrior must not mirror active statuses.");

        rules.TryUse(7389, false, false, true);
        var afterRelease = rules.Statuses().ToDictionary(s => s.Id);
        if (afterRelease[1177] != new JobStatus(1177, 15, 3) || afterRelease[2624] != new JobStatus(2624, 30, 0)
            || afterRelease[1897].Remaining != 0 || afterRelease[2677].Remaining != 0)
            throw new Exception("Inner Release must mirror three stacks and Primal Rend Ready.");

        rules.Advance(1);
        for (var i = 0; i < 3; i++)
        {
            if (rules.TryUse(3549, true, true, true) == null) throw new Exception("Inner Release stack must allow Fell Cleave.");
            if (i < 2) rules.Advance(2.6);
        }
        var spent = rules.Statuses().ToDictionary(s => s.Id);
        if (((WarriorCombat)rules).InnerReleaseRemaining <= 0 || spent[1177] != new JobStatus(1177, 0, 0))
            throw new Exception("Inner Release with zero stacks must be removed while its timer is still running.");
        Console.WriteLine("PASS: job registry resolves Warrior 90 and Dark Knight 90 and exposes Warrior's local combat contract.");
    }
}
