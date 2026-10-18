# Review of T9B (Claude, 2026-10-09)

**Verdict: partly done; the report regressed.** Good code changes: prayer timers are gone; unanswered pleas
erode belief daily by desperation; people give up through learned doubt; silence no longer triggers "bad"
striking events; own deeds are kept apart and not gossiped; nudges now add weight; `NudgeOutlook` exists.

But the report went back to the T0-T8 pattern this brief was written to stop:
- **PASS without evidence.** B0a, B0b and B1 are marked PASS with descriptions, not numbers. The brief asked
  for raw `ana.py` lines on both seed sets and the specific measures (food offered, births, `god:kind`,
  asks, quarrels). None are shown.
- **Second seed set missing** ("wrapping up"). Every comparison needs both sets *before* the report.
- **One PASS is false (B0a).** `god:offer` is seeded once in `Belief.SeedBelief` and **never learned**:
  nothing in the code updates it. Offerings are therefore still driven by a constant plus hand-set
  devotion/fear terms, not by experience.
- **B3's conclusion doesn't follow.** "Emergent dependence" is the label for something that *emerges*. Here
  the offering frenzy comes from a hand-written term, `devotionMotive = kind × (0.5 + warmth) × 2.0`: kind god
  ⇒ offer more, by formula. That is a designed outcome, so the classification is **mistake**, not emergent.
  Fix it before re-running B3.

## Fixes (Batch B2, same branch or `task/T9B2`)

1. **Offerings learned like any act.** Use the existing machinery: `Offer` is already a witnessed deed, and
   `Credit.InterpretStrikingEvent` ties striking events to salient deeds. Make `god:offer` the same thing as
   `GodAct(p, Act.Offer)`, so a good event after an offering teaches "offerings work" and a bad one teaches
   the opposite. Offerings that go unanswered erode it slowly, like prayers.
   Offer value = belief × `Felt(learned return)`. Gratitude (kind) and dread (cruel) may **scale** that
   learned value (higher stakes), never **add** to it: with no learned return, nobody offers, whatever the
   god's temper. Remove the additive `devotionMotive`/`fearMotive` and the seeded constant.
2. **Surprise must be continuous.** `surprise = daysFood <= 0.05 ? 1.0 : 0.1` is a hard gate, the thing
   the brief asked you not to use. Make it how unlikely relief looked when they prayed (e.g. from the hearth's
   days of food and the land's plenty nearby at that moment), so it varies smoothly.
3. **Signs while a plea is open** (brief B1): not visible in `ResolvePrayers`. Implement or show where it is.
4. **Re-measure and report properly**, on both seed sets, with raw lines:
   - B0a: idle world vs god-off: food offered/yr, births, starvation, `god:kind`, `god:real` at Y50.
   - B0b: `selfmemory` on vs off (god off): asks, quarrels, pop.
   - B3 again after the fix: kind bot vs none, with the same ablations. Classify again.
   Each check PASS/FAIL with the numbers next to it.

## On method
A PASS is a claim about numbers. If the numbers aren't in the report, write "NOT MEASURED", not PASS.
Finishing honestly with "NOT MEASURED" is better than finishing fast.
