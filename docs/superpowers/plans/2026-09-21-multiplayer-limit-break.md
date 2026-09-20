# 多人極限技同步 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 讓 P6 的四個 LB 練習場景能在多人房間裡跑，全隊共用一條極限技槽，漏開 LB 照實戰判失敗。

**Architecture:** 延伸既有的決定性共模擬模型。只有「誰拿到 LB 槽」與「通過／失敗判定」歸主機權威；LB 的效果（幾何、量表、動畫）由各機器用送來的座標各自重放。AI 的 LB 不上線，靠決定性保持一致。

**Tech Stack:** C# / .NET 9、Dalamud.NET.Sdk 13、LiteNetLib 2.1.4

**Spec:** `docs/superpowers/specs/2026-09-21-multiplayer-limit-break-design.md`

## Global Constraints

- 建置指令固定為 `DALAMUD_HOME="$APPDATA/FFXIVSimpleLauncher/Dalamud/Injector" dotnet build AnoMech/AnoMech.csproj`，必須 0 警告 0 錯誤。
- 測試指令固定為 `MSBuildEnableWorkloadResolver=false dotnet run --project Tests/AnoMech.P3.Tests.csproj`，必須全數通過。
- 新增的 `.cs` 檔若要被測試看到，**必須**加進 `Tests/AnoMech.P3.Tests.csproj` 的 `<Compile Include="../AnoMech/..." />` 清單。
- 註解只寫程式碼講不出來的理由，不複述符號名稱（見 `CLAUDE.md`）。
- 協定版本最終為 `NetProtocol.Version = 6`。
- 不計算傷害、治療、護盾數值 —— 沿用專案既有範圍。

---

### Task 1: LimitBreakArbiter（純邏輯，可單元測試）

裁判邏輯不能埋進 `MultiplayerSession` —— 那個類別需要 `Game` 實體，現有測試碰不到。

**Files:**
- Create: `AnoMech/Core/Multiplayer/LimitBreakArbiter.cs`
- Modify: `Tests/AnoMech.P3.Tests.csproj`
- Test: `Tests/MultiplayerChecks.cs`

**Interfaces:**
- Produces: `LimitBreakArbiter` 具備 `byte Holder { get; }`（255 = 空）、`uint[] Acks { get; }`（長度 8）、
  `bool TryClaim(byte role, uint requestId)`、`void Release()`、`void Reset()`。

- [ ] **Step 1: 加進測試專案的編譯清單**

在 `Tests/AnoMech.P3.Tests.csproj` 裡，`MultiplayerChecks` 相關 Compile 項目附近加入：

```xml
<Compile Include="../AnoMech/Core/Multiplayer/LimitBreakArbiter.cs" />
```

- [ ] **Step 2: 寫失敗測試**

在 `Tests/MultiplayerChecks.cs` 的 `Run()` 裡，`DisconnectedSlotReturnsToAi();` 之後加入 `LimitBreakArbitration();`，
並新增方法：

```csharp
    private static void LimitBreakArbitration()
    {
        var arbiter = new LimitBreakArbiter();
        Check(arbiter.Holder == 255, "a fresh arbiter holds nothing");

        Check(arbiter.TryClaim(0, 1), "the first claim wins the bar");
        Check(arbiter.Holder == 0 && arbiter.Acks[0] == 1, "the winner holds the bar and its request is acknowledged");

        Check(!arbiter.TryClaim(3, 7), "a second claim loses while the bar is held");
        Check(arbiter.Acks[3] == 7, "a losing claim is still acknowledged, or the client waits forever");
        Check(arbiter.Holder == 0, "losing a claim does not move the bar");

        Check(!arbiter.TryClaim(0, 1), "replaying the same request must not claim twice");

        arbiter.Release();
        Check(arbiter.Holder == 255, "releasing frees the bar");
        Check(arbiter.TryClaim(3, 8), "the next request wins once the bar is free");

        arbiter.Reset();
        Check(arbiter.Holder == 255 && arbiter.Acks[3] == 0, "reset clears the holder and every ack");
    }
```

- [ ] **Step 3: 跑測試確認失敗**

執行：`MSBuildEnableWorkloadResolver=false dotnet run --project Tests/AnoMech.P3.Tests.csproj`
預期：編譯失敗，`error CS0246: 找不到類型或命名空間名稱 'LimitBreakArbiter'`

- [ ] **Step 4: 實作**

建立 `AnoMech/Core/Multiplayer/LimitBreakArbiter.cs`：

```csharp
namespace AnoMech.Core.Multiplayer;

// The party shares one limit break bar, so someone has to decide who got it. Kept free of Game and
// networking types: MultiplayerSession needs both, and the tests can reach neither.
public sealed class LimitBreakArbiter
{
    public const byte Nobody = 255;

    private readonly uint[] acks = new uint[Wire.Slots];

    public byte Holder { get; private set; } = Nobody;
    public uint[] Acks => acks;

    // Acknowledges the request either way: a client that never hears back cannot tell a lost race
    // from a lost packet, and would wait forever.
    public bool TryClaim(byte role, uint requestId)
    {
        if (role >= Wire.Slots) return false;
        var fresh = requestId > acks[role];
        acks[role] = requestId > acks[role] ? requestId : acks[role];
        if (!fresh || Holder != Nobody) return false;
        Holder = role;
        return true;
    }

    public void Release() => Holder = Nobody;

    public void Reset()
    {
        Holder = Nobody;
        System.Array.Clear(acks);
    }
}
```

- [ ] **Step 5: 跑測試確認通過**

執行：`MSBuildEnableWorkloadResolver=false dotnet run --project Tests/AnoMech.P3.Tests.csproj`
預期：全數 PASS

- [ ] **Step 6: Commit**

```bash
git add AnoMech/Core/Multiplayer/LimitBreakArbiter.cs Tests/MultiplayerChecks.cs Tests/AnoMech.P3.Tests.csproj
git commit -m "Add a testable arbiter for the shared limit break bar"
```

---

### Task 2: 線上協定（訊息、幀欄位、版本）

**Files:**
- Modify: `AnoMech/Core/Net/NetProtocol.cs`
- Modify: `AnoMech/Core/Multiplayer/MultiplayerMessages.cs`
- Test: `Tests/MultiplayerChecks.cs`

**Interfaces:**
- Consumes: Task 1 的 `LimitBreakArbiter.Nobody`。
- Produces: `MessageType.LimitBreakUsed = 21`；
  `LimitBreakUsedDto(uint RunId, uint RequestId, uint ActionId, Vector3 CasterPosition, Vector3? Aim)`
  具 `Write(NetDataWriter)` 與 `static bool TryRead(NetDataReader, out LimitBreakUsedDto)`；
  `TickFrame.LimitBreakHolder`（byte）、`TickFrame.LimitBreaks`（`List<(byte Role, uint ActionId, Vector3 CasterPosition, Vector3? Aim)>`）、
  `TickFrame.LimitBreakAcks`（`uint[8]`）、`TickFrame.FailReason`（`string?`）。

- [ ] **Step 1: 寫失敗測試**

在 `Tests/MultiplayerChecks.cs` 的 `MessagesRoundTripAndRejectGarbage()` 裡，
`var marksBack = RoundTrip(new MarkersDto(...)` 那一行之前加入：

```csharp
        var lbUsed = RoundTrip(new LimitBreakUsedDto(3, 9, 208, new Vector3(1.5f, 0f, -2.5f), new Vector3(0f, 0f, 4f)).Write,
            (NetDataReader r, out LimitBreakUsedDto v) => LimitBreakUsedDto.TryRead(r, out v), "LimitBreakUsed");
        Check(lbUsed == new LimitBreakUsedDto(3, 9, 208, new Vector3(1.5f, 0f, -2.5f), new Vector3(0f, 0f, 4f)),
            "LimitBreakUsed keeps its run, request, action and both points");
        var lbGround = RoundTrip(new LimitBreakUsedDto(3, 10, 205, new Vector3(0f, 0f, 0f), null).Write,
            (NetDataReader r, out LimitBreakUsedDto v) => LimitBreakUsedDto.TryRead(r, out v), "LimitBreakUsed(no aim)");
        Check(lbGround.Aim is null, "a missing aim point survives as null");
        Check(Rejects(w => { w.Put(3u); w.Put(9u); w.Put(208u); w.Put(float.NaN); w.Put(0f); w.Put(0f); w.Put(false); },
            (NetDataReader r, out LimitBreakUsedDto v) => LimitBreakUsedDto.TryRead(r, out v)),
            "LimitBreakUsed with a non-finite coordinate is rejected");

        var lbFrame = new TickFrame { Tick = 6, LimitBreakHolder = 2, FailReason = "魔數：H1 未在 DEBUFF 到期前完成 LB。" };
        lbFrame.LimitBreaks.Add((2, 208, new Vector3(0f, 0f, 1f), null));
        lbFrame.LimitBreakAcks[2] = 4;
        var lbFrameBack = RoundTrip(w => FrameCodec.Write(w, lbFrame),
            (NetDataReader r, out TickFrame v) => FrameCodec.TryRead(r, out v), "TickFrame(limit breaks)");
        Check(lbFrameBack.LimitBreakHolder == 2 && lbFrameBack.LimitBreakAcks[2] == 4,
            "the frame carries the bar holder and the acks");
        Check(lbFrameBack.LimitBreaks.Count == 1 && lbFrameBack.LimitBreaks[0].ActionId == 208,
            "the frame carries the accepted limit breaks");
        Check(lbFrameBack.FailReason == "魔數：H1 未在 DEBUFF 到期前完成 LB。", "the frame carries the failure reason");

        var plainFrame = RoundTrip(w => FrameCodec.Write(w, new TickFrame { Tick = 7 }),
            (NetDataReader r, out TickFrame v) => FrameCodec.TryRead(r, out v), "TickFrame(plain)");
        Check(plainFrame.LimitBreakHolder == LimitBreakArbiter.Nobody && plainFrame.LimitBreaks.Count == 0
              && plainFrame.FailReason is null, "a frame with no limit break state stays empty");
```

檔案頂端若沒有 `using System.Numerics;` 與 `using AnoMech.Core.Multiplayer;` 請補上。

- [ ] **Step 2: 跑測試確認失敗**

執行：`MSBuildEnableWorkloadResolver=false dotnet run --project Tests/AnoMech.P3.Tests.csproj`
預期：編譯失敗，`error CS0246: 找不到類型或命名空間名稱 'LimitBreakUsedDto'`

- [ ] **Step 3: 加入訊息型別與版本**

在 `AnoMech/Core/Net/NetProtocol.cs` 的 `MessageType` 列舉尾端加入：

```csharp
    LimitBreakUsed = 21,
```

同檔案把版本常數改為：

```csharp
    public const ushort Version = 6; // Shared limit break bar: the room must agree on who holds it.
```

- [ ] **Step 4: 加入 DTO**

在 `AnoMech/Core/Multiplayer/MultiplayerMessages.cs` 的 `MarkersDto` 之後加入：

```csharp
// A client asking for the shared bar. Carries no object id: SimEnemy ids are allocated per machine,
// so geometry is rebuilt from the caster's position and the point it aimed at instead.
public readonly record struct LimitBreakUsedDto(uint RunId, uint RequestId, uint ActionId, Vector3 CasterPosition, Vector3? Aim)
{
    public void Write(NetDataWriter writer)
    {
        writer.Put(RunId);
        writer.Put(RequestId);
        writer.Put(ActionId);
        writer.Put(CasterPosition.X);
        writer.Put(CasterPosition.Y);
        writer.Put(CasterPosition.Z);
        writer.Put(Aim.HasValue);
        if (Aim is { } aim)
        {
            writer.Put(aim.X);
            writer.Put(aim.Y);
            writer.Put(aim.Z);
        }
    }

    public static bool TryRead(NetDataReader reader, out LimitBreakUsedDto used)
    {
        used = default;
        if (!reader.TryGetUInt(out var runId) || !reader.TryGetUInt(out var requestId) || !reader.TryGetUInt(out var actionId)
            || !reader.TryGetFinite(Wire.CoordinateLimit, out var cx) || !reader.TryGetFinite(Wire.CoordinateLimit, out var cy)
            || !reader.TryGetFinite(Wire.CoordinateLimit, out var cz) || !reader.TryGetBool(out var hasAim)) return false;
        Vector3? aim = null;
        if (hasAim)
        {
            if (!reader.TryGetFinite(Wire.CoordinateLimit, out var ax) || !reader.TryGetFinite(Wire.CoordinateLimit, out var ay)
                || !reader.TryGetFinite(Wire.CoordinateLimit, out var az)) return false;
            aim = new Vector3(ax, ay, az);
        }
        used = new LimitBreakUsedDto(runId, requestId, actionId, new Vector3(cx, cy, cz), aim);
        return true;
    }
}
```

- [ ] **Step 5: 加入幀欄位**

在同檔案的 `TickFrame` 類別中，`public SyncStateDto? Sync { get; set; }` 之前加入：

```csharp
    public byte LimitBreakHolder { get; set; } = LimitBreakArbiter.Nobody;
    public List<(byte Role, uint ActionId, Vector3 CasterPosition, Vector3? Aim)> LimitBreaks { get; } = [];
    public uint[] LimitBreakAcks { get; } = new uint[Wire.Slots];
    // Judging moved to the host, so the reason has to travel to the room that only sees the wipe.
    public string? FailReason { get; set; }
```

- [ ] **Step 6: 加入編解碼**

在 `FrameCodec` 的旗標常數後加入：

```csharp
    private const byte HasLimitBreaks = 16;
    private const byte HasFailReason = 32;
    private const int MaxLimitBreaksPerFrame = 8;
    private const int MaxFailReasonBytes = 256;
```

`Write` 的 `flags` 計算改為（新增兩個條件）：

```csharp
        var flags = (byte)((frame.Markers != null ? HasMarkers : 0) | (frame.Invulns.Count > 0 ? HasInvulns : 0)
                           | (frame.Sync != null ? HasSync : 0) | (frame.TimelineMask != 0 ? HasTimelines : 0)
                           | (frame.LimitBreaks.Count > 0 || frame.LimitBreakHolder != LimitBreakArbiter.Nobody ? HasLimitBreaks : 0)
                           | (frame.FailReason != null ? HasFailReason : 0));
```

`Write` 中 `frame.Sync?.Write(writer);` 之前加入：

```csharp
        if ((flags & HasLimitBreaks) != 0)
        {
            writer.Put(frame.LimitBreakHolder);
            for (var i = 0; i < Wire.Slots; i++) writer.Put(frame.LimitBreakAcks[i]);
            var count = Math.Min(frame.LimitBreaks.Count, MaxLimitBreaksPerFrame);
            writer.Put((byte)count);
            for (var i = 0; i < count; i++)
            {
                var (role, actionId, caster, aim) = frame.LimitBreaks[i];
                writer.Put(role);
                writer.Put(actionId);
                writer.Put(caster.X);
                writer.Put(caster.Y);
                writer.Put(caster.Z);
                writer.Put(aim.HasValue);
                if (aim is { } point)
                {
                    writer.Put(point.X);
                    writer.Put(point.Y);
                    writer.Put(point.Z);
                }
            }
        }
        if (frame.FailReason != null) writer.Put(frame.FailReason);
```

`TryRead` 的旗標檢查改為：

```csharp
        if ((flags & ~(HasMarkers | HasInvulns | HasSync | HasTimelines | HasLimitBreaks | HasFailReason)) != 0) return false;
```

`TryRead` 中讀完 Invulns 之後、讀 Sync 之前加入：

```csharp
        if ((flags & HasLimitBreaks) != 0)
        {
            if (!reader.TryGetByte(out var holder) || !Wire.ValidRole(holder, true)) return false;
            result.LimitBreakHolder = holder;
            for (var i = 0; i < Wire.Slots; i++)
                if (!reader.TryGetUInt(out result.LimitBreakAcks[i])) return false;
            if (!reader.TryGetByte(out var lbCount) || lbCount > MaxLimitBreaksPerFrame) return false;
            for (var i = 0; i < lbCount; i++)
            {
                if (!reader.TryGetByte(out var role) || !Wire.ValidRole(role, false) || !reader.TryGetUInt(out var actionId)
                    || !reader.TryGetFinite(Wire.CoordinateLimit, out var cx) || !reader.TryGetFinite(Wire.CoordinateLimit, out var cy)
                    || !reader.TryGetFinite(Wire.CoordinateLimit, out var cz) || !reader.TryGetBool(out var hasAim)) return false;
                Vector3? aim = null;
                if (hasAim)
                {
                    if (!reader.TryGetFinite(Wire.CoordinateLimit, out var ax) || !reader.TryGetFinite(Wire.CoordinateLimit, out var ay)
                        || !reader.TryGetFinite(Wire.CoordinateLimit, out var az)) return false;
                    aim = new Vector3(ax, ay, az);
                }
                result.LimitBreaks.Add((role, actionId, new Vector3(cx, cy, cz), aim));
            }
        }
        if ((flags & HasFailReason) != 0)
        {
            if (!reader.TryGetString(out var reason) || reason.Length == 0
                || System.Text.Encoding.UTF8.GetByteCount(reason) > MaxFailReasonBytes) return false;
            result.FailReason = reason;
        }
```

- [ ] **Step 7: 跑測試確認通過**

執行：`MSBuildEnableWorkloadResolver=false dotnet run --project Tests/AnoMech.P3.Tests.csproj`
預期：全數 PASS

- [ ] **Step 8: 建置確認無警告**

執行：`DALAMUD_HOME="$APPDATA/FFXIVSimpleLauncher/Dalamud/Injector" dotnet build AnoMech/AnoMech.csproj`
預期：`建置成功。` 0 警告 0 錯誤

- [ ] **Step 9: Commit**

```bash
git add AnoMech/Core/Net/NetProtocol.cs AnoMech/Core/Multiplayer/MultiplayerMessages.cs Tests/MultiplayerChecks.cs
git commit -m "Carry limit break claims, the bar holder and a failure reason on the wire"
```

---

### Task 3: 客戶端也要拿到人類格子遮罩

沒有這步，AI 會在客戶端替真人代按 LB，主機卻不會，兩邊立刻分歧。

**Files:**
- Modify: `AnoMech/Core/Multiplayer/MultiplayerSession.cs`（`ClientStart` 內的 `MultiplayerContext.Begin` 呼叫）
- Test: `Tests/MultiplayerChecks.cs`

**Interfaces:**
- Consumes: 既有的 `MultiplayerContext.Begin(MultiplayerRole, int humanSlots, byte[]?)` 與 `StartRunDto.SlotOwners`。

- [ ] **Step 1: 寫失敗測試**

在 `Tests/MultiplayerChecks.cs` 的 `Run()` 裡，`LimitBreakArbitration();` 之後加入 `ClientKnowsHumanSlots();`，並新增：

```csharp
    private static void ClientKnowsHumanSlots()
    {
        // SlotOwners holds a player id per slot, or Wire.NoPlayer where the AI drives it.
        byte[] owners = [1, Wire.NoPlayer, 2, Wire.NoPlayer, Wire.NoPlayer, Wire.NoPlayer, Wire.NoPlayer, Wire.NoPlayer];
        Check(Wire.HumanSlotMask(owners) == 0b0000_0101, "every slot with an owner counts as human");
        Check(Wire.HumanSlotMask([Wire.NoPlayer, Wire.NoPlayer, Wire.NoPlayer, Wire.NoPlayer,
            Wire.NoPlayer, Wire.NoPlayer, Wire.NoPlayer, Wire.NoPlayer]) == 0, "an all-AI party has no human slots");
    }
```

- [ ] **Step 2: 跑測試確認失敗**

執行：`MSBuildEnableWorkloadResolver=false dotnet run --project Tests/AnoMech.P3.Tests.csproj`
預期：編譯失敗，`Wire` 沒有 `HumanSlotMask`

- [ ] **Step 3: 實作**

`SlotOwners` 是線上資料，所以放在 `Wire` —— 那個類別已經被測試專案連結，不必新增檔案。
在 `AnoMech/Core/Multiplayer/MultiplayerMessages.cs` 的 `Wire` 類別中，`ValidRole` 之後加入：

```csharp
    // The client used to begin with an empty mask, so IsHumanControlled was always false there and the AI
    // pressed limit breaks for remote players that the host left alone.
    public static int HumanSlotMask(byte[] slotOwners)
    {
        var mask = 0;
        for (var slot = 0; slot < Wire.Slots && slot < slotOwners.Length; slot++)
            if (slotOwners[slot] != Wire.NoPlayer) mask |= 1 << slot;
        return mask;
    }
```

把 `ClientStart` 裡的

```csharp
            MultiplayerContext.Begin(MultiplayerRole.Client, 0, start.OverridePayload);
```

改為

```csharp
            MultiplayerContext.Begin(MultiplayerRole.Client, Wire.HumanSlotMask(start.SlotOwners), start.OverridePayload);
```

- [ ] **Step 4: 跑測試確認通過**

執行：`MSBuildEnableWorkloadResolver=false dotnet run --project Tests/AnoMech.P3.Tests.csproj`
預期：全數 PASS

- [ ] **Step 5: Commit**

```bash
git add AnoMech/Core/Multiplayer/ Tests/MultiplayerChecks.cs
git commit -m "Give clients the human slot mask so AI skips the same slots the host does"
```

---

### Task 4: AI 只補沒有真人的格子

**Files:**
- Modify: `AnoMech/Scenarios/Top/P6AlphaOmega/TopP6FullAi.cs:174-177`
- Test: `Tests/MultiplayerChecks.cs`

**Interfaces:**
- Consumes: Task 3 建立的客戶端遮罩。

- [ ] **Step 1: 寫失敗測試**

在 `Tests/MultiplayerChecks.cs` 的 `Run()` 裡，`ClientKnowsHumanSlots();` 之後加入 `AiLeavesHumanLimitBreaks();`，並新增：

```csharp
    private static void AiLeavesHumanLimitBreaks()
    {
        MultiplayerContext.Begin(MultiplayerRole.Host, 0b0000_0101, null);
        Check(TopP6LimitBreakRules.AiMayPress(PartyRole.RegenHealer), "the AI covers a slot with no player in it");
        Check(!TopP6LimitBreakRules.AiMayPress(PartyRole.MainTank), "a human slot presses its own limit break");
        Check(!TopP6LimitBreakRules.AiMayPress(PartyRole.OffTank), "every human slot is left alone, not just the local one");
        MultiplayerContext.End();
    }
```

檔案頂端補上 `using AnoMech.Scenarios.Top.P6AlphaOmega;` 與 `using AnoMech.Core.Game.Party;`。
判斷放在 `TopP6LimitBreakRules` —— 那個檔已經被測試專案連結，且只相依 `PartyRole` 與 `MultiplayerContext`，
不會牽出 `SimWorld`。不需要新增檔案，也不需要改 csproj。

- [ ] **Step 2: 跑測試確認失敗**

執行：`MSBuildEnableWorkloadResolver=false dotnet run --project Tests/AnoMech.P3.Tests.csproj`
預期：編譯失敗，`TopP6LimitBreakRules` 沒有 `AiMayPress`

- [ ] **Step 3: 實作**

在 `AnoMech/Scenarios/Top/P6AlphaOmega/TopP6LimitBreakRules.cs` 中加入
（檔案頂端補上 `using AnoMech.Core.Game;` 與 `using AnoMech.Core.Game.Party;`）：

```csharp
    // A human slot presses its own limit break; the AI only fills the seats nobody is sitting in.
    internal static bool AiMayPress(PartyRole role) => !MultiplayerContext.IsHumanControlled((int)role);
```

把 `StartLimitBreak` 開頭的守衛

```csharp
        if (runtime == null || role == world.Party.PlayerRole ||
            !runtime.IsAvailable || runtime.IsBusy(role))
            return;
```

改為

```csharp
        if (runtime == null || role == world.Party.PlayerRole || !TopP6LimitBreakRules.AiMayPress(role) ||
            !runtime.IsAvailable || runtime.IsBusy(role))
            return;
```

- [ ] **Step 4: 跑測試確認通過**

執行：`MSBuildEnableWorkloadResolver=false dotnet run --project Tests/AnoMech.P3.Tests.csproj`
預期：全數 PASS

- [ ] **Step 5: Commit**

```bash
git add AnoMech/Scenarios/Top/P6AlphaOmega/ Tests/MultiplayerChecks.cs
git commit -m "Stop the AI pressing limit breaks for slots a player is sitting in"
```

---

### Task 5: 本地 LB 的通知掛鉤與遠端重放入口

**Files:**
- Modify: `AnoMech/Core/Game/MultiplayerContext.cs`
- Modify: `AnoMech/Core/Combat/PracticeLimitBreakRuntime.cs`

**Interfaces:**
- Produces: `MultiplayerContext.LimitBreakUsed`（`Action<int, uint, Vector3, Vector3?>?`，參數為 role、actionId、施法者座標、瞄準點）；
  `PracticeLimitBreakRuntime.TryStartRemote(PartyRole role, uint actionId, Vector3 casterPosition, Vector3? aim)`。

- [ ] **Step 1: 加入通知掛鉤**

在 `AnoMech/Core/Game/MultiplayerContext.cs` 的 `InvulnGranted` 之後加入：

```csharp
    // The local player's own limit break: the host arbitrates the shared bar, so it has to hear about it.
    public static Action<int, uint, System.Numerics.Vector3, System.Numerics.Vector3?>? LimitBreakUsed { get; set; }
```

並在 `Begin` 中 `InvulnGranted = null;` 之後加入 `LimitBreakUsed = null;`。
同樣在 `End()` 裡把它清為 null（比照 `InvulnGranted` 的處理）。

- [ ] **Step 2: 從 TryStart 通知**

在 `AnoMech/Core/Combat/PracticeLimitBreakRuntime.cs` 的 `TryStart` 中，
`SyncPlayerLock(pending);` 與 `return true;` 之間加入：

```csharp
        // Only the local player's press needs announcing: AI presses are deterministic on every machine,
        // and a replayed remote press must not echo back out.
        if (caster is SimPlayer)
            MultiplayerContext.LimitBreakUsed?.Invoke((int)role, actionId, caster.Position, location ?? target?.Position);
```

同樣在前面 `if (cast.ActionId == 0) { Complete(pending); return true; }` 的 `Complete(pending);` 之後、`return true;` 之前加入同一段
（瞬發技能走的是那條路徑）。

檔案頂端補上 `using AnoMech.Core.Game;`（若尚未存在）。

- [ ] **Step 3: 加入遠端重放入口**

在同檔案 `TryStart` 之後加入：

```csharp
    // TryStart refuses a location for anything that is not ground targeted, so a relayed aim point becomes
    // a facing instead and the geometry falls back to the caster's own rotation.
    internal bool TryStartRemote(PartyRole role, uint actionId, Vector3 casterPosition, Vector3? aim)
    {
        if (disposed || !TryGetValidatedAction(actionId, out var action)) return false;
        var caster = world.Party.Get(role);
        if (caster == null) return false;
        if (action.TargetArea) return TryStart(role, actionId, aim, null);
        if (aim is { } point)
            caster.SetRotation(MathF.Atan2(point.X - casterPosition.X, point.Z - casterPosition.Z));
        return TryStart(role, actionId, null, null);
    }
```

- [ ] **Step 4: 建置確認**

執行：`DALAMUD_HOME="$APPDATA/FFXIVSimpleLauncher/Dalamud/Injector" dotnet build AnoMech/AnoMech.csproj`
預期：`建置成功。` 0 警告 0 錯誤

- [ ] **Step 5: 跑既有測試確認沒壞**

執行：`MSBuildEnableWorkloadResolver=false dotnet run --project Tests/AnoMech.P3.Tests.csproj`
預期：全數 PASS

- [ ] **Step 6: Commit**

```bash
git add AnoMech/Core/Game/MultiplayerContext.cs AnoMech/Core/Combat/PracticeLimitBreakRuntime.cs
git commit -m "Announce the local player's limit break and accept a relayed one"
```

---

### Task 6: 主機端裁判與廣播

**Files:**
- Modify: `AnoMech/Core/Multiplayer/MultiplayerSession.cs`

**Interfaces:**
- Consumes: Task 1 `LimitBreakArbiter`、Task 2 `LimitBreakUsedDto` 與幀欄位、Task 5 的掛鉤與 `TryStartRemote`。

- [ ] **Step 1: HostRun 加狀態**

在 `HostRun` 類別中，`public List<(byte Role, float Seconds)> PendingInvulns { get; } = [];` 之後加入：

```csharp
        public LimitBreakArbiter Bar { get; } = new();
        public List<(byte Role, uint ActionId, Vector3 CasterPosition, Vector3? Aim)> PendingLimitBreaks { get; } = [];
        public bool LimitBreakDirty { get; set; }
        public string? PendingFailReason { get; set; }
```

- [ ] **Step 2: 接住主機自己的 LB**

在 `HostStartRun` 中，設定 `MultiplayerContext.InvulnGranted = ...` 那一行之後加入：

```csharp
        MultiplayerContext.LimitBreakUsed = (role, actionId, caster, aim) =>
        {
            if (!run.Bar.TryClaim((byte)role, run.Bar.Acks[role] + 1)) return;
            run.PendingLimitBreaks.Add(((byte)role, actionId, caster, aim));
            run.LimitBreakDirty = true;
        };
```

- [ ] **Step 3: 處理客戶端的請求**

在 `OnHostMessage` 的 `case MessageType.Markers ...` 之後加入：

```csharp
            case MessageType.LimitBreakUsed when LimitBreakUsedDto.TryRead(reader, out var used):
                if (hostRun is { } lbRun && used.RunId == lbRun.RunId && lbRun.Remote.TryGetValue(player.Id, out var caster))
                {
                    lbRun.LimitBreakDirty = true;
                    if (lbRun.Bar.TryClaim(caster.Role, used.RequestId)
                        && game.World.LimitBreaks?.TryStartRemote((PartyRole)caster.Role, used.ActionId, used.CasterPosition, used.Aim) == true)
                        lbRun.PendingLimitBreaks.Add((caster.Role, used.ActionId, used.CasterPosition, used.Aim));
                    else
                        lbRun.Bar.Release();
                }
                break;
```

- [ ] **Step 4: 槽釋放時通知裁判**

在 `RunHostTick` 中，`run.PendingInvulns.Clear();` 之後加入：

```csharp
        run.PendingLimitBreaks.Clear();
```

在同一方法 `game.Tick(Step);` 之後加入：

```csharp
        // The runtime frees the bar on its own (a cancel, a refill, a finished cast); mirror that decision.
        if (run.Bar.Holder != LimitBreakArbiter.Nobody && game.World.LimitBreaks?.IsAvailable == true)
        {
            run.Bar.Release();
            run.LimitBreakDirty = true;
        }
```

- [ ] **Step 5: 放進幀**

在 `RunHostTick` 中 `frame.Invulns.AddRange(run.PendingInvulns);` 之後加入：

```csharp
        if (run.LimitBreakDirty || run.PendingLimitBreaks.Count > 0)
        {
            frame.LimitBreakHolder = run.Bar.Holder;
            Array.Copy(run.Bar.Acks, frame.LimitBreakAcks, Wire.Slots);
            frame.LimitBreaks.AddRange(run.PendingLimitBreaks);
            run.LimitBreakDirty = false;
        }
        if (run.PendingFailReason is { } reason)
        {
            frame.FailReason = reason;
            run.PendingFailReason = null;
        }
```

- [ ] **Step 6: 結束時清乾淨**

在 `EndHostRun` 中 `MultiplayerContext.End();` 之前加入：

```csharp
        MultiplayerContext.LimitBreakUsed = null;
        run.Bar.Reset();
```

- [ ] **Step 7: 建置與測試**

執行：`DALAMUD_HOME="$APPDATA/FFXIVSimpleLauncher/Dalamud/Injector" dotnet build AnoMech/AnoMech.csproj`
預期：`建置成功。` 0 警告 0 錯誤

執行：`MSBuildEnableWorkloadResolver=false dotnet run --project Tests/AnoMech.P3.Tests.csproj`
預期：全數 PASS

- [ ] **Step 8: Commit**

```bash
git add AnoMech/Core/Multiplayer/MultiplayerSession.cs
git commit -m "Arbitrate the shared limit break bar on the host and publish it in the frame"
```

---

### Task 7: 客戶端送出、重放與回捲

**Files:**
- Modify: `AnoMech/Core/Multiplayer/MultiplayerSession.cs`

**Interfaces:**
- Consumes: Task 6 的幀欄位內容、Task 5 的掛鉤與 `TryStartRemote`。

- [ ] **Step 1: ClientRun 加狀態**

在 `ClientRun` 類別中，`public uint MarkerRequest { get; set; }` 之後加入：

```csharp
        public uint LimitBreakRequest { get; set; }
        public uint PendingLimitBreakRequest { get; set; }
```

- [ ] **Step 2: 本地按下時送出**

在 `ClientStart` 中，`clientRun = run;` 之前加入：

```csharp
        MultiplayerContext.LimitBreakUsed = (role, actionId, caster, aim) =>
        {
            if (clientRun is not { } sending || role != sending.Role) return;
            sending.LimitBreakRequest++;
            sending.PendingLimitBreakRequest = sending.LimitBreakRequest;
            Net.Client.Send(MessageType.LimitBreakUsed,
                new LimitBreakUsedDto(sending.RunId, sending.LimitBreakRequest, actionId, caster, aim).Write,
                DeliveryMethod.ReliableOrdered);
        };
```

- [ ] **Step 3: 套用幀裡的 LB**

在 `RunClientTick` 中，`foreach (var (role, seconds) in frame.Invulns) party.GiveInvuln((PartyRole)role, seconds);` 之後加入：

```csharp
        foreach (var (role, actionId, caster, aim) in frame.LimitBreaks)
        {
            // Our own press already started locally; replaying it would double the cast.
            if (role == run.Role) continue;
            game.World.LimitBreaks?.TryStartRemote((PartyRole)role, actionId, caster, aim);
        }
        if (frame.LimitBreakAcks[run.Role] >= run.PendingLimitBreakRequest && run.PendingLimitBreakRequest != 0)
        {
            if (frame.LimitBreakHolder != run.Role)
            {
                game.World.LimitBreaks?.Cancel((PartyRole)run.Role);
                Chat("極限技已被其他人使用。");
            }
            run.PendingLimitBreakRequest = 0;
        }
        if (frame.FailReason is { } reason) Chat(reason);
```

- [ ] **Step 4: 結束時清乾淨**

在 `EndClientRun` 中 `MultiplayerContext.End();` 之前加入：

```csharp
        MultiplayerContext.LimitBreakUsed = null;
```

- [ ] **Step 5: 建置與測試**

執行：`DALAMUD_HOME="$APPDATA/FFXIVSimpleLauncher/Dalamud/Injector" dotnet build AnoMech/AnoMech.csproj`
預期：`建置成功。` 0 警告 0 錯誤

執行：`MSBuildEnableWorkloadResolver=false dotnet run --project Tests/AnoMech.P3.Tests.csproj`
預期：全數 PASS

- [ ] **Step 6: Commit**

```bash
git add AnoMech/Core/Multiplayer/MultiplayerSession.cs
git commit -m "Send, replay and roll back limit breaks on clients"
```

---

### Task 8: 判定收歸主機，並開放多人

**Files:**
- Modify: `AnoMech/Scenarios/Top/P6AlphaOmega/TopP6AlphaOmegaScenario.cs`（`Fail` 與 `StartCombat` 的 LB 檢查）
- Modify: `AnoMech/Scenarios/Top/P6AlphaOmega/TopP6AlphaOmegaExtended.cs`（`SupportsMultiplayer`、`ScheduleMagicNumber` 的兩處檢查）
- Modify: `AnoMech/Core/Multiplayer/MultiplayerSession.cs`（把失敗原因交給主機廣播）

- [ ] **Step 1: 讓客戶端跳過 LB 判定**

在 `TopP6AlphaOmegaScenario` 中加入：

```csharp
    // Judging runs on the host alone: the checks read a local clock, and a relayed limit break arrives a
    // playback delay late, so a client would fail a pull the host passed.
    private static bool JudgesLimitBreaks => !MultiplayerContext.IsClient;
```

`StartCombat` 中宇宙記憶的檢查改為：

```csharp
            if (Extended && JudgesLimitBreaks &&
                !TopP6LimitBreakRules.IsTankLbActive(lastLimitBreakAt[(int)PartyRole.MainTank], limitBreakClock) &&
                !TopP6LimitBreakRules.IsTankLbActive(lastLimitBreakAt[(int)PartyRole.OffTank], limitBreakClock))
            { Fail("宇宙記憶：傷害結算前未開啟有效坦克極限技。"); return; }
```

`TopP6AlphaOmegaExtended.ScheduleMagicNumber` 中兩處檢查同樣加上 `JudgesLimitBreaks &&`：

```csharp
            if (JudgesLimitBreaks && !TopP6LimitBreakRules.IsTankLbActive(lastLimitBreakAt[(int)tank], limitBreakClock))
                Fail($"魔數：{(tank == PartyRole.MainTank ? "MT" : "ST")} 未在傷害結算前開啟有效 LB。");
```

```csharp
                if (!failed && JudgesLimitBreaks && pendingMagicNumberHealer == healer)
                    Fail($"魔數：{(healer == PartyRole.RegenHealer ? "H1" : "H2")} 未在 DEBUFF 到期前完成 LB。");
```

- [ ] **Step 2: 把失敗原因交給主機廣播**

在 `TopP6AlphaOmegaScenario.Fail` 中，`Plugin.ChatGui.PrintError(message);` 之後加入：

```csharp
        MultiplayerContext.RunFailed?.Invoke(message);
```

在 `AnoMech/Core/Game/MultiplayerContext.cs` 的 `LimitBreakUsed` 之後加入：

```csharp
    // Host only: the room sees the wipe but not the reason, which is judged here.
    public static Action<string>? RunFailed { get; set; }
```

並在 `Begin` 與 `End` 中比照 `LimitBreakUsed` 清為 null。

在 `MultiplayerSession.HostStartRun` 中，設定 `MultiplayerContext.LimitBreakUsed = ...` 之後加入：

```csharp
        MultiplayerContext.RunFailed = reason => run.PendingFailReason = reason;
```

在 `EndHostRun` 中比照加入 `MultiplayerContext.RunFailed = null;`。

- [ ] **Step 3: 開放多人**

在 `TopP6AlphaOmegaExtended.cs` 把

```csharp
    public bool SupportsMultiplayer => extendedStart == null;
```

改為

```csharp
    public bool SupportsMultiplayer => true;
```

- [ ] **Step 4: 建置與測試**

執行：`DALAMUD_HOME="$APPDATA/FFXIVSimpleLauncher/Dalamud/Injector" dotnet build AnoMech/AnoMech.csproj`
預期：`建置成功。` 0 警告 0 錯誤

執行：`MSBuildEnableWorkloadResolver=false dotnet run --project Tests/AnoMech.P3.Tests.csproj`
預期：全數 PASS

- [ ] **Step 5: Commit**

```bash
git add AnoMech/Scenarios/Top/P6AlphaOmega/ AnoMech/Core/Game/MultiplayerContext.cs AnoMech/Core/Multiplayer/MultiplayerSession.cs
git commit -m "Judge limit breaks on the host and open the P6 practice scenes to multiplayer"
```

---

## 實機驗證（無法自動化）

以下需要兩台繁中客戶端對跑，請使用者執行：

1. 房主與加入者各拿一個坦克格，跑「宇宙記憶 → 通關」，確認兩人都能開 LB、且開過之後對方的極限技槽變空。
2. 兩人同時按 LB，確認只有一個成功，另一個讀條被中斷並看到「極限技已被其他人使用。」。
3. 魔數時故意讓其中一人不開 LB，確認**兩台**都團滅，且兩台都看到同一句失敗原因。
4. 確認沒有真人的格子仍由 AI 正常代開 LB。
5. 舊版客戶端連線會被協定 v6 擋下並顯示版本不同。
