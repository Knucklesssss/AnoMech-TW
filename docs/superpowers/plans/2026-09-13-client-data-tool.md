# Client Data Tool Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A standalone console tool that reads the local Traditional Chinese FFXIV client, writes a level-90 skill data list per job to `docs/client-data/<ABBR>-90.md`, cross-checked against version-matched xivapi data.

**Architecture:** `tools/ClientData/` (net9.0 console, references the launcher's Lumina assemblies) holds three pure files — `MacroText.cs`, `TraitUpgrades.cs`, `XivapiCompare.cs` — plus `Program.cs` (Lumina reading, selection, markdown, xivapi HTTP, Warrior golden check). `tools/ClientData.Checks/` compiles only the pure files and runs offline assertions.

**Tech Stack:** C# / net9.0, Lumina + Lumina.Excel from FFXIVSimpleLauncher's API 13 Dalamud, `System.Net.Http`, `System.Text.Json`.

**Spec:** `docs/superpowers/specs/2026-09-13-client-data-tool-design.md`

## Global Constraints

- Lumina path: `C:/Users/Knuckles/AppData/Roaming/FFXIVSimpleLauncher/Dalamud/Injector/` (`Lumina.dll` reports assembly version 0.0.0.0 while `Lumina.Excel.dll` references Lumina 6.0.0.0 — an `AssemblyLoadContext.Default.Resolving` handler must return the local `Lumina.dll` before any Lumina.Excel type is touched, with Lumina work in a separate `[MethodImpl(MethodImplOptions.NoInlining)]` method).
- Client path default `Z:/FINAL FANTASY XIV TC/game`; sheets opened with `Language.TraditionalChinese` and `PanicOnSheetChecksumMismatch = true`; version read from `game/ffxivgame.ver`.
- Official TC job names come from `ClassJob.Name` (e.g. 暗黑騎士, 白魔道士, 奪魂者, 黑魔道士).
- Level 90 only. No plugin code changes (`AnoMech/`, `Tests/`, `Tests.PartyLayout/` untouched). No new NuGet packages. No push, no publish.
- xivapi: `https://v2.xivapi.com/api/sheet/<Sheet>?version=<v>&rows=<ids>&fields=<f>`; at most 100 rows per request; default versions `7.3, 7.31, 7.35, 7.38, 7.4`.
- Exit codes: `0` list written and xivapi-checked; `1` fatal, no list written; `2` list written but xivapi check incomplete; `3` Warrior golden check failed.
- Every `dotnet` command: `$env:MSBuildEnableWorkloadResolver = 'false'` first. The PowerShell tool resets its working directory — use absolute paths (repo root `C:\Users\Knuckles\OneDrive\文件\claude\AnoMech`).
- `docs/` is git-ignored: stage with `git add -f`. Commit messages end with `Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>`.

## File Structure

| File | Responsibility |
| --- | --- |
| `tools/ClientData/ClientData.csproj` | Console project, Lumina references |
| `tools/ClientData/MacroText.cs` | Evaluate description macros for a job/level (pure) |
| `tools/ClientData/TraitUpgrades.cs` | Find 「A變為B」 replacements in trait text (pure) |
| `tools/ClientData/XivapiCompare.cs` | Field diffs and version pick (pure) |
| `tools/ClientData/Program.cs` | CLI, Lumina reading, selection, markdown, xivapi HTTP, golden check |
| `tools/ClientData.Checks/*` | Offline assertions for the three pure files |

---

### Task 1: Pure evaluation and comparison logic with offline checks

**Files:**
- Create: `tools/ClientData/MacroText.cs`, `tools/ClientData/TraitUpgrades.cs`, `tools/ClientData/XivapiCompare.cs`
- Create: `tools/ClientData.Checks/ClientData.Checks.csproj`, `tools/ClientData.Checks/Program.cs`
- Modify: `docs/superpowers/specs/2026-09-13-client-data-tool-design.md`

**Interfaces:**
- Produces:
  - `public static class MacroText { public static (string Text, bool Unresolved) Evaluate(string macro, uint classJob, int level); }`
  - `public static class TraitUpgrades { public static IReadOnlyList<(uint From, uint To)> Find(string traitText, IReadOnlyDictionary<string, uint> actionIdsByName); }`
  - `public readonly record struct FieldValue(string Sheet, uint Row, string Field, string Value);`
  - `public readonly record struct FieldDiff(string Sheet, uint Row, string Field, string Local, string? Remote);`
  - `public static class XivapiCompare { public static IReadOnlyList<FieldDiff> Diff(IEnumerable<FieldValue> local, IEnumerable<FieldValue> remote); public static string PickVersion(IReadOnlyList<string> versions, IReadOnlyDictionary<string, int> diffCounts); }`
  - All in namespace `ClientData`.

- [ ] **Step 1: Write the failing checks**

Create `tools/ClientData.Checks/ClientData.Checks.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="../ClientData/MacroText.cs" />
    <Compile Include="../ClientData/TraitUpgrades.cs" />
    <Compile Include="../ClientData/XivapiCompare.cs" />
  </ItemGroup>
</Project>
```

Create `tools/ClientData.Checks/Program.cs`:

```csharp
using ClientData;

const string FellCleave = "對目標發動物理攻擊　<colortype(504)><edgecolortype(505)>威力：<edgecolortype(0)><colortype(0)><if([gnum68==21],<if([gnum72>=94],580,520)>,520)><br><if([gnum68==21],<if([gnum72>=80],該技能在<colortype(500)><edgecolortype(501)>原初的混沌<edgecolortype(0)><colortype(0)>狀態中，且<colortype(500)><edgecolortype(501)>獸魂<edgecolortype(0)><colortype(0)>在50點以上時變為狂魂<br>,)>,)><colortype(504)><edgecolortype(505)>發動條件：<edgecolortype(0)><colortype(0)><colortype(500)><edgecolortype(501)>獸魂<edgecolortype(0)><colortype(0)>50點";

void Require(bool condition, string message) { if (!condition) throw new Exception(message); }

var war90 = MacroText.Evaluate(FellCleave, 21, 90);
Require(!war90.Unresolved, "Warrior 90 Fell Cleave must fully evaluate.");
Require(war90.Text == "對目標發動物理攻擊　威力：520\n該技能在原初的混沌狀態中，且獸魂在50點以上時變為狂魂\n發動條件：獸魂50點", $"Warrior 90 text wrong: {war90.Text}");
Require(MacroText.Evaluate(FellCleave, 21, 94).Text.Contains("威力：580"), "Level 94 branch must choose 580.");
var drk = MacroText.Evaluate(FellCleave, 32, 90);
Require(drk.Text.StartsWith("對目標發動物理攻擊　威力：520\n發動條件"), $"Other job must take else branches: {drk.Text}");
var unknownParam = MacroText.Evaluate("A<if([gnum69>=3],B,C)>D", 21, 90);
Require(unknownParam.Unresolved && unknownParam.Text == "A<if([gnum69>=3],B,C)>D", "Unsupported gnum must stay raw and be flagged.");
var unknownTag = MacroText.Evaluate("X<sheet(Action,1,0)>Y", 21, 90);
Require(unknownTag.Unresolved && unknownTag.Text == "X<sheet(Action,1,0)>Y", "Unknown tags must stay raw and be flagged.");
var plain = MacroText.Evaluate("猛攻的累積次數增加到3次", 21, 90);
Require(!plain.Unresolved && plain.Text == "猛攻的累積次數增加到3次", "Plain text must pass through.");
Console.WriteLine("PASS: macro evaluation for job/level conditions and fail-closed unknowns.");

var names = new Dictionary<string, uint> { ["狂暴"] = 38, ["原初的解放"] = 7389, ["原初"] = 1, ["地毀人亡"] = 3550, ["混沌旋風"] = 16463, ["旋風"] = 2, ["鋼鐵旋風"] = 51 };
Require(TraitUpgrades.Find("狂暴變為原初的解放", names).SequenceEqual(new[] { (38u, 7389u) }), "Longest target name must win.");
Require(TraitUpgrades.Find("鋼鐵旋風變為地毀人亡", names).SequenceEqual(new[] { (51u, 3550u) }), "Longest source name must win.");
Require(TraitUpgrades.Find("原初的混沌效果：獲得50點獸魂，地毀人亡變為混沌旋風", names).SequenceEqual(new[] { (3550u, 16463u) }), "Mid-sentence replacement must be found.");
Require(TraitUpgrades.Find("猛攻的累積次數增加到3次", names).Count == 0, "No 變為 means no replacement.");
Require(TraitUpgrades.Find("未知技能變為原初的解放", names).Count == 0, "Unknown source name must not produce a replacement.");
Console.WriteLine("PASS: trait text replacement detection.");

FieldValue[] local = [new("Action", 31, "CooldownGroup", "58"), new("Action", 31, "Recast100ms", "25"), new("Action", 52, "MaxCharges", "2")];
FieldValue[] remote = [new("Action", 31, "CooldownGroup", "58"), new("Action", 31, "Recast100ms", "30")];
var diffs = XivapiCompare.Diff(local, remote);
Require(diffs.SequenceEqual(new[] { new FieldDiff("Action", 31, "Recast100ms", "25", "30"), new FieldDiff("Action", 52, "MaxCharges", "2", null) }), "Diff must report changed and missing fields in local order.");
Require(XivapiCompare.PickVersion(["7.3", "7.31", "7.35"], new Dictionary<string, int> { ["7.3"] = 4, ["7.31"] = 1, ["7.35"] = 1 }) == "7.31", "Fewest diffs wins; ties keep earlier version.");
Console.WriteLine("PASS: xivapi field diff and version pick.");
```

- [ ] **Step 2: Run checks to verify they fail**

Run: `$env:MSBuildEnableWorkloadResolver = 'false'; dotnet run --project "C:\Users\Knuckles\OneDrive\文件\claude\AnoMech\tools\ClientData.Checks\ClientData.Checks.csproj"`
Expected: build FAIL with `CS2001` (the three source files do not exist) or `CS0103` for `MacroText`/`TraitUpgrades`/`XivapiCompare`.

- [ ] **Step 3: Write the implementation**

Create `tools/ClientData/MacroText.cs`:

```csharp
using System.Text;
using System.Text.RegularExpressions;

namespace ClientData;

// Lumina has no macro evaluator. Client descriptions for level-90 jobs only use
// colortype/edgecolortype/br and if([gnum68..]/[gnum72..]); everything else stays raw.
public static class MacroText
{
    private static readonly Regex Condition = new(@"^gnum(\d+)(==|>=)(\d+)$");

    public static (string Text, bool Unresolved) Evaluate(string macro, uint classJob, int level)
    {
        var unresolved = false;
        var text = Eval(macro, classJob, level, ref unresolved);
        return (text, unresolved);
    }

    private static string Eval(string s, uint job, int level, ref bool unresolved)
    {
        var sb = new StringBuilder();
        var i = 0;
        while (i < s.Length)
        {
            if (s[i] != '<') { sb.Append(s[i++]); continue; }
            var end = TagEnd(s, i);
            if (end < 0) { unresolved = true; sb.Append(s, i, s.Length - i); break; }
            var tag = s[i..(end + 1)];
            i = end + 1;
            if (tag == "<br>") sb.Append('\n');
            else if (tag.StartsWith("<colortype(") || tag.StartsWith("<edgecolortype(")) { }
            else if (tag.StartsWith("<if(") && TryIf(tag, job, level, ref unresolved, out var chosen)) sb.Append(chosen);
            else { unresolved = true; sb.Append(tag); }
        }
        return sb.ToString();
    }

    // Brackets are skipped so the '>' in ">=" does not close a tag.
    private static int TagEnd(string s, int start)
    {
        var depth = 0;
        for (var j = start; j < s.Length; j++)
        {
            if (s[j] == '[') { j = s.IndexOf(']', j); if (j < 0) return -1; continue; }
            if (s[j] == '<') depth++;
            else if (s[j] == '>' && --depth == 0) return j;
        }
        return -1;
    }

    private static bool TryIf(string tag, uint job, int level, ref bool unresolved, out string chosen)
    {
        chosen = "";
        if (!tag.EndsWith(")>") || tag.Length < 8 || tag[4] != '[') return false;
        var inner = tag[4..^2];
        var close = inner.IndexOf(']');
        if (close < 0 || close + 1 >= inner.Length || inner[close + 1] != ',') return false;
        var match = Condition.Match(inner[1..close]);
        if (!match.Success) return false;
        var value = int.Parse(match.Groups[3].Value);
        int? actual = match.Groups[1].Value switch { "68" => (int)job, "72" => level, _ => null };
        if (actual == null) return false;
        var args = SplitTopLevel(inner[(close + 2)..]);
        if (args == null) return false;
        var holds = match.Groups[2].Value == "==" ? actual == value : actual >= value;
        chosen = Eval(holds ? args.Value.Then : args.Value.Else, job, level, ref unresolved);
        return true;
    }

    private static (string Then, string Else)? SplitTopLevel(string s)
    {
        var depth = 0;
        var commas = new List<int>();
        for (var j = 0; j < s.Length; j++)
        {
            if (s[j] == '[') { j = s.IndexOf(']', j); if (j < 0) return null; continue; }
            if (s[j] == '<') depth++;
            else if (s[j] == '>') depth--;
            else if (s[j] == ',' && depth == 0) commas.Add(j);
        }
        return commas.Count == 1 ? (s[..commas[0]], s[(commas[0] + 1)..]) : null;
    }
}
```

Create `tools/ClientData/TraitUpgrades.cs`:

```csharp
namespace ClientData;

public static class TraitUpgrades
{
    private const string Becomes = "變為";

    // Longest names win so 「鋼鐵旋風」 is not read as 「旋風」.
    public static IReadOnlyList<(uint From, uint To)> Find(string traitText, IReadOnlyDictionary<string, uint> actionIdsByName)
    {
        var result = new List<(uint, uint)>();
        var byLength = actionIdsByName.Where(n => n.Key.Length > 0).OrderByDescending(n => n.Key.Length).ToList();
        for (var at = traitText.IndexOf(Becomes, StringComparison.Ordinal); at >= 0; at = traitText.IndexOf(Becomes, at + Becomes.Length, StringComparison.Ordinal))
        {
            var from = byLength.FirstOrDefault(n => at >= n.Key.Length && string.CompareOrdinal(traitText, at - n.Key.Length, n.Key, 0, n.Key.Length) == 0);
            var after = at + Becomes.Length;
            var to = byLength.FirstOrDefault(n => string.CompareOrdinal(traitText, after, n.Key, 0, n.Key.Length) == 0 && after + n.Key.Length <= traitText.Length);
            if (from.Key != null && to.Key != null) result.Add((from.Value, to.Value));
        }
        return result;
    }
}
```

Create `tools/ClientData/XivapiCompare.cs`:

```csharp
namespace ClientData;

public readonly record struct FieldValue(string Sheet, uint Row, string Field, string Value);

public readonly record struct FieldDiff(string Sheet, uint Row, string Field, string Local, string? Remote);

public static class XivapiCompare
{
    public static IReadOnlyList<FieldDiff> Diff(IEnumerable<FieldValue> local, IEnumerable<FieldValue> remote)
    {
        var remoteByKey = remote.ToDictionary(r => (r.Sheet, r.Row, r.Field), r => r.Value);
        return local
            .Select(l => new FieldDiff(l.Sheet, l.Row, l.Field, l.Value, remoteByKey.TryGetValue((l.Sheet, l.Row, l.Field), out var v) ? v : null))
            .Where(d => d.Remote != d.Local)
            .ToList();
    }

    public static string PickVersion(IReadOnlyList<string> versions, IReadOnlyDictionary<string, int> diffCounts)
        => versions.Where(diffCounts.ContainsKey).OrderBy(v => diffCounts[v]).First();
}
```

(`OrderBy` is stable, so ties keep the earlier version.)

- [ ] **Step 4: Run checks to verify they pass**

Run: `$env:MSBuildEnableWorkloadResolver = 'false'; dotnet run --project "C:\Users\Knuckles\OneDrive\文件\claude\AnoMech\tools\ClientData.Checks\ClientData.Checks.csproj"; "exit=$LASTEXITCODE"`
Expected: the three `PASS:` lines and `exit=0`, no warnings.

- [ ] **Step 5: Record spec corrections found during planning**

In `docs/superpowers/specs/2026-09-13-client-data-tool-design.md`:

1. Replace the job-order line `- 職業順序：暗黑騎士 → 騎士 → 白魔法師 → 賢者 → 武士 → 鐮刀 → 機工士 → 黑魔法師；其餘 12 個職業之後再排。` with `- 職業順序：暗黑騎士 → 騎士 → 白魔道士 → 賢者 → 武士 → 奪魂者 → 機工士 → 黑魔道士；其餘 12 個職業之後再排。職業名稱一律採客戶端 ClassJob 官方名稱。`
2. In the roadmap table replace `| 4 | 白魔法師 |` with `| 4 | 白魔道士 |` and `| 5–9 | 賢者、武士、鐮刀、機工士、黑魔法師 |` with `| 5–9 | 賢者、武士、奪魂者、機工士、黑魔道士 |`, and `（例如黑魔法師的隨機觸發` with `（例如黑魔道士的隨機觸發`.
3. Replace the whole `- **技能**：…` bullet under 資料怎麼挑 with:

```markdown
- **技能**：非 PvP、習得等級 1–90，且（技能的職業欄位是本職業或其前置職業，或屬於職能技能且職業分類含本職業）；再加上技能替換表（ActionIndirection）中本職業、習得等級 ≤ 90 的替換技能。連段前置取自技能的 ActionCombo 欄位。ActionComboRoute 經本機讀取確認是 PvP 連段路線，不使用。以戰士驗證：已知 32 個技能全數命中，多出的只有被特性升級取代的基礎技能（38、49、51、3551）。
```

4. Replace the `- **說明文字**：…` bullet with:

```markdown
- **說明文字**：Lumina 沒有巨集求值器。本機全資料的說明巨集只有 `colortype`、`edgecolortype`、`br` 與 `if`，條件只有 `gnum68`（職業，`==`／`>=`）、`gnum72`（等級，`>=`）與少量 `gnum69>=`。工具以本職業 ID 與 90 級求值 `gnum68`／`gnum72` 條件、去除顏色標記、`br` 轉換行；其他條件或未知標記保留原文並列入需人工確認。
```

5. Replace the `- **替換關係**：…` bullet with:

```markdown
- **替換關係**：兩個來源。(1) 技能替換表的狀態替換（例如裂石飛環 → 狂魂）；(2) 特性說明中「A變為B」且 A、B 皆為本職業技能名稱者（例如「原初之魂變為裂石飛環」），標註來源為特性說明，並一律列入需人工確認。
```

6. Under 執行方式 append a bullet: `- 啟動器的 \`Lumina.dll\` 組件版本為 0.0.0.0，而 \`Lumina.Excel.dll\` 參照 Lumina 6.0.0.0；工具在使用任何資料表型別前註冊組件載入處理，回傳該 \`Lumina.dll\`。`
7. Under 錯誤處理 append a bullet: `- 結束代碼：0 清單已產出且完成 xivapi 核對；1 致命錯誤、未產出；2 已產出但未完成 xivapi 核對（含使用 \`--skip-xivapi\`）；3 戰士標準答案檢查失敗。`

- [ ] **Step 6: Commit**

```bash
cd "C:/Users/Knuckles/OneDrive/文件/claude/AnoMech"
git add tools/ClientData/MacroText.cs tools/ClientData/TraitUpgrades.cs tools/ClientData/XivapiCompare.cs tools/ClientData.Checks/ClientData.Checks.csproj tools/ClientData.Checks/Program.cs
git add -f docs/superpowers/specs/2026-09-13-client-data-tool-design.md
git commit -m "feat: add client data macro, upgrade and xivapi diff logic

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 2: Client reader, markdown list and command line (xivapi skipped)

**Files:**
- Create: `tools/ClientData/ClientData.csproj`, `tools/ClientData/JobData.cs`, `tools/ClientData/ClientReader.cs`, `tools/ClientData/Markdown.cs`, `tools/ClientData/Options.cs`, `tools/ClientData/Tool.cs`, `tools/ClientData/Program.cs`

**Interfaces:**
- Consumes from Task 1: `MacroText.Evaluate(string, uint, int)`, `TraitUpgrades.Find(string, IReadOnlyDictionary<string, uint>)`, `FieldDiff`.
- Produces (namespace `ClientData`):
  - `public sealed class ToolException(string message) : Exception(message)`
  - `public sealed record ActionRow(uint Id, string Name, string Category, byte Level, ushort Cast100ms, ushort Recast100ms, byte CooldownGroup, byte AdditionalCooldownGroup, byte MaxCharges, byte CostType, ushort CostValue, sbyte Range, byte EffectRange, byte CastType, bool CanTargetSelf, bool CanTargetParty, bool CanTargetHostile, bool IsPlayerAction, bool IsRoleAction, bool IsPvP, uint ComboFrom, uint ClassJobCategory, uint StatusGainSelf, string Description, bool DescriptionUnresolved)`
  - `public sealed record TraitRow(uint Id, string Name, byte Level, short Value, uint ClassJob, string Description, bool DescriptionUnresolved)`
  - `public sealed record StatusRow(uint Id, string Name, byte MaxStacks, byte StatusCategory, bool IsPermanent, bool CanDispel, string Description, bool DuplicateName)`
  - `public sealed record Replacement(uint From, uint To, string Source)`
  - `public sealed record JobData(uint ClassJob, string Abbreviation, string Name, int Level, string GameVersion, IReadOnlyList<ActionRow> Actions, IReadOnlyList<TraitRow> Traits, IReadOnlyList<StatusRow> Statuses, IReadOnlyList<Replacement> Replacements, IReadOnlyList<string> ManualChecks)`
  - `public sealed record XivapiCheck(bool Completed, string? Version, IReadOnlyDictionary<string, int> DiffCounts, IReadOnlyList<FieldDiff> Diffs, string? FailureReason)` with `static XivapiCheck Skipped(string reason)`
  - `public sealed record Options(string Abbreviation, string GamePath, int Level, string OutputDirectory, bool SkipXivapi, string? XivapiVersion, bool CheckWarrior)` with `static Options Parse(string[] args)`
  - `public static class ClientReader { public static JobData Read(string gamePath, string abbreviation, int level); }`
  - `public static class Markdown { public static string Write(JobData job, XivapiCheck check, DateTimeOffset readAt); }`
  - `public static class Tool { public static int Run(string[] args); }` — Task 3 edits the line marked `// xivapi check`.

This task reads the real client, so its verification is running the tool, not a unit test.

- [ ] **Step 1: Create the project and model**

`tools/ClientData/ClientData.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LuminaDirectory>C:/Users/Knuckles/AppData/Roaming/FFXIVSimpleLauncher/Dalamud/Injector/</LuminaDirectory>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="Lumina"><HintPath>$(LuminaDirectory)Lumina.dll</HintPath></Reference>
    <Reference Include="Lumina.Excel"><HintPath>$(LuminaDirectory)Lumina.Excel.dll</HintPath></Reference>
  </ItemGroup>
</Project>
```

`tools/ClientData/JobData.cs`:

```csharp
namespace ClientData;

public sealed class ToolException(string message) : Exception(message);

public sealed record ActionRow(uint Id, string Name, string Category, byte Level, ushort Cast100ms, ushort Recast100ms,
    byte CooldownGroup, byte AdditionalCooldownGroup, byte MaxCharges, byte CostType, ushort CostValue, sbyte Range,
    byte EffectRange, byte CastType, bool CanTargetSelf, bool CanTargetParty, bool CanTargetHostile, bool IsPlayerAction,
    bool IsRoleAction, bool IsPvP, uint ComboFrom, uint ClassJobCategory, uint StatusGainSelf, string Description,
    bool DescriptionUnresolved);

public sealed record TraitRow(uint Id, string Name, byte Level, short Value, uint ClassJob, string Description, bool DescriptionUnresolved);

public sealed record StatusRow(uint Id, string Name, byte MaxStacks, byte StatusCategory, bool IsPermanent, bool CanDispel, string Description, bool DuplicateName);

public sealed record Replacement(uint From, uint To, string Source);

public sealed record JobData(uint ClassJob, string Abbreviation, string Name, int Level, string GameVersion,
    IReadOnlyList<ActionRow> Actions, IReadOnlyList<TraitRow> Traits, IReadOnlyList<StatusRow> Statuses,
    IReadOnlyList<Replacement> Replacements, IReadOnlyList<string> ManualChecks);

public sealed record XivapiCheck(bool Completed, string? Version, IReadOnlyDictionary<string, int> DiffCounts, IReadOnlyList<FieldDiff> Diffs, string? FailureReason)
{
    public static XivapiCheck Skipped(string reason) => new(false, null, new Dictionary<string, int>(), [], reason);
}
```

- [ ] **Step 2: Create options, entry point and tool runner**

`tools/ClientData/Options.cs`:

```csharp
namespace ClientData;

public sealed record Options(string Abbreviation, string GamePath, int Level, string OutputDirectory, bool SkipXivapi, string? XivapiVersion, bool CheckWarrior)
{
    private const string Usage = "用法：ClientData <職業縮寫> [--game 路徑] [--level 90] [--out 目錄] [--skip-xivapi] [--xivapi-version 7.3] [--check-war]";

    public static Options Parse(string[] args)
    {
        string? abbreviation = null, output = null, version = null;
        var game = "Z:/FINAL FANTASY XIV TC/game";
        var level = 90;
        bool skip = false, check = false;
        for (var i = 0; i < args.Length; i++)
        {
            string Next() => i + 1 < args.Length ? args[++i] : throw new ToolException($"{args[i]} 缺少參數值。");
            switch (args[i])
            {
                case "--game": game = Next(); break;
                case "--level": level = int.TryParse(Next(), out var parsed) ? parsed : throw new ToolException("--level 必須是整數。"); break;
                case "--out": output = Next(); break;
                case "--skip-xivapi": skip = true; break;
                case "--xivapi-version": version = Next(); break;
                case "--check-war": check = true; break;
                default:
                    if (args[i].StartsWith("--") || abbreviation != null) throw new ToolException($"無法辨識的參數：{args[i]}\n{Usage}");
                    abbreviation = args[i].ToUpperInvariant();
                    break;
            }
        }
        if (check) abbreviation ??= "WAR";
        if (abbreviation == null) throw new ToolException(Usage);
        if (check && abbreviation != "WAR") throw new ToolException("--check-war 只能用於 WAR。");
        if (level != 90) throw new ToolException("目前只支援 90 級。");
        if (!Directory.Exists(Path.Combine(game, "sqpack")) || !File.Exists(Path.Combine(game, "ffxivgame.ver")))
            throw new ToolException($"找不到客戶端資料：{game}");
        return new(abbreviation, game, level, output ?? Path.Combine(RepoRoot(), "docs", "client-data"), skip, version, check);
    }

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "AnoMech.sln"))) return dir.FullName;
        throw new ToolException("找不到 AnoMech.sln，請用 --out 指定輸出目錄。");
    }
}
```

`tools/ClientData/Program.cs`:

```csharp
using System.Runtime.Loader;

namespace ClientData;

public static class Program
{
    private const string LuminaDll = "C:/Users/Knuckles/AppData/Roaming/FFXIVSimpleLauncher/Dalamud/Injector/Lumina.dll";

    public static int Main(string[] args)
    {
        // The launcher's Lumina.dll reports version 0.0.0.0 while Lumina.Excel.dll
        // references 6.0.0.0; Dalamud ignores the mismatch in-game, a console app must not.
        AssemblyLoadContext.Default.Resolving += (context, name) => name.Name == "Lumina" ? context.LoadFromAssemblyPath(LuminaDll) : null;
        return Tool.Run(args);
    }
}
```

`tools/ClientData/Tool.cs`:

```csharp
using System.Runtime.CompilerServices;

namespace ClientData;

public static class Tool
{
    // Kept out of Main so no Lumina type is resolved before the load handler exists.
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Run(string[] args)
    {
        try
        {
            var options = Options.Parse(args);
            var job = ClientReader.Read(options.GamePath, options.Abbreviation, options.Level);
            var check = XivapiCheck.Skipped("xivapi 核對尚未實作"); // xivapi check
            Directory.CreateDirectory(options.OutputDirectory);
            var path = Path.Combine(options.OutputDirectory, $"{job.Abbreviation}-{job.Level}.md");
            var temp = path + ".tmp";
            File.WriteAllText(temp, Markdown.Write(job, check, DateTimeOffset.Now));
            File.Move(temp, path, overwrite: true);
            Console.WriteLine($"已寫入 {path}（技能 {job.Actions.Count}、特性 {job.Traits.Count}、狀態 {job.Statuses.Count}、需人工確認 {job.ManualChecks.Count}）");
            return check.Completed ? 0 : 2;
        }
        catch (ToolException ex) { Console.Error.WriteLine(ex.Message); return 1; }
        catch (Exception ex) { Console.Error.WriteLine($"讀取失敗：{ex}"); return 1; }
    }
}
```

- [ ] **Step 3: Create the client reader**

`tools/ClientData/ClientReader.cs`:

```csharp
using Lumina;
using Lumina.Data;
using Lumina.Excel.Sheets;
using LuminaAction = Lumina.Excel.Sheets.Action;

namespace ClientData;

public static class ClientReader
{
    public static JobData Read(string gamePath, string abbreviation, int level)
    {
        var data = new GameData(Path.Combine(gamePath, "sqpack"), new LuminaOptions
        {
            DefaultExcelLanguage = Language.TraditionalChinese,
            PanicOnSheetChecksumMismatch = true,
        });
        var gameVersion = File.ReadAllText(Path.Combine(gamePath, "ffxivgame.ver")).Trim();
        var jobs = data.GetExcelSheet<ClassJob>().Where(j => j.JobIndex > 0).ToList();
        var job = jobs.FirstOrDefault(j => j.Abbreviation.ExtractText() == abbreviation);
        if (job.RowId == 0)
            throw new ToolException($"找不到職業縮寫 {abbreviation}。可用：{string.Join(", ", jobs.Select(j => j.Abbreviation.ExtractText()))}");
        var categoryFlag = typeof(ClassJobCategory).GetProperty(abbreviation)
            ?? throw new ToolException($"職業分類表沒有 {abbreviation} 欄位。");
        var parent = job.ClassJobParent.RowId == job.RowId ? 0 : job.ClassJobParent.RowId;
        var categories = data.GetExcelSheet<ClassJobCategory>();
        var actions = data.GetExcelSheet<LuminaAction>();
        bool InCategory(LuminaAction a) => categories.GetRowOrDefault(a.ClassJobCategory.RowId) is { } c && (bool)categoryFlag.GetValue(c)!;

        var selected = actions
            .Where(a => !a.IsPvP && a.ClassJobLevel >= 1 && a.ClassJobLevel <= level
                && (a.ClassJob.RowId == job.RowId || (parent != 0 && a.ClassJob.RowId == parent) || (a.IsRoleAction && InCategory(a))))
            .Select(a => a.RowId)
            .ToHashSet();
        if (selected.Count == 0) throw new ToolException($"{abbreviation} 在 {level} 級查無技能。");

        var replacements = new List<Replacement>();
        foreach (var r in data.GetExcelSheet<ActionIndirection>().Where(r => r.ClassJob.RowId == job.RowId))
        {
            if (actions.GetRowOrDefault(r.Name.RowId) is not { } to || actions.GetRowOrDefault(r.PreviousComboAction.RowId) is not { } from) continue;
            if (to.ClassJobLevel > level || from.ClassJobLevel > level) continue;
            selected.Add(to.RowId);
            selected.Add(from.RowId);
            replacements.Add(new(from.RowId, to.RowId, "技能替換表"));
        }

        var manual = new List<string>();
        var actionText = data.GetExcelSheet<ActionTransient>();
        var actionCategories = data.GetExcelSheet<ActionCategory>();
        var actionRows = selected.OrderBy(id => id).Select(id =>
        {
            var a = actions.GetRow(id);
            var (text, unresolved) = MacroText.Evaluate(actionText.GetRowOrDefault(id)?.Description.ToMacroString() ?? "", job.RowId, level);
            if (unresolved) manual.Add($"技能 {id} {a.Name.ExtractText()} 的說明含無法求值的巨集");
            return new ActionRow(id, a.Name.ExtractText(), actionCategories.GetRowOrDefault(a.ActionCategory.RowId)?.Name.ExtractText() ?? "",
                a.ClassJobLevel, a.Cast100ms, a.Recast100ms, a.CooldownGroup, a.AdditionalCooldownGroup, a.MaxCharges,
                a.PrimaryCostType, a.PrimaryCostValue, a.Range, a.EffectRange, a.CastType, a.CanTargetSelf, a.CanTargetParty,
                a.CanTargetHostile, a.IsPlayerAction, a.IsRoleAction, a.IsPvP, a.ActionCombo.RowId, a.ClassJobCategory.RowId,
                a.StatusGainSelf.RowId, text, unresolved);
        }).ToList();

        var traitText = data.GetExcelSheet<TraitTransient>();
        var traitRows = data.GetExcelSheet<Trait>()
            .Where(t => t.Level >= 1 && t.Level <= level && (t.ClassJob.RowId == job.RowId || (parent != 0 && t.ClassJob.RowId == parent)))
            .OrderBy(t => t.Level).ThenBy(t => t.RowId)
            .Select(t =>
            {
                var (text, unresolved) = MacroText.Evaluate(traitText.GetRowOrDefault(t.RowId)?.Description.ToMacroString() ?? "", job.RowId, level);
                if (unresolved) manual.Add($"特性 {t.RowId} {t.Name.ExtractText()} 的說明含無法求值的巨集");
                return new TraitRow(t.RowId, t.Name.ExtractText(), t.Level, t.Value, t.ClassJob.RowId, text, unresolved);
            }).ToList();

        var nameGroups = actionRows.Where(a => a.Name.Length > 0).GroupBy(a => a.Name).ToList();
        foreach (var g in nameGroups.Where(g => g.Count() > 1))
            manual.Add($"技能名稱「{g.Key}」對應多個 ID：{string.Join("、", g.Select(a => a.Id))}");
        var idsByName = nameGroups.ToDictionary(g => g.Key, g => g.Min(a => a.Id));
        foreach (var t in traitRows)
            foreach (var (from, to) in TraitUpgrades.Find(t.Description, idsByName))
            {
                replacements.Add(new(from, to, $"特性 {t.Id}"));
                manual.Add($"特性 {t.Id} {t.Name}：{from} → {to} 由說明文字比對得出");
            }

        var statuses = data.GetExcelSheet<Status>();
        var statusIdsByName = statuses.Where(s => s.RowId != 0)
            .GroupBy(s => s.Name.ExtractText())
            .Where(g => g.Key.Length >= 2)
            .ToDictionary(g => g.Key, g => g.Select(s => s.RowId).ToList());
        var allText = string.Join("\n", actionRows.Select(a => a.Description).Concat(traitRows.Select(t => t.Description)));
        var statusIds = new SortedSet<uint>();
        foreach (var (name, ids) in statusIdsByName)
            if (allText.Contains(name, StringComparison.Ordinal)) statusIds.UnionWith(ids);
        foreach (var a in actionRows.Where(a => a.StatusGainSelf != 0))
            statusIds.UnionWith(statusIdsByName.GetValueOrDefault(statuses.GetRow(a.StatusGainSelf).Name.ExtractText()) ?? [a.StatusGainSelf]);
        var statusRows = statusIds.Select(id =>
        {
            var s = statuses.GetRow(id);
            var name = s.Name.ExtractText();
            var duplicate = statusIdsByName.TryGetValue(name, out var same) && same.Count > 1;
            var (text, unresolved) = MacroText.Evaluate(s.Description.ToMacroString(), job.RowId, level);
            if (duplicate) manual.Add($"狀態 {id}「{name}」與其他狀態同名：{string.Join("、", same!)}");
            if (unresolved) manual.Add($"狀態 {id}「{name}」的說明含無法求值的巨集");
            return new StatusRow(id, name, s.MaxStacks, s.StatusCategory, s.IsPermanent, s.CanDispel, text, duplicate);
        }).ToList();

        return new JobData(job.RowId, abbreviation, job.Name.ExtractText(), level, gameVersion, actionRows, traitRows, statusRows,
            replacements.Distinct().ToList(), manual.Distinct().ToList());
    }
}
```

- [ ] **Step 4: Create the markdown writer**

`tools/ClientData/Markdown.cs`:

```csharp
using System.Text;

namespace ClientData;

public static class Markdown
{
    public static string Write(JobData job, XivapiCheck check, DateTimeOffset readAt)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {job.Name}（{job.Abbreviation}）{job.Level} 級客戶端資料").AppendLine();
        sb.AppendLine($"- 客戶端版本：`{job.GameVersion}`");
        sb.AppendLine($"- 讀取時間：{readAt:yyyy-MM-dd HH:mm:ss zzz}");
        sb.AppendLine(check.Completed
            ? $"- 對應 xivapi 國際服版本：`{check.Version}`"
            : $"- **未完成 xivapi 核對**：{check.FailureReason}。此清單不得直接用於撰寫規則。");
        sb.AppendLine("- 冷卻組、額外冷卻組為 Action 資料表欄位值，不等於遊戲原生冷卻陣列索引。").AppendLine();

        sb.AppendLine("## 技能").AppendLine();
        sb.AppendLine("| ID | 名稱 | 類型 | 等級 | 詠唱秒 | 冷卻秒 | 冷卻組 | 額外冷卻組 | 充能 | 消耗類型 | 消耗值 | 射程 | 範圍 | 目標 | 連段前置 | 替換為 | 自身附加狀態 |");
        sb.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |");
        foreach (var a in job.Actions)
        {
            var targets = string.Join("／", new[] { a.CanTargetSelf ? "自身" : null, a.CanTargetParty ? "隊友" : null, a.CanTargetHostile ? "敵人" : null }.OfType<string>());
            var replacedBy = string.Join("、", job.Replacements.Where(r => r.From == a.Id).Select(r => $"{r.To}（{r.Source}）"));
            sb.AppendLine($"| {a.Id} | {Cell(a.Name)}{(a.IsRoleAction ? "（職能）" : "")} | {Cell(a.Category)} | {a.Level} | {a.Cast100ms / 10.0:0.#} | {a.Recast100ms / 10.0:0.#} | {a.CooldownGroup} | {a.AdditionalCooldownGroup} | {a.MaxCharges} | {a.CostType} | {a.CostValue} | {a.Range} | {a.EffectRange} | {targets} | {(a.ComboFrom == 0 ? "" : a.ComboFrom.ToString())} | {replacedBy} | {(a.StatusGainSelf == 0 ? "" : a.StatusGainSelf.ToString())} |");
        }

        sb.AppendLine().AppendLine("## 特性").AppendLine();
        sb.AppendLine("| ID | 名稱 | 等級 | 數值 | 說明 |");
        sb.AppendLine("| --- | --- | --- | --- | --- |");
        foreach (var t in job.Traits)
            sb.AppendLine($"| {t.Id} | {Cell(t.Name)} | {t.Level} | {t.Value} | {Cell(t.Description)} |");

        sb.AppendLine().AppendLine("## 狀態").AppendLine();
        sb.AppendLine("| ID | 名稱 | 最大層數 | 分類 | 永久 | 可驅散 | 需人工確認 | 說明 |");
        sb.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- |");
        foreach (var s in job.Statuses)
            sb.AppendLine($"| {s.Id} | {Cell(s.Name)} | {s.MaxStacks} | {s.StatusCategory} | {(s.IsPermanent ? "是" : "")} | {(s.CanDispel ? "是" : "")} | {(s.DuplicateName ? "同名" : "")} | {Cell(s.Description)} |");

        sb.AppendLine().AppendLine("## 技能說明").AppendLine();
        foreach (var a in job.Actions)
        {
            sb.AppendLine($"### {a.Id} {a.Name}{(a.DescriptionUnresolved ? "（含無法求值的巨集）" : "")}").AppendLine();
            sb.AppendLine(a.Description.Length == 0 ? "（無說明）" : a.Description.Replace("\n", "  \n")).AppendLine();
        }

        sb.AppendLine("## 需人工確認").AppendLine();
        if (job.ManualChecks.Count == 0) sb.AppendLine("無");
        foreach (var m in job.ManualChecks) sb.AppendLine($"- {m}");

        sb.AppendLine().AppendLine("## xivapi 差異").AppendLine();
        if (!check.Completed) { sb.AppendLine("未完成核對。"); return sb.ToString(); }
        sb.AppendLine("各版本差異數：" + string.Join("、", check.DiffCounts.Select(kv => $"{kv.Key}={kv.Value}"))).AppendLine();
        if (check.Diffs.Count == 0) { sb.AppendLine($"與 `{check.Version}` 無差異。"); return sb.ToString(); }
        sb.AppendLine("| 資料表 | 列 | 欄位 | 本機 | xivapi |");
        sb.AppendLine("| --- | --- | --- | --- | --- |");
        foreach (var d in check.Diffs)
            sb.AppendLine($"| {d.Sheet} | {d.Row} | {d.Field} | {Cell(d.Local)} | {Cell(d.Remote ?? "（xivapi 無此值）")} |");
        return sb.ToString();
    }

    private static string Cell(string text) => text.Replace("|", "\\|").Replace("\n", "<br>");
}
```

- [ ] **Step 5: Build and run against Warrior**

Run: `$env:MSBuildEnableWorkloadResolver = 'false'; dotnet build "C:\Users\Knuckles\OneDrive\文件\claude\AnoMech\tools\ClientData\ClientData.csproj" 2>&1 | Select-String 'error|個錯誤|個警告'`
Expected: `0 個錯誤`, `0 個警告`.

Run (output to a temporary directory, not `docs/`): `$out = Join-Path $env:TEMP 'clientdata-task2'; $env:MSBuildEnableWorkloadResolver = 'false'; dotnet run --project "C:\Users\Knuckles\OneDrive\文件\claude\AnoMech\tools\ClientData\ClientData.csproj" --no-build -- WAR --skip-xivapi --out $out; "exit=$LASTEXITCODE"; Select-String -Path (Join-Path $out 'WAR-90.md') -Pattern '^\| 7386 \||^\| 52 \||^\| 1177 \||^\| 1303 \||^\| 421 \||未完成 xivapi 核對|49 → 3549'`
Expected: `已寫入 …WAR-90.md` line, `exit=2`, and matches for: the 7386 row containing `| 30 | 8 | 72 | 2 |`, the 52 row containing `| 60 | 20 | 71 | 2 |`, status rows 1177 and 1303 both with `同名`, trait 421, the `未完成 xivapi 核對` banner, and a manual-check line containing `49 → 3549`.

Run error paths: `dotnet run --project "C:\Users\Knuckles\OneDrive\文件\claude\AnoMech\tools\ClientData\ClientData.csproj" --no-build -- XYZ --skip-xivapi --out $out; "exit=$LASTEXITCODE"` → message listing available abbreviations, `exit=1`. `... -- WAR --game C:/nope --out $out; "exit=$LASTEXITCODE"` → `找不到客戶端資料`, `exit=1`.

Run offline checks again: `dotnet run --project "C:\Users\Knuckles\OneDrive\文件\claude\AnoMech\tools\ClientData.Checks\ClientData.Checks.csproj"` → three `PASS:` lines.

If an `ActionCategory` sheet property name differs from `Name`, or another Lumina member does not compile, make the smallest fix and report each change with its compiler error.

- [ ] **Step 6: Commit**

```bash
cd "C:/Users/Knuckles/OneDrive/文件/claude/AnoMech"
git add tools/ClientData/ClientData.csproj tools/ClientData/JobData.cs tools/ClientData/Options.cs tools/ClientData/Program.cs tools/ClientData/Tool.cs tools/ClientData/ClientReader.cs tools/ClientData/Markdown.cs
git commit -m "feat: read local client job data into a markdown list

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 3: xivapi cross-check, Warrior golden check and the Dark Knight list

**Files:**
- Create: `tools/ClientData/XivapiClient.cs`, `tools/ClientData/WarriorGolden.cs`
- Modify: `tools/ClientData/Tool.cs` (the line ending in `// xivapi check`)
- Create (generated): `docs/client-data/DRK-90.md`

**Interfaces:**
- Consumes: `JobData`, `ActionRow`, `TraitRow`, `StatusRow`, `Replacement`, `XivapiCheck`, `Options` (Task 2); `FieldValue`, `FieldDiff`, `XivapiCompare.Diff`, `XivapiCompare.PickVersion` (Task 1).
- Produces:
  - `public static class XivapiClient { public static readonly string[] DefaultVersions; public static IEnumerable<FieldValue> LocalValues(JobData job); public static XivapiCheck Check(JobData job, string? onlyVersion); }`
  - `public static class WarriorGolden { public static IReadOnlyList<string> Check(JobData job); }`

- [ ] **Step 1: Create the xivapi client**

`tools/ClientData/XivapiClient.cs`:

```csharp
using System.Text.Json;

namespace ClientData;

public static class XivapiClient
{
    public static readonly string[] DefaultVersions = ["7.3", "7.31", "7.35", "7.38", "7.4"];
    private static readonly string[] ActionFields = ["ClassJobLevel", "Cast100ms", "Recast100ms", "CooldownGroup", "AdditionalCooldownGroup", "MaxCharges", "PrimaryCostType", "PrimaryCostValue", "Range", "EffectRange", "CastType", "CanTargetSelf", "CanTargetParty", "CanTargetHostile", "IsPlayerAction", "IsPvP", "ActionCombo", "ClassJobCategory"];
    private static readonly string[] TraitFields = ["Level", "ClassJob", "Value"];
    private static readonly string[] StatusFields = ["MaxStacks", "StatusCategory", "IsPermanent", "CanDispel"];
    private static readonly HashSet<string> RowReferences = ["ActionCombo", "ClassJobCategory", "ClassJob"];

    public static IEnumerable<FieldValue> LocalValues(JobData job)
    {
        static string B(bool value) => value ? "true" : "false";
        foreach (var a in job.Actions)
        {
            (string Field, string Value)[] values =
            [
                ("ClassJobLevel", a.Level.ToString()), ("Cast100ms", a.Cast100ms.ToString()), ("Recast100ms", a.Recast100ms.ToString()),
                ("CooldownGroup", a.CooldownGroup.ToString()), ("AdditionalCooldownGroup", a.AdditionalCooldownGroup.ToString()),
                ("MaxCharges", a.MaxCharges.ToString()), ("PrimaryCostType", a.CostType.ToString()), ("PrimaryCostValue", a.CostValue.ToString()),
                ("Range", a.Range.ToString()), ("EffectRange", a.EffectRange.ToString()), ("CastType", a.CastType.ToString()),
                ("CanTargetSelf", B(a.CanTargetSelf)), ("CanTargetParty", B(a.CanTargetParty)), ("CanTargetHostile", B(a.CanTargetHostile)),
                ("IsPlayerAction", B(a.IsPlayerAction)), ("IsPvP", B(a.IsPvP)), ("ActionCombo", a.ComboFrom.ToString()),
                ("ClassJobCategory", a.ClassJobCategory.ToString()),
            ];
            foreach (var (field, value) in values) yield return new("Action", a.Id, field, value);
        }
        foreach (var t in job.Traits)
        {
            yield return new("Trait", t.Id, "Level", t.Level.ToString());
            yield return new("Trait", t.Id, "ClassJob", t.ClassJob.ToString());
            yield return new("Trait", t.Id, "Value", t.Value.ToString());
        }
        foreach (var s in job.Statuses)
        {
            yield return new("Status", s.Id, "MaxStacks", s.MaxStacks.ToString());
            yield return new("Status", s.Id, "StatusCategory", s.StatusCategory.ToString());
            yield return new("Status", s.Id, "IsPermanent", B(s.IsPermanent));
            yield return new("Status", s.Id, "CanDispel", B(s.CanDispel));
        }
    }

    public static XivapiCheck Check(JobData job, string? onlyVersion)
    {
        string[] versions = onlyVersion != null ? [onlyVersion] : DefaultVersions;
        var local = LocalValues(job).ToList();
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var counts = new Dictionary<string, int>();
            var diffs = new Dictionary<string, IReadOnlyList<FieldDiff>>();
            foreach (var version in versions)
            {
                var remote = new List<FieldValue>();
                Fetch(http, version, "Action", job.Actions.Select(a => a.Id), ActionFields, remote);
                Fetch(http, version, "Trait", job.Traits.Select(t => t.Id), TraitFields, remote);
                Fetch(http, version, "Status", job.Statuses.Select(s => s.Id), StatusFields, remote);
                diffs[version] = XivapiCompare.Diff(local, remote);
                counts[version] = diffs[version].Count;
            }
            var best = XivapiCompare.PickVersion(versions, counts);
            return new XivapiCheck(true, best, counts, diffs[best], null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return XivapiCheck.Skipped($"xivapi 連線或查詢失敗：{ex.Message}");
        }
    }

    private static void Fetch(HttpClient http, string version, string sheet, IEnumerable<uint> ids, string[] fields, List<FieldValue> into)
    {
        var requested = string.Join(",", fields.Select(f => RowReferences.Contains(f) ? f + ".value" : f));
        foreach (var chunk in ids.Chunk(100))
        {
            var url = $"https://v2.xivapi.com/api/sheet/{sheet}?version={Uri.EscapeDataString(version)}&rows={string.Join(",", chunk)}&fields={requested}";
            using var response = http.GetAsync(url).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode) throw new HttpRequestException($"{sheet} 版本 {version} 回傳 HTTP {(int)response.StatusCode}");
            using var document = JsonDocument.Parse(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            foreach (var row in document.RootElement.GetProperty("rows").EnumerateArray())
            {
                var id = row.GetProperty("row_id").GetUInt32();
                var values = row.GetProperty("fields");
                foreach (var field in fields)
                    if (values.TryGetProperty(field, out var value)) into.Add(new(sheet, id, field, Normalize(value)));
            }
        }
    }

    private static string Normalize(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Object when value.TryGetProperty("value", out var inner) => Normalize(inner),
        _ => value.ToString(),
    };
}
```

- [ ] **Step 2: Create the Warrior golden check**

`tools/ClientData/WarriorGolden.cs`:

```csharp
namespace ClientData;

// Values hand-verified in docs/superpowers/specs/2026-09-13-warrior-client-contract.md.
public static class WarriorGolden
{
    public static IReadOnlyList<string> Check(JobData job)
    {
        var failures = new List<string>();
        var actions = job.Actions.ToDictionary(a => a.Id);
        ActionRow? A(uint id)
        {
            if (actions.TryGetValue(id, out var a)) return a;
            failures.Add($"缺少技能 {id}");
            return null;
        }
        void Expect(bool ok, string what) { if (!ok) failures.Add(what); }

        foreach (var id in new uint[] { 31, 37, 42, 45, 41, 16462, 3549, 3550, 16465, 16463, 25753 })
            if (A(id) is { } a) Expect(a.CooldownGroup == 58, $"{id} 冷卻組應為 58，實際 {a.CooldownGroup}");
        if (A(52) is { } infuriate)
            Expect(infuriate is { Recast100ms: 600, CooldownGroup: 20, AdditionalCooldownGroup: 71, MaxCharges: 2 },
                $"52 應為 600/20/71/2，實際 {infuriate.Recast100ms}/{infuriate.CooldownGroup}/{infuriate.AdditionalCooldownGroup}/{infuriate.MaxCharges}");
        if (A(7386) is { } onslaught)
            Expect(onslaught is { Recast100ms: 300, CooldownGroup: 8, AdditionalCooldownGroup: 72, MaxCharges: 2 },
                $"7386 應為 300/8/72/2，實際 {onslaught.Recast100ms}/{onslaught.CooldownGroup}/{onslaught.AdditionalCooldownGroup}/{onslaught.MaxCharges}");
        if (A(7387) is { } upheaval && A(25752) is { } orogeny)
            Expect(upheaval.CooldownGroup == orogeny.CooldownGroup, $"7387 與 25752 應同冷卻組，實際 {upheaval.CooldownGroup}/{orogeny.CooldownGroup}");
        foreach (var id in new uint[] { 41, 16462, 3550, 16463, 25752 })
            if (A(id) is { } a) Expect(a.EffectRange == 5, $"{id} 效果範圍應為 5，實際 {a.EffectRange}");
        foreach (var id in new uint[] { 46, 7386, 25753 })
            if (A(id) is { } a) Expect(a.Range == 20, $"{id} 射程應為 20，實際 {a.Range}");

        var statuses = job.Statuses.ToDictionary(s => s.Id);
        foreach (var id in new uint[] { 1177, 1303 })
            Expect(statuses.TryGetValue(id, out var s) && s.DuplicateName, $"狀態 {id} 應列出並標為同名");
        foreach (var id in new uint[] { 1897, 2677, 2624 })
            Expect(statuses.ContainsKey(id), $"缺少狀態 {id}");
        foreach (var (from, to) in new (uint, uint)[] { (38, 7389), (49, 3549), (51, 3550), (3551, 25751) })
            Expect(job.Replacements.Any(r => r.From == from && r.To == to) || job.ManualChecks.Any(m => m.Contains($"{from} → {to}")),
                $"缺少升級 {from} → {to}");
        foreach (var id in new uint[] { 157, 267, 421, 505 })
            Expect(job.Traits.Any(t => t.Id == id), $"缺少特性 {id}");
        return failures;
    }
}
```

- [ ] **Step 3: Wire both into the tool**

In `tools/ClientData/Tool.cs` replace the line

```csharp
            var check = XivapiCheck.Skipped("xivapi 核對尚未實作"); // xivapi check
```

with

```csharp
            if (options.CheckWarrior)
            {
                var failures = WarriorGolden.Check(job);
                foreach (var failure in failures) Console.Error.WriteLine($"戰士標準答案不符：{failure}");
                Console.WriteLine(failures.Count == 0 ? "PASS: 戰士標準答案檢查通過。" : $"FAIL: {failures.Count} 項不符。");
                return failures.Count == 0 ? 0 : 3;
            }
            var check = options.SkipXivapi ? XivapiCheck.Skipped("使用 --skip-xivapi 略過") : XivapiClient.Check(job, options.XivapiVersion);
```

- [ ] **Step 4: Build and run the Warrior golden check**

Run: `$env:MSBuildEnableWorkloadResolver = 'false'; dotnet build "C:\Users\Knuckles\OneDrive\文件\claude\AnoMech\tools\ClientData\ClientData.csproj" 2>&1 | Select-String 'error|個錯誤|個警告'`
Expected: `0 個錯誤`, `0 個警告`.

Run: `$env:MSBuildEnableWorkloadResolver = 'false'; dotnet run --project "C:\Users\Knuckles\OneDrive\文件\claude\AnoMech\tools\ClientData\ClientData.csproj" --no-build -- --check-war; "exit=$LASTEXITCODE"`
Expected: `PASS: 戰士標準答案檢查通過。` and `exit=0`.

If any golden item fails, do not change the expected values or loosen the check. Report DONE_WITH_CONCERNS listing every failure line and what the client data actually contains for it; the controller rules on it.

- [ ] **Step 5: Run xivapi cross-check on Warrior (temporary output)**

Run: `$out = Join-Path $env:TEMP 'clientdata-task3'; $env:MSBuildEnableWorkloadResolver = 'false'; dotnet run --project "C:\Users\Knuckles\OneDrive\文件\claude\AnoMech\tools\ClientData\ClientData.csproj" --no-build -- WAR --out $out; "exit=$LASTEXITCODE"; Select-String -Path (Join-Path $out 'WAR-90.md') -Pattern '對應 xivapi 國際服版本|各版本差異數|未完成 xivapi 核對'`
Expected: `exit=0`, a `對應 xivapi 國際服版本` line and a `各版本差異數：7.3=…、7.31=…、7.35=…、7.38=…、7.4=…` line. Record the chosen version and the counts in the report. (`exit=2` with the incomplete banner means the network failed: retry once; if it still fails, report BLOCKED with the message.)

- [ ] **Step 6: Generate the Dark Knight list**

Run: `$env:MSBuildEnableWorkloadResolver = 'false'; dotnet run --project "C:\Users\Knuckles\OneDrive\文件\claude\AnoMech\tools\ClientData\ClientData.csproj" --no-build -- DRK; "exit=$LASTEXITCODE"`
Expected: `已寫入 C:\Users\Knuckles\OneDrive\文件\claude\AnoMech\docs\client-data\DRK-90.md（技能 …、特性 …、狀態 …、需人工確認 …）` and `exit=0`. In the report, record the four counts, the chosen xivapi version, the per-version diff counts, and the number of diff rows.

Run offline checks once more: `dotnet run --project "C:\Users\Knuckles\OneDrive\文件\claude\AnoMech\tools\ClientData.Checks\ClientData.Checks.csproj"` → three `PASS:` lines.

Confirm the plugin's existing checks are unaffected: `$env:MSBuildEnableWorkloadResolver = 'false'; dotnet run --project "C:\Users\Knuckles\OneDrive\文件\claude\AnoMech\Tests\AnoMech.P3.Tests.csproj"; "p3exit=$LASTEXITCODE"; dotnet run --project "C:\Users\Knuckles\OneDrive\文件\claude\AnoMech\Tests.PartyLayout\Tests.PartyLayout.csproj"; "layoutexit=$LASTEXITCODE"` → `p3exit=0` and `layoutexit=0`.

- [ ] **Step 7: Commit**

```bash
cd "C:/Users/Knuckles/OneDrive/文件/claude/AnoMech"
git add tools/ClientData/XivapiClient.cs tools/ClientData/WarriorGolden.cs tools/ClientData/Tool.cs
git add -f docs/client-data/DRK-90.md
git commit -m "feat: cross-check client data with xivapi and add Dark Knight list

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 4: User review of the Dark Knight list

No code. The controller stops and hands the user `docs/client-data/DRK-90.md` with: the chosen xivapi version and diff counts, the counts of skills/traits/statuses/manual checks, and the request to skim for obviously missing Dark Knight skills, traits or statuses (detailed verification happens in sub-project 2).

- [ ] **Step 1: Hand over the list and wait for the user's confirmation**
