# 多人極限技同步設計（P6 LB 練習）

日期：2026-09-21　分支：從 `api13-tw` 0.3.31.0 開

## 目標

讓 P6 的四個 LB 練習場景（`完整時間軸`、`限制解除①`、`限制解除②`、`宇宙流星`）能在多人房間裡跑，
並且**模擬實戰情況**：全隊共用一條極限技槽，誰先按到誰用掉，該開 LB 的人沒開就照實戰判失敗。

目前這四個場景是單人限定（`TopP6AlphaOmegaExtended.SupportsMultiplayer => extendedStart == null`），
`CanHostStart` 會擋下並顯示「此練習尚未支援多人極限技同步」。

## 為什麼現在不能用

1. **沒有「客戶端 → 主機」的操作通道。** 場景進行中客戶端只送姿勢與場地標記，都是呈現層。
   遠端玩家按下 LB 這件事主機永遠不知道，跑到魔數判定就會因為「補師沒開 LB」而判失敗。
2. **極限技槽是本地的。** `PracticeLimitBreakRuntime` 每台機器各有一個 `available` 旗標，
   八個人會各自有一條槽，實戰中「坦克 LB 排在補師 LB 前面」的約束不存在。
3. **`TopP6FullAi.StartLimitBreak` 只跳過本地玩家**，沒有檢查該格子是不是真人，
   所以每台機器的 AI 都會替其他真人代按。

## 選定方案：A2（只有槽歸主機裁判，其餘共模擬）

專案既有的多人模型是**決定性共模擬**：主機與客戶端用同一個 seed 各跑一份場景，
主機廣播姿勢與週期性 Sync 幀校正，`MismatchCount` 盯著分歧。

A2 延伸這個模型，而不是在旁邊另蓋一套主機權威模型：

| 項目 | 權威 |
| --- | --- |
| 極限技槽（誰拿到） | **主機** |
| LB 的效果（幾何、量表、狀態、動畫） | 兩邊各自重放 |
| 通過／失敗判定 | **主機** |
| AI 代打的 LB | 兩邊各自模擬，不上線 |

落選方案：A1／A3 把 LB 全部收歸主機、客戶端只做表現，需要為敵人消失、魔數解除各開同步管道，
且與既有共模擬模型並存，長期難維護。

## 線上協定

### 客戶端 → 主機：`LimitBreakUsed`（新訊息）

| 欄位 | 說明 |
| --- | --- |
| RunId | 對應目前這場 |
| RequestId | 該客戶端遞增的請求序號，沿用 `MarkersDto.RequestId` 的做法 |
| ActionId | 極限技 ID |
| CasterPosition | 施法者當下座標 |
| AimPoint | 瞄準點，可空 |

**不送任何物件 ID。** 各機器的 `SimEnemy` id 是本地配發的，送過去對不上。
`ResolveLimitBreakGeometry` 只需要施法者座標與瞄準點：
地面型（法系 LB）用瞄準點當圓心，直線型（遠敏 LB）用 `location ?? target.Position` 算朝向，
兩者都能由這兩個座標還原，與機器無關。

職能不放進訊息 —— 主機從送件者的格子得知，客戶端無法冒充別人。

### 主機 → 客戶端：`TickFrame` 新增三欄

| 欄位 | 說明 |
| --- | --- |
| `LimitBreakHolder`（byte） | 目前持槽的職能，255 = 空著 |
| `LimitBreaks` | 本幀被接受的 LB 事件（職能、ActionId、兩個座標），寫法照現有的 `Invulns` |
| `LimitBreakAcks[8]` | 每格最後處理到的 RequestId，寫法照現有的 `MarkerAcks` |

另外新增 `FailReason`（可空字串，只在變動時送一次，寫法照 `Markers`）：
判定收歸主機後，客戶端需要知道失敗的原因才能顯示訊息。此欄位不限 LB 使用，
任何場景的 `Fail()` 都可以透過它把原因帶給房內的人。

協定版本 `NetProtocol.Version` 5 → 6。

## 行為

### 按下 LB（客戶端）

先動，主機反悔再取消 —— 與場地標記同一套回捲規則：

1. 客戶端本地照現在的流程 `TryStart`，讀條立刻開始，手感與單人一致。
2. 同時送出 `LimitBreakUsed`，帶遞增的 RequestId。
3. 收到的幀中若 `LimitBreakAcks[自己的職能] >= 自己的 RequestId`
   而 `LimitBreakHolder` 不是自己，代表被別人搶先：本地取消讀條，提示「極限技已被使用」。

碰撞在實務上很罕見（實戰中大家講好誰開），但發生時畫面會回捲，這是選「先動」的已知代價。

### 主機裁判

主機每收到一個 `LimitBreakUsed`：

- 若槽是空的 → 接受：替該職能呼叫 `TryStart`，把事件放進本幀的 `LimitBreaks`，
  `LimitBreakHolder` 設為該職能。
- 若槽已被佔用 → 拒絕：不放事件，但仍推進該格的 `LimitBreakAcks`，讓對方知道請求已處理。

兩種情況都推進 ack，客戶端才能區分「還沒處理」與「被拒絕」。

### 重放（所有機器）

**線上訊息的內容，剛好就是 `TryStart` 的參數。**
所以重放遠端的 LB 就是替那個職能呼叫 `runtime.TryStart(role, actionId, location, target: null)` ——
與 `TopP6FullAi.StartLimitBreak` 替 NPC 代打走的是同一條路。
讀條、釋放動畫、幾何結算、量表變化全部沿用現有程式碼，不需要新的表現層邏輯。

`OnLimitBreakResolved` 的簽章 `(role, actionId, location, target)` 已經吃得下：
直線型把瞄準點當 `location` 傳入即可，`ResolveLimitBreakGeometry` 本來就優先讀 `location`。

### AI 與真人格子

`TopP6FullAi.StartLimitBreak` 目前只跳過 `world.Party.PlayerRole`，
改為同時跳過 `MultiplayerContext.IsHumanControlled(role)`：**AI 只補沒有真人的格子**。
真人沒開到自己的 LB 就會判失敗，那正是要練的東西。

AI 的判斷在每台機器上都一樣（決定性），所以 AI 的 LB 不需要上線，
但它一樣要經過本地 runtime 的槽佔用，與真人的 LB 競爭同一條槽。

### 失敗與團滅

判定收歸主機獨有：客戶端在多人場景中跳過三處與 LB 有關的判定分支 ——
宇宙記憶的坦克 LB 檢查、魔數的坦克 LB 檢查、魔數的補師 LB 逾時檢查。
其餘與 LB 無關的 `Fail()`（例如機制物件生成失敗）維持兩邊各自判定不變。

理由：判定用的是本地時鐘（`limitBreakClock`），而遠端 LB 在客戶端上會晚一個播放延遲才重放，
共模擬會造成主機判過、客戶端判失敗的分歧。收歸主機可以整類消掉這個問題。

主機 `Fail()` 時：照現有流程 `WipeAllPlayers`（死亡狀態透過既有的 Sync 幀傳下去），
並把原因寫進 `FailReason` 送出一次，客戶端顯示同樣的訊息。
代價是客戶端的失敗訊息會晚一個播放延遲，可接受。

### 極限技槽的顯示

`PracticeLimitBreakGauge.Update(available)` 在客戶端改由 `LimitBreakHolder` 驅動
（有人持槽 → 槽顯示為已用掉）。不需要額外工作，是槽同步的自然結果。

## 可測試性

裁判邏輯不能埋在 `MultiplayerSession` 裡 —— 那個類別需要 `Game` 實體，
現有的 `MultiplayerChecks` 只碰得到 `MultiplayerContext` 的靜態狀態。

因此裁判抽成一個純粹的小類別 `LimitBreakArbiter`，由 `MultiplayerSession` 持有：

```
接受或拒絕一個請求（職能、RequestId）-> (bool accepted, byte holder, uint[] acks)
槽被釋放時通知它
```

不依賴 `Game`、`SimWorld` 或網路，可直接單元測試。

## 測試

- `LimitBreakArbiter`：同一 tick 兩個職能請求 → 只有一個被接受，落選者的 ack 仍前進；
  槽釋放後下一個請求才會被接受；重複的 RequestId 不會重複接受。
- `LimitBreakUsedDto` 與 `TickFrame` 新欄位的往返編解碼，併入現有的
  `MultiplayerChecks.MessagesRoundTripAndRejectGarbage`。
- `TopP6FullAi.StartLimitBreak` 跳過真人格子：在 `Tests/MultiplayerChecks.cs` 新增一組檢查，
  用 `MultiplayerContext.Begin` 標記人類格子後斷言該格子不會被 AI 代打（該檔已有操作這組靜態狀態的先例）。
- **端對端（房主與客戶端各開一次 LB、搶槽、漏開判失敗）需要兩台繁中客戶端對跑，無法自動化。**

## 不在範圍

- P6 以外的場景（其他階段沒有 LB 判定）。
- LB 以外的技能同步：一般職業技能仍是各自本地模擬，不上線。
- 傷害、治療、護盾數值 —— 沿用現有範圍，本次不變。

## 風險

- **共模擬分歧**：A2 依賴兩邊算出同樣的幾何結果。座標由訊息帶過來可消除主要來源，
  但若任一台機器的隕星生成狀態已經不同（既有的 `MismatchCount` 問題），結果仍會不同。
  判定收歸主機可確保至少「通過與否」是一致的。
- **回捲可見**：搶槽時客戶端的讀條會被中斷，畫面上看得出來。
- **協定升版**：6 之後，房主與加入者都要更新到同一版才能連線。
