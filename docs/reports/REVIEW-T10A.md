# Review of T10A (Claude, 2026-10-12)

**Verdict: mostly accepted.** A0 is a clean, honest correction. The relief learning and the timer-free offerings
are built as designed, with the right tests. The favor audit is the most useful analysis so far. Three gaps:

1. **Check 1 is 16/20, not 20/20.** The evidence says seeds 41-56 were compared. Run the cmp for 57-60 too.
   (The ana totals match exactly, so it will very likely pass — but a PASS means all 20.)
2. **"Idle well within noise" is overstated.** Set 2 total pop is −15.1% and starvation moves in opposite
   directions on the two sets. That's probably noise, but it's at the edge; say "borderline on set 2".
3. **The skilful bot's population drop got worse and isn't discussed:** 1,026 vs 1,327 (−23%) and 918 vs
   1,267 (−28%), both sets. That's the biggest result in the table and the report doesn't mention it.

## A4 (new, do before Batch B): owner decisions on offerings and favor pacing — implement
The owner decided both (2026-10-12), so A4 is now a change, not a measurement:

**A4a. Offered food is mostly eaten at a shared feast.** As in many real religions, an offering is a meal shared
with the god: most of the food is eaten by those present (it feeds them and bonds them like a gathering, reuse
`Gatherings.cs` bonding), and only a small part is burned or left for the god. Propose the burned share and
disclose it. Favor comes from what is given up (the burned share) weighted by belief, so big feasts still please
the god without destroying the camp's food.

**A4b. Favor pacing** for a faithful band of ~30 (owner-approved targets; all scale up with more believers):
| Kind of act | Examples | How often the god can afford it |
|---|---|---|
| small | omen, nudge | every week or two |
| medium | bounty | a few times a year |
| large | weather, heal, lightning, blessed birth | about once a year or rarer |
Set the favor cap and income rates (prayer, offering, rite) so a faithful band of ~30 hits that pace, instead of
tuning each number separately. Show the arithmetic in the report (income per year, cap, cost per act → acts per
year). Keep costs as they are unless the arithmetic can't work without changing them.

**Measure once, after both changes:** idle, kind and skilful on both sets: food given up per year, favor earned
and spent, days broke, acts per year by size vs the targets, births and population. Discuss every large result.

Then Batch B as written in `docs/T10-BRIEF.md`.
