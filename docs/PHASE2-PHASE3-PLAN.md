# Phase 2 & Phase 3 Plan — Milestone Scenario & Sim $\leftrightarrow$ Unity API

From: Argon. Date: 2026-10-18.
Status: Ready for implementation on branch `task/phase2-3-api` (from `master` God Core v1).

---

## 1. Phase 2 — Milestone 1 Scenario & End-to-End God Tests

### 1.1 Canonical Player Scenario (`MilestoneScenario`)
Create `Anthro.Sim/Api/MilestoneScenario.cs` providing a clean entry point for the Milestone 1 playable experience:
- **Founding Setup:** 1 founding band (~30 people) + 2 kin camps of the same people on the 20 km × 20 km valley map (`Region.Create(seed, web, bandSize: 30, otherBands: 0, kinCamps: 2)`), starting in late summer (Day 60 of Summer) with `Favor = 50.0`, `Policy = GodPolicy.None` (player-controlled).
- **Discovery Web:** Bundled / auto-loaded so Unity or headless callers don't need manual path hunting.

### 1.2 Scenario Verification Tests (`Anthro.Tests/MilestoneScenarioTests.cs`)
Fast, deterministic tests (< 10 s total) verifying the player-facing god loop:
1. **Prayer & Answer Loop:** When a camp member prays (or a prayer is open) and the player casts `CastBounty` / `CastOmen` / `CastHeal`, the prayer resolves as answered, witnesses update `god:real` and `god:answers`, and a notification is emitted.
2. **All Powers via Command Queue:** Queuing `Omen`, `Bounty` (plants/animals), `Weather`, `BlessedBirth`, `Heal` (when `GodHelp >= 4.0`), and `Lightning` (when `GodHarm >= 4.0`) through `GameSession` deducts the expected favor, updates `GodHelp`/`GodHarm`, and applies at the start of the next hour step.
3. **Fork Nudges:** When a life branch point (`Fork`) is raised (marriage, camp split/move, duel, raid), the player can inspect the option margin and apply a `Nudge` that shifts the decision weight deterministically.
4. **Misinterpretation & Relief:** Casting a wrathful omen after a witnessed `Take` lowers `god:take`; holding back wrath after an offering in dread raises `god:offer`.

---

## 2. Phase 3 — `Anthro.Sim/Api/` (Sim $\leftrightarrow$ Unity Bridge)

All types live in `Anthro.Sim/Api/` (`netstandard2.1`, zero Unity dependencies) so `Anthro.Sim.dll` drops straight into Unity 6.3.

### 2.1 `GameSession` (Main Controller)
- `GameSession.StartNew(ulong seed, DiscoveryWeb? web = null, int kinCamps = 2)`
- `void StepHours(int hours = 1)` — advances the simulation while respecting pause/speed state and draining queued player commands deterministically at the start of each hour.
- **Time Controls:** `bool IsPaused`, `int HoursPerTick` (e.g. `1` = hourly, `6` = 4×/day, `24` = daily fast-forward), plus auto-pause hooks on high-priority notifications (urgent prayers, life forks).

### 2.2 Read-Only Snapshot Views (Per-Tick UI & Map Rendering)
Lightweight read-only view structs/classes populated on demand or per tick without exposing mutable sim internals:
- **`GodStateView`:** `Favor`, `SustainableFavor`, `GodHelp`, `GodHarm`, `GodFaded`, `IgnoredEmbers`, active discount multipliers, and per-power `CanCast` / `Cost` status.
- **`WorldCellView`:** Terrain, elevation, plant type & current/capacity rations, fish, wood, flint, weir status, and herd counts per cell `(x, y)`.
- **`CampView`:** `BandId`, `Name`, `TribeName`, cell `(CampX, CampY)`, population, total food / days of food, tools, firewood, belief band counts (`Skeptic`, `Open`, `Believer`, `Devout`), and mean cultural/theological profile (`god:real`, `god:answers`, `god:kind`, `god:give`, `god:take`, `god:attack`, `god:offer`, `god:thank`).
- **`PersonView`:** `Id`, `Name`, `Sex`, `AgeYears`, `BandId`, cell `(X, Y)` *(cell-level; local in-camp hearth offset is handled by Unity)*, current `Verb` and `ActivityStep` string, `Health`, `Satiety`, `Warmth`, `Happiness`, `Dread`, `BeliefBand`, personal `god:*` expectations, traits/skills summary, and family IDs (`Spouses`, `Children`, `Parents`, `HouseholdId`).
- **`PrayerView` & `ForkView`:**
  - `PrayerView`: `PrayerId`, `AskerId`, `AskerName`, `BandId`, `CampX`, `CampY`, `Need` (`Hunger`, `Sickness`, `Danger`, `Thanks`), `StartDay`, `HasOffering`, `Urgency`.
  - `ForkView`: `ForkId`, `PersonId`, `PersonName`, `Kind`, description, current option scores/margin, and nudge favor cost.

### 2.3 Unified Notification Stream (`SimNotification`)
- Pull-based queue (`IReadOnlyList<SimNotification> DrainNotifications()`) combining:
  - Important `Chronicle` entries (`Importance.Notable` / `Major`),
  - New/resolved `Prayer` events,
  - New `Fork` branch points,
  - `GodEvent` confirmations and `LastEmber` warnings.
- Each notification includes `(Tick, Year, Season, Day, Kind, Text, CampX, CampY, FocalPersonId, FocalBandId)` so Unity can display a toast and click-to-focus the camera.

### 2.4 Save / Load & Determinism Verification
- **`GameSave` serializer (`Anthro.Sim/Api/GameSave.cs`):**
  - Captures complete deterministic state of `Region` (`SimClock`, `Rng`, `GodRng`, `WorldMap` cells, `EcologySystem` herds, `Bands`, `People` with IDs/relationships/`Expectations`/`Deeds`, `ActivePrayers`, `God` state).
  - `byte[] SaveToBytes(GameSession)` / `GameSession LoadFromBytes(byte[], DiscoveryWeb?)`.
- **Determinism Unit Test:** Run seed 42 for 500 hours $\to$ save to bytes $\to$ load into a fresh `GameSession` $\to$ run both original and loaded sessions for another 500 hours $\to$ assert 100% bit-for-bit identical population, positions, food, beliefs, and RNG state.
