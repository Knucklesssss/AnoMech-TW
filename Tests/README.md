# P3 regression checks

Run from the repository root (no game or test-framework package required):

```powershell
$env:MSBuildEnableWorkloadResolver = 'false'
dotnet run --project Tests/AnoMech.P3.Tests.csproj
```

The checks link the production P3 rules, AI, role assignment and event scheduler.
Native game objects are replaced with test doubles; game VFX and the player's input
still require in-game verification at normal speed.

For this machine's Taiwan API13 build:

```powershell
$env:MSBuildEnableWorkloadResolver = 'false'
dotnet build AnoMech/AnoMech.csproj --no-restore '-p:DalamudLibPath=C:/Users/Knuckles/AppData/Roaming/FFXIVSimpleLauncher/Dalamud/Injector/'
```

## Implementation scope

- Transition: independent cannon-role assignment, live circle/ring/arm checks,
  and bot movement through both expanding ring sequences. Entering the central
  voidzone is treated as an immediate training failure, not a bleeding damage simulation.
- The default arm layout is the south-first arrangement in the embedded capture.
  The reverse option is explicitly a practice variant, not a claim about the game's
  complete set of random patterns.
- Hello World: poison propagates by live contact (3y center-to-center), including
  wrong recipients and opposite-color overwrite. Matching debugger blocks infection;
  same-color contact does not refresh the timer. Each pickup lasts 27s, then explodes
  within 6y, grants immunity and clears only its matching tower defect.
- Tethers resolve by actual distance, clear both their VFX and debuffs, and fail on
  timeout/death or overlapping breaks. Towers require one actual occupant and their
  10s defect must be cleared by the correct poison expiry.
- AI assigns paired tower/waiting/pickup destinations by shortest combined distance,
  latches them for the stage, and separates inner/outer routes during rotation.
  Far receivers wait for their tether to actually break before retreating. Full four-
  round runs at 6y/s are checked at 30/60 fps across eight starting role layouts.
- Boss visuals and stack/defamation status propagation still follow the capture.
  This is not yet live spread/stack damage and propagation. Simultaneous conflicting
  poison contact uses older-holder-first order; it does not model server RNG/latency.

Geometry is corroborated by [BossMod's P3 intermission implementation](https://github.com/awgil/ffxiv_bossmod/blob/ecf7a1d2d4a694bf177fc5a4b7966e0f6524f2ad/BossMod.Ultimate/Endwalker/Ultimate/TOP/P3Intermission.cs).
Movement follows the [supplied P3 guide](https://ffxiv-top.tsuki-sakura.workers.dev/guide#p3-1).
Damage is evaluated at the capture's action-effect timestamps. Imported 1.7s arm
casts have a 0.26/0.27s release delay to align their effects with those timestamps.

For in-game acceptance, begin with the original arrangement at 1x speed and check
the two north stacks, four southern spreads, post-third-ring move, and first HW
pickup. Then test the reverse practice arrangement and the fourth HW round.

For the Hello World update, also deliberately touch an unintended player with a
poison, move out of a tower, and leave a tether unbroken. Verify the resulting
status/failure, then reset and complete a normal run. Tether VFX disappearance and
native status rendering require this in-game check. Transition and Monitors are
unchanged by this update.

Poison/tether interactions follow [Tuufless's mechanic descriptions](https://ffxiv.tuufless.com/elemental/top/03_omega_reconfigured/).
