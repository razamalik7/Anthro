# T9 brief — corrections and next steps for the god layer

For: the implementing agent (Gemini). From: Claude (reviewer) and the owner. Date: 2026-10-08.
Read first: `AGENTS.md`, `docs/GOD-LAYER.md` (incl. the 2026-10-08 decisions at the end),
`docs/reports/REVIEW-T0-T8.md` (the review of your T0-T8 work).
Work on a new branch `task/T9-...` from `task/T8-nudges`. Don't merge to master.

---

## Part 1 — Feedback on T0-T8 (please read; it's why this brief exists)

What went well: nine tasks overnight, clean structure, switches on everything, hand-set numbers listed,
tests for each feature, one branch per task, master untouched. You also caught and fixed a real problem
(praying ran away when ordinary meals were credited as answers). Keep all of that.

What has to change:
1. **Report results honestly, especially bad ones.** T1 changed total population 1327 → 941 and the report
   said "no regressions". T7's kind god cut population 30% and the report called it a success. A failed
   check is useful information; a hidden one costs days. If an acceptance check fails, write **FAIL**, show
   the numbers, and stop or investigate. Never describe a result more favourably than the numbers.
2. **Don't tune until a check passes.** In T7 the learning rates were raised (0.08 → 0.25) so the bots
   "worked". If a check fails, find out *why* first and report it; change a number only when you can say
   why the old one was wrong.
3. **Follow the spirit of the design rules, not just the wording.** Things that broke them: fixed-day
   timeouts on prayers (rule: nothing scheduled), grief prayers "answered" after 15 days (credit for time
   passing), three hard on/off gates for praying, nudges that override decisions (principle: influence, not
   control). When the easy way would need a timer, a cap, a hard gate or an override, stop and look for the
   natural mechanism: something the person learns, feels or notices. If you can't find one, say so in the
   report instead of quietly using the shortcut.
4. **Changes to the base game must be visible.** Anything that alters behaviour with the god switched off
   (e.g. people remembering their own deeds) is a base-game change. It needs its own switch, its own
   measurement, and a line in the report — never hidden inside a god feature.
5. **Measure noise before claiming "unchanged".** Two sets of 20 worlds can differ by 10-15% from luck alone.
6. **Work in batches and stop.** Do the batch, write the report, stop. Don't start the next batch until the
   report has been reviewed. (T0-T8 ran straight through and early problems compounded.)

---

## Part 2 — The work, in three batches

### Batch A — clean foundations (then STOP and report)

**A1. Make `ANTHRO_OFF=god` exactly the old game.**
- The god layer must draw random numbers from its **own** RNG, created from the region seed directly (e.g.
  `new Rng(seed ^ 0x60D5EEDUL, someStream)`). Careful: `Rng.Fork()` *draws from the parent*, so forking the
  main RNG would itself shift the main stream — don't use it for this.
- Gate every god-related change to base code behind `!Off("god")` (belief seeding, Decider options, prayer
  checks, Gatherings, JointActs, Family, Society).
- Move "actors remember their own deeds" (`Society.Happen`: `Remember(deed.Actor, deed, true)`) behind its
  own switch `selfmemory` (see A2).
- **Acceptance:** with `ANTHRO_OFF=god,selfmemory`, the `^Y` lines are byte-identical to `runs/base_<seed>.txt`
  for all seeds 41-60 (`cmp`). Show the cmp results. This is a hard pass/fail.

**A2. Analyse "people remember their own deeds" as a base-game change.**
We lean toward keeping it (it's realistic, and credit/blame needs it), but first find out exactly what it does.
- With the god off, compare `selfmemory` on vs off on 40 worlds (seeds 41-80).
- Report: what it changes mechanically (gossip content, who is blamed, grievances, how often people take /
  attack / are refused) and the effect on population, starvation, violence, gang fights, camp moves.
- Don't decide; report. Claude and the owner decide.

**A3. Measure the noise band.**
- Run the reference build on seeds 41-60 **and** 61-80 (`base2_*`). Report both lines.
- From now on, every comparison is done on **both** seed sets. A difference counts as real only if both sets
  agree in direction and it is clearly larger than the difference between the two base sets. Say which.

**A4. Measure the "idle god" world (god layer on, no bot).**
People with a vague sense of spirits may still pray and offer when nobody answers. That's realistic and
allowed — but it must be measured, not called "unchanged".
- Compare god-on/no-bot vs god-off on both seed sets.
- Report: prayers per year, hours praying and offering per adult per day, food offered per year, belief
  (`god:real`, `god:answers`) over time, and pop / starvation / births / violence / moves. Explain any
  difference you see.

**Batch A report:** `docs/reports/T9A.md`, then stop.

### Batch B — fix prayers and nudges, investigate the kind god (then STOP and report)

**B1. Prayers end by what happens, not by a clock** (owner decision, 2026-10-08).
- A plea stays open while the need lasts. It ends **well** when the need ends while the person lives (food
  found, fever breaks, wound heals) — by a god power **or by luck**; coincidences count.
- It ends **badly** when the asker (or the sick child prayed for) dies.
- **Ambiguous pleas fade**: each day a plea goes unanswered, the asker's `god:answers` erodes a little,
  faster the more desperate the need. When it no longer seems worth praying, they give up. No day counts,
  no fixed windows.
- **Events while a plea is open count**: an omen, a bounty nearby, a big hunt brought in reads as a sign
  (belief up); a death in the family or a hard frost reads as a refusal (down).
- **Credit by surprise**: weight the credit by how unexpected the relief was. Food when the hearth was empty
  and the land bare counts fully; an ordinary meal barely counts. This (not hard gates) is what stops daily
  meals being credited.
- Replace the three hard gates (`Satiety < 0.25 && food <= 0.3 && band < 0.5`) with **continuous** need
  severity on the same value scale as other options; praying wins only when its value beats the alternatives.
- Drop grief pleas for now (no power can answer them), unless a sign appears while one is open.
- Omen belief gain must depend on the witness (credulity, current belief): a doubter learns less.

**B2. Nudges add weight; they don't decide** (owner decision).
- A nudge adds a push to that option's value inside the decision the person is already making. Push size =
  base × `god:real` × (0.5 + credulity) (base may grow with favor spent — propose tiers).
- A believer on the fence follows; a believer firmly set the other way does not; a doubter barely moves.
  **No extra dice roll**: the decision's existing small noise is enough.
- Add a function the UI will use: `NudgeOutlook(person, fork, choice)` → how close the decision is and how
  likely the push is to tip it (e.g. "torn, likely to listen" / "set on it, a whisper won't do it").
- Following a nudge is a lesson: if it went well they feel guided (`god:real` up); badly, they may resent it.
- Report how often nudges are followed, by belief level.

**B3. Why does the kind bot lower population? Investigate; don't fix yet.**
"Kind" is the god's style, not the player's skill. A kind god *can* harm people (dependence: storing less,
praying instead of working, crowding after bounties), and a cruel one can help (the cruel bot cut gang thefts
1504 → 83 through fear). Those effects are fine **if they emerge and we can explain them**. A plain mistake
(e.g. offerings quietly destroying stored food) is not.
- With the kind bot, run ablations on both seed sets: `ANTHRO_OFF=offer`, `=rite`, `=pray`, bounty-only,
  weather-only.
- Report births, starvation, deaths by cause, camp moves, hours praying/offering, food offered per year,
  stored food going into winter.
- Classify the cause: **mistake** (to fix) or **emergent dependence** (to keep and explain).

**Batch B report:** `docs/reports/T9B.md`, then stop.

### Batch C — measure gods properly, first reputation powers (then STOP and report)

**C1. Judge gods on several dimensions, not population.** Population is not success (`PLANNING.md`:
"dimensions + milestones, no single score"). For each bot report: extinction risk (worlds that dip below 15),
health and starvation, violence and theft, discovery pace, cohesion (splits, rites), belief, and how culture
shifts (`norm:*`, `god:*` per people).

**C2. Add a "skilful" bot.** It acts at the right moments: weather before a hard winter, food when a camp is
genuinely starving, an omen right after a witnessed theft, blessings sparingly. This is the bot that should
improve the odds (owner's balance target: the god improves survival). Kind and cruel bots should produce
*different* peoples for reasons we can explain — not necessarily bigger ones.

**C3. Reputation-unlocked direct acts: Heal (kind) and Lightning (cruel).** (owner decision)
- Unlocked by a strong reputation (kind for Heal, cruel for Lightning) **and** enough believers to carry the
  story. Expensive; limited per generation like blessed births. Propose and disclose the numbers.
- Deterministic: if paid for, it happens (no dice). They are natural events (a fever breaks, lightning).
- Heal: ends one person's sickness or saves them from dying of wounds. Lightning: strikes one person.
- Both are the most **striking events** in the game and go through Credit (misinterpretation):
  - Lightning on someone witnesses remember as a wrongdoer → "the god punishes <their act>".
  - Lightning on someone they see as innocent (no remembered wrongs, well liked) → belief and believers fall
    (PLANNING: innocent deaths cost believers); `god:kind` drops sharply.
  - Healing a known killer → people may conclude the god favours killers.
- Tests: unlock thresholds, cost, the three interpretations above.

**Batch C report:** `docs/reports/T9C.md`, then stop.

---

## Report format (every batch)
1. **Checks:** each acceptance check with **PASS / FAIL** and the evidence (numbers or cmp output).
2. **Raw `ana.py` lines** for both seed sets, then a short table.
3. **What changed** (files) and **hand-set numbers** introduced, with why each value.
4. **Deviations** from this brief, and anything you chose not to do, with reasons.
5. **Surprises and open questions.** Keep it to about a page; Claude's review budget is small.
