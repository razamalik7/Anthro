# T11 brief — devotion, the favor bank, and living belief

For: Gemini. From: Claude and the owner. Date: 2026-10-13.
Read first: `AGENTS.md` (incl. "Don't waste runs"), `docs/reports/M2.md`, and the "Decisions 2026-10-13" section at
the end of `docs/GOD-LAYER.md`. Branch `task/T11` from `task/T10B`. Don't merge to master.
Standard of evidence: paired 40-world comparisons vs `godoff`, every claim as "+a% (95% CI b to c, 40 worlds)".
Effects whose CI barely clears zero are "probable", not settled, unless other metrics agree.

## Review of M2 (short)
Accepted, with one correction. The pacing/belief diagnosis (crisis-only income, the "offering paradox", acts too
rare to be seen) is excellent. But M2a's "true driver: theft and violence spirals" rests on **raw totals**
(6,551 vs 2,840 thefts), which a few worlds dominate; your own paired M1 numbers call idle thefts and violent
deaths noise. Don't explain a paired result with raw totals. Redo it as D0 below.

---

## D0. Idle-world deficit, properly (measure only, reuse existing runs)
Idle vs god-off: pop −18%, births −15%, techs −21% (paired, M1), while fertility per woman is unchanged (M2).
Births fall with normal fertility ⇒ fewer women of reproductive age, or women spending fewer years in a union.
With paired CIs: deaths by cause and age group (especially women 15-44), adult women alive per year, share of
women 15-44 married, separations, out-migration of whole households. Name the driver only if its paired CI
excludes zero and its size can account for the births gap.

## D1. Everyday devotion: thanksgiving (owner decision)
Believers also pray and give thanks in good times, not only in crises.
- It's a learned habit, like everything else: a thanks followed by good fortune strengthens it, a thanks followed
  by nothing (or by a blow) weakens it. Value = belief × learned return of thanking (`god:thank`), in the Decider.
- Thanks-giving is witnessed and gives favor weighted by the person's belief, steady and small.
- Credit stays weighted by surprise, so in comfortable times good fortune earns little and devotion should thin
  out on its own (owner: easy times may mean less prayer — **emergent, not a rule**). Measure whether it happens.

## D2. The favor bank leaks instead of capping (owner decision: option b)
Remove the hard `FavorCap` clip. Favor above what the believers can sustain (`Σ god:real × k`) leaks away slowly
each day (a proportional decay of the excess). Famine bursts are kept for a while instead of thrown away, but
can't be hoarded forever. Disclose `k` and the leak rate.

## D3. Living belief: strength, content, diversity (owner decision)
Belief has two parts that must both be visible:
- **Strength** per person (`god:real`), read in four bands for reporting only (not new rules): skeptic (< 0.3),
  open (0.3-0.6), believer (0.6-0.8), devout (> 0.8 and practising: prays/thanks/offers).
- **Content** per person: the `god:<act>` profile, `god:kind`, `god:answers`.
Changes:
- **The devout teach more strongly**: in `SocialLearning`, a speaker's `god:` topics are copied with weight scaled by
  their devotion (as charisma and following already scale it). Nothing else new: temperament (credulity,
  openness), experience (who saw what) and personal events already create diversity.
- **Personal shocks**: a death in the family during an unanswered plea should be able to break an individual's
  faith quickly (large `god:real` / `god:answers` drop for that person), and a dramatic answered plea should be
  able to convert one. Check whether existing learning already does this before adding anything.

## D4. Pacing scales with faith (owner decision)
Targets are no longer one fixed number:
| Stage | Small acts (omen, nudge) | Medium (bounty) | Large (weather, heal, lightning, blessed birth) |
|---|---|---|---|
| new god, few believers | ~1 a month | rare | rare |
| faithful band (~30, most believers) | ~weekly | a few a year | ~1 a year |
Report favor income and acts per year against the stage the band is actually in.

## Owner targets to measure (not to tune toward blindly; report each with CIs)
| Situation | After ~1 generation (25 y) |
|---|---|
| responsive, visible god (active skilful bot) | most people open or believer; ~10-20% devout; a lasting skeptic minority ~10-20% |
| no god (idle) | back to background belief |
| any | the `god:<act>` profile differs measurably between people and between camps (never unanimous) |
| comfortable times | belief and practice fall off unless the god keeps showing itself |
| idle god after years of activity | believers fall back within ~1 generation, faster for the less credulous |
New measures: share of adults in each band (Y1, 10, 25, 50); spread of `god:<act>` within a camp (std dev) and
between camps of the same people; devotion acts per adult per year by season and by how fed the camp is.
Also one **"god goes quiet" run**: active skilful for 25 years, then idle for 25 (both sets) for the last row.

## Runs (only these)
God-off identity check on 5 seeds. Idle (new code) and active skilful (new code), both sets. The god-goes-quiet run,
both sets. Unit tests once at the end. D0 reuses existing runs.

Report `docs/reports/T11.md`: D0-D4, then the targets table with CIs and a plain verdict per row (met / not met /
not measured). Then **stop**: the owner reviews before Batch C (gods as patterns).
