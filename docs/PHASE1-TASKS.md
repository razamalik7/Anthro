# Phase 1 tasks — god core, headless

Spec: `docs/GOD-LAYER.md` (start only after the owner approves it). Rules and workflow: `AGENTS.md`.
Do the tasks **in order**, one branch each, each ending with `docs/reports/T<n>.md`. Reference for "baseline
unchanged": the current master build on seeds 41-60, 50 years, `<seed> 50 2` — run it once first and keep
the outputs as `runs/base_<seed>.txt`.

New code goes in `Anthro.Sim/God/` as partial classes of `Simulation` / `Region` (see `Customs.cs` for the
pattern: topic constants, accessors with defaults, seeding, `Off(...)` switches).

## T0 — Baseline reference (Runner, Flash)
Build Demog, run seeds 41-60 × 50 y, save as `runs/base_*`. Report the `ana.py base` line.
**Done when** the report exists.

## T1 — Belief state
- `God/Belief.cs`: topics `god:real`, `god:answers`, `god:kind`, `god:<act>` helpers; accessors with defaults;
  seeding per people (call next to `SeedCustoms` in `Society.cs`); fading toward the seed when unfed
  (daily, tiny, in the person daily update next to the lean-season lesson in `Simulation.cs`).
- Add `god:` to the inherited prefixes in `Society.InheritSocialExpectations` and `Region.AddKinCamps`.
- Demog: add believers (count `god:real` > 0.5) and mean `god:real` to the yearly culture line.
**Done when** builds, 15 tests pass, baseline unchanged (20 worlds), belief stays low and varied with no god.

## T2 — The god object and powers API (no powers yet)
- `God/God.cs` on `Region`: favor, cap, reputation, start pool 50, an event list (prayers, power uses) and a
  command method per power (stubs). Deterministic: commands applied at the start of an hour step.
**Done when** builds, tests pass, baseline unchanged.

## T3 — Praying and prayers as requests
- `Verb.Pray` + activity in `Activities/` + option in `Activities/Decider.cs` (value per GOD-LAYER §2).
- `Prayer` records (who, need, place, day), open while the need lasts; resolution teaches `god:answers` /
  `god:real` to the asker and witnesses (reuse witness logic in `Society.cs`).
- Favor from prayers into the God object (§2), cap from believers.
- Demog: prayers asked / resolved-good / resolved-bad per year; favor.
**Done when** baseline unchanged (praying fades with no god); a test bot that answers nothing shows belief
falling, and a scripted "coincidence" test shows credit rising.

## T4 — Powers
Omen, Bounty (plants, animals), Weather, Blessed birth — each through existing systems (cells,
`EcologySystem`, `Climate`, `NewChild`). Costs from GOD-LAYER §4. One unit test per power showing its effect.
**Done when** each power changes the world as described and the tests pass.

## T5 — Credit and blame (misinterpretation)
- `God/Credit.cs`: on striking events, witnesses tie the event to a salient remembered deed → `god:<act>`
  lesson (GOD-LAYER §5); Decider adds `Felt(god:act) × god:real` to that act's value.
**Done when** a test shows: omen right after a witnessed take → witnesses' `god:take` drops and taking falls;
baseline unchanged.

## T6 — Reputation, offerings, rites, last embers, fading
Per GOD-LAYER §2 and §6.
**Done when** tests: a cruel god gets more fear offerings; ignored embers → the god fades.

## T7 — Bot gods in Demog + the 20-world A/B
Demog arg/env `ANTHRO_GOD=none|kind|cruel|random`: a simple policy that answers prayers and spends favor.
Report: survival, population, belief, favor, `god:` beliefs per people vs none.
**Done when** kind improves odds of survival vs none; ignoring prayers breeds doubt; cruel breeds fear.

## T8 — Fork nudges (can follow Phase 2)
Detect life branch points (marrying, leaving camp, a duel, joining a raid) as notifications; the nudge shifts
that one decision.
