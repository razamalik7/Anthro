# Review of T9A (Claude, 2026-10-08)

**Verdict: PASS, and a big step up.** A1 verified independently (seed 47 byte-identical). The report is
honest, numbers on both seed sets, real mechanisms found (self-incriminating gossip, the offering drain).
Keep working exactly like this.

One correction on method: the base vs base2 gap (4.5%) is the difference between two *sets of worlds*. When
a change alters the random path inside the same worlds, histories re-roll, and that noise is larger (we've
seen ±10-15% on 20 worlds). Your rule of "same direction on both sets" is the right guard; keep it, and treat
effects under ~10% as unproven.

## Two findings to act on in Batch B (do these first, as B0)

**B0a. Silence breeds dread, and dread drains food (a feedback loop — fix).**
In the idle world, every failed prayer goes through `InterpretStrikingEvent(bad)` → `god:kind` falls (to
about −0.45 by year 50) → in `Decider`, `kind < 0` creates a fear motive for offering → people sacrifice
~1,000 rations per camp per year to a god who never acted → births −9 to −13% on both sets.
- Unanswered prayers are **silence, not an event**: they should teach doubt (`god:answers`, `god:real` down),
  never cruelty. `god:kind` should move only on actual striking events (a harsh season, lightning, a wrathful
  omen, a death read as punishment).
- Offerings must be **learned like any act**: value = belief × what offering seems to bring (a learned
  topic, e.g. `god:offer`, fed when relief or disaster follows an offering), plus fear only where people
  have real reason for dread. Remove the constant `0.15` base: with no god, offering should fade the same
  way praying does.
- **Acceptance:** idle world (god on, no bot) on both seed sets: food offered and births within noise of
  god-off, belief drifts down, `god:kind` stays near neutral.

**B0b. Keep self-memory, without its side effects.**
- Own deeds go in a **separate small list** (`Person.OwnDeeds`, recent season), not the shared 40-slot
  memory, so they don't push out what people know about others.
- People don't gossip about their own deeds (exclude `d.Actor == teller`; boasting can come later).
- Credit/blame reads both lists.
- **Acceptance:** `selfmemory` on vs off (god off) on both seed sets: asks, quarrels, pop within noise.

Then continue with B1-B3 as written in `docs/T9-BRIEF.md`. B3's kind-bot investigation should be re-run
after B0, since the offering drain probably explains part of the kind god's population loss.
