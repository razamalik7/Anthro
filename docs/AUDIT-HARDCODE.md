# Hard-coding audit (first pass, 2026-10-06)

What in the sim is authored rather than emergent or grounded in data, sorted by how much it
matters for the design rule "structural, organic, emergent; real data; disclose hand-set numbers".

Categories:
- **A. Calibration fitted to real data** — fine, disclosed.
- **B. Physical / biological facts from data** — fine.
- **C. Authored behavioural weights** — the "species psychology": how strongly each kind of
  pull counts. Unavoidable in a utility model, but each is a candidate for becoming learned.
- **D. Calendar / schedule rules** — things that happen on a set day or season rather than
  from conditions.
- **E. Hard gates and fixed dice** — "if X then Y" shortcuts and fixed-chance rolls.
- **F. Knowledge leaks** — people deciding from the true state of the world, not what they know.
- **G. Design rules** — deliberate game constraints, labelled as such.

Priority to fix: **F > D > E > C**. A and B stay; G stays by design.

---

## F. Knowledge leaks (people know what they couldn't)

| Where | What leaks | Fix |
|---|---|---|
| `Region.CampScore` / `FindCampSite` (used when a group splits or strikes out) | New camp sites are picked from the true plant stock and true positions of every herd within 5 km. | **FIXED**: `Simulation.ChooseCampSite` scores places the leader knows (remembered food, recent game, known camps avoided). Only when they know nothing in range does the old true-map pick remain (heading into unknown country) — small remaining leak. |
| `Simulation.RefreshForagePatches` → `FishSpots`, `WoodSources`, `ForagePatches` | Built from the true state of cells near camp. `Decider` gates fishing on `FishSpots.Count > 0`; `GatherWood` falls back to `WoodSources`. | **FIXED**: fishing gated on `MentalMap.KnowsFishing`; wood with nothing known → nearest woods visible from camp. (`ForagePatches`/`FishSpots` still used for band-overlap in `Region.Encounters` — a world-level check, fine.) |
| `HuntParty.ChooseTarget` | Drops sighted herds that have since died or left (via `Ecology.ById`). | Keep stale leads; the trail goes cold on arrival (already handled by tracking). |
| `Inventing.RefreshMaterials` (`_here`: "wolves", "game") | Reads true animal positions within 5 km of camp. | **FIXED**: from camp members' sightings in the last 14 days. |
| `Dogs.WolvesNear` (taking in pups) | True wolf positions. | **FIXED**: wolves someone in camp has seen near it in the last 30 days. |
| `Society.SpareFor` / ask & take choices | True contents of other hearths. | Defensible (a camp sees what's at each fire); keep, maybe add noise. |
| `Social.SeekCompany` | Own people's camps always "known". | Defensible (one knows roughly where one's people range); keep, disclosed. |

## D. Calendar / schedule rules

| Where | Rule | Verdict |
|---|---|---|
| `Cell.DailyRegrow` fish runs | Spring days 10-40, autumn 20-50. | Should follow water temperature / day length (rivers warm). Medium priority. |
| `Cell.SeasonFactor` per plant | Fixed seasonal availability table (greens peak spring, nuts autumn). | Partly grounded (phenology); now modulated by local warmth and rain. Could become temperature-driven phenology. |
| `Cell.DailyRegrow` autumn/winter floor | Nuts/grain/berries ripened over summer don't vanish on a cold autumn day. | Acceptable structural rule. |
| `EcologySystem` `BirthDay` (day 50) and `RutDay` | Animal births and rut on fixed days. | Real species are photoperiod-timed, so fixed dates are close to real; acceptable. |
| `Dogs` pups day 60 | Dog litters and taking pups on a fixed day. | Same as above; acceptable. |
| `Simulation.ReconsiderMarriages` | Each mid-season. | A check cadence, not an event; acceptable. Could be triggered by events (a failed provisioning, a fight). |
| `Infection` | Colds ×3 in winter, fevers ×2 in summer by season. | **FIXED**: colds ×(1 + 2×cold, from camp temperature); insect fevers ×T/10 (0.2-2). |
| `Hunt` tactics | Fire drives not in spring; persistence hunting best in summer. | Persistence **FIXED** (follows heat). Fire drive still "not in spring" — should follow dryness. Low. |
| `Inventing.Pressure` "winter", "dark" | Seasonal pressures for discoveries. | **FIXED**: from camp temperature and day length. |
| `SimClock` sunrise/sunset by season | Step changes. | **FIXED**: smooth day length (8-16 h, ~45° latitude). |
| `HearthActivities` fuel burn by season | Winter 2.0, summer 0.3. | **FIXED**: 0.8 + 0.07 per degree below 20 °C. |

## E. Hard gates and fixed dice

| Where | Gate / dice | Verdict |
|---|---|---|
| `Simulation.WolvesNearCamp` | 25% daily attack roll when a starving pack is near and strong enough. | **FIXED**: daily chance comes from the pack's state — hunger past ~6 days × how its strength compares with the camp's (people, torches, dogs); zero against a camp it can't match. |
| `Family.Jealousy` | 15% chance the partner notices. | Should be perception: partner present and awake and watching. Low. |
| `JointActs` rally clash | 50/50 who strikes first. | Use initiative (boldness, temper). Low. |
| `Region.Create` founders know other camps | 60% chance each. | Only matters with `otherBands`; fine. |
| `Hunt` fever from butchering | 0.4% per kill. | Grounded-ish (zoonoses); fine. |
| `EcologySystem` herd re-choosing ground | 5%/day random. | Fine (animal wandering). |
| `Dogs` taking pups | 30%/year when conditions hold. | **FIXED**: chance = the keenest knower's interest in animals × food to spare (and wolves seen near camp). |
| Maturity gates in `Decider` | Forage ≥0.2, fish ≥0.3, trap ≥0.4, ask ≥0.6, confront ≥0.7, raid/rally ≥0.8. | Grounded in child development roughly; acceptable but authored. |
| `Decider` ask/take | Only when hunger > 0.3 and hearth < 2 days. | Authored threshold; could be the value of asking vs other options always. Low. |
| `Tribe.ConsiderSchisms` | Rift ≥ 1, then 10%/yr scaled. | Authored (added tonight). Should become a decision by the camp's people. Medium. |
| `Social.ConsiderSplitting` | Discontent ≥ 0.3, proposal at most once a season, a year before a newcomer may leave again. | Authored thresholds; acceptable structure. |

## C. Authored behavioural weights (the biggest bucket)

Concentrated in `Decider.Options`. Each option's score is `drive × learned yield × (interest/trait pulls)`;
the learned yields are emergent, the shape and constants are authored:
- Base appeals: Rest 0.05; Socialize 0.2 + 0.5×sociability (+0.7 at dusk); Play 1−maturity;
  Care 0.5 + …; WatchAndLearn 0.5 + 0.6×curiosity; Knap toolNeed×1.3; GatherWood ×1.4.
- Food work: base pull 0.25×room; hunger ×1.5; meat craving ×0.7 / ×0.5; Foresight ×1 (added tonight,
  made uniform after the user's question); yield normalised by 0.4 rations/h.
- Hunger thresholds: 6 days of food = no hunger; fresh food room 6 days.
- Drive weighting per verb (hunting 0.6 food + 0.4 standing, etc.).
- Hunger dampens non-essential options by 70%.
- Choice noise sd 0.15.
- Social: anger heat a/(a+3), temper ×1.5, guilt ×0.5, fear ×0.3, take ×0.5×(1.3−0.6 warmth).
- Splitting: Needed adults = 2 + 6×(1−boldness); overwork 7-11 h; crowding n/60; loneliness +2
  (added tonight).
- Dogs: 0.3 rations/dog/day (grounded), scraps = 50% of spoilage + 0.05/person (authored).
- Discovery spread (tonight): talk +0.15, watching +0.35, talk ceiling 40%, "heard of it" ×(1+5p).
- Breakups (tonight): provider gap ×0.8, childless ×0.5.
- Fecundity: workload −5.5%/h above 4 h (direction grounded in Jasienska & Ellison, size authored).
- Ecology (tonight): herd Need 0.45, dormant forage 0.3, condition rate 2%/day, starving below 0.25,
  wolf scent range 4 km, wolf chases ×3/day.

**Direction:** the cleanest path is to turn base appeals into learned satisfaction (people learn
what an hour of talk, rest or play does for them, like food yields), leaving only innate drives.

## A / B. Calibrations and grounded facts (kept, listed for disclosure)

- `ChestRisk` 0.003, `WeaningRisk` 4 (child mortality fit; lowered tonight once hunger killed
  children naturally), `FeverRate` 3e-4, `AccidentRate` 2.5e-6/h (fit to 1.5-2.5 accident deaths
  per 1,000/yr), `DiscoveryRate` 6e-7 (pacing — user's call).
- Coale-Trussell fertility schedule; secondary sterility 2.5%/birth; lactational suppression
  3-5 years by maternal fat.
- Climate: 6.5 °C/km lapse; elevation unit = 500 m (authored); snow 0.6 cm/day, 1% settling,
  1.5 cm/°C melt; rain AR(1) sd 0.1/0.15; mast AR(1) −0.4, sd 0.6.
- Species densities, birth/death rates (field data); hare meat 1 ration; wolf 2.2 rations/day.
- Spoilage rates; MeatKeepsDays 4/20/60; carry capacity 12 rations; inner bark 0.12/h, 10/cell.

## G. Design rules

- `Region.MaxTribes = 5` peoples (manageability and speed).
- One starting people (user's choice: one camp, or with `kinCamps`).

---

## Next steps for the audit
1. Line-by-line pass over `Simulation.cs`, `Society.cs`, `Social.cs`, `Hunt.cs` for remaining E items.
2. Fix the F leaks (camp sites, fish/wood spots, materials, pups) — mechanical and high value.
3. Replace the season switches in D with the conditions they stand for (temperature, day length).

## Additions 2026-10-07 (self-audit of the new systems)

Fixed:
- Fight length capped at 3 exchanges → fights now end of themselves (exhaustion feeds fear, wounds,
  yielding, routs, peacemakers). The loop limit of 40 is only a technical guard; fights end well before it.
- Gang-theft cooldown (7 days) → removed. A successful taking spends the anger behind it; failed rallies
  teach that rallying doesn't work.
- Feasts on a fixed day each season → a wish to gather that grows over time (faster with ritual) and is
  acted on when there's food to feast: gatherings follow plenty.
- Fish runs on fixed calendar days → by river warmth.
- Brawls by fixed onlooker rule → a people's custom of taking sides (`norm:sides`) and of duels (`norm:duel`).
- Marriage reviews on fixed days → event-driven (a hungry season; a birth in camp for long-childless couples; births and hunger in plural households).
- Restlessness from crowding only → from any discontent.
- Elders' voice: now learned from how the moves the old pushed turned out.
- Rationing: learned two-way from seasons the family actually budgeted; keeping food rots slower in the cold (Q10 ≈ 2, hand-set).

Still open:
- Split thresholds (overwork 7 h, crowding scale 60): hand-set, grounded in forager work hours and band
  fission sizes (~50-60); could follow social technology later.
- Hunting prestige: cultural only (a share-of-food signal was self-reinforcing and was removed).
- Weir build rate (~12 person-hours), decay (~1 year unmaintained): hand-set world facts.
- Badly-wounded threshold (0.6) for being unable to fight: a body fact, hand-set.

## Additions 2026-10-18 (T11C / T10C — God Layer Calibration & Pattern Bots)

Fixed / Emergent Replacements:
- Removed day-counter timers and gags (`LastGodSignDay >= 0`, `DailyThanksCount`, `LastOfferFeastDay`) in favor of continuous personal and camp urges (`ThanksUrge`, `OfferUrge`) and learned expectation returns.
- Removed artificial `0.45` surprise floor in `Credit.cs` and artificial `0.55` ceiling on belief inheritance.
- Replaced hard belief clamp `TemperamentSignTarget` with smooth, convex credulity-weighted receptivity: `Pow(Clamp((cred - 0.12) / 0.75, 0.05, 1.5), 1.6)`.
- Restored genuine physical costs for grave goods (`burial.goods` in `Kill`).
- Separated scalar `Reputation` into 2D independent dimensions `GodHelp` and `GodHarm`.

Hand-set Numbers (disclosed):
- `Belief.cs`: `SeedBelief` `baseReal` in `[0.14, 0.30]` (individual `[0.08, 0.35]`), `baseAnswers` `[0.02, 0.18]`, `baseThank` `[0.12, 0.22]`; `FadeBelief` `restReal = Clamp(0.06 + 0.32 * cred, 0.06, 0.34)`, `fadeRate = 0.00030 * Pow(Clamp((1.15 - cred) / 0.65, 0.35, 1.9), 1.3)`; daily `ThanksUrge += 0.05 * real`.
- `Decider.cs`: Thanksgiving trigger `ThanksUrge >= 1.0` (score `+0.12 + 1.8 * pull`); offering trigger `sim.OfferUrge >= 1.0` (or fear offering under cruel gods `kind < -0.3`).
- `CampActivities.cs` / `Prayers.cs`: Thanksgiving favor `0.50 * real`; crisis plea favor `1.5 * real`; offering favor `burned * real * 8.0 * (1.0 + |kind| * 1.5)`.
- `God.cs`: `SustainableFavor = Math.Max(50.0, People.Sum(GodReal) * 4.0)`, `FavorLeakRate = 0.02/day`. Heal unlock `GodHelp >= 4.0`, Lightning unlock `GodHarm >= 4.0`. Max discount factor `0.40`.
- `BotGod.cs`: Pattern bots `Charity` (give-reward interval 8d), `Demanding` (offering-check 10d, lapse penalty 35d), `Malevolent` (anger cycle 18d), `Stern` (wrongdoer check 8d), `Capricious` (random act interval 12 ± 6d).

