# P4 藍屏練習

獨立手寫場景，沒有錄影重播。範圍為三輪直線分散／四人直線分攤、原砲線重複攻擊、兩組地靈脈與即時失誤判定。不模擬輸出、LB、減傷或血量門檻；藍屏結束依機制失誤紀錄回報結果。

## 攻略與資料來源

- [使用者指定攻略](https://ffxiv-top.tsuki-sakura.workers.dev/guide#p4)：南側兩組分攤；北至南依坦克、遠程、治療、近戰排序。同側雙點名時，較南的點名者與另一側近戰交換。
- [BossMod P4WaveCannon](https://github.com/awgil/ffxiv_bossmod/blob/master/BossMod.Ultimate/Endwalker/Ultimate/TOP/P4WaveCannon.cs)、[技能資料](https://github.com/awgil/ffxiv_bossmod/blob/master/BossMod.Ultimate/Endwalker/Ultimate/TOP/TOPEnums.cs)：長 100、寬 6 的直線砲；地靈脈範圍依序 0–6、6–12、12–18、18–24 碼。
- [cactbot 時間軸](https://github.com/OverlayPlugin/cactbot/blob/main/ui/raidboss/data/06-ew/ultimate/the_omega_protocol.txt)：以 P4 重新可選取的 607.1 為場景零秒。下表為目前採用的練習時間，分散傷害相對視覺動作延後約 0.6 秒；繁中實際快照與特效仍需遊戲內核對。

| 輪次 | 分攤點名 | 分散快照 | 原砲線重擊 | 分攤快照 |
| --- | ---: | ---: | ---: | ---: |
| 1 | 11.9 | 14.9 | 19.6 | 19.9 |
| 2 | 22.0 | 25.0 | 29.5 | 29.7 |
| 3 | 31.9 | 35.1 | 39.6 | 39.8 |

地靈脈兩組：22.3／24.4／26.4／28.4 秒，39.5／41.5／43.5／45.5 秒。藍屏詠唱 46.5–54.5 秒。

## 實測

2026-09-11：使用者回報遊戲內測試無問題，核准發布 0.3.16.6。

進入 P4 → 藍屏，選擇日服 tuuufless，使用 1 倍速。點名可選每輪有／無。先確認三輪砲線、點名效果、穿圈節奏與聊天失誤訊息；無敵模式只供觀察，發生失誤仍不會回報通過。

既有引擎倍速只縮短事件間隔，不會加速角色移動；30／60 FPS 跑位測試針對 1 倍速。P4 天候先沿用 P3，沒有額外指定 BGM。

自動測試：`dotnet run --project Tests/AnoMech.P3.Tests.csproj`，涵蓋所有 28 種雙點名組合、三輪輪換、原砲線快照、四人承傷、地靈脈邊界與視覺 helper 清理。
