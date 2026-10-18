# Anthro handoff (Updated 2026-10-18)

Start here if you are a new agent, or the owner on a new tool/account or second PC. Everything the project needs is in this
repo; nothing depends on any single machine or AI account.

## What to read, in order
1. `AGENTS.md` — the design rules, how to build/run/test, the run-efficiency rules, the workflow.
2. `docs/agent-memory/` — the owner's standing preferences and project state that Claude kept as memory:
   design principles, balance target (survival hard without a god), realism-not-exactness, "base sim before god",
   and the running state/queue. Treat them as the owner's instructions.
3. `docs/PLANNING.md` — the vision and decided design. `docs/REFLECTION.md` — where we are vs the plan.
4. `docs/GOD-LAYER.md` — the god layer design plus every dated decision since (2026-10-08 … 10-13).
5. `docs/UNITY-VISION.md` & `docs/PHASE2-PHASE3-PLAN.md` — Phase 2/3 API contract, Phase 4 Unity 6.3 vision, the Three Clocks, Owned Asset Inventory, and *Nova Roma* / *Planet Zoo 2* / *Townscaper* / *The Sims* touchstones.
6. The latest reports: `docs/reports/T11.md` and `docs/reports/T10C.md` (Phase 1 completion), `docs/PROGRESS.md` (tail).

## State (Updated 2026-10-18)
- **Base sim:** done and passed its readiness review (build v80, commit 786a739 on master).
- **God layer (Phase 1 — Complete & Merged to `master`):** All tasks `T0` … `T11` and `T10C` (Batch C: Gods as Patterns, Not Labels) verified across 40-world paired bootstrap pools and merged to `master` (`commit 20855f8`). All 5 owner acceptance rows in `docs/reports/T11.md` are **MET**, and all 5 pattern bots in `docs/reports/T10C.md` teach distinct cultural `god:*` profiles with zero `ANTHRO_OFF=god` baseline drift.
- **Phase 2 (Milestone Scenario) & Phase 3 (`Anthro.Sim/Api/`) — Complete & Merged to `master` (`commit 9f1574a`):**
  - `Anthro.Sim/Api/Views.cs`, `GameSession.cs`, `GameSave.cs` provide the complete headless Sim↔Unity contract (read-only tick snapshots, drained notifications, god power & prayer/fork command recording, time controls, and bit-for-bit deterministic binary save/load replay).
  - 52/52 unit tests in `Anthro.Tests` pass (47 core + 5 `MilestoneApiTests`).
- **Next:** **Phase 4 — Unity 6.3 slice**. See `docs/PHASE2-PHASE3-PLAN.md` and `docs/UNITY-VISION.md` (our visual, camera, scale, pacing, and UX reference anchored on *Nova Roma*).
- **Measurement standard:** paired per-seed comparison vs `runs/godoff_*` / `godoff2_*`, 40 worlds, 95% bootstrap
  CI (`Anthro.Demog/tools/paired_cmp.py`). With 20 unpaired worlds, differences under ~25% are noise.

## Lessons about working with agents on this project
- Agents tend to: report PASS without numbers; spin drops; tune numbers until checks pass; use timers/hard gates/
  overrides instead of learned mechanisms; restate old numbers from memory. Briefs with explicit acceptance checks,
  batch-and-stop, and an independent reviewer fixed most of this.
- The owner decides anything that changes how the world works or what people value; agents propose.

## Plan to Unity (approved 2026-10-07)
Phase 1 god core (DONE) → Phase 2 milestone scenario + god tests (DONE) → Phase 3 `Anthro.Sim/Api/` (DONE) →
Phase 4 Unity 6.3 slice (map, camps, people doing verbs, notifications, prayer panel, power targeting, person inspector; see `docs/UNITY-VISION.md`).
