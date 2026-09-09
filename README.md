# ![AnoMech](images/icon.png) AnoMech — 繁中版 (TC / API13)

這是 [anomek/AnoMech](https://github.com/anomek/AnoMech) 的**非官方分支**，移植到
**FINAL FANTASY XIV 繁體中文版**所使用的 Dalamud（API 13，7.3 基底客戶端），介面已繁體中文化。

原作者為 [Anomek](https://github.com/anomek)。本分支與原作者、Square Enix 及台港澳營運商均無關聯。

> **實驗性質，請先看完「目前狀況」再裝。**

---

## 這是什麼

在旅館裡單人練習高難副本機制。插件會在你的客戶端生出假隊友和假 boss，讓它們照真實時間軸跑機制——
全部在本機模擬，不會送任何東西到伺服器。

進旅館後打 `/anomech`（或 `/ano`）開啟。

## 安裝

在遊戲中打 `/xlsettings` → **實驗性**分頁 → 自訂插件庫，加入這條網址後按存檔：

```
https://raw.githubusercontent.com/Knucklesssss/AnoMech-TW/api13-tw/repo.json
```

接著 `/xlplugins` 就能搜尋到 **AnoMech (TC)** 並安裝。之後有更新也會自動收到。

## 目前狀況

**可用的場景（6 個）**

- 絕歐米茄（The Omega Protocol）
  - P2 Party Synergy
  - P5 Delta
  - P5 Sigma
  - P5 Omega
  - P6 Exasquares / Wave Cannon 2
- 絕神兵（The Weapon's Refrain）
  - Ultimate Predation

**已知限制**

- **沒有 Dancing Mad (DMU)。** 那是 7.51 才上線的內容，繁中版的 7.3 客戶端裡根本沒有相關的
  NPC、技能與特效資料，無法移植。上游有 5 個 DMU 場景，本分支全部移除。
- **副本不會正式「開始」**，結界可能不會降下。負責送出開始事件的
  `EventFramework.ProcessDirectorUpdate` 在繁中版的執行檔上還沒找到正確的簽章，該呼叫會被跳過。
- **實測非常有限。** 目前只完整跑過 TOP P5 Delta。其餘 5 個場景尚未有人驗證。
- 上游原本就有的問題同樣存在（連線距離判定粗略、Optical Unit 的直線 AOE 不會顯示等）。

**與伺服器的關係**

進入模擬區域時，插件會擋掉伺服器封包以維持假區域穩定。這段期間：

- 隊員加入或離開不會反映在隊伍列表上，直到你離開模擬區域
- 準備確認不會跳出

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
