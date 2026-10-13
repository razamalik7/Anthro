# Working on Anthro (for any AI agent)

Anthro is an anthropological / evolution god-sim. The simulation is plain C# (`Anthro.Sim`, netstandard2.1,
C# 9, no Unity references) and runs headless; Unity comes later for presentation. Read `docs/PLANNING.md`
(the vision), `docs/REFLECTION.md` (where we are vs the plan) and the tail of `docs/PROGRESS.md` (latest
state) before changing anything. Current work: **the god layer**, spec in `docs/GOD-LAYER.md`, tasks in
`docs/PHASE1-TASKS.md`.

## Roles
- **Claude (Opus)**: design decisions, specs, reviews. Budget-limited: hand it short reports, not logs.
- **Implementer (Antigravity: Gemini Pro / Sonnet)**: one task at a time from `docs/PHASE1-TASKS.md`.
- **Runner (Gemini Flash)**: builds, multi-world runs, extracting numbers into reports.
- **The owner** decides anything that changes how the world works or what people value.

## The design rules (non-negotiable)
1. **Emergent and organic.** Nothing scheduled, nothing global: no fixed-date events, no timers or
   cooldowns, no caps that decide outcomes. Rhythms come from need, the body and culture.
2. **Two kinds of numbers.** *World facts* (spoilage, animal sizes, disease) are grounded in real data where
   possible. *Valuations* (fear vs hunger, glory, risk) come from the person (traits, situation) and their
   culture, never one global constant. Any hand-set number must be **disclosed** in your report and, if it
   stays, in `docs/AUDIT-HARDCODE.md`.
3. **People decide with one fixed formula fed by learned expectations.** Behaviour changes by changing what
   people learn (`People/Expectations.cs` topics, `Society.Learn`, `SocialLearning`), not by adding rules
   that force outcomes. Customs are learned topics (`norm:*`, see `Customs.cs`).
4. **Realism, not exactness.** Rates needn't match foragers exactly; they must be natural and explainable.
5. **Survival without a god is hard.** Bands can die out; discovery is slow. Don't tune toward "everyone
   survives". The god is what improves the odds.
6. **Audit → talk → change.** If a task reveals a problem outside its scope, report it; don't fix it.
7. Every new mechanism gets an `ANTHRO_OFF` switch (`Simulation.Off("name")` in `Customs.cs`) so it can be
   ablated.

## Code map (Anthro.Sim)
- `Region.cs` — the world: bands (`Simulation` each = one camp), clock, ecology, `Step()` (one hour).
- `Simulation.cs` (+ partials `Society.cs`, `Social.cs`, `Family.cs`, `JointActs.cs`, `Fights.cs`,
  `Customs.cs`, `Gatherings.cs`, `Tribe.cs`) — one camp's daily/hourly life.
- `Activities/Decider.cs` — how each person picks what to do next (options with values on one scale).
- `People/` — `Person`, `Household`, `Expectations` (learned beliefs), `Deeds` (remembered acts),
  `SocialLearning` (beliefs spreading by talk).
- `Core/Chronicle.cs` — the story log (future notifications). `Core/Rng.cs` — deterministic RNG; **never**
  use `System.Random` or anything non-deterministic.

## Build, test, run
Use the full dotnet path on this machine: `"/c/Program Files/dotnet/dotnet"` (Git Bash).
- Tests: `dotnet test Anthro.Tests` (~5 min; 15 tests must pass).
- Analysis tool **Demog**: `dotnet build -c Release Anthro.Demog -o runs/bin`, then from `runs/`:
  `dotnet bin/Demog.dll <seed> <years> <kinCamps> [grants|-] [otherPeoples]`
  - standard world: `<seed> 50 2` (3 camps of one people, ~70 people)
  - two peoples: `<seed> 40 2 - 1`
  - tech start: `<seed> 40 2 dry.keep,dry.rack,store.cache,store.pit,fish.spear,fish.weir,camp.return,camp.long,lang.story,lang.ritual,shelter.hut,grain.harvest,grain.clear`
  - stdout = yearly report (`Y  n pop ...` lines, FOOD, culture every 10 y); stderr = progress `[seed] Yn pop n`.
  - Env: `ANTHRO_OFF=a,b` (ablate features), `ANTHRO_CHRON=<regex>` (print matching chronicle lines),
    `ANTHRO_DEBUG_FIGHTS`, `ANTHRO_DEBUG_RATION`, `ANTHRO_TRACE`.
- Compare builds with `python ../Anthro.Demog/tools/ana.py <prefixA> <prefixB>` (files `<prefix>_<seed>.txt`).

## Testing rules
- Compare builds on **≥20 worlds** (seeds 41-60; re-check on 61-80). 6-10 worlds can't see 20-30% effects.
- Run worlds in parallel (`for s in $(seq 41 60); do ... & done; wait`). A 50-year world takes ~3 min alone.
- **Never write a wait loop that needs every world to reach the last year**: worlds can go extinct early.
  Use `wait` on the processes.
- The **no-god baseline must not change** (population, starvation, violence, discovery pace within noise of
  the reference runs) unless the task says so.

## Don't waste runs (multi-world runs are the biggest time cost)
- **Never re-run reference results.** `runs/godoff_*` (seeds 41-60) and `runs/godoff2_*` (61-80) are the god-off
  references; reuse them. Reuse any earlier run whose code hasn't changed since (say which file you reused).
- **Only run conditions the change can affect.** E.g. a change to offerings needs idle + the bots that use
  offerings, not every bot and every ablation. Ablations only when a result needs explaining.
- **God-off identity check:** 5 seeds per batch (e.g. 41-45) is enough while developing; all 20 only before a
  merge to master.
- **Iterate small, confirm big.** While developing, use 3-5 worlds to see that something works at all (no
  conclusions from them). Run the full two-set comparison **once**, at the end of the batch, on the final code.
- **Unit tests once per batch**, at the end (they take ~5 min), plus the specific new test while writing it.
- If a full comparison was already done for this exact code, don't repeat it for the report.

## Workflow
- One task per git branch (`task/T1-belief` …); commit when its acceptance check passes. Never commit to
  `master` directly; never push; never rewrite history.
- Finish each task with `docs/reports/T<n>.md` (≤1 page): what changed (files), hand-set numbers, switches,
  results table (A/B over 20 worlds), anything surprising, open questions. That report is what Claude reviews.
- Don't edit `docs/PLANNING.md` decisions; propose changes in the report.
