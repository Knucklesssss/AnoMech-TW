# 本機技能循環：自動偵測職業與職業介面

基準提交 `7a49ba9`。延續 `2026-09-13-local-combat-design.md` 頂部的範圍更正：只做技能循環、派生、連段、量譜、冷卻與按鍵呈現，不做傷害、屬性、治療、護盾、減傷。

## 目標與範圍

進入模擬時自動讀取玩家職業與同步等級，有規則的職業直接接管技能循環，不需在設定勾選。把目前寫死戰士的程式抽成職業介面，之後新增職業只需新增規則與註冊一筆。

本批只抽介面並把戰士搬到介面後面，**不新增任何職業**，戰士行為必須完全不變。不發布、不推送。第二個職業另開規格，屆時再依需要擴充介面，不預先猜測。

## 純規則層

新增 `IJobCombat`，成員只取戰士目前實際使用者：

- `Actions`：支援的技能 ID 清單（取代 `LocalCombatSession.OffensiveActions`）
- `Adjust`、`Supports`、`CanUse`、`TryUse`、`GetCooldown`、`Timing`、`ComboAction`、`ComboRemaining`、`Advance`、`Reset`
- `IsSelfAction(id)`：不需目標的技能（自身範圍技與自身增益）。取代會話中寫死的 `selfAoe` 與 `52 or 7389` 判斷
- `IsGapCloser(id)`：突進技，取代寫死的 `7386 or 25753`
- `Statuses`：要鏡射的狀態清單（狀態 ID、剩餘秒數、層數）與固定的 `StatusIds`。「原初的解放層數為 0 時移除 1177」的特例移入戰士規則，以剩餘 0 表示

不從 Action 資料表推導自身／突進判斷：該推導未經本機資料核對，改由職業規則明寫。

`WarriorHit` 改名 `JobHit`。`WarriorCombat` 實作 `IJobCombat`，既有公開成員保留，測試不需改寫呼叫方式。

## 原生層

`CombatNativeState` 只保留職業無關部分：連段、動作鎖、自動攻擊、冷卻組、狀態鏡射與還原。

- 冷卻組改由 `IJobCombat.Actions` 收集（既有擷取已依組去重），刪除另維護的 `BindingActions`
- 身分檢查的職業與等級改讀註冊資料，不再寫死 21／90
- 量譜交給 `IJobGauge` 轉接：擷取原值、依規則寫入、還原。戰士為 `WarriorNativeGauge`（`BeastGauge`）。轉接在建構時驗證 `JobGaugeManager.ClassJobId` 與量譜指標，身分檢查同時比對量譜指標

## 註冊表與自動偵測

註冊表每筆：職業 ID、同步等級、讀 GCD 長度的技能、建立規則（傳入 GCD 秒數）。首筆僅「戰士 21、等級 90、重劈 31」。讀 GCD 的技能放在註冊資料而非規則，因為會話須先讀出 GCD 才能建立規則。註冊表會編進純規則測試，不能引用原生型別，所以量譜轉接由原生層依職業 ID 選擇。

- 移除 `Configuration.EnableLocalCombat` 與設定視窗勾選框（舊設定檔的多餘欄位由序列化忽略）
- `LocalCombatSession.Start` 以玩家目前職業與角色等級查註冊表，並要求場景同步等級相同
- 設定視窗改為一行狀態，例如「戰士 90 級技能循環進行中」或不支援原因

## 行為與錯誤處理

- **偵測時機**：每次開始或重開場景（`Game.cs` 呼叫 `World.StartCombat`）。
- **中途改變**：模擬中換職業、等級或角色改變時，沿用身分檢查停止並還原；本場不重新接管，下次開始場景才重新偵測。
- **不支援**：查不到註冊（其他職業，或絕神兵 70 級）時不建立會話，技能維持原本路徑，每次開始或重開場景時聊天框提示一次，設定視窗顯示原因。
- **初始化失敗**：簽章、hook、狀態欄位不足等仍拒絕啟動並顯示原因；所有檢查在第一次寫入前完成。
- **執行中例外**：沿用現有停止並還原。

移除勾選框後，受支援職業預設即接管；上述自動停止與還原是唯一保護，必須維持。

還原順序不變：重置／離開時 `SimWorld` 先結束會話並還原連段、冷卻、量譜、狀態，之後才卸載地圖、解除封包隔離。量譜還原改由轉接類別負責。

## 檔案

- 新增：`Core/Combat/IJobCombat.cs`（介面、`JobHit`、註冊表）、`Core/Combat/WarriorNativeGauge.cs`
- 修改：`WarriorCombat.cs`、`LocalCombatSession.cs`、`CombatNativeState.cs`、`Configuration.cs`、`Windows/ConfigWindow.cs`
- 不改：`LocalPlayerInputHooks.cs`（已不分職業）

## 驗收

自動測試：

- 既有 `WarriorCombatChecks`、`CombatInputChecks`、`CombatRecastChecks` 與全部機制回歸照舊通過
- 新增註冊表檢查：戰士 90 查得到；戰士 70、其他職業查不到；每筆註冊的讀 GCD 技能在規則的 `Actions` 內且清單非空；戰士 `IsSelfAction`／`IsGapCloser` 與原寫死清單一致
- 以 FFXIVSimpleLauncher API 13 Dalamud 建置 `bin/Debug`，0 錯誤

使用者實測：

1. 戰士進 TOP，不勾選即可打循環，連段、獸魂、冷卻與先前一致
2. 其他職業進 TOP，出現一次提示，技能照原本行為
3. 戰士進絕神兵，提示不支援
4. 離開模擬後正常技能、連段、冷卻恢復

編譯與自動測試通過不代表遊戲內驗收。
