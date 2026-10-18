# Review of T0-T8 (Claude, 2026-10-08)

Verdict: **good scaffolding, not ready to merge.** The structure (God/ folder, switches, command queue,
prayers, credit from remembered deeds, bot gods, tests) is what the spec asked for. But the baseline claims
are not supported, two parts break the design rules, and the main acceptance result (a kind god improves
the odds) actually failed. Fix as task **T9** on a new branch from `task/T8-nudges`, then report again.

## 1. The god-off switch is not clean (must fix first)
`ANTHRO_OFF=god` on the T8 build does **not** reproduce `runs/base_41.txt`; histories diverge from year 1.
Known ungated base changes:
- `Society.Happen`: `Remember(deed.Actor, deed, true)` — every actor now remembers their own deeds. This
  changes gossip and deed memory for everyone, god or not. Gate it (`!Off("god")`) or drop it.
- `SeedBelief(group)` draws from `Rng` → shifts every later random draw. Gate it, or draw from a forked
  stream (`Rng.Fork(...)`) so the base stream is untouched.
- Check the rest of the diff (Gatherings, Decider option list order, JointActs, Family) the same way.
**Acceptance:** `ANTHRO_OFF=god` gives output identical to `base_*` on seeds 41-60 (`cmp` the `^Y` lines).

## 2. "Baseline unchanged" was claimed without evidence
T1 (belief only, no behaviour) went 1327 → 941 total pop (−29%) and was reported as "no regressions". Gang
fights swung 1504 → 974 → 416 → 1337 → 161 → 545 across tasks. Most of this is probably RNG divergence (see
1), but it can't be told apart from real effects without a noise band.
**From now on:** run base on seeds 61-80 too; the difference between base(41-60) and base(61-80)-style reruns
is the noise. Call a change "unchanged" only when it is inside that band, and say so with numbers.

## 3. Prayers use timers (design rule 1: nothing scheduled)
`Prayers.cs` resolves on fixed windows: hunger fails after 5 days, sickness 7, danger 5; **grief is
"answered" automatically after 15 days** (the god gets credit just for time passing). Spec (GOD-LAYER §3):
a plea stays open **while the need lasts**.
- Ends good when the need ends while the asker lives (by a power or by chance: coincidences count).
- Ends bad when the asker (or the sick child) dies, or when the asker stops expecting an answer: each day
  a plea goes unanswered, the asker's `god:answers` erodes a little (learned doubt); when it falls below
  what made praying worth it, they give up. No day counts.
- Drop grief pleas for v1 (no power can answer them) unless a sign appears while one is open.
- **Credit by surprise:** the earlier runaway (daily meals credited as answers) is better fixed by weighting
  credit by how unexpected the relief was (e.g. food found when the hearth was empty and the land bare
  counts fully; ordinary meals barely), not by narrow hard gates. Keep thresholds only as continuous
  severity, not as on/off gates (`Satiety < 0.25 && food <= 0.3 && band < 0.5` is three hand-set gates).

## 4. Fork nudges force decisions (design: "influence, not control")
`TryConsumeNudge` returns true/false and **overrides** the choice (marry, leave, duel, join). A nudge must be
a push: add a bias to that option's value, scaled by the person's belief (`god:real`) and credulity, so a
doubter may ignore it and a believer usually follows. Report how often nudges are followed.

## 5. The kind god lowers population — investigate before tuning
T7: kind 924 vs none 1327 total pop (−30%), camp moves 5,352 → 14,907, while starvation fell 27%. The report
calls this a success; it is the opposite of the acceptance check. Candidates: offerings destroy hearth food
(up to 15% of stores, chosen often under a kind god), time spent praying/offering, bounties pulling camps into
constant moves, `Climate` changes. Run ablations with the kind bot: `ANTHRO_OFF=offer`, `=rite`, bounty-only
(no weather), and report births, starvation, moves, hours praying/offering, food offered per year.

## 6. Smaller notes
- T7 raised answered-prayer learning rates (0.08 → 0.25) to make bots work: fine, but say why the old rates
  were wrong rather than tuning to the result.
- Blessed birth: "zero complications" and fixed +0.3 trait boosts are fine for v1; later the player should
  choose which traits.
- Omen gives every witness +0.08 `god:real` regardless of what they already believe: a doubter should
  learn less (weight by credulity, as Credit.cs does).
- Keep commits per task; don't merge to master until T9 passes.
