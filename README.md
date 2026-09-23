# ![AnoMech](images/icon.png) AnoMech — 繁中版 (TC / API13)

這是 [anomek/AnoMech](https://github.com/anomek/AnoMech) 的**非官方分支**，移植到
**FINAL FANTASY XIV 繁體中文版**所使用的 Dalamud（API 13，7.3 基底客戶端），介面已繁體中文化。

原作者為 [Anomek](https://github.com/anomek)。本分支與原作者、Square Enix 及台港澳營運商均無關聯。

> **實驗性質，請先看完「目前狀況」再裝。**

---

## 這是什麼

在旅館或住宅室內練習高難副本機制。插件會在你的客戶端生出假隊友和假 boss，讓它們照真實時間軸跑機制。
可以一個人練，也可以開房讓朋友連線一起練，沒有真人的位置由 AI 補上。

模擬全部在本機進行，不會送任何東西到遊戲伺服器。多人連線的網路說明見下方「與伺服器的關係」。

進旅館，或住宅區的室內（個人／公會房屋、個人房間、公寓房間、公會工房、公寓大廳）後打 `/anomech`（或 `/ano`）開啟，房內有其他玩家也可使用。不開放屋外住宅區。

## 安裝

在遊戲中打 `/xlsettings` → **實驗性**分頁 → 自訂插件庫，加入這條網址後按存檔：

```
https://raw.githubusercontent.com/Knucklesssss/AnoMech-TW/api13-tw/repo.json
```

接著 `/xlplugins` 就能搜尋到 **AnoMech (TC)** 並安裝。之後有更新也會自動收到。

## 目前狀況

以下為 0.4.2.0 的狀況。

**可用的場景（14 個）**

每個場景可以在「地區」列切換打法，括號內是打法名稱。

- 絕歐米茄（The Omega Protocol）
  - P2 Party Synergy — 美服（Standard）／日服（tuuufless）／陸服（B站莫古力）
  - P3 Hello, World（含轉場）— 日服（tuuufless）／陸服（B站莫古力）
  - P3 螢幕砲 — 原有（標準AI）／陸服（B站莫古力、十字法）；含承傷判定
  - P4 藍屏 — 日服（tuuufless）／陸服（B站莫古力）
  - P5 Delta — 原有（Standard）／陸服（B站莫古力）
  - P5 Sigma — 美服（Standard）／日服（tuuufless）／陸服（B站莫古力）
  - P5 Omega — 原有（Standard、標準（第二目標帶 2 層））／陸服（B站莫古力）
  - P6 阿爾法歐米茄 — 原有／陸服（B站莫古力），共六個場景：
    - 機制練習（無 LB）：開場段、限制解除 → 波動砲
    - LB 練習（至通關）：完整時間軸、限制解除①、限制解除②、宇宙流星
- 絕神兵（The Weapon's Refrain）
  - Ultimate Predation（NAUR）

**主視窗的分頁**

- **練習**：選場景、職能與打法後開始。
- **多人連線**：房主建立房間，把邀請碼給朋友；每個人操作自己的角色。
- **連戰**：把多個場景排成清單依序練習。全員存活就進下一個，有人死亡就重來同一個。
- **隊伍順序**：自訂模擬中隊伍列表的排列。隊員旁的編號與巨集 `<1>`～`<8>` 會跟著改；
  遊戲內建的隊伍選取快捷鍵仍照原本順序。
- **職業支援**：90 級的所有戰鬥職業（青魔除外）都能在模擬中用原本的熱鍵打技能循環，
  包含連擊、量譜、冷卻與技能變換。不計算傷害與治療量。

**其他功能**

- **目標減傷練習**（預設開啟，可在設定關閉）：記錄你施放的雪仇、昏亂、牽制、武裝解除，
  機制命中時顯示有沒有蓋到、還剩幾秒。只記錄你自己，不改變血量或生死。
- **P6 極限技**：LB 練習場景提供場景限定的 LB3，並判定是否在時限內開好。
- **站位編輯器**（`/ano positions`）：自訂 AI 隊友在各步驟的站位。只在單人練習時可用，
  多人連線以及 P3 Hello, World、螢幕砲不適用。

**你好世界的標記方式（P5 西格瑪 / P5 歐米茄）**

設定視窗裡可選：

- **系統標**：插件自動把八個人全部掛上標記，適合邊學邊看。
- **玩家手標**：插件完全不掛標，改成**讀你自己在遊戲裡放的標記**來決定誰跑哪個位置——
  攻擊 1～4 與鎖鏈 1・2 各自對應固定的站位，可以拿來練你們自己的指派。沒放的標記會沿用
  插件原本挑的人，所以只放一半也跑得完。你好世界的持有者不受標記影響，他們的站位由機制決定。

**已知限制**

- **沒有 Dancing Mad (DMU)。** 那是 7.51 才上線的內容，繁中版的 7.3 客戶端裡根本沒有相關的
  NPC、技能與特效資料，無法移植。上游有 5 個 DMU 場景，本分支全部移除。
- **副本不會正式「開始」**，結界可能不會降下。負責送出開始事件的
  `EventFramework.ProcessDirectorUpdate` 在繁中版的執行檔上還沒找到正確的簽章，該呼叫會被跳過。
- **仍屬實驗性質。** 每個版本還沒在遊戲內確認的項目，列在該版 release notes 的
  「尚未在遊戲內確認」段落；其他已知問題列在「已知限制」段落。
- **手動下標記的巨集請用 `<t>` 而不是 `<mo>`。** 假隊友要「被標記過一次」之後，`<mo>`
  才認得它們，所以 `/mk attack1 <mo>` 在剛開場時會靜靜地沒有反應；`/mk attack1 <t>`
  則一直有效。插件會在場景開始時幫每個假隊友各掛一次標記再立刻清除，用意就是把這層
  暖機做掉，但這條路是否對所有情況都有效尚未確認。
  「螢幕砲」另外提供 `/ano mark attack1`，直接寫標記、沒有暖機問題：
  優先標在滑鼠指向的對象，沒有就標在目前目標上，`/ano mark clear` 清掉全部。
  這個指令只在該場景有效——其他 P 用隊伍列表或 `/mk <t>` 都正常，不需要第二套做法。
- P3「Hello, World」是從單次錄影重建的，Bot 走位依攻略手寫，尚未逐項驗證完畢。
- 上游原本就有的問題部分仍在（例如連線距離判定粗略）。

**與伺服器的關係**

進入模擬區域時，插件會擋掉伺服器封包以維持假區域穩定。這段期間：

- 隊員加入或離開不會反映在隊伍列表上，直到你離開模擬區域
- 準備確認不會跳出

多人連線是玩家電腦之間直接連線（UDP），不經過遊戲伺服器，也不經過任何中繼伺服器：

- 房主建立房間時，會向 Google 或 Cloudflare 的公開 STUN 伺服器查詢一次自己的對外位址。
  只有這一次查詢，連線內容不會經過它。
- 房主有時需要在路由器設定 UDP 42420 的通訊埠轉送，建立房間時插件會告訴你需不需要；加入者不用設定。
- 房主與加入者的連線協定版本必須相同，不同時會被拒絕連線。每版 release notes 會註明協定版本。

## 這個分支改了什麼

依 AGPL 第 5 條標示修改內容：

- 建置目標由 `Dalamud.NET.Sdk` 15 降為 13（net10.0 → net9.0）
- 改寫 net9.0 編譯器無法解析的 C# 14 語法（`extension` 區塊、屬性中的 `field`、
  .NET 10 的 `Enumerable.Shuffle`）
- 還原 Dalamud v14／v15 才有的 API 用法
- 補上 API13 版 FFXIVClientStructs 缺少的結構與函式（角色旗標位元、`SpawnObjectPacket`、
  `ModelContainer` 欄位、`VfxObject` 成員函式、敵對列表數量欄位等）
- 移除 `Scenarios/Umad`（見上方限制）
- 所有簽章改為可失敗，並在啟動時輸出解析報告；重新推導 `GameMain.LoadZone`，
  修正 `GetEventObjectByIndex` 過短而會誤配到錯誤位址的簽章
- 介面繁體中文化。機制與型態名稱維持英文；副本名稱改為直接讀取客戶端自身的
  `ContentFinderCondition` 資料表，因此顯示的一定是官方用語
- 依 [tuuufless 的攻略](https://ffxiv-top.tsuki-sakura.workers.dev/guide)新增日服戰術：
  TOP P2 Party Synergy（開場兩列、同組同符號靠南換邊、分攤換離眼睛較遠者）與
  TOP P5 Sigma（在兩隻手臂中間排隊、依優先度分派歐米茄 F 側三人、擊退後看左右按
  雙人塔→單人塔踩塔）
- P5 西格瑪與 P5 歐米茄新增「玩家手標」模式：插件不掛標，改讀玩家放置的隊伍標記決定站位
- 修正 P2 的 Optical Unit 生成在 `NewNorthA` 的**反方向**且背對場地，導致 4:4 分散整組
  轉了 180°、直線雷射判定框也永遠掃不到人（同一份程式碼中只有這一處的 Z 軸符號寫反）
- 修正 PS 符號陣列順序（`× □ 〇 ▽` → `〇 × ▽ □`），該陣列的索引就是連線組別編號，
  順序錯會讓 P2 與 P5 西格瑪每一排掛到錯的符號
- 新增 P3（歐米茄最終形態），分成「Hello, World」與「螢幕砲」兩個場景。
  「Hello, World」以 `tools/parser.py` 從一次實戰錄影重建時間軸，每次執行會重新洗牌
  八人在循環中的位置，Bot 走位依 tuuufless 攻略手寫；傳毒階段的接毒時機改為讀取塔的
  實際狀態而非固定秒數，接線的一方會就近選擇帶毒者。
  「螢幕砲」則是手寫場景而非重播錄影：王側左右、三位持有者與各自的螢幕方向全部隨機，
  結算時逐圈計算命中數並回報未承傷／重疊承傷。幾何與判定規則抽在
  `TopP3MonitorRules.cs`，不依賴 Dalamud

- 新增 P4 藍屏場景，以及 P3 Hello, World 的即時機制判定（漏塔、連線超時、被他人的毒波及）
- 依 B站莫古力攻略新增陸服打法：P2 Party Synergy、P3 Hello, World、P3 螢幕砲（另有十字法）、
  P4 藍屏、P5 Delta／Sigma／Omega、P6
- 新增多人連線：玩家電腦之間直接連線，多人一起練同一個場景
- 新增本機職業技能模擬：90 級所有戰鬥職業（青魔除外）
- 新增連戰、隊伍列表順序、目標減傷練習，以及主視窗分頁
- 部分功能移植自 [YS6720/AnoMechTC](https://github.com/YS6720/AnoMechTC)（同為 AnoMech 的繁中移植，
  AGPL-3.0-or-later）：P6 開場段與「限制解除 → 波動砲」、P6 完整時間軸與三個 LB 練習入口、
  P6 極限技與核爆分配、P5 Omega「第二目標帶 2 層」、P2 Standard 同列分攤交換、P4 時序、
  站位編輯器、NPC 收集器與原生事件錄製等。來源程式的作者與衍生關係以該儲存庫為準

## 授權

與上游相同，採 **AGPL-3.0-or-later**。原始碼見本 repo，完整條款見 [LICENSE.md](LICENSE.md)。

## 提醒

使用第三方插件違反 FINAL FANTASY XIV 的服務條款，風險請自行承擔。這點與上游原版相同。

---
---

# 以下為上游原始說明（英文）

*Another FFXIV mechanics simulator*

> 注意：下方「Currently implemented」列出的 Dancing Mad (Ultimate) 系列**不包含在本分支中**。

Simulate FFXIV raid mechanics client-side for solo practice. Go to any Inn, open the plugin with `/anomech` and start practicing!

Thanks to improvemnts by [WorstAquaPlayer](https://github.com/WorstAquaPlayer) plugin is quite stable now! No more
crashes after training session.

**WARNING!!!**

**You are cut off from server traffic while in the sim zone.** To keep the
fake zone stable, the plugin firewalls incoming packets from the server.
While simulating:
  * Players joining or leaving your party will not appear in the party list
  until you leave the sim zone.
  * Ready checks will not pop.

## Installation

See: https://github.com/anomek/MyDalamudPlugins

## Currently implemented:
- Dancing Mad (Ultimate)
    - P2 Forsaken
      - NA
        - [Kroxy-Rinon 341 (Center/N Stacks) melee adjust](https://raidplan.io/plan/UATE__aDcw1-bgVv)
        - [South Adjust 341](https://raidplan.io/plan/uq7zdjvuu7uuw8fj)
        - diamond markers or week one positions
      - EU _by [Wydox](https://github.com/Wydox)_
        - [\[LPDU\] Buddies](https://raidplan.io/plan/142oXOZpPc_jh3dd)
        - [\[Old\] p3Z Buddy Meow](https://raidplan.io/plan/lZWqxfxvyhF9sp3Z)
        - [\[Old\] zP6 South adjust](https://raidplan.io/plan/rtc1FcuZFMuyBzP6)
    - P3 Black Hole _old bh (DSA, single tethers, n/s stomps)_
    - P4 Kefka Says _kefkabin_
    - P5 Exaflares _by [Wydox](https://github.com/Wydox)_
    - P5 Forsaken Null _no ai or damage_
- The Omega Protocol (Ultimate): _NA pf strats_
    - P2 Party Synergy
    - P5 Delta
    - P5 Sigma
    - P5 Omega
    - P6 Exasquares / Wave Cannon 2
- The Weapon's Refrain (Ultimate) _by [WorstAquaPlayer](https://github.com/WorstAquaPlayer)_
    - Ultimate Predaction

## Details

* Spawns fake party members and boss NPCs into the live game client
* Drives their positions, cast bars, tethers, and VFX so mechanics play out visually
* Your fake party members are full fledged bots that will do mechanics.
  Some scanarios also have solo mode where you can practice without disctractions.


## How to help
1. Please provide feedback and report any issues in scenarios: bad timing, damage, config not working at it supposed
2. Bot AI currently only covers strategies from my region. Adding strategies for other regions requires little coding.
   Feel free to create pull request or contact me.
3. Adding new scenario is more involved. `tools/parser.py` generates a baseline scenario from a log, which still
   needs randomization, mechanic-failure logic and bot AI added by hand.
4. Plugin-development or reverse-engineering help, and improvement ideas, are also welcome.


## Known issues
* Minor visual and timing issues may occur
* In scenarios for Top Omega Protocol (Ultimate):
  * Tether distance threshold are very rough estimations
  * Line AOE from Optiocal Unit (eye) doesn't render

#  Acknowledgments

Thanks for contributors:
* [WorstAquaPlayer](https://github.com/WorstAquaPlayer) - rewriting core & fixing crashes, scenarios for uwu
* [Wydox](https://github.com/Wydox) - EU strats for Forsaken, UMAD Exaflares, core improvements

AnoMech leans heavily on the work of other Dalamud plugins. Huge thanks to their authors!
Without them, the following would not be possible:

* **Hyperborea** — solo duty arena loading.
* **FFXIV-RaidsRewritten** — stunning the player on death and playing raid VFX.
* **bossmod** — mechanics timings and positions.
