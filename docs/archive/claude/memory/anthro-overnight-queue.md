---
name: anthro-overnight-queue
description: "Anthro state 2026-10-07 evening (pre-compaction): base-sim readiness work, what's done, what's next"
metadata:
  node_type: memory
  type: project
  originSessionId: 865e9bd1-fa3d-4202-bc44-bde12f493603
  modified: 2026-10-07T00:40:04.476Z
---

Goal now: get the base sim "all good" before the god layer ([[anthro-sim-before-god]]).
Full logs: Anthro/docs/PROGRESS.md, REFLECTION.md, AUDIT-DECISIONS.md, AUDIT-HARDCODE.md (additions
2026-10-07), TECH-SETTLEMENT.md (settlement forms: towns need agriculture; tech effect kinds).

**User decisions 2026-10-07:** discovery pace 3/40 y is fine. Violence/conflict should be common
(drama) but cultural, not rules: duels vs brawls vs ambush, side-taking cultural. Two-peoples test
light (no economics). Speed was worth doing first. Agriculture proper waits; forager tech → at most
semi-settled villages at rich places. Disliked hard caps like the 3-exchange fight limit.

**Done today (current build = Demog bin_v66):** speed +18% (hunt Outlook, decision caches, gossip);
fights end by exhaustion (no cap); violence seeds wider; customs norm:sides & norm:duel; Ambush
activity (kills unseen → family blames a suspect); side-taking in gangs; gang cooldown removed;
gatherings driven by urge+plenty; weirs as built structures at river cells (Cell.Weir); metrics
days-at-site & weirs; rationing unlearned only from rotted stores (StoresSpoiledRecent); schism rift
uses full culture distance (_cultureTopics). Tests last run: 15 pass (before speed/conflict work —
rerun needed).

**Results:** 20 worlds×40 y v63/v65: pop ~930-980, conflict in 15/20, killings in 10/20, forms
vary. Tech start: camps stay ~5 y at a site, ~90 weirs. 150-y runs (long66 vs long): median pop
Y150 84 vs 46, 1 extinction, 3/20 worlds with 2 peoples, rationing still drifts down to ~0.3.

**State 2026-10-07 late (build Demog bin_v80):** rationing fixed (cold rot Q10≈2 + two-way lesson);
gang loop fixed; event-driven marriage reviews; elders learn from moves; hunting-prestige learning
tried and removed (self-reinforcing). Readiness review in PROGRESS.md: all ✅ except #8 peoples never
split in 150 y (0/20). Open: how much families store (median ~2 wk into winter); rationing drifts to 0.12.

**User 2026-10-07:** one people is fine for now (#8 schisms deferred). Base sim judged ready.
Rationing falls with storage tech (stores in surplus → "pinched for nothing"); rebound after hard winters unverified.

**Next:** god layer per plan (Phase 0 design → 1 god core → 2 milestone setup+tests → 3 sim↔Unity API → 4 Unity
slice). Owner must approve docs/GOD-LAYER.md first. Since 2026-10-07 the repo is in git (baseline commit
786a739 on master); Demog now lives in Anthro.Demog/ (build to runs/bin). Workflow with Antigravity: AGENTS.md
+ docs/PHASE1-TASKS.md (T0-T8), one branch per task, reports in docs/reports/T<n>.md for Claude to review.
Claude budget is tight (weekly limit ~87% on 10-07): review reports, don't run sims.

**Gotchas:** wait loops must not require all worlds to reach the last year (extinct worlds stop
early — a loop hung 4 h); compare builds on ≥20 worlds; tech-start grants: dry.keep,dry.rack,store.cache,store.pit,fish.spear,fish.weir,camp.return,camp.long,lang.story,lang.ritual,shelter.hut,grain.harvest,grain.clear; Demog in OLD scratchpad
ad250276…/scratchpad/Demog, new bin_vN per build; args seed years kin grants otherPeoples;
dotnet "/c/Program Files/dotnet/dotnet"; python patch files (heredoc quoting breaks); env
ANTHRO_DEBUG_FIGHTS/_KILLS/_CHRON/_TRACE, ANTHRO_OFF switches.

Related: [[anthro-balance-target]], [[anthro-design-principles]], [[anthro-realism-not-exactness]].
