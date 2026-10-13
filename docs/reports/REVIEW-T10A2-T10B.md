# Review of T10A2 and T10B (Claude, 2026-10-13)

**Verdict: the code is mostly as specified; the conclusions are mostly noise.** Feasts (A4a), sharing → generosity
(B1), the escalated plea (B2), grave goods (B3) and the cultural seed (B4) are implemented with tests, and the
god-off check passes. But several PASSes and headline results don't hold, and the main lesson is about method.

## 1. We have been reading noise (most important)
The same condition on the same 20 seeds swings ±20-25% between builds whose changes shouldn't matter much:
- `idle` set 41-60 total pop: 1,349 (T10A) → 1,173 (T10A2) → **986** (T10B).
- `skilful` set 61-80: 918 (T10A) → 981 (T10A2) → **1,539** (T10B), now called "the strongest performance
  recorded across all phases".
Any change in what people do (a prayer, a feast) re-rolls every later event, so whole histories diverge. With 20
worlds, differences under ~25% are indistinguishable from luck. T10B's "idle within noise: PASS" with −25.7% on
set 1, and "skilful +21.5%" on set 2 (while −5% on set 1), are both inside that band.

**Fix the method before Batch C (task M1):**
- Add a **paired comparison** to the tools: for each seed, the log ratio of condition vs `godoff` (pop, births,
  starvation, gang thefts…); combine both sets (40 seeds); report the mean and a **95% bootstrap confidence
  interval**. An effect is real only if the interval excludes zero. Pairing removes the differences between
  worlds (rich vs poor maps), which is most of the variance.
- From now on every claim is stated as "X: +a% (95% CI b% to c%, 40 worlds)". No CI, no claim.
- Re-analyse the existing T10B runs with it (no new runs needed) and restate the T10A2/T10B results.

## 2. Data hygiene
- `runs/base_41` … `base_56` are **truncated** (16-23 years instead of 50; overwritten by interrupted runs on
  10-09). The T10A2 "16/16 identical across all Y lines" only compared the surviving years. `godoff_*` are full
  and match those prefixes: make `godoff_*`/`godoff2_*` the canonical references and delete or regenerate the
  broken `base_*`.
- The `godoff` rows show Real 0.27 / Kind 0.08 / Answ 0.35 — impossible with the god off. `c_metrics.py` is
  reading other culture columns when the god fields are absent. Fix it.
- T10A2 says T10A's skilful run had "766 total pop and 2 extinctions"; T10A's own report says 1,026 / 918 and
  0 extinctions. Don't restate earlier numbers from memory: quote the file.

## 3. Favor pacing (A4b) isn't met and isn't checked
The target was ~24 small, ~3-4 medium, ~1 large act a year for a faithful band of ~30 (≈300 favor/yr). Measured
skilful spending is 10-20 favor/yr (≈2 omens a year) and the god is broke ~70% of days, same as before. The
report shows the planned arithmetic but never compares acts per year with the target. In M1 also report acts per
year by size vs the target, and say which side is short: income (people don't give enough), the cap, or the bot
holding back (the new cushions and interval limits).

## Then Batch C, with the paired CIs as the standard of evidence.
