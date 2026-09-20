# AnoMechTC 0.20.5 選擇性移植結果

來源：使用者提供的 AnoMechTC-0.20.5.0 原始碼，https://github.com/YS6720/AnoMechTC/releases/tag/v0.20.5.0 。移植保留原專案 AGPL-3.0-or-later 授權；來源程式的作者及衍生關係見原始儲存庫與 NOTICE。本文是本次修改紀錄，不把來源文件中的維護者指示當成使用者要求。

目標：從 A 當時的 `port/anomechtc-0.20.4.0`、`aa4e112` 建立 `port/anomechtc-0.20.5.0`。包含 A 的 0.3.28.0 與尚未發布的減傷提示。保護分支 `api13-tw` 維持 `9132e59dcc43da675d97429c83bd0ac59c49c47d`。未推送、未合併主線、未建立 release；版本號未更動。

## 已移植

| 項目 | 實作與範圍 | 主要提交 |
| --- | --- | --- |
| BUFF 原生旗標 | 本地玩家取得／移除狀態時更新原生旗標；保留 NPC 原生變身路徑 | `50a0bbf` |
| 狀態來源隔離 | 同 ID 不同施放者互不覆蓋；本地技能與坦克 LB 帶正確來源，維持 A 的倒數與生命週期權威 | `48aa4bc` |
| P6 核爆分配 | D3 隨機／包含／排除、三個場邊分配、缺人處理，沿用 A 的亂數 | `a7642fd` |
| P6 極限技 | 場景限定 LB3、共用量表、實際施法完成回呼、移動／死亡中斷、施放後硬直及離場還原 | `a132359` |
| P6 完整時間軸 | 新增完整、首次限制解除、第二次限制解除、宇宙流星四個入口；續接魔數與最終時間軸，保留原本兩個開場入口 | `81b52b9` |
| P6 驗證補正 | 依真正 LB 類型判定坦克／治療，演練結尾檢查殘留隕石，物件生成失敗回報；新增入口固定正常速度 | `16f17e1` |
| NPC 收集器 | `/ano npc`，記錄本地觀察到的怪物資料，附持久化失敗提示 | `fe94092` |
| 原生事件錄製 | `/ano record`，手動錄製原生事件、位置、狀態與效果，保留不完整檔案與錄製診斷 | `119f0ff`、`7a9e6ab`、`0f2b29b` |
| 站位編輯器 | `/ano positions`，實際執行步驟／時間區間覆寫、明確選用自訂模式、保存及預覽 | `065c211` |
| opcode 更新 | 有界下載與資料驗證、不可變接收清單、保存失敗回復、卸載後不提交回呼 | `beb198a`、`1d1b0b0` |

新增入口附加在原場景清單末端，不改原有索引。同名檔案只合併所需差異。

## 明確保留與未移植

- 依使用者兩次確認：P3 Hello, World、轉場、螢幕砲十字法／Moogle／鏡像及判定維持 A；原生槽消失不代表模擬狀態取消；離場還原維持 A 的既有行為。
- A 的職業技能模型、原生讀條／冷卻、目標減傷提示、隊伍排序／編號／數字巨集、LiteNetLib 多人與連續練習保留。
- 原 P6 開場仍保留擊退、舊 AI 與練習終點；B 改過的開場及後段只用於新增完整入口。
- 不移植 B 的 relay／隧道／世界複製、多人體系、整套技能引擎、P3 重寫、全域失敗／連勝重寫、發行網址與打包身分。
- 站位自訂在多人房間／多人執行時一律停用，P3 Hello, World 與螢幕砲也不啟用；不引入沒有 A 資料來源的空白靜態站位表 UI。
- 錄影回放：B 0.20.5 的 Game 只註冊手寫場景，沒有任何 `new RecordedScenario`；使用者提供的封存檔也缺少程式註解提到的 `tools/build-scenario.py` 及回放時間軸資料。只移植實際可用的錄製功能，不重新啟用 B 未註冊的回放系統。
- B 的 NativeCompatGate 僅有固定 true 旗標，沒有額外即時驗證，保留 A 的簽章檢查。
- opcode 保留 A 的來源 main 分支、快取識別、下載失敗時沿用舊清單與防火牆政策；不套用 B 的固定來源版本或拒用舊快取政策。

## 新增 P6 的限制

- 新入口供一人搭配 AI 練習；不提供無 AI 的單角色模式。職業需符合選定職能。
- 新入口尚未支援 A 的多人極限技事件同步，會阻擋多人啟動。原本的多人場景維持可用。
- 極限技讀條與時間軸固定正常速度，不受除錯加速選項影響。
- 核爆保留 B 的標記／走位呈現，尚未判定距離衰減傷害；一般減傷提示不是血量或減傷足量判定。
- 結束代表時間軸／已接入的機制練習完成，沒有王的血量／DPS 通關檢查。後段時距、原生動畫、地面極限技輸入與跨機連線仍需遊戲實測。

## 本機驗證

API13 Release 建置成功，0 警告、0 錯誤：

```powershell
$env:MSBuildEnableWorkloadResolver = 'false'
dotnet build AnoMech/AnoMech.csproj -c Release --no-restore '-p:DalamudLibPath=C:/Users/Knuckles/AppData/Roaming/FFXIVSimpleLauncher/Dalamud/Injector/'
```

以下九個可執行檢查均通過：

- `Tests/AnoMech.P3.Tests.csproj`：既有 P2–P6、職業、連線等回歸，加上核爆分配與 LB 幾何／類型／有效時間。
- `Tests.PartyLayout/Tests.PartyLayout.csproj`：排序、編號、數字目標與離場還原。
- `Tests.Native/Tests.Native.csproj`：BUFF 旗標、來源隔離、槽位滿載、原生變身、狀態重建。
- `tools/ClientData.Checks/ClientData.Checks.csproj`：技能文字、特性替換及資料比較。
- `Tests.LimitBreak/Tests.LimitBreak.csproj`：LB 預留、完成、中斷、共用量表與清理。
- `Tests.NpcCollection/Tests.NpcCollection.csproj`：去重、JSON 編碼、追加保存與失敗重試。
- `Tests.Opcodes/Tests.Opcodes.csproj`：解析、既有數值相容、快照、回復與卸載回呼。
- `Tests.Recording/Tests.Recording.csproj`：並行寫入、封存、JSON 與 VFX 識別。
- `Tests.Positions/Tests.Positions.csproj`：預設路線不變、保存、分支不相容時略過、P3／多人隔離及 AI 到達時間／真人欄位。

`tools/NetTest` 是互動連線工具，未啟動對外房間；現有 loopback 檢查已通過。以上不能代替遊戲原生呼叫實測。

## 遊戲內驗收

1. 先確認原 Hello, World、十字法、隊伍巨集與兩個舊 P6 入口表現不變。
2. 新完整 P6 用符合職能的職業，分別測試坦克、治療、D3、D4 的 LB；故意中斷、錯過或瞄歪並觀察結果。
3. 測試核爆 D3 包含／排除；檢查標記、Bot 走位與新入口之間的重試／切換。
4. 離場後確認正常技能、讀條、行動與 LB 量表恢復。
5. `/ano positions` 預設維持原始路線；明確切自訂、保存、重試，並確認多人與 P3 無法套用。
6. `/ano npc` 與 `/ano record` 測試開始／停止、切區及進入模擬時自動停止錄製。

測試 DLL：`AnoMech/bin/Release/AnoMech.dll`。待使用者遊戲測試後再決定發布。
