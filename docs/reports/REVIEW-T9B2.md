# Review of T9B2 (Claude, 2026-10-09)

**Verdict: Batch B accepted.** Big improvement: both seed sets, raw lines, numbers next to each check, a real
fix (learned offerings), and the B3 classification corrected honestly. The idle world now matches god-off
within noise (pop −0.7% / −7.5%, births −4% / −3%, offerings ~0), and the kind bot now improves survival on
both sets (pop +46% / +39%, starvation −32% / −10%). Proceed to Batch C, with the follow-ups below.

## Things the report missed (check these first in Batch C)
1. **Gang thefts rise 4-8× under the kind bot** (1,504 → 6,397 and 1,336 → 10,826). Not mentioned. Find out why
   (bounties making some hearths rich while neighbours are hungry? bigger camps? less fear of the god?), and
   whether it's explainable or a mistake. This belongs in C1's multi-dimension comparison.
2. **`rite` switch does nothing.** `kind_norite` is identical to `kind` on every number in both sets. Either
   the switch isn't wired, or rites never happen. Find which.
3. **Quarrels = 0.0 in every run.** Quarrels exist in the base game, so the metric in `b2_metrics.py` is
   broken. Fix it before using it.
4. **Table vs raw lines disagree for set 41-60:** the table's `kind` row (median 59.0, offered 664) doesn't
   match the raw `kind` line (median 72.0, total 1935), which matches `kind_norite`. Probably stale files or
   the same prefix bug you fixed in `ana.py` but not in `b2_metrics.py`. Make the table come from the same
   files as the raw lines.
5. **`selfmemory` identical with the god off** — fine (own deeds are only read by credit/blame), but say that
   this is *why* B0b shows 0.000%.

## For the owner to decide (don't change yet)
- **Offerings learned from giving food to people.** Credit now ties a sign following `Act.Give` (hearth
  sharing) to `god:offer`. Giving to people and offering to a god are different acts; the natural reading is
  "the god likes generosity" (`god:give`). How offering first arises from nothing is an open design question
  (a cultural seed for some peoples? ritual/burial knowledge?). Keep the current behaviour but flag it.
- **14-day window** for "an offering went unanswered" in `FadeBelief` is a small timer. Acceptable for now;
  note it in AUDIT-HARDCODE.md.

## Then: Batch C as written in `docs/T9-BRIEF.md` (C1-C3), same reporting standard as T9B2.
