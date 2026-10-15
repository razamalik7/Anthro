# Anthro handoff (2026-10-14)

Start here if you are a new agent, or the owner on a new tool/account. Everything the project needs is in this
repo; nothing depends on any AI account.

## What to read, in order
1. `AGENTS.md` — the design rules, how to build/run/test, the run-efficiency rules, the workflow.
2. `docs/agent-memory/` — the owner's standing preferences and project state that Claude kept as memory:
   design principles, balance target (survival hard without a god), realism-not-exactness, "base sim before god",
   and the running state/queue. Treat them as the owner's instructions.
3. `docs/PLANNING.md` — the vision and decided design. `docs/REFLECTION.md` — where we are vs the plan.
4. `docs/GOD-LAYER.md` — the god layer design plus every dated decision since (2026-10-08 … 10-13).
5. The latest brief and reports: `docs/T11-BRIEF.md` (next task), `docs/reports/` (T0 … M2 and Claude's reviews).
6. `docs/PROGRESS.md` (tail) — the base-sim history and its readiness review.

## State
- **Base sim:** done and passed its readiness review (build v80, commit 786a739 on master).
- **God layer (Phase 1):** built on stacked branches `task/T0-baseline` … `task/T11` (not merged). Belief,
  prayers (outcome-ended, no timers), offerings (learned; shared feasts; escalated plea; grave goods; cultural seed),
  5 powers, credit/blame by misinterpretation, relief learning, nudges as weights, Heal/Lightning, bots
  (none/kind/cruel/skilful), everyday devotion (thanksgiving), leaking favor bank, living belief (4 bands, devout
  teaching multiplier, personal shocks), faith-scaled pacing. God-off switch reproduces the base game byte-for-byte.
- **Completed:** `docs/reports/T11.md` finished; idle deficit explained via early winter starvation culling founder
  girls (-20.1% reproductive woman-years in Y25-50); 40-world paired CIs and owner targets evaluated.
- **Claude Archive:** Full history of Claude Code sessions (8 sessions, ~60 MB JSONL plus readable Markdown transcripts)
  and memory files permanently preserved in `docs/archive/claude/` and `docs/agent-memory/`.
- **Next:** Batch C of `docs/T10-BRIEF.md` (gods as patterns: C1, C2, C3), then merge "god core v1" to master, then
  Phase 2 (one band of ~30 + god tests), Phase 3 (sim↔Unity API), Phase 4 (Unity slice). Plan: see the end of this file.
- **Measurement standard:** paired per-seed comparison vs `runs/godoff_*` / `godoff2_*`, 40 worlds, 95% bootstrap
  CI (`Anthro.Demog/tools/paired_cmp.py`). With 20 unpaired worlds, differences under ~25% are noise.

## Lessons about working with agents on this project
- Agents tend to: report PASS without numbers; spin drops; tune numbers until checks pass; use timers/hard gates/
  overrides instead of learned mechanisms; restate old numbers from memory. Briefs with explicit acceptance checks,
  batch-and-stop, and an independent reviewer fixed most of this.
- The owner decides anything that changes how the world works or what people value; agents propose.

## Plan to Unity (approved 2026-10-07)
Phase 1 god core (finishing) → Phase 2 milestone scenario + god tests → Phase 3 `Anthro.Sim/Api/` (read-only
snapshot per tick, event stream from the chronicle, command input for powers/prayer answers/time controls,
save/load + determinism check; `Person.X/Y` is cell-level, in-camp placement is a Unity job) → Phase 4 Unity 6.3
simple-shapes slice (map, camps, people doing verbs, notifications, prayer panel, power targeting, person inspector).
