# Review of M1 (Claude, 2026-10-13)

**Verdict: accepted — the best report so far.** The paired tool is right (per-seed log ratios, 40 worlds,
bootstrap CI), the data hygiene is done, and the re-analysis honestly overturns earlier headlines. The pacing
diagnosis is clear: income ~145 favor/yr vs ~300 target, the cap clips bursts, and the bot spends only 12% of what
it gets.

Two notes on reading the CIs:
- With ~25 metrics tested at 95%, one or two "REAL" results are expected by chance. Treat effects whose interval
  barely clears zero (e.g. idle pop −18%, CI to −1.1%) as **probable**, not settled, unless they form a coherent
  pattern with other metrics.
- Say which build each condition came from when results are pooled from different runs.

## M2 (next, before Batch C)

**M2a. Why is the idle world smaller?** Idle vs god-off: pop −18% (−33 to −1), births −15% (−26 to −4), techs
−21% (−34 to −5). Births and techs agree, so this looks real. With no god acting, what costs people? Compare idle
vs god-off on time use (hours per adult per day by activity, especially praying, offering, feasting), food given up
(escalated pleas, feasts, grave goods), births per woman and birth spacing, and deaths by cause. Measure only.

**M2b. Make the skilful bot an active player.** The bot stands in for a human, and a human spends what they have.
Remove the 15-favor cushion and the narrow windows (`Day % 4320`, late-autumn-only weather, 10-year heal timer in
the bot); keep only the game's own limits (costs, unlock rules, per-generation limits on blessed births / heal /
lightning). It should spend most of its income on whatever helps most right now (omen after theft, bounty in
hunger, weather before a hard season). Report spend as % of income.

**M2c. Belief growth.** Under every bot, mean `god:real` stays ~0.3 (kind 0.46), so no band becomes "faithful", and
the pacing target (written for a faithful band) never applies. With the active skilful bot, report belief over time
(Y1, 10, 20 … 50: mean `god:real`, share of believers > 0.5) and favor income as belief grows.

Run only: idle (reuse T10B), active skilful (new, both sets). Paired CIs vs godoff. Then **stop**: the owner decides
cap, income and how fast faith should grow from these results before Batch C.
