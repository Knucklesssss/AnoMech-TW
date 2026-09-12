# Task 3 report: Warrior offensive rule model

## Result

Implemented the pure level-90 Warrior offensive state model and the minimum shared timing extensions required by it. The model emits potency metadata only. It does not add native hooks, auto-attacks, numerical healing, shields, mitigation, or damage conversion.

## TDD evidence

RED was observed before each production increment:

- Missing model: `Warrior offensive state is missing.`
- Initial action model: Heavy Swing returned no hit instead of the literal 200-potency result.
- Tempest/AoE: Storm's Eye returned no hit instead of the combo 440-potency result.
- Timing extension: compilation failed on the intentionally test-first missing `IsAvailable`, `LockRemaining`, and `Reduce` API.
- Infuriate/Chaos: targetless in-combat Infuriate incorrectly reported unavailable.
- Inner Release/Rend: targetless Inner Release incorrectly reported unavailable.
- Charge abilities: Onslaught returned no hit instead of its 150-potency gap-closer result.
- Gauge cap: combo gains raised Beast above 100; the failing fixture observed this before `GrantBeast` capped every gain.

GREEN evidence:

- `dotnet run --project Tests/AnoMech.P3.Tests.csproj --no-restore` exits 0 and prints both the shared timing PASS and Warrior offensive state PASS, followed by every existing regression PASS.
- The same run covers all 17 supported action IDs with literal hit/state fixtures, dead-use rejection for every supported action, target/range/bind/resource/cooldown rejection, read-only `CanUse`, exact timer boundaries, reset, and invalid time atomicity.

## Files

- `AnoMech/Core/Combat/WarriorCombat.cs` — pure Warrior state, action adjustment/readiness/use, combos, Beast, Tempest, Chaos, Inner Release, Rend Ready, cooldowns and potency-only hit output.
- `AnoMech/Core/Combat/CombatTiming.cs` — read-only availability/lock inspection and checked serial recharge reduction.
- `Tests/WarriorCombatChecks.cs` — behavioral Warrior fixtures and rejection boundaries.
- `Tests/CombatTimingChecks.cs` — reduction/readiness/lock coverage and the immediately-before-30-second recovery assertion.
- `Tests/Program.cs`, `Tests/AnoMech.P3.Tests.csproj` — one Warrior check/source registration each; unrelated registrations are preserved and excluded from this task's staged hunks.

## Self-review

- `CanUse` delegates to a non-registering timing query and does not alter combo, resources, timers, cooldowns, or lock state.
- Failed validation occurs before cooldown/resource mutation. Unsupported and deferred utility actions remain unsupported.
- Empty self-AoE activations consume one activation but return no hit and do not grant combo, Beast, Tempest, or recharge reduction.
- Tempest is sampled before applying a triggering combo effect, so the triggering hit is unbuffed and later hits receive 1.1.
- Chaos spenders retain their 50-Beast cost and do not consume Inner Release stacks. Fell Cleave and Decimate consume an Inner Release stack instead of Beast.
- Infuriate reductions cross serial recharge boundaries, carry only current excess, and bank no credit at full charges.
- Combo-preserving actions do not refresh the combo timer. Beast gains cap at 100. Reset clears all state and restores timing groups.
- No native adapter or health/defense behavior was added; this remains non-playable until later stages.

## Verification note

The standalone regression project builds and runs cleanly. The full plugin build is verified with the installed FFXIVSimpleLauncher Dalamud injector path supplied explicitly, avoiding the incompatible default XIVLauncher development path.

## Review fix round 1

- RED: after Heavy Swing established combo action 31, an empty Overpower activation left combo action 31 active. `dotnet run --project Tests/AnoMech.P3.Tests.csproj --no-restore` exited 1 at `AoeCombosRequireAnEnemyHitForGains` with `An empty Overpower must clear a nonmatching combo...`.
- GREEN: empty Overpower and empty Mythril Tempest now clear any prior combo before returning no hit; neither starts/advances a combo nor grants Beast or Tempest. The same regression command exits 0 with the Warrior offensive-state check and the complete existing suite passing.
