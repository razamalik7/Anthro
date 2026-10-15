# Review of T11 (Overseer / Argon, 2026-10-15)

**Verdict: partial accept — good measurement structure, but 5 real code/analysis bugs must be fixed before Batch C.**

The paired 40-world reporting format, the `quiet` experiment (active Y1–25, silent Y26–50), and the leaking favor bank (`D2`, $k = 4.0$, leak $2\%/\text{day}$) are well-structured. However, inspecting the code in `9377fb1` and the raw per-world distributions behind `T11.md` reveals five issues that explain why **Row 1 of the Owner Targets failed** (40% skeptics, 7.4% devout) and why `FAVOR_AUDIT` reported `1,555 favor/yr` alongside `78.5% broke days`.

---

## 1. Why Row 1 Failed & Why Favor Income is Bimodal (The Core Diagnosis)

`T11.md` §5 reports a 40-world mean of `Offerings: 1555.01 favor/yr` and `Broke days: 78.5%`. Both numbers cannot be typical at the same time. Inspecting the 40 `skilful` files individually shows an extreme **bimodal split**:
- **36 of 40 worlds** are stuck in Stage 1: offerings earn `< 1 favor/yr`, the god is broke **85–90% of days**, casts `< 1 omen/yr`, and ends Y25 with **0% devout** and **~45% skeptics**.
- **4 of 40 worlds** (`skilful_52`, `skilful2_65`, `skilful2_69`, `skilful2_77`) hit a runaway offering-feast loop, earning **8,750 to 25,123 favor/yr** and casting **60–70 omens/yr**.

Both halves of this bimodal split come from specific bugs in the T11 code:

### Bug A: Why 36/40 worlds starve in Stage 1
1. **`BotGod.cs:236` only blesses Camp 0 (`region.Bands.FirstOrDefault`) and kept the `Favor >= 15` cushion:**
   - Proactive omens are cast on `region.Bands.FirstOrDefault(b => b.Band.Members.Any())` — so **Camps 1 and 2 (two-thirds of the tribe) never receive proactive signs** unless Camp 0 dies out.
   - It also kept `region.Favor >= 15.0` (which M2b explicitly asked to remove).
2. **Thanksgiving (`D1`) is gated out at birth and uses a 7-day timer:**
   - `SeedBelief` seeds `god:thank` at `0.01` (65% of worlds) and `god:real` at `0.20`. In `Decider.cs:479`, `thankScore = 0.20 * 0.01 * 0.5 = 0.001`, which fails the hardcoded `if (thankScore > 0.02)` gate. In two-thirds of worlds, **nobody can ever give thanks**.
   - Worse, `Decider.cs:474` and `Credit.cs:78,89` added a `LastThanksDay` daily cooldown and a **hard 7-day window (`Clock.Day - witness.LastThanksDay <= 7`)**, violating Design Rule #1 (*no timers or cooldowns; we just removed the 14-day offering timer in T10 A3*).
   - And `Credit.cs:91` hardcoded `double surprise = regionWide ? 0.3 : 0.6;` on divine powers instead of weighting credit by how comfortable/surprising good fortune is. Meanwhile `FadeBelief` erodes `god:thank` by `0.005/day` unconditionally.
3. **Math of Stage 1 belief equilibrium:**
   - With 1 omen per 30 days on Camp 0 only (`+0.08 * (0.4 + 0.8 * cred) ≈ +0.064` to `god:real`) against daily fade (`0.001/day` toward `0.20`, i.e. `-0.03 * (real - 0.20)` per 30 days), Camp 0's `god:real` asymptotes to:
     $$0.20 + \frac{0.064}{0.03 + 0.064} \approx 0.41 \quad (\text{Open, never Believer } > 0.5)$$
   - Because almost no one crosses `IsBeliever (> 0.5)`, `isFaithfulBand` (`believers / adults >= 0.5` in `BotGod.cs:219`) **never triggers**, trapping 36/40 worlds in Stage 1 forever.

### Bug B: Why 4/40 worlds explode to 25,000 favor/yr
- In `Decider.cs:503–516`, once `god:offer` and `god:real` cross ~0.7 in a lucky world, Path 1 (`learned offering habit`) gives `offerScore = real * Felt(learned) * scale ≈ 0.85 * 1.6 * 2.5 = 3.4`.
- A Decider score of `3.4` beats foraging, hunting, socializing, and sleeping. Because `Offer` has no satiety/meal coupling and 85% of the offered food is eaten right back by the camp (`CampActivities.cs:289`), every adult in camp holds an `Offer` feast **every waking hour** until `DaysOfFood < 2.0`, generating 20,000+ favor/yr.

---

## 2. Three Other Issues Found in Audit

### Bug C: The Real Mechanical Root Cause of `D0` (Idle vs God-off Divergence)
`T11.md` §1 shows that in T10B `idle`, Y1–10 starvation was higher (`+54%`), reducing women 15–44 in Y25–50 (`-20.1%`). That explains why births fell given normal TFR, **but not why `idle` differed from `godoff` in Year 1 when `Pray` = 0.000 h/d and `Offer` = 0.4 rations/yr**.
Inspecting `SocialLearning.cs:24–33` reveals the actual bug:
```csharp
var topics = speaker.Expect.Topics;
int n = System.Math.Min(TopicsPerTalk, topics.Count);
weight *= (double)topics.Count / n;
if (weight > 0.3) weight = 0.3;
for (int i = 0; i < n; i++)
{
    var key = topics[rng.Range(0, topics.Count)];
```
1. **Main RNG desynchronization on Day 1 Hour 1:** `SocialLearning.CopyFrom` uses `sim.Rng` (the main world RNG), and `topics.Count` is larger when `SeedBelief` adds `god:*` keys. Thus `rng.Range(0, topics.Count)` changes the random stream on the very first conversation of Day 1! After Hour 1 of Day 1, `idle_41` and `godoff_41` do not even have the same weather, deer births, or wolf attacks.
2. **Dilution of secular social learning:** Because `weight` hits the `0.3` cap (`if (weight > 0.3) weight = 0.3`), adding 4–8 `god:*` topics into `Expect.Topics` steals ~25–35% of the `TopicsPerTalk = 6` conversation slots away from secular norms (`norm:rationing`, `norm:peacemaking`, `bound:*`) without increasing per-topic weight.

### Bug D: `devout` Reporting Band Omitted "and practising" (`D3`)
- Brief (`docs/T11-BRIEF.md:40`): `devout (> 0.8 and practising: prays/thanks/offers)`.
- `Anthro.Demog/Program.cs:92` only checked `Simulation.GodReal(m) >= 0.8` without checking whether the person practices (`OwnDeeds` or recent prayer/thanks/offering).

### Bug E: Mislabelled `Thanksgiving: 1.00 favor/yr` in `FAVOR_AUDIT`
- `Prayers.cs:374` adds thanksgiving favor into `FavorEarnedPrayer`, not `FavorEarnedOther`.
- `FavorEarnedOther` is initialized to `StartFavorPool = 50.0` (`God.cs:61`), so `50.0 / 50 years = 1.00/yr` in every run (`skilful`, `idle`, `quiet`). `T11.md` §5 misread `other:1.00` as thanksgiving income.

---

## 3. Fixes to Do Next (`T11B` — Clean Up Before Batch C)

1. **Fix `SocialLearning.CopyFrom` (fixes D0 & preserves true paired worlds):**
   - Keep `god:*` topics in a separate pass or draw their topic index from `Region.GodRng` (or sample up to `TopicsPerTalk` secular topics as before, plus 1–2 `god:` topics using `GodRng` scaled by speaker's devotion `0.3 + 1.2 * real`). That way:
     - Secular learning is never diluted by adding `god:*` keys;
     - `sim.Rng` stays synchronized with `godoff` until a person actually chooses a god action (`Pray`/`Offer`) or the god casts a power!
2. **Fix Everyday Devotion (`D1`) — No Timers, Continuous & Surprise-Weighted:**
   - Remove the 7-day timer (`Clock.Day - LastThanksDay <= 7`). Like prayers and offerings, track whether a person has given thanks since their last sign/blow (`p.HasPendingThanks` or `p.LastThanksDay > p.LastGodSignDay`), or let thanks be a natural mealtime/rest gratitude habit when fed (`p.Satiety >= 0.7`, once per meal/day as a body rhythm, not a 7-day credit clock).
   - Weight `god:thank` credit by genuine surprise of good fortune (e.g., `1.0 - campFedRoutine` or relief after hard times), so in long comfortable times surprise is small and `god:thank` naturally thins out toward a modest baseline instead of dying to zero on Day 30.
   - Track thanksgiving favor in its own `FavorEarnedThanks` field so `FAVOR_AUDIT` reports `pray`, `thank`, `offer`, `rite` accurately.
3. **Cap Runaway Hourly `Offer` Feasts (`A4a` / `D1`):**
   - An offering is a **shared feast** (`CampActivities.cs:285`). People do not hold feasts every hour of the day when their stomachs are already full (`P.Stomach > 0.9`) or when the camp just feasted today (`sim.Clock.Day == sim.LastOfferFeastDay` or when nobody in camp is hungry for a feast). Grounding `Offer` in actual meal/feast readiness prevents the `3.4` Decider score from turning every waking hour into an offering loop.
4. **Fix `BotGod.StepSkilful` Pacing (`D4`):**
   - Remove the `Favor >= 15.0` cushion on proactive signs (`Favor >= 5.0` is the cost of an Omen).
   - Rotate or pick the camp that has gone longest without a sign (`LastGodSignDay`), instead of `Bands.FirstOrDefault()` (Camp 0 only).
   - Scale proactive omen interval smoothly with the camp's mean belief / believer share (from 30 days at `meanReal <= 0.25` down to 7 days at `meanReal >= 0.60`), rather than a hard binary cliff at `believers / adults >= 0.5`.
5. **Fix `devout` Band Check (`D3`):**
   - In `Program.cs`, count `devout` as `GodReal(m) >= 0.8` **and** having practiced (prayed, thanked, or offered in the last year).
   - In `favor_audit.py` and `t11_analysis.py`, report median + IQR (or bootstrap CI) alongside mean so bimodal outliers are immediately visible.
