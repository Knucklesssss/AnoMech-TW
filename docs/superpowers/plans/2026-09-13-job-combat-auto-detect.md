# Job Combat Auto-Detect Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Start local skill-rotation simulation automatically for whichever registered job/level the player enters the simulation with, with Warrior 90 moved behind a job interface and its behavior unchanged.

**Architecture:** A pure `IJobCombat` interface plus `JobCombatRegistry` (compiled into the console tests) replaces Warrior-specific code in `LocalCombatSession`. `CombatNativeState` keeps only job-neutral native mirroring; each job's gauge is a small `IJobGauge` adapter chosen by a native-side switch. The `EnableLocalCombat` setting and checkbox are removed.

**Tech Stack:** C# / net9.0, Dalamud.NET.Sdk 13 (API 13, Traditional Chinese client), FFXIVClientStructs, Lumina; console regression checks in `Tests/`.

**Spec:** `docs/superpowers/specs/2026-09-13-job-combat-auto-detect-design.md`

## Global Constraints

- Build against FFXIVSimpleLauncher's API 13 Dalamud only: `$env:MSBuildEnableWorkloadResolver = 'false'` and `-p:DalamudLibPath=C:/Users/Knuckles/AppData/Roaming/FFXIVSimpleLauncher/Dalamud/Injector/`. Never "fix" toward the SDK 15 / XIVLauncher text in `CLAUDE.md`.
- The PowerShell tool resets its working directory; use absolute paths (repo root `C:\Users\Knuckles\OneDrive\文件\claude\AnoMech`).
- 本批只抽介面並把戰士搬到介面後面，**不新增任何職業**，戰士行為必須完全不變。不發布、不推送。
- 只做技能循環、派生、連段、量譜、冷卻與按鍵呈現，不做傷害、屬性、治療、護盾、減傷。
- Existing Warrior/input/recast tests must pass unchanged except the `WarriorHit` → `JobHit` type rename.
- `docs/` is in `.gitignore`; stage docs with `git add -f`.
- Commit messages end with `Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>`.

## File Structure

| File | Responsibility |
| --- | --- |
| `AnoMech/Core/Combat/IJobCombat.cs` (create) | Pure job rules contract, `JobHit`, `JobStatus`, `JobCombatEntry`, `JobCombatRegistry` |
| `AnoMech/Core/Combat/WarriorCombat.cs` (modify) | Implements `IJobCombat`; owns Warrior action/self/gap-closer/status lists |
| `AnoMech/Core/Combat/WarriorNativeGauge.cs` (create) | Warrior Beast Gauge capture/write/restore |
| `AnoMech/Core/Combat/CombatNativeState.cs` (modify) | `IJobGauge`, native gauge switch, job-neutral native mirroring/restore |
| `AnoMech/Core/Combat/LocalCombatSession.cs` (modify) | Registry-based start, job-neutral validation/execution |
| `AnoMech/Configuration.cs`, `AnoMech/Windows/ConfigWindow.cs` (modify) | Remove opt-in setting and checkbox |
| `Tests/JobCombatRegistryChecks.cs` (create) | Registry and Warrior contract checks |

---

### Task 1: Pure job interface and registry

**Files:**
- Create: `AnoMech/Core/Combat/IJobCombat.cs`
- Modify: `AnoMech/Core/Combat/WarriorCombat.cs`
- Create test: `Tests/JobCombatRegistryChecks.cs`
- Modify: `Tests/AnoMech.P3.Tests.csproj`, `Tests/Program.cs`, `Tests/WarriorCombatChecks.cs:447`
- Modify: `docs/superpowers/specs/2026-09-13-job-combat-auto-detect-design.md`

**Interfaces:**
- Consumes: existing `CombatTiming`, `WarriorCombat` public members.
- Produces:
  - `public interface IJobCombat` with `IReadOnlyList<uint> Actions`, `IReadOnlyList<ushort> StatusIds`, `CombatTiming Timing`, `uint ComboAction`, `double ComboRemaining`, `uint Adjust(uint)`, `bool Supports(uint)`, `bool IsSelfAction(uint)`, `bool IsGapCloser(uint)`, `(int Group, double Recast, int Charges) GetCooldown(uint)`, `bool CanUse(uint actionId, bool hasTarget, bool inRange, bool inCombat, bool alive = true, bool bound = false, bool checkTiming = true)`, `JobHit? TryUse(uint actionId, bool hasTarget, bool inRange, bool inCombat, bool alive = true, bool bound = false)`, `IEnumerable<JobStatus> Statuses()`, `void Advance(double)`, `void Reset()`
  - `public readonly record struct JobHit(uint ActionId, bool IsAoe, bool GapCloser)`
  - `public readonly record struct JobStatus(ushort Id, double Remaining, ushort Param)`
  - `public sealed record JobCombatEntry(byte ClassJob, byte Level, uint GcdProbeAction, Func<double, IJobCombat> CreateRules)`
  - `public static class JobCombatRegistry { IReadOnlyList<JobCombatEntry> Entries; JobCombatEntry? Find(byte classJob, byte level); }`

- [ ] **Step 1: Write the failing test**

Create `Tests/JobCombatRegistryChecks.cs`:

```csharp
using AnoMech.Core.Combat;

internal static class JobCombatRegistryChecks
{
    public static void Run()
    {
        var entry = JobCombatRegistry.Find(21, 90) ?? throw new Exception("Warrior 90 must be registered.");
        if (JobCombatRegistry.Find(21, 70) != null || JobCombatRegistry.Find(19, 90) != null || JobCombatRegistry.Find(0, 90) != null)
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
        Console.WriteLine("PASS: job registry resolves only Warrior 90 and exposes Warrior's local combat contract.");
    }
}
```

In `Tests/AnoMech.P3.Tests.csproj`, add after the `WarriorCombat.cs` line:

```xml
    <Compile Include="../AnoMech/Core/Combat/IJobCombat.cs" />
```

In `Tests/Program.cs`, change

```csharp
WarriorCombatChecks.Run();
CombatInputChecks.Run();
```

to

```csharp
WarriorCombatChecks.Run();
JobCombatRegistryChecks.Run();
CombatInputChecks.Run();
```

- [ ] **Step 2: Run test to verify it fails**

Run: `$env:MSBuildEnableWorkloadResolver = 'false'; dotnet run --project "C:\Users\Knuckles\OneDrive\文件\claude\AnoMech\Tests\AnoMech.P3.Tests.csproj"`
Expected: build FAIL with `CS2001` (source file `IJobCombat.cs` could not be found) and/or `CS0103`/`CS0246` for `JobCombatRegistry` and `JobStatus`.

- [ ] **Step 3: Write minimal implementation**

Create `AnoMech/Core/Combat/IJobCombat.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace AnoMech.Core.Combat;

public interface IJobCombat
{
    IReadOnlyList<uint> Actions { get; }
    IReadOnlyList<ushort> StatusIds { get; }
    CombatTiming Timing { get; }
    uint ComboAction { get; }
    double ComboRemaining { get; }
    uint Adjust(uint actionId);
    bool Supports(uint actionId);
    // No target needed: self-centred AoEs and self buffs.
    bool IsSelfAction(uint actionId);
    bool IsGapCloser(uint actionId);
    (int Group, double Recast, int Charges) GetCooldown(uint actionId);
    bool CanUse(uint actionId, bool hasTarget, bool inRange, bool inCombat, bool alive = true, bool bound = false, bool checkTiming = true);
    JobHit? TryUse(uint actionId, bool hasTarget, bool inRange, bool inCombat, bool alive = true, bool bound = false);
    // Remaining <= 0 means the status must be absent.
    IEnumerable<JobStatus> Statuses();
    void Advance(double seconds);
    void Reset();
}

public readonly record struct JobHit(uint ActionId, bool IsAoe, bool GapCloser);

public readonly record struct JobStatus(ushort Id, double Remaining, ushort Param);

// GcdProbeAction lives here, not on the rules: the session reads the GCD
// duration from the client before it can construct the rules.
public sealed record JobCombatEntry(byte ClassJob, byte Level, uint GcdProbeAction, Func<double, IJobCombat> CreateRules);

public static class JobCombatRegistry
{
    public static IReadOnlyList<JobCombatEntry> Entries { get; } =
    [
        new(21, 90, 31, gcd => new WarriorCombat(gcd)),
    ];

    public static JobCombatEntry? Find(byte classJob, byte level)
        => Entries.FirstOrDefault(e => e.ClassJob == classJob && e.Level == level);
}
```

Modify `AnoMech/Core/Combat/WarriorCombat.cs`:

1. Replace `using System;` with:

```csharp
using System;
using System.Collections.Generic;
```

2. Replace `public sealed class WarriorCombat` with `public sealed class WarriorCombat : IJobCombat`.

3. After `public CombatTiming Timing { get; } = new();` add:

```csharp
    public IReadOnlyList<uint> Actions { get; } = [31, 37, 42, 45, 41, 16462, 46, 3549, 3550, 16465, 16463, 25753, 7386, 7387, 25752, 52, 7389];
    public IReadOnlyList<ushort> StatusIds { get; } = [1177, 1897, 2677, 2624];

    public IEnumerable<JobStatus> Statuses()
    {
        yield return new(1177, innerReleaseStacks > 0 ? innerReleaseRemaining : 0, (ushort)innerReleaseStacks);
        yield return new(1897, chaosRemaining, 0);
        yield return new(2677, tempestRemaining, 0);
        yield return new(2624, rendRemaining, 0);
    }

    public bool IsGapCloser(uint actionId) => actionId is 25753 or 7386;
```

4. Change the `TryUse` return type from `WarriorHit?` to `JobHit?`, and its final `return new WarriorHit(actionId, isAoe, gapCloser);` to `return new JobHit(actionId, isAoe, gapCloser);`.

5. Change `private static bool IsSelfAction(uint actionId)` to `public bool IsSelfAction(uint actionId)` (body unchanged).

6. Delete the trailing record:

```csharp
public readonly record struct WarriorHit(
    uint ActionId,
    bool IsAoe,
    bool GapCloser);
```

In `Tests/WarriorCombatChecks.cs:447` change `private static void AssertHit(WarriorHit? actual,` to `private static void AssertHit(JobHit? actual,`.

- [ ] **Step 4: Run tests and production build to verify they pass**

Run: `$env:MSBuildEnableWorkloadResolver = 'false'; dotnet run --project "C:\Users\Knuckles\OneDrive\文件\claude\AnoMech\Tests\AnoMech.P3.Tests.csproj" 2>&1 | Select-String 'PASS: Warrior|PASS: job registry|error|Exception'`
Expected: `PASS: Warrior offensive action state…` and `PASS: job registry resolves only Warrior 90…`, no error/Exception lines. Then run the full command without the filter and confirm it exits 0.

Run: `$env:MSBuildEnableWorkloadResolver = 'false'; dotnet build "C:\Users\Knuckles\OneDrive\文件\claude\AnoMech\AnoMech\AnoMech.csproj" --no-restore '-p:DalamudLibPath=C:/Users/Knuckles/AppData/Roaming/FFXIVSimpleLauncher/Dalamud/Injector/'`
Expected: `0 個錯誤` (the session still compiles against `WarriorCombat`; `JobHit` keeps `GapCloser`).

- [ ] **Step 5: Record the two spec corrections**

In `docs/superpowers/specs/2026-09-13-job-combat-auto-detect-design.md`:

- Delete the bullet `- \`GcdProbeAction\`：用來讀 GCD 長度的技能（戰士為 31）`.
- Replace `註冊表每筆：職業 ID、同步等級、建立規則（傳入 GCD 秒數）、建立量譜轉接。首筆僅「戰士 21、等級 90」。` with:

```markdown
註冊表每筆：職業 ID、同步等級、讀 GCD 長度的技能、建立規則（傳入 GCD 秒數）。首筆僅「戰士 21、等級 90、重劈 31」。讀 GCD 的技能放在註冊資料而非規則，因為會話須先讀出 GCD 才能建立規則。註冊表會編進純規則測試，不能引用原生型別，所以量譜轉接由原生層依職業 ID 選擇。
```

- In 驗收, replace `每筆註冊的 \`GcdProbeAction\` 在 \`Actions\` 內且清單非空` with `每筆註冊的讀 GCD 技能在規則的 \`Actions\` 內且清單非空`.

- [ ] **Step 6: Commit**

```bash
cd "C:/Users/Knuckles/OneDrive/文件/claude/AnoMech"
git add AnoMech/Core/Combat/IJobCombat.cs AnoMech/Core/Combat/WarriorCombat.cs Tests/JobCombatRegistryChecks.cs Tests/AnoMech.P3.Tests.csproj Tests/Program.cs Tests/WarriorCombatChecks.cs
git add -f docs/superpowers/specs/2026-09-13-job-combat-auto-detect-design.md
git commit -m "feat: add local combat job interface and registry

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 2: Job-neutral session, native gauge adapter and auto-detect

**Files:**
- Create: `AnoMech/Core/Combat/WarriorNativeGauge.cs`
- Modify: `AnoMech/Core/Combat/CombatNativeState.cs` (lines 17–230, the whole class; keep the file's `using` lines 1–16 and add `using System.Linq;` if absent)
- Modify: `AnoMech/Core/Combat/LocalCombatSession.cs` (whole file)
- Modify: `AnoMech/Configuration.cs:15`, `AnoMech/Windows/ConfigWindow.cs:27-37`

**Interfaces:**
- Consumes from Task 1: `IJobCombat`, `JobStatus`, `JobCombatEntry`, `JobCombatRegistry.Find(byte, byte)`, `WarriorCombat.Beast`.
- Produces:
  - `internal interface IJobGauge { bool Matches { get; } void Mirror(IJobCombat rules); void Restore(); }`
  - `internal static class JobNativeGauge { IJobGauge Create(byte classJob); }`
  - `CombatNativeState(SimPlayer simPlayer, JobCombatEntry job, IJobCombat rules)`, `void Mirror(bool autos, bool resetAdditional = false)`
  - `LocalCombatSession.Start(SimWorld world, byte level, out string reason)` (signature unchanged)

Native code cannot run in the console tests; verification is build + full regression + source checks, then Task 3 in-game.

- [ ] **Step 1: Create the Warrior gauge adapter**

Create `AnoMech/Core/Combat/WarriorNativeGauge.cs`:

```csharp
using System;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Gauge;

namespace AnoMech.Core.Combat;

internal sealed unsafe class WarriorNativeGauge : IJobGauge
{
    private readonly WarriorGauge* gauge;
    private readonly byte beast;

    public WarriorNativeGauge()
    {
        var job = JobGaugeManager.Instance();
        if (job == null || job->ClassJobId != 21 || job->CurrentGauge == null)
            throw new InvalidOperationException("Warrior native gauge is unavailable.");
        gauge = (WarriorGauge*)job->CurrentGauge;
        beast = gauge->BeastGauge;
    }

    public bool Matches
    {
        get
        {
            var job = JobGaugeManager.Instance();
            return job != null && job->ClassJobId == 21 && job->CurrentGauge == (void*)gauge;
        }
    }

    public void Mirror(IJobCombat rules) => gauge->BeastGauge = (byte)((WarriorCombat)rules).Beast;

    public void Restore() => gauge->BeastGauge = beast;
}
```

- [ ] **Step 2: Make CombatNativeState job-neutral**

In `AnoMech/Core/Combat/CombatNativeState.cs`, keep lines 1–16 (usings/namespace), add `using System.Linq;` among them if it is not already there, and replace everything from `public sealed unsafe class CombatNativeState : IDisposable` to the end of the file with:

```csharp
internal interface IJobGauge
{
    bool Matches { get; }
    void Mirror(IJobCombat rules);
    void Restore();
}

// Native gauge adapters stay out of JobCombatRegistry: the registry is compiled
// into the pure console tests, which cannot reference unsafe game types.
internal static class JobNativeGauge
{
    public static IJobGauge Create(byte classJob) => classJob switch
    {
        21 => new WarriorNativeGauge(),
        _ => throw new InvalidOperationException($"No native gauge adapter for job {classJob}."),
    };
}

public sealed unsafe class CombatNativeState : IDisposable
{
    private readonly JobCombatEntry job;
    private readonly IJobCombat rules;
    private readonly IJobGauge gauge;
    private readonly BattleChara* player;
    private readonly ActionManager* manager;
    private readonly UIState* ui;
    private readonly ulong objectId;
    private readonly long started = Stopwatch.GetTimestamp();
    private readonly uint comboAction;
    private readonly float comboTimer;
    private readonly float animationLock;
    private readonly bool autoAttack;
    private readonly List<RecastSnapshot> recasts = [];
    private readonly List<StatusSnapshot> statuses = [];
    private bool disposed;
    private bool written;

    private sealed record RecastSnapshot(int NativeGroup, uint BindingAction, bool Additional,
        uint ActionId, bool Active, float Elapsed, float Total);
    private sealed record StatusSnapshot(ushort Id, ushort Param, float Remaining, GameObjectId Source);

    internal CombatNativeState(SimPlayer simPlayer, JobCombatEntry job, IJobCombat rules)
    {
        this.job = job;
        this.rules = rules;
        var signaturesReady = true;
        foreach (var (name, address) in new (string, nint)[]
        {
            ("ActionManager.GetRecastGroup", ActionManager.Addresses.GetRecastGroup.Value),
            ("ActionManager.GetAdditionalRecastGroup", ActionManager.Addresses.GetAdditionalRecastGroup.Value),
            ("ActionManager.GetRecastGroupDetail", ActionManager.Addresses.GetRecastGroupDetail.Value),
            ("ActionManager.GetActionRange", ActionManager.Addresses.GetActionRange.Value),
            ("ActionManager.StartCooldown", ActionManager.Addresses.StartCooldown.Value),
            ("StatusManager.AddStatus", StatusManager.Addresses.AddStatus.Value),
            ("StatusManager.RemoveStatus", StatusManager.Addresses.RemoveStatus.Value),
            ("StatusManager.GetStatusIndex", StatusManager.Addresses.GetStatusIndex.Value),
            ("ActionEffectHandler.Receive", ActionEffectHandler.Addresses.Receive.Value),
        }) signaturesReady &= SignatureReport.TrackAddress(name, address) != 0;
        if (!Plugin.PlayerInputHooks.CombatHooksReady || StatusManagerPointers.OnGainStatus == null || !signaturesReady)
            throw new InvalidOperationException("Required combat native signatures are unavailable.");
        player = simPlayer.BattleCharaPtr;
        manager = ActionManager.Instance();
        ui = UIState.Instance();
        if (player == null || manager == null || ui == null)
            throw new InvalidOperationException("Local combat native state is unavailable.");
        gauge = JobNativeGauge.Create(job.ClassJob);
        objectId = player->GetGameObjectId().ObjectId;
        comboAction = manager->Combo.Action;
        comboTimer = manager->Combo.Timer;
        animationLock = manager->AnimationLock;
        autoAttack = ui->WeaponState.AutoAttackState.IsAutoAttacking;
        if (!float.IsFinite(comboTimer) || !float.IsFinite(animationLock))
            throw new InvalidOperationException("Invalid native combo/animation timer.");
        // Groups are de-duplicated, so the first listed action binds each group.
        foreach (var action in rules.Actions)
        {
            CaptureRecast(manager->GetRecastGroup((int)ActionType.Action, action), action, false);
            var additional = manager->GetAdditionalRecastGroup(ActionType.Action, action);
            if (additional >= 0) CaptureRecast(additional, action, true);
        }
        var freeSlots = 0;
        foreach (var status in player->StatusManager.Status)
        {
            if (status.StatusId == 0) freeSlots++;
            if (!rules.StatusIds.Contains(status.StatusId)) continue;
            if (statuses.Exists(s => s.Id == status.StatusId))
                throw new InvalidOperationException("Duplicate offensive status sources cannot be safely owned.");
            if (simPlayer.HasStatus(status.StatusId))
                throw new InvalidOperationException("Scenario already owns a local combat job status.");
            if (!float.IsFinite(status.RemainingTime)) throw new InvalidOperationException("Invalid native status timer.");
            statuses.Add(new(status.StatusId, status.Param, status.RemainingTime, status.SourceObject));
        }
        if (freeSlots + statuses.Count < rules.StatusIds.Count)
            throw new InvalidOperationException("Insufficient native status slots for local job buffs.");
        // All bindings and snapshots are validated before the first write.
    }

    private void CaptureRecast(int group, uint action, bool additional)
    {
        if (group < 0) throw new InvalidOperationException($"Missing native recast binding for {action}.");
        if (recasts.Exists(r => r.NativeGroup == group)) return;
        var detail = manager->GetRecastGroupDetail(group);
        if (detail == null) throw new InvalidOperationException($"Missing native recast record for {action}.");
        _ = CombatRecastView.Restore(detail->IsActive, detail->Elapsed, detail->Total, 0);
        recasts.Add(new(group, action, additional, detail->ActionId, detail->IsActive, detail->Elapsed, detail->Total));
    }

    public bool MatchesIdentity => !disposed && Plugin.ClientState.IsLoggedIn && Plugin.ObjectTable.LocalPlayer?.Address == (nint)player
        && player->GetGameObjectId().ObjectId == objectId && player->ClassJob == job.ClassJob && player->Level == job.Level
        && ActionManager.Instance() == manager && UIState.Instance() == ui
        && gauge.Matches;

    public void SuppressNativeAutoAttack()
    {
        if (!MatchesIdentity) return;
        ui->WeaponState.AutoAttackState.IsAutoAttacking = false;
    }

    private void ValidateBindings(uint action)
    {
        var main = manager->GetRecastGroup((int)ActionType.Action, action);
        var additional = manager->GetAdditionalRecastGroup(ActionType.Action, action);
        if (!recasts.Exists(r => r.NativeGroup == main && !r.Additional)
            || (additional >= 0 && !recasts.Exists(r => r.NativeGroup == additional && r.Additional)))
            throw new InvalidOperationException($"Action {action} has an unowned native recast binding.");
    }

    public double AdditionalRemaining(uint action)
    {
        if (!MatchesIdentity) throw new InvalidOperationException("Local combat player identity changed.");
        var group = manager->GetAdditionalRecastGroup(ActionType.Action, action);
        if (group < 0) return 0;
        var detail = manager->GetRecastGroupDetail(group);
        if (detail == null) throw new InvalidOperationException("Additional recast record disappeared.");
        _ = CombatRecastView.Restore(detail->IsActive, detail->Elapsed, detail->Total, 0);
        return detail->IsActive ? Math.Max(0, detail->Total - detail->Elapsed) : 0;
    }

    public void StartCooldown(uint action)
    {
        if (!MatchesIdentity) throw new InvalidOperationException("Local combat player identity changed.");
        ValidateBindings(action);
        manager->StartCooldown(ActionType.Action, action);
    }

    public void Mirror(bool autos, bool resetAdditional = false)
    {
        if (!MatchesIdentity) throw new InvalidOperationException("Local combat player identity changed.");
        written = true;
        manager->ActionQueued = false;
        manager->Combo.Action = rules.ComboAction;
        manager->Combo.Timer = (float)rules.ComboRemaining;
        manager->AnimationLock = (float)rules.Timing.LockRemaining;
        gauge.Mirror(rules);
        ui->WeaponState.AutoAttackState.IsAutoAttacking = autos;
        foreach (var recast in recasts)
        {
            // Native Update advances additional groups initialized by
            // StartCooldown. Do not apply the charge duration or global lock to them.
            if (recast.Additional && !resetAdditional) continue;
            if (!MatchesIdentity) return;
            var detail = manager->GetRecastGroupDetail(recast.NativeGroup);
            if (detail == null) throw new InvalidOperationException("Native recast record disappeared.");
            var (group, seconds, charges) = rules.GetCooldown(recast.BindingAction);
            var view = recast.Additional
                ? new CombatRecastView(false, 0, 0)
                : CombatRecastView.Project(seconds, charges, rules.Timing.Charges(group, seconds, charges), rules.Timing.Remaining(group));
            detail->ActionId = recast.BindingAction;
            detail->IsActive = view.IsActive;
            detail->Elapsed = (float)view.Elapsed;
            detail->Total = (float)view.Total;
        }
        foreach (var status in rules.Statuses())
            MirrorStatus(status.Id, status.Remaining, status.Param);
    }

    private void MirrorStatus(ushort id, double remaining, ushort param)
    {
        if (!MatchesIdentity) return;
        if (remaining <= 0) { Statuses.Remove((Character*)player, id); return; }
        if (player->StatusManager.GetStatusIndex(id) < 0)
            Statuses.AddStatusInit((Character*)player, id, param);
        if (MatchesIdentity) Statuses.Apply((Character*)player, id, (float)remaining, param, player->GetGameObjectId());
    }

    public void Dispose()
    {
        if (disposed) return;
        // Identity is checked before every write; no stale pointer is restored to
        // a new entity. Session marks itself inactive before calling this method.
        try
        {
            if (!written || !MatchesIdentity) return;
            var elapsed = Stopwatch.GetElapsedTime(started).TotalSeconds;
            manager->ActionQueued = false;
            manager->Combo.Timer = (float)Math.Max(0, comboTimer - elapsed);
            manager->Combo.Action = manager->Combo.Timer > 0 ? comboAction : 0;
            manager->AnimationLock = (float)Math.Max(0, animationLock - elapsed);
            gauge.Restore();
            ui->WeaponState.AutoAttackState.IsAutoAttacking = autoAttack;
            foreach (var recast in recasts)
            {
                if (!MatchesIdentity) return;
                var detail = manager->GetRecastGroupDetail(recast.NativeGroup);
                if (detail == null) continue;
                var view = CombatRecastView.Restore(recast.Active, recast.Elapsed, recast.Total, elapsed);
                detail->ActionId = recast.ActionId;
                detail->IsActive = view.IsActive;
                detail->Elapsed = (float)view.Elapsed;
                detail->Total = (float)view.Total;
            }
            foreach (var id in rules.StatusIds)
            {
                if (!MatchesIdentity) return;
                Statuses.Remove((Character*)player, id);
            }
            foreach (var status in statuses)
            {
                if (!MatchesIdentity) return;
                var remaining = status.Remaining - elapsed;
                if (remaining <= 0) continue;
                Statuses.AddStatusInit((Character*)player, status.Id, status.Param);
                if (MatchesIdentity) Statuses.Apply((Character*)player, status.Id, (float)remaining, status.Param, status.Source);
            }
        }
        finally { disposed = true; }
    }
}
```

Note the constructor is now `internal` because it takes the internal `IJobGauge`-backed state; `LocalCombatSession` is in the same assembly.

- [ ] **Step 3: Make LocalCombatSession registry-driven**

Replace the whole of `AnoMech/Core/Combat/LocalCombatSession.cs` with:

```csharp
using System;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Native;
using AnoMech.Core.SimObjects;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using SheetAction = Lumina.Excel.Sheets.Action;

namespace AnoMech.Core.Combat;

public sealed unsafe class LocalCombatSession : IDisposable
{
    private readonly SimWorld world;
    private readonly SimPlayer player;
    private readonly IJobCombat model;
    private readonly CombatInputBuffer buffer = new();
    private readonly CombatNativeState native;
    private readonly SimCast cast;
    private long lastTick = Stopwatch.GetTimestamp();
    private long lastMessage;
    private bool inCombat;
    public bool Active { get; private set; }
    public bool AutoAttacking { get; private set; }
    public string Reason { get; private set; } = "本機技能循環進行中";

    private LocalCombatSession(SimWorld world, SimPlayer player, JobCombatEntry job)
    {
        this.world = world;
        this.player = player;
        if (SignatureReport.TrackAddress("ActionManager.GetAdjustedRecastTime", ActionManager.Addresses.GetAdjustedRecastTime.Value) == 0)
            throw new InvalidOperationException("技能冷卻讀取介面未就緒。");
        var gcd = ActionManager.GetAdjustedRecastTime(ActionType.Action, job.GcdProbeAction, true) / 1000d;
        if (gcd <= 0) throw new InvalidOperationException("無法讀取技能冷卻。");
        model = job.CreateRules(gcd);
        var sheet = Plugin.DataManager.GetExcelSheet<SheetAction>();
        foreach (var id in model.Actions.Append(7u))
            if (!sheet.TryGetRow(id, out _)) throw new InvalidOperationException($"Missing installed Action {id}.");
        cast = new SimCast(player, world.Coordinates);
        native = new CombatNativeState(player, job, model);
        Active = true;
        try { native.Mirror(false, resetAdditional: true); }
        catch { Stop("本機戰鬥初始化失敗"); throw; }
    }

    public static LocalCombatSession? Start(SimWorld world, byte level, out string reason)
    {
        reason = "本機技能循環未啟動";
        if (!world.Map.IsZoneLoaded || world.Party.Player is not { } player || player.BattleCharaPtr == null)
        { reason = "需要已載入的模擬場景"; return null; }
        var job = JobCombatRegistry.Find(player.BattleCharaPtr->ClassJob, player.BattleCharaPtr->Level);
        if (job == null || job.Level != level)
        {
            reason = "目前職業或同步等級尚未支援本機技能循環";
            Plugin.ChatGui.PrintError($"[AnoMech] {reason}，技能維持原本行為。");
            return null;
        }
        if (!Plugin.PlayerInputHooks.CombatHooksReady)
        { reason = "本機戰鬥必要 hook 未全部就緒"; return null; }
        try
        {
            var session = new LocalCombatSession(world, player, job);
            reason = session.Reason;
            return session;
        }
        catch (Exception ex)
        { reason = $"本機戰鬥未啟動：{ex.Message}"; Plugin.Log.Error(ex, "Local combat activation failed"); return null; }
    }

    public bool CheckIdentity()
    {
        if (!Active) return false;
        if (Plugin.ClientState.IsLoggedIn && world.Map.IsZoneLoaded && Plugin.GameInstance?.ActiveScenario != null && native.MatchesIdentity) return true;
        Stop("本機戰鬥已停止：角色、職業或模擬區域已改變");
        return false;
    }

    public void Tick()
    {
        if (!CheckIdentity()) return;
        var now = Stopwatch.GetTimestamp();
        var seconds = Stopwatch.GetElapsedTime(lastTick, now).TotalSeconds;
        lastTick = now;
        model.Advance(seconds);
        buffer.Advance(seconds);
        if (!Alive)
        {
            buffer.Reset(); AutoAttacking = false; inCombat = false;
            return;
        }
        if (Plugin.GameInstance!.Paused) return;
        if (buffer.Pending is { } pending)
        {
            if (!Validate(pending.ActionId, pending.TargetId, false)) buffer.Reset();
            else if (Validate(pending.ActionId, pending.TargetId, true))
            { buffer.Take(true); Execute(pending.ActionId, pending.TargetId); }
        }
    }

    private bool Alive => !player.Dead && player.BattleCharaPtr != null && player.BattleCharaPtr->Health > 0;
    private bool Bound => RestrictedStatus(movement: true);
    private bool RestrictedStatus(bool movement)
    {
        var sheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Status>();
        foreach (var status in player.BattleCharaPtr->StatusManager.Status)
        {
            if (status.StatusId == 0 || !sheet.TryGetRow(status.StatusId, out var row)) continue;
            if (movement ? row.LockMovement || row.LockControl : row.LockActions || row.LockControl) return true;
        }
        return false;
    }
    public uint Adjust(uint id) => model.Adjust(id);
    public bool Supports(uint id) => model.Supports(id);

    public uint ActionStatus(uint id, ulong targetId, bool checkTiming)
    {
        if (!CheckIdentity()) return 572;
        if (targetId == 0xE0000000 || targetId == 0) targetId = CurrentTargetId();
        if (id == 7)
            return Alive && !Plugin.GameInstance!.Paused && !RestrictedStatus(movement: false)
                && (AutoAttacking || ResolveTarget(targetId) is { } target && InRange(7, target)) ? 0u : 572u;
        if (!Supports(id)) return 573;
        if (!Validate(id, targetId, false)) return 572;
        return !checkTiming || Validate(id, targetId, true) ? 0u : 582u;
    }

    public bool TryInput(ActionType type, uint id, ulong targetId, out bool accepted)
    {
        accepted = false;
        if (!CheckIdentity()) return false;
        if ((type == ActionType.GeneralAction && id == 1) || (type == ActionType.Action && id == 7))
        {
            if (!Alive || Plugin.GameInstance!.Paused || RestrictedStatus(movement: false)) { AutoAttacking = false; return true; }
            if (!AutoAttacking && ResolveTarget(targetId == 0 || targetId == 0xE0000000 ? CurrentTargetId() : targetId) == null)
            { Explain("自動攻擊需要目前可選取的模擬敵人。"); return true; }
            AutoAttacking = !AutoAttacking;
            accepted = true;
            return true;
        }
        if (Plugin.GameInstance!.Paused && (type is ActionType.Action or ActionType.Item
            || (type == ActionType.GeneralAction && id == 4))) return true;
        if (type != ActionType.Action && type != ActionType.Item) return false;
        if (type == ActionType.Action && id == LocalPlayerInputHooks.SprintActionId) return false;
        if (type != ActionType.Action || !Supports(id))
        { Explain("目前僅模擬此職業已支援的技能循環與派生，不處理此技能／道具效果。"); return true; }
        // Resolve default target once at button press; queued input keeps this ID.
        targetId = targetId == 0xE0000000 || targetId == 0 ? CurrentTargetId() : targetId;
        id = Adjust(id);
        if (!Validate(id, targetId, false)) { Explain("技能條件不符：請檢查目標、距離、存活狀態與資源。"); return true; }
        if (Validate(id, targetId, true)) { buffer.Reset(); Execute(id, targetId); accepted = true; }
        else
        {
            var (group, recast, charges) = model.GetCooldown(id);
            var wait = Math.Max(model.Timing.LockRemaining,
                model.Timing.Charges(group, recast, charges) > 0 ? 0 : model.Timing.Remaining(group));
            wait = Math.Max(wait, native.AdditionalRemaining(id));
            accepted = buffer.Queue(id, targetId, wait);
        }
        return true;
    }

    private bool Validate(uint id, ulong targetId, bool timing)
    {
        if (!Alive || Plugin.GameInstance!.Paused || RestrictedStatus(movement: false)) return false;
        id = Adjust(id);
        var target = ResolveTarget(targetId);
        var hasTarget = model.IsSelfAction(id) ? Enemies().Any(e => InEffectRange(id, Position(player), e)) : target != null;
        if (model.IsGapCloser(id) && target != null && world.IsOutsideArena(GapEndpoint(target))) return false;
        return model.CanUse(id, hasTarget, target != null && InRange(id, target), inCombat, Alive, Bound, timing)
            && (!timing || native.AdditionalRemaining(id) <= 0);
    }

    private void Execute(uint id, ulong targetId)
    {
        id = Adjust(id);
        var target = ResolveTarget(targetId);
        var self = model.IsSelfAction(id);
        var hasTarget = self ? Enemies().Any(e => InEffectRange(id, Position(player), e)) : target != null;
        var hit = model.TryUse(id, hasTarget, target != null && InRange(id, target), inCombat, Alive, Bound);
        // This client-only timer initializer supplies the actual additional
        // recast duration. Main groups are replaced by the pure model below.
        native.StartCooldown(id);
        // CanUse was already true: null also means a successful buff or empty AoE.
        Plugin.PlayerInputHooks.RecordLocalAction();
        if (hit is { } action)
        {
            if (action.GapCloser && target != null) player.SetPosition(GapEndpoint(target));
            inCombat = true;
        }
        var presentationTarget = self ? null : target;
        cast.Start(id, presentationTarget == null ? Position(player) : Position(presentationTarget), 0,
            presentationTarget?.GameObjectId ?? player.GameObjectId, 0, 0, 0, .6f);
        native.Mirror(AutoAttacking);
    }

    private System.Collections.Generic.IEnumerable<SimEnemy> Enemies()
        => world.Children.OfType<SimEnemy>().Where(Eligible);
    private static bool Eligible(SimEnemy enemy)
    {
        var p = enemy.BattleCharaPtr;
        return enemy.IsActive && p != null && p->Health > 0 && p->DrawObject != null && p->DrawObject->IsVisible
            && (p->TargetableStatus & ObjectTargetableFlags.IsTargetable) != 0;
    }
    private SimEnemy? ResolveTarget(ulong id) => Enemies().FirstOrDefault(e => e.GameObjectId.ObjectId == id);
    private static ulong CurrentTargetId() => Plugin.TargetManager.Target?.GameObjectId ?? 0xE0000000;
    private Vector3 Position(SimCharacter actor) => world.Coordinates.ToLocal(actor.BattleCharaPtr->Position);
    private bool InRange(uint id, SimEnemy enemy)
    {
        var row = Plugin.DataManager.GetExcelSheet<SheetAction>().GetRow(id);
        var distance = Vector3.Distance(Position(player), Position(enemy)) - player.HitboxRadius - enemy.HitboxRadius;
        var range = row.Range < 0 ? ActionManager.GetActionRange(id) : row.Range;
        return float.IsFinite(range) && range >= 0 && distance <= range;
    }
    private bool InEffectRange(uint id, Vector3 center, SimEnemy enemy)
        => Vector3.Distance(center, Position(enemy)) - enemy.HitboxRadius
            <= Plugin.DataManager.GetExcelSheet<SheetAction>().GetRow(id).EffectRange;
    private Vector3 GapEndpoint(SimEnemy target)
    {
        var from = Position(player);
        var to = Position(target);
        var delta = from - to;
        var distance = delta.Length();
        return distance <= player.HitboxRadius + target.HitboxRadius ? from
            : to + delta / distance * (player.HitboxRadius + target.HitboxRadius);
    }

    private void Explain(string text)
    {
        var now = Stopwatch.GetTimestamp();
        if (lastMessage != 0 && Stopwatch.GetElapsedTime(lastMessage, now).TotalSeconds < 2) return;
        lastMessage = now;
        Plugin.ChatGui.PrintError($"[AnoMech] {text}");
    }
    public void BeforeNativeUpdate() { if (CheckIdentity()) native.SuppressNativeAutoAttack(); }
    public void AfterNativeUpdate() { if (CheckIdentity()) native.Mirror(AutoAttacking); }
    public void Stop(string reason)
    {
        if (!Active) return;
        Active = false;
        Reason = reason;
        buffer.Reset();
        AutoAttacking = false;
        try { native.Dispose(); }
        catch (Exception ex)
        {
            Reason = $"本機戰鬥已停止；還原失敗：{ex.Message}";
            Plugin.Log.Error(ex, "Local combat restoration failed");
        }
    }
    public void Dispose() => Stop("本機戰鬥已結束");
}
```

Behavior equivalence notes for the reviewer: for Warrior 90 the start gate is unchanged (player job 21, player level 90, scenario level 90). `IsSelfAction` also covers Infuriate 52 and Inner Release 7389; for those, `hasTarget` is ignored by `CanUse` (self action) and `TryUse` returns before reading it, and they were already presented without a target, so behavior is identical. Action-sheet row validation moved inside the constructor and still runs before `CombatNativeState` snapshots or writes anything.

- [ ] **Step 4: Remove the opt-in setting and checkbox**

In `AnoMech/Configuration.cs` delete the line:

```csharp
    public bool EnableLocalCombat { get; set; } = false;
```

In `AnoMech/Windows/ConfigWindow.cs` replace:

```csharp
        var localCombat = configuration.EnableLocalCombat;
        if (ImGui.Checkbox("90 級戰士技能循環預覽（下次開始場景生效）", ref localCombat))
        {
            configuration.EnableLocalCombat = localCombat;
            configuration.Save();
        }
        ImGui.TextWrapped("使用原本熱鍵練習連段、派生與量譜消耗；不計算傷害、屬性或減傷。其他職業尚未加入。");
```

with:

```csharp
        ImGui.TextWrapped("開始模擬時自動偵測職業；已支援的職業可用原本熱鍵練習連段、派生與量譜消耗，不計算傷害、屬性或減傷。");
```

(The following `if (Plugin.GameInstance is { } game) { ImGui.TextWrapped(game.World.CombatReason); }` block stays.)

- [ ] **Step 5: Verify no Warrior hard-coding or setting remains outside Warrior files**

Run: `cd "C:/Users/Knuckles/OneDrive/文件/claude/AnoMech"; grep -rnE "EnableLocalCombat|OffensiveActions|WarriorHit" AnoMech Tests --include=*.cs; grep -nE "BeastGauge|WarriorGauge|ClassJobId != 21|ClassJob != 21|7386 or 25753|41 or 16462|1177" AnoMech/Core/Combat/LocalCombatSession.cs AnoMech/Core/Combat/CombatNativeState.cs`
Expected: no output.

- [ ] **Step 6: Build the plugin**

Run: `$env:MSBuildEnableWorkloadResolver = 'false'; dotnet build "C:\Users\Knuckles\OneDrive\文件\claude\AnoMech\AnoMech\AnoMech.csproj" --no-restore -c Debug '-p:DalamudLibPath=C:/Users/Knuckles/AppData/Roaming/FFXIVSimpleLauncher/Dalamud/Injector/' 2>&1 | Select-String 'root at|error|個錯誤|個警告'`
Expected: `Dalamud.NET.Sdk: root at C:/Users/Knuckles/AppData/Roaming/FFXIVSimpleLauncher/Dalamud/Injector/`, `0 個錯誤`. If `CS0051`/`CS0053` accessibility errors appear because the public `CombatNativeState` exposes internal types, make the whole `CombatNativeState` class `internal` (it is only used by `LocalCombatSession` in the same assembly) and rebuild.

- [ ] **Step 7: Run all regression checks**

Run: `$env:MSBuildEnableWorkloadResolver = 'false'; dotnet run --project "C:\Users\Knuckles\OneDrive\文件\claude\AnoMech\Tests\AnoMech.P3.Tests.csproj"; if ($?) { dotnet run --project "C:\Users\Knuckles\OneDrive\文件\claude\AnoMech\Tests.PartyLayout\Tests.PartyLayout.csproj" }`
Expected: both exit 0; output contains `PASS: Warrior offensive action state…`, `PASS: job registry resolves only Warrior 90…`, `P3 regression checks passed.` and `PASS: party layout stays custom…`.

- [ ] **Step 8: Commit**

```bash
cd "C:/Users/Knuckles/OneDrive/文件/claude/AnoMech"
git add AnoMech/Core/Combat/WarriorNativeGauge.cs AnoMech/Core/Combat/CombatNativeState.cs AnoMech/Core/Combat/LocalCombatSession.cs AnoMech/Configuration.cs AnoMech/Windows/ConfigWindow.cs
git commit -m "feat: auto-detect local combat job and remove Warrior opt-in

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 3: In-game acceptance (user)

No code. The implementer stops here and hands over; `bin/Debug/AnoMech.dll` from Task 2 Step 6 is already at the dev plugin load location.

- [ ] **Step 1: Hand the user this checklist**

1. `/xlplugins`：停用插件庫版 AnoMech，重新載入開發版 `bin\Debug\AnoMech.dll`。
2. 戰士（90）進 TOP 任一場景：設定視窗已無勾選框；不做任何設定即可打循環，連段、獸魂、狂魂／原初的解放、猛攻充能、冷卻顯示與先前一致。
3. 換成其他職業進 TOP：聊天框出現一次「目前職業或同步等級尚未支援本機技能循環，技能維持原本行為。」，設定視窗顯示同一原因。
4. 戰士進絕神兵（70 級）：同樣提示不支援。
5. 模擬中切換職業：本機循環停止，設定視窗顯示「本機戰鬥已停止：角色、職業或模擬區域已改變」；重開場景才重新偵測。
6. 離開模擬後，正常技能、連段、冷卻與獸魂恢復。

- [ ] **Step 2: Record the result**

If all pass, report to the user that the plan is complete (no push, no release). If any item fails, stop and use superpowers:systematic-debugging with the failing item; do not start a new job.
