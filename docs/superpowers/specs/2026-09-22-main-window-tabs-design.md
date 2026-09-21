# 主視窗分頁化設計

日期：2026-09-22　分支：`port/anomechtc-0.20.5.0`（0.4.0.0 之後）

## 目標

把「多人同步」「連戰」「隊伍列表順序」「職業支援列表」四個獨立視窗收進主視窗，
成為與「練習」並列的分頁：

```
未開始
[練習][多人連線][連戰][隊伍順序][職業支援]
```

參考對象是 Astra 的 TC 版（「AnoMech 使用者版 v0.20.5.15」）的三張截圖。
**該版的程式碼取不到**，上游 `anomek/AnoMech`（master 在 v0.4.0.0）與本倉庫所有分支都沒有
`BeginTabBar`，所以這份設計是從截圖反推，不是移植。細節與該版不會完全一致。

## 現況

`MainWindow`（584 行）是一個兩欄表格：左欄場景清單（可用 `<` 收合），右欄場景詳細與控制。
左欄上方有四顆按鈕，各自開一個獨立視窗：

| 視窗 | 行數 | 尺寸 |
| --- | --- | --- |
| `MultiplayerWindow` | 212 | 最小 480×320 |
| `ChainWindow` | 151 | 預設 620×420 |
| `PartyListOrderWindow` | 76 | 預設 |
| `JobSupportWindow` | 47 | 預設 |

四個都在 `Plugin` 的 `WindowSystem` 註冊，並有對應的 `Toggle*Ui()`。
標題列另有一顆「多人同步」圖示按鈕。

## 決定

| 項目 | 決定 |
| --- | --- |
| 分頁數 | 五個：練習／多人連線／連戰／隊伍順序／職業支援 |
| 舊的獨立視窗 | **全部拿掉**，只剩分頁 |
| 視窗尺寸 | 插件不自動調，只設最小尺寸；其餘由使用者拉 |
| 進場自動收合 | **維持現狀**，多人連線分頁跟著收合 |

## 版面

- 狀態列（`RunStatus`，例如「未開始」）在分頁列**上方**，不屬於任何分頁。
- **練習**分頁：維持現在的兩欄表格與 `<` 收合，內容不動。
- 其餘四個分頁：各自佔滿視窗寬度，沒有左欄。

## 程式結構

四個視窗降級為面板：去掉 `Window` 基底與尺寸設定，只留 `Draw()`，
由 `MainWindow` 在對應分頁內呼叫。

| 現在 | 之後 |
| --- | --- |
| `MultiplayerWindow : Window, IDisposable` | `MultiplayerPanel` |
| `ChainWindow : Window, IDisposable` | `ChainPanel` |
| `PartyListOrderWindow : Window, IDisposable` | `PartyListOrderPanel` |
| `JobSupportWindow : Window, IDisposable` | `JobSupportPanel` |

`Draw()` 的內容逐字搬移，不改寫。四個 `Draw()` 都是自足的 ImGui，
不依賴自己身處一個獨立視窗，唯一的例外見下。

### `OnOpen` 的去處

`MultiplayerWindow.OnOpen()` 會執行 `directConnection = HostReadiness.PublicAddressOnThisPc()`。
沒有了視窗就沒有這個回呼，改成**切換到多人連線分頁的那一幀**觸發：
`MainWindow` 記住上一次選中的分頁，與本幀不同且本幀是多人連線時，呼叫面板的 `OnShown()`。

### 進入點

`Plugin` 的四個方法保留名稱與簽章，行為改為「打開主視窗並切到指定分頁」：

```
ToggleMultiplayerUi / ToggleChainUi / TogglePartyListOrderUi / ToggleJobSupportUi
```

這樣 `/anomech` 的指令處理與其他呼叫端都不需要改動。
`WindowSystem` 少註冊這四個視窗；`MainWindow` 改為持有四個面板。

### 移除

- 標題列的「多人同步」圖示按鈕（它現在是一個分頁）。齒輪（設定）保留。
- 左欄上方那一列四顆工具按鈕（`DrawPanelTools`）與 `PanelTools` 資料表。
- `ScenarioPanelWidth()` 裡為那一列加的寬度下限 —— 按鈕不存在了，面板寬度回到只由
  場景名稱決定。

## 尺寸

`MainWindow.SizeConstraints.MinimumSize` 提高到容納最寬的分頁（多人連線原本的 480×320）。
不隨分頁切換調整大小，不記錄每個分頁各自的尺寸。

## 已知代價

主視窗在進入模擬場地時會自動收合（`PreOpenCheck`，除非開啟「精簡模擬控制」）。
多人連線變成分頁後一併被收合，**跑場景時要展開主視窗才看得到連線狀態與斷線通知**。
先前它是獨立視窗，可以一直攤在旁邊。

此代價為明示選擇。若日後造成困擾，兩個可行的補救是：多人連線分頁例外保留獨立視窗，
或改為「房間裡有人時不自動收合」。本次不做。

## 驗證

`MainWindow` 不在測試專案中，本次改動全屬版面與搬移，沒有可單獨測試的分支邏輯。
可提供的保證是建置乾淨、既有測試不退步，以及搬移前後 `Draw()` 內容逐字比對。
**分頁切換、收合行為、各面板在新寬度下的表現需要實機確認。**

## 不在範圍

- 四個面板**內部**的任何版面或行為調整。
- 設定視窗、站位編輯器、除錯視窗等其他視窗。
- 主視窗自動收合規則的變更。
