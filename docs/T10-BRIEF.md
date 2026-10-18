# T10 brief — learning from relief, where offering comes from, gods as patterns

For: the implementing agent (Gemini). From: Claude and the owner. Date: 2026-10-12.
Read first: `AGENTS.md`, `docs/reports/REVIEW-T9C.md`, and the "Decisions 2026-10-12" section at the end of
`docs/GOD-LAYER.md`. Branch from `task/T9C`. Don't merge to master.

Same standard as T9B2 (your best report): both seed sets for every comparison, raw lines, numbers next to every
PASS/FAIL, NOT MEASURED where you have no numbers. **New rule from the T9C review: every claim quotes both seed
sets. If one set shows an effect and the other doesn't, say exactly that. A drop is a drop; never describe it as
"without ballooning".**

Three batches. Stop after each and write the report.

---

## Batch A — measure the skilful bot, learn from relief, no offering timer

**A0. Correct the T9C record.** Add a short section to `docs/reports/T9C.md` headed "Correction (T10)" stating
plainly: the skilful bot lowered population on both sets (−11%, −13%), raised gang thefts 3× on set 61-80, cut
starvation only on set 41-60, and 0/40 vs 1/40 dips is not evidence.

**A1. Favor audit (measure only, change nothing).** For each bot (none, kind, cruel, skilful) on both sets:
favor earned per year by source (prayer, offering, rite, other), favor spent per year by power, the share of
days the god had too little favor to act, and when in the run it was broke. Answer: is the skilful bot short of
favor (economy too tight for a god to matter) or does it have favor and spend it badly (bot problem)?

**A2. Learning from relief** (the missing kind of learning). Today people only learn "something good followed
X, so the god likes X". Add the other half: "something bad **stopped** after X, so X appeases it".
- After a striking bad event (a wrathful omen, harsh weather, lightning, a death read as punishment), witnesses
  who believe carry **dread**: a per-person feeling that fades on its own gradually (no timer, a continuous
  decay), faster if nothing else bad happens.
- What they do while in dread (offer, pray, give, refrain from taking) is remembered as their attempt to appease.
- If dread fades without another blow, they credit that attempt in proportion to the relief (the bigger the
  dread that lifted, the stronger the lesson): `god:<act>` up.
- If another blow comes despite it, the attempt failed: `god:<act>` down.
- Weight by belief and credulity, like the rest of Credit. Switch: `relief`.
- This is how a malevolent god earns fear offerings: it strikes, then holds back after offerings.

**A3. Offerings work like prayers: no 14-day window.** An offering stays "open" until a sign comes (good or bad)
or the person's hope fades: each day without a sign, its expected return erodes a little. Remove the 14-day
window from `FadeBelief`.

**Acceptance:** god-off still byte-identical to base on 41-60 (cmp). Idle world within noise of god-off on both
sets. Tests: (blow → offering → no more blows → `god:offer` rises); (blow → offering → another blow →
`god:offer` falls); offerings with no sign at all fade without any day count.

Report `docs/reports/T10A.md`, then **stop**.

---

## Batch B — where offering comes from (owner decision)

**B1. Sharing teaches generosity, not offering.** In `Credit.cs`, a sign after `Act.Give` (sharing food with
people) teaches `god:give` ("the god likes generosity"), no longer `god:offer`.

**B2. Offering as an escalated plea (the main path).** When a prayer is open, the need is desperate and the
plea is going unanswered, the person may add a gift (food, a tool) to the plea. Its value comes from desperation
× belief, not from a learned return: it's an improvisation, bargaining with the unseen. If relief follows (even
by chance), `god:offer` is learned through the normal outcome learning. Disclose any number you introduce.

**B3. Grave goods.** Peoples who know `burial.rites` leave something with their dead (a small amount of food or
a tool). That's a witnessed gift to the unseen: if good things follow, it can teach `god:offer` like any other
offering.

**B4. A small cultural starting point.** Seed `god:offer` per people with small variation (some start with a
weak offering habit, most with none), the way customs are seeded in `Customs.SeedCustoms`. From `GodRng`.

**Acceptance:** god-off still byte-identical. Idle world within noise. Report: in how many worlds offering
appears, by which path (plea, grave goods, seed), and whether it fades when nothing answers.

Report `docs/reports/T10B.md`, then **stop**.

---

## Batch C — gods as patterns, not labels

The owner's decision: kind and cruel aren't good and bad signs. A god's character is **what it responds to and
how**; people learn those patterns (truly or falsely), and that is their religion. Neither character means
success; population is not the score.

**C1. Two separate measures.**
- **Help vs harm**: what the god actually did to people (deaths caused, sickness healed, food given, seasons
  softened or harshened). This replaces `Reputation` for unlocking Heal and Lightning.
- **What people believe the god wants**: the `god:<act>` profile per people (`take`, `give`, `attack`,
  `offer`, …). Report it per people.

**C2. Rebuild the bots as patterns.**
| Bot | Pattern | Expected belief (if it works) |
|---|---|---|
| charity | rewards witnessed sharing with bounties and good signs | the god loves generosity |
| demanding | rewards offerings, sends hardship when offerings lapse | the god demands gifts |
| malevolent | sends destruction that eases after offerings | offerings hold back its anger |
| stern | strikes witnessed theft and violence (omens, lightning when unlocked) | the god punishes wrongdoing |
| capricious | acts at random | shifting superstitions, doubt |
Keep `skilful` as the "good player" reference, adjusted using what A1 found.

**C3. Compare all of them** on both sets: population, starvation, dips below 15, violence, thefts, discovery,
belief, the `god:<act>` profile, favor earned and spent. For each bot, say whether people came to believe what
the pattern should teach, and classify every surprising outcome as explainable or a mistake. Don't tune toward
population.

Report `docs/reports/T10C.md`, then **stop**.
