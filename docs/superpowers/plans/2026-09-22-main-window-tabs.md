# 主視窗分頁化 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 把「多人同步」「連戰」「隊伍列表順序」「職業支援列表」四個獨立視窗收進 `MainWindow`，成為與「練習」並列的五個分頁，並刪除原本的獨立視窗。

**Architecture:** 四個 `Window` 子類降級成純面板類別（只留 `Draw()`），由 `MainWindow` 在 `ImGui.BeginTabBar` 的各分頁內呼叫。`Draw()` 的內容逐字搬移，不改寫。`Plugin` 的四個 `Toggle*Ui()` 保留名稱，改為「打開主視窗並切到該分頁」。

**Tech Stack:** C# / .NET 9、Dalamud.NET.Sdk 13、`Dalamud.Interface.Windowing`、ImGui（`Dalamud.Bindings.ImGui`）

**Spec:** `docs/superpowers/specs/2026-09-22-main-window-tabs-design.md`

## Global Constraints

- 建置指令固定為：`DALAMUD_HOME="$APPDATA/FFXIVSimpleLauncher/Dalamud/Injector" dotnet build AnoMech/AnoMech.csproj`。直接 `dotnet build` 會抓到國際服的 Dalamud（API 14 / net10）而出現 CS1705。
- 每個 task 結束時建置必須 0 錯誤 0 警告。
- 每個 task 結束時 `dotnet run --project Tests/AnoMech.P3.Tests.csproj` 必須全數通過（本計畫不新增測試，見下）。
- 四個面板 `Draw()` 的**內容一字不改**。本計畫只動類別宣告、建構子、呼叫端與版面容器。
- 註解遵循 `CLAUDE.md`：只寫程式碼自己講不出來的理由，不要複述符號名稱或記錄這次改了什麼。
- 提交訊息用英文，結尾加上 `Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>`。
- 不新增全域狀態列。`MultiplayerSession.RunStatus` 留在多人面板 `Draw()` 的第一行。
- 精簡模擬控制路徑（`Draw()` 開頭的 `IsInInstance && CompactSimulationControls` 分支）維持現狀，**不加分頁列**。

## 關於測試

`MainWindow` 與四個視窗都**不在** `Tests/AnoMech.P3.Tests.csproj` 裡（該專案只納入指定的 `Compile Include` 檔案，不含 `AnoMech/Windows/`）。本次改動全屬版面容器與類別搬移，沒有可單獨測試的分支邏輯，因此**本計畫不新增單元測試**，也不要為了湊測試而把 ImGui 樁擴張到整個 `MainWindow`。

每個 task 的驗證是：

1. 建置 0 錯誤 0 警告。
2. 既有測試全過（確認沒有波及被納入測試的檔案）。
3. `git diff` 逐行確認搬移的 `Draw()` 內容未被改寫。

實機確認（分頁切換、收合行為、各面板在新寬度下的表現）由使用者完成。

---

### Task 1: 分頁骨架與視窗尺寸

把現有的兩欄表格包進一個只有「練習」分頁的 `TabBar`，並處理視窗尺寸行為。行為在這一步應與現在完全相同，只是多了一條分頁列。

**為什麼尺寸要在這一步處理：** `MainWindow` 建構子有 `Flags |= ImGuiWindowFlags.AlwaysAutoResize`，視窗永遠貼著內容大小、使用者拉不動。分頁化之後每切一次分頁視窗就會自己跳成該分頁的大小，而 spec 要的是「插件不自動調，使用者自己拉」。因此完整面板要關掉自動縮放；精簡模擬控制那條路徑保留它（那是一條窄控制列，自動縮放才對）。

**Files:**
- Modify: `AnoMech/Windows/MainWindow.cs`（建構子約 76-82 行、`Draw()` 約 127-158 行）
- Test: 無（見「關於測試」）

**Interfaces:**
- Consumes: 無
- Produces:
  - `internal enum MainTab { Practice, Multiplayer, Chain, PartyOrder, JobSupport }`
  - `private MainTab requestedTab;` 與 `private MainTab? pendingTab;` —— `pendingTab` 由後續 task 的 `Plugin.Toggle*Ui()` 設定，用來在下一幀強制切到指定分頁
  - `internal void ShowTab(MainTab tab)` —— 後續 task 由 `Plugin` 呼叫
  - `MainWindow.SizeConstraints.MinimumSize` 提高為 `(480, 320)`

- [ ] **Step 1: 加入分頁列舉與欄位**

在 `MainWindow` 類別內、`private bool _leftPanelOpen = true;` 之後加入：

```csharp
    // internal because Plugin's Toggle*Ui entry points name a tab.
    internal enum MainTab { Practice, Multiplayer, Chain, PartyOrder, JobSupport }

    // Set by Plugin's Toggle*Ui entry points; consumed on the next frame to force that tab open.
    private MainTab? pendingTab;
    private MainTab currentTab = MainTab.Practice;

    internal void ShowTab(MainTab tab)
    {
        pendingTab = tab;
        IsOpen = true;
    }
```

`ShowTab` 此刻還沒有呼叫端，會出現「未使用」的情況；C# 不會因此產生警告，保持原樣，Task 2 就會用到。

- [ ] **Step 2: 完整面板關閉自動縮放**

把建構子裡的：

```csharp
        Flags |= ImGuiWindowFlags.AlwaysAutoResize;
```

改成一個之後每幀依模式設定的欄位。先在建構子移除該行，並在 `Draw()` 最前面（進入任何分支之前）加入：

```csharp
        var compact = plugin.Game.World.Map.IsInInstance && plugin.Configuration.CompactSimulationControls;
        // The compact strip should hug its content; the full panel must stay where the user put it,
        // or every tab switch would resize the window out from under them.
        if (compact) Flags |= ImGuiWindowFlags.AlwaysAutoResize;
        else Flags &= ~ImGuiWindowFlags.AlwaysAutoResize;
```

然後把原本 `Draw()` 開頭的條件改成使用這個區域變數：

```csharp
        if (compact)
        {
```

（其餘精簡分支的內容與結尾的 `return;` 不動。）

同時把建構子裡的最小尺寸從 `new Vector2(220, 80)` 提高到能容納最寬的分頁 —— 多人視窗原本要求 480×320，分頁化之後那個下限落到主視窗身上：

```csharp
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(480, 320),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
```

- [ ] **Step 3: 把兩欄表格包進分頁列**

把 `Draw()` 中精簡分支之後的這一段：

```csharp
        var leftWidth = _leftPanelOpen ? ScenarioPanelWidth() : 30f;

        if (ImGui.BeginTable("##layout", 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingFixedFit))
        {
            ImGui.TableSetupColumn("##left", ImGuiTableColumnFlags.WidthFixed, leftWidth);
            ImGui.TableSetupColumn("##right", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            DrawScenariosPanel();
            ImGui.TableSetColumnIndex(1);
            DrawMainContent();
            ImGui.EndTable();
        }
```

換成：

```csharp
        if (!ImGui.BeginTabBar("##maintabs")) return;
        DrawTab(MainTab.Practice, "練習", DrawPracticeTab);
        ImGui.EndTabBar();
        pendingTab = null;
```

並在 `Draw()` 之後加入兩個方法：

```csharp
    // A tab requested through ShowTab is forced selected for one frame; ImGui owns the choice otherwise.
    private void DrawTab(MainTab tab, string label, Action draw)
    {
        var flags = pendingTab == tab ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None;
        if (!ImGui.BeginTabItem(label, flags)) return;
        currentTab = tab;
        draw();
        ImGui.EndTabItem();
    }

    private void DrawPracticeTab()
    {
        var leftWidth = _leftPanelOpen ? ScenarioPanelWidth() : 30f;
        if (!ImGui.BeginTable("##layout", 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingFixedFit)) return;
        ImGui.TableSetupColumn("##left", ImGuiTableColumnFlags.WidthFixed, leftWidth);
        ImGui.TableSetupColumn("##right", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        DrawScenariosPanel();
        ImGui.TableSetColumnIndex(1);
        DrawMainContent();
        ImGui.EndTable();
    }
```

`currentTab` 此刻只被寫入不被讀取，Task 2 會讀它來判斷分頁切換。

- [ ] **Step 4: 建置**

執行：

```bash
DALAMUD_HOME="$APPDATA/FFXIVSimpleLauncher/Dalamud/Injector" dotnet build AnoMech/AnoMech.csproj
```

預期：`建置成功。0 個警告 0 個錯誤`。

若出現 `ImGuiTabItemFlags` 或 `Action` 找不到，確認 `MainWindow.cs` 頂部已有 `using System;` 與 `using Dalamud.Bindings.ImGui;`（兩者現在都在）。

- [ ] **Step 5: 既有測試**

執行：

```bash
dotnet run --project Tests/AnoMech.P3.Tests.csproj
```

預期：與改動前相同的通過輸出，最後一行為 `Multiplayer: deterministic random, ...`。

- [ ] **Step 6: Commit**

```bash
git add AnoMech/Windows/MainWindow.cs
git commit -m "$(cat <<'EOF'
Wrap the main panel in a tab bar

One tab so far, holding the two-column layout unchanged. The window also stops
auto-resizing in full-panel mode: with tabs, a window that hugs its content
would jump to a new size on every tab switch. The compact in-instance strip
keeps auto-resize, since that is a narrow control bar.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: 多人連線分頁

把 `MultiplayerWindow` 降級成 `MultiplayerPanel` 並掛上第二個分頁。

**Files:**
- Modify: `AnoMech/Windows/MultiplayerWindow.cs`（改名檔案為 `MultiplayerPanel.cs`）
- Modify: `AnoMech/Windows/MainWindow.cs`
- Modify: `AnoMech/Plugin.cs:66, 94, 100, 193, 286, 420`
- Test: 無

**Interfaces:**
- Consumes: `MainWindow.MainTab`、`MainWindow.ShowTab(MainTab)`、`MainWindow.DrawTab(MainTab, string, Action)`（Task 1）
- Produces:
  - `internal sealed class MultiplayerPanel`，建構子 `MultiplayerPanel(MultiplayerSession session)`
  - `public void Draw()`
  - `public void OnShown()` —— 取代原本的 `OnOpen()`

- [ ] **Step 1: 檔案改名**

```bash
git mv AnoMech/Windows/MultiplayerWindow.cs AnoMech/Windows/MultiplayerPanel.cs
```

- [ ] **Step 2: 改類別宣告與建構子**

把 `AnoMech/Windows/MultiplayerPanel.cs` 的：

```csharp
internal sealed class MultiplayerWindow : Window, IDisposable
```

改成：

```csharp
internal sealed class MultiplayerPanel
```

把建構子：

```csharp
    public MultiplayerWindow(MultiplayerSession session)
        : base("多人同步###AnoMechConnectionTest")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(480, 320),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
        this.session = session;
    }

    public override void OnOpen() => directConnection = HostReadiness.PublicAddressOnThisPc();

    public void Dispose() { }

    public override void Draw()
```

改成：

```csharp
    public MultiplayerPanel(MultiplayerSession session) => this.session = session;

    // Probing the public address is a network round trip, so it runs when the tab is brought up
    // rather than every frame — the same moment the window's OnOpen used to fire.
    public void OnShown() => directConnection = HostReadiness.PublicAddressOnThisPc();

    public void Draw()
```

`Draw()` 以下的**內容完全不動**。

移除檔案頂部已不再需要的 `using Dalamud.Interface.Windowing;`；若 `using System;` 只為 `IDisposable` 而存在則一併移除，但若檔內另有用途則保留（改完後由建置的警告／錯誤決定）。

- [ ] **Step 3: MainWindow 持有面板並掛上分頁**

在 `MainWindow` 欄位區加入：

```csharp
    private readonly MultiplayerPanel multiplayerPanel;
```

在建構子 `this.plugin = plugin;` 之後加入：

```csharp
        multiplayerPanel = new MultiplayerPanel(plugin.Multiplayer);
```

在 `Draw()` 的分頁列中，`DrawTab(MainTab.Practice, ...)` 之後加入：

```csharp
        DrawTab(MainTab.Multiplayer, "多人連線", multiplayerPanel.Draw);
```

並把 `DrawTab` 改成在分頁由別的分頁切換過來時通知面板：

```csharp
    private void DrawTab(MainTab tab, string label, Action draw)
    {
        var flags = pendingTab == tab ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None;
        if (!ImGui.BeginTabItem(label, flags)) return;
        if (currentTab != tab)
        {
            currentTab = tab;
            if (tab == MainTab.Multiplayer) multiplayerPanel.OnShown();
        }
        draw();
        ImGui.EndTabItem();
    }
```

`Plugin.Multiplayer` 是 `internal static`，`MainWindow` 取用得到，**但目前的建立順序是錯的**：`Plugin.cs` 第 92 行建立 `MainWindow`，第 93 行才指派 `Multiplayer`，照上面寫會拿到 null。把這兩行對調：

```csharp
        Multiplayer = new MultiplayerSession(Game);
        MainWindow = new MainWindow(this);
```

`MultiplayerSession` 只需要第 89 行建立的 `Game`，所以提前建立是安全的。

- [ ] **Step 4: Plugin 移除視窗**

- 刪除第 66 行 `private MultiplayerWindow MultiplayerWindow { get; init; }`
- 刪除第 94 行 `MultiplayerWindow = new MultiplayerWindow(Multiplayer);`
- 刪除第 100 行 `WindowSystem.AddWindow(MultiplayerWindow);`
- 刪除第 193 行 `MultiplayerWindow.Dispose();`
- 第 286 行 `case "net": MultiplayerWindow.Toggle(); break;` 改為 `case "net": ToggleMultiplayerUi(); break;`
- 第 420 行改為：

```csharp
    public void ToggleMultiplayerUi() => MainWindow.ShowTab(MainWindow.MainTab.Multiplayer);
```

- [ ] **Step 5: 移除標題列的多人圖示**

刪除 `MainWindow` 建構子中這一整段：

```csharp
        TitleBarButtons.Add(new TitleBarButton
        {
            Icon = FontAwesomeIcon.Users,
            IconOffset = new Vector2(2f, 1f),
            Click = _ => plugin.ToggleMultiplayerUi(),
            ShowTooltip = () => ImGui.SetTooltip("多人同步"),
        });
```

齒輪（`FontAwesomeIcon.Cog`）那一段保留。

- [ ] **Step 6: 建置與測試**

```bash
DALAMUD_HOME="$APPDATA/FFXIVSimpleLauncher/Dalamud/Injector" dotnet build AnoMech/AnoMech.csproj
dotnet run --project Tests/AnoMech.P3.Tests.csproj
```

預期：建置 0 錯誤 0 警告；測試輸出與改動前相同。

- [ ] **Step 7: 確認 Draw() 未被改寫**

```bash
git diff --stat AnoMech/Windows/MultiplayerPanel.cs
git diff AnoMech/Windows/MultiplayerPanel.cs | grep -E "^[-+]" | grep -v "^[-+][-+]" | head -40
```

預期：差異只出現在類別宣告、建構子、`OnOpen`/`OnShown`、`Dispose`、`using` 幾處，`Draw()` 主體與其下所有 `Draw*` 方法沒有任何 `+`/`-` 行。

- [ ] **Step 8: Commit**

```bash
git add -A AnoMech/Windows/ AnoMech/Plugin.cs
git commit -m "$(cat <<'EOF'
Move multiplayer into a main-window tab

The window becomes a plain panel the main window draws. Its OnOpen probe of the
public address now fires when the tab is brought up, which is the same moment it
fired before.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 3: 連戰分頁

**Files:**
- Modify: `AnoMech/Windows/ChainWindow.cs`（改名為 `ChainPanel.cs`）
- Modify: `AnoMech/Windows/MainWindow.cs`
- Modify: `AnoMech/Plugin.cs:75, 112, 113, 196, 421`
- Test: 無

**Interfaces:**
- Consumes: `MainWindow.MainTab`、`MainWindow.ShowTab`、`MainWindow.DrawTab`
- Produces: `public sealed class ChainPanel`，建構子 `ChainPanel(Plugin plugin, MainWindow main)`，`public void Draw()`

- [ ] **Step 1: 檔案改名**

```bash
git mv AnoMech/Windows/ChainWindow.cs AnoMech/Windows/ChainPanel.cs
```

- [ ] **Step 2: 改類別宣告與建構子**

把：

```csharp
public sealed class ChainWindow : Window, IDisposable
```

改成：

```csharp
public sealed class ChainPanel
```

把：

```csharp
    internal ChainWindow(Plugin plugin, MainWindow main) : base("連戰###AnoMechChain")
    {
        this.plugin = plugin;
        this.main = main;
        Size = new Vector2(620, 420);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public void Dispose() { }

    public override void Draw()
```

改成：

```csharp
    internal ChainPanel(Plugin plugin, MainWindow main)
    {
        this.plugin = plugin;
        this.main = main;
    }

    public void Draw()
```

`Draw()` 以下內容不動。`main` 欄位仍被 `Draw()` 讀取（`main.SelectedScenario` / `SelectedStrat` / `SelectedWaymark`），保留。

移除不再需要的 `using Dalamud.Interface.Windowing;`。

- [ ] **Step 3: MainWindow 持有面板並掛上分頁**

欄位區加入：

```csharp
    private readonly ChainPanel chainPanel;
```

建構子（在 `multiplayerPanel` 之後）加入：

```csharp
        chainPanel = new ChainPanel(plugin, this);
```

分頁列在多人連線之後加入：

```csharp
        DrawTab(MainTab.Chain, "連戰", chainPanel.Draw);
```

- [ ] **Step 4: Plugin 移除視窗**

- 刪除第 75 行 `private ChainWindow ChainWindow { get; init; }`
- 刪除第 112 行 `ChainWindow = new ChainWindow(this, MainWindow);`
- 刪除第 113 行 `WindowSystem.AddWindow(ChainWindow);`
- 刪除第 196 行 `ChainWindow.Dispose();`
- 第 421 行改為：

```csharp
    public void ToggleChainUi() => MainWindow.ShowTab(MainWindow.MainTab.Chain);
```

- [ ] **Step 5: 建置與測試**

```bash
DALAMUD_HOME="$APPDATA/FFXIVSimpleLauncher/Dalamud/Injector" dotnet build AnoMech/AnoMech.csproj
dotnet run --project Tests/AnoMech.P3.Tests.csproj
```

預期：建置 0 錯誤 0 警告；測試通過。

- [ ] **Step 6: 確認 Draw() 未被改寫**

```bash
git diff AnoMech/Windows/ChainPanel.cs | grep -E "^[-+]" | grep -v "^[-+][-+]" | head -30
```

預期：差異只在類別宣告、建構子、`Dispose`、`using`。

- [ ] **Step 7: Commit**

```bash
git add -A AnoMech/Windows/ AnoMech/Plugin.cs
git commit -m "$(cat <<'EOF'
Move the chain list into a main-window tab

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 4: 隊伍順序與職業支援分頁

兩個視窗都只有一個 `Draw()`、沒有額外狀態，改動形狀完全相同，合為一個 task。

**Files:**
- Modify: `AnoMech/Windows/PartyListOrderWindow.cs`（改名為 `PartyListOrderPanel.cs`）
- Modify: `AnoMech/Windows/JobSupportWindow.cs`（改名為 `JobSupportPanel.cs`）
- Modify: `AnoMech/Windows/MainWindow.cs`
- Modify: `AnoMech/Plugin.cs:67, 68, 101-104, 194, 195, 422, 423`
- Test: 無

**Interfaces:**
- Consumes: `MainWindow.MainTab`、`MainWindow.ShowTab`、`MainWindow.DrawTab`
- Produces:
  - `public sealed class PartyListOrderPanel`，建構子 `PartyListOrderPanel(Plugin plugin)`，`public void Draw()`
  - `public sealed class JobSupportPanel`，建構子 `JobSupportPanel()`，`public void Draw()`

- [ ] **Step 1: 檔案改名**

```bash
git mv AnoMech/Windows/PartyListOrderWindow.cs AnoMech/Windows/PartyListOrderPanel.cs
git mv AnoMech/Windows/JobSupportWindow.cs AnoMech/Windows/JobSupportPanel.cs
```

- [ ] **Step 2: 改 PartyListOrderPanel**

把：

```csharp
public sealed class PartyListOrderWindow : Window, IDisposable
{
    private readonly Configuration configuration;

    public PartyListOrderWindow(Plugin plugin) : base("隊伍列表順序###AnoMechPartyListOrder")
    {
        Flags = ImGuiWindowFlags.AlwaysAutoResize;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(360, 80),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
        configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void Draw()
```

改成：

```csharp
public sealed class PartyListOrderPanel
{
    private readonly Configuration configuration;

    public PartyListOrderPanel(Plugin plugin) => configuration = plugin.Configuration;

    public void Draw()
```

`Draw()` 以下不動。移除 `using Dalamud.Interface.Windowing;`，並依建置結果移除不再使用的 `using System;` / `using System.Numerics;`。

- [ ] **Step 3: 改 JobSupportPanel**

把：

```csharp
public sealed class JobSupportWindow : Window, IDisposable
```

改成：

```csharp
public sealed class JobSupportPanel
```

把：

```csharp
    public JobSupportWindow() : base("職業支援列表###AnoMechJobSupport")
    {
        Flags = ImGuiWindowFlags.AlwaysAutoResize;
    }

    public void Dispose() { }

    public override void Draw()
```

改成：

```csharp
    public void Draw()
```

`Draw()` 以下不動。移除 `using Dalamud.Interface.Windowing;`，並依建置結果移除不再使用的 `using System;`。

- [ ] **Step 4: MainWindow 掛上兩個分頁**

欄位區加入：

```csharp
    private readonly PartyListOrderPanel partyOrderPanel;
    private readonly JobSupportPanel jobSupportPanel;
```

建構子加入：

```csharp
        partyOrderPanel = new PartyListOrderPanel(plugin);
        jobSupportPanel = new JobSupportPanel();
```

分頁列在連戰之後加入：

```csharp
        DrawTab(MainTab.PartyOrder, "隊伍順序", partyOrderPanel.Draw);
        DrawTab(MainTab.JobSupport, "職業支援", jobSupportPanel.Draw);
```

- [ ] **Step 5: Plugin 移除兩個視窗**

- 刪除第 67、68 行的兩個屬性
- 刪除第 101-104 行的四行建立與註冊
- 刪除第 194、195 行的兩行 `Dispose()`
- 第 422、423 行改為：

```csharp
    public void TogglePartyListOrderUi() => MainWindow.ShowTab(MainWindow.MainTab.PartyOrder);
    public void ToggleJobSupportUi() => MainWindow.ShowTab(MainWindow.MainTab.JobSupport);
```

- [ ] **Step 6: 建置與測試**

```bash
DALAMUD_HOME="$APPDATA/FFXIVSimpleLauncher/Dalamud/Injector" dotnet build AnoMech/AnoMech.csproj
dotnet run --project Tests/AnoMech.P3.Tests.csproj
```

預期：建置 0 錯誤 0 警告；測試通過。

- [ ] **Step 7: Commit**

```bash
git add -A AnoMech/Windows/ AnoMech/Plugin.cs
git commit -m "$(cat <<'EOF'
Move party order and job support into main-window tabs

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 5: 移除左欄的工具按鈕列

四個目的地都成為分頁之後，左欄上方那一列按鈕只是重複的入口，刪除它並還原為此加上的面板寬度下限。

**Files:**
- Modify: `AnoMech/Windows/MainWindow.cs`（`DrawScenariosPanel`、`ScenarioPanelWidth`、`PanelTools`、`PanelToolsWidth`、`DrawPanelTools`）
- Test: 無

**Interfaces:**
- Consumes: Task 2-4 建立的四個分頁
- Produces: 無

- [ ] **Step 1: 刪除按鈕列的呼叫**

在 `DrawScenariosPanel` 中刪除這一行：

```csharp
            DrawPanelTools();
```

其上方的 `ImGui.TextUnformatted("場景");`、`SmallButton("<##collapse")`，與其下方的 `DrawJobSupport();`、`ImGui.Separator();` 全部保留。

- [ ] **Step 2: 刪除按鈕列本身**

刪除以下三個成員（連同其註解）：

```csharp
    private static readonly (string Label, string Full)[] PanelTools = ...
    private static float PanelToolsWidth() { ... }
    private void DrawPanelTools() { ... }
```

- [ ] **Step 3: 還原面板寬度**

在 `ScenarioPanelWidth()` 中刪除這一行：

```csharp
        widest = Math.Max(widest, PanelToolsWidth());
```

保留其後的 `var measured = widest + style.CellPadding.X * 2;` 與 `return Math.Max(180f, measured);`。

- [ ] **Step 4: 建置與測試**

```bash
DALAMUD_HOME="$APPDATA/FFXIVSimpleLauncher/Dalamud/Injector" dotnet build AnoMech/AnoMech.csproj
dotnet run --project Tests/AnoMech.P3.Tests.csproj
```

預期：建置 0 錯誤 0 警告；測試通過。

- [ ] **Step 5: 確認沒有殘留的參考**

```bash
grep -rn "PanelTools\|MultiplayerWindow\|ChainWindow\|PartyListOrderWindow\|JobSupportWindow" --include=*.cs AnoMech
```

預期：沒有任何輸出。若有輸出，逐一處理後重跑建置。

- [ ] **Step 6: Commit**

```bash
git add AnoMech/Windows/MainWindow.cs
git commit -m "$(cat <<'EOF'
Drop the scenario panel's tool buttons

All four destinations are tabs now, so the row was a second way to reach the
same places. The panel goes back to being sized by scenario names alone.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
EOF
)"
```

---

## 完成後

交給使用者實機確認以下項目（本計畫無法自動驗證）：

1. 五個分頁都畫得出來，內容與改版前一致。
2. `/anomech net` 會打開主視窗並切到多人連線分頁。
3. 切到多人連線分頁時，「本機直連」的判斷有更新（原本是開啟視窗時才跑）。
4. 完整面板現在可以用滑鼠拉大小，且切分頁時尺寸不會自己跳動。
5. 進入模擬場地時主視窗仍會自動收合；開啟「精簡模擬控制」時仍是那條窄控制列，沒有分頁。
6. 連戰分頁的「加入副本」仍會正確帶入練習分頁當下選取的場景與策略。
