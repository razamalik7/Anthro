# Overnight progress log

Work agreed before you went to sleep, in order:
1. Performance + bug fixes (one-way partner moves, party size not sharing risk)
2. Valuations from traits/situation/culture, culture norms, natural meal & hunting rhythms
3. Disease rework (distinct diseases, spread through real contact)
4. Discovery system, including the Healing skill

Nothing is committed to git (you haven't asked me to). Design calls I made on
my own are marked **[call]** so you can push back.

---

## Step 1: performance and fixes ✅

### Performance
- Added an opt-in profiler: `dotnet run --project Anthro.Runner -- --profile`.
- Found the two hot spots: everyone re-checking whether to move camp every day
  over their entire memory of places, and socializing copying all beliefs every hour.
- Fixes that are also better behavior:
  - People only think about moving camp when **disappointed**: a foraging trip
    well below what they'd come to expect makes them restless.
  - Place memories are kept **per season**, so only the relevant ones are searched.
  - Beliefs are exchanged **once per conversation**, not every hour of it.
  - Exploring picks a random far spot instead of scanning the whole area.
- Result: ~20-35 s per 100 years at 30-90 people (was 130-450 s). The target is
  still <10 s; more optimization is pending.

### One-way partner moves (fixed)
- Neighbor bands are now built as real families (parents, children, couples)
  with bonds among people who grew up together. The founding band got the same
  fix: before, founders had no friendships outside their own families.
- **Ties count only a person's closest ~8 bonds**, weighted: own children ×3,
  other kin ×2, friends ×1. Before, ties summed over the whole band, so a big
  band always won.
- Neighbors' bonds are calibrated each year so a typical neighbor is as tied to
  their people as a typical one of ours (their daily lives aren't simulated, so
  their friendships can't deepen on their own).
- People also weigh **how well-off each band looks**. A hungry band is less
  appealing to marry into.
- **A residence custom forms from what couples actually do** (does the woman
  usually join the man's band, or the reverse). **[call]** It starts neutral.
- Result: partners now move both ways (e.g. 14 joined / 6 left; 4 / 9; 17 / 8).

### Party size and risk (fixed)
- A charging animal goes for 1 person (2 for mammoths), usually the boldest
  near the front. Everyone else gets a strike in as it comes, and enough hits
  drop it before it reaches anyone.
- People's danger belief is now "how likely someone gets hurt going after
  this animal"; their **personal** risk is that divided by party size. So "only
  take on mammoths in a big group" is something people learn and act on.

### Band splitting (new, structural) **[call]**
Bands were swelling to 130-165 people. Real forager bands split when they get
too big. Now:
- A restless adult whose idea to move camp fails may propose **leaving to find
  new land**, if the band has had a **long hard stretch** (hunger smoothed over
  about a season) or the camp has grown **so big most of it is near-strangers**
  (social strain, a documented cause of real band splits).
- Starting from their own family, the people closest to them each weigh relief
  against the bonds they'd leave behind. Children go with parents.
- At least 12 must go and 12 must stay (a tiny group can't make it alone, and
  people know it). Someone turned down doesn't raise it again for a season.
- **Those who leave become a neighbor band** with their relationships intact.
  They can visit later, and partners can move between them.
- Result: populations cycle between ~25 and ~90 instead of exploding.

### Also
- Added **Healing** as the 11th interest (pulls toward caring for the sick;
  grows when tending the sick or injured).

### Still open from step 1
- **Mammoth deaths** are still high (20-45 per century). It's emergent:
  bold, prestige-hungry hunters plus deferential parties. It will shift once
  valuations (step 2) make the felt value of life personal, especially for
  parents of small children. Revisit after step 2.
- Performance target (<10 s per 100 years) not met yet.

Tests: 9/9 passing.

---

## Step 2: valuations, culture norms, natural rhythms ✅

### Eating is need-driven (no meal times)
- Each person has a **stomach** (food in the belly, in days of their own
  need) that empties hour by hour, faster when working hard (hunting burns
  ~2× sleeping).
- **Nourishment** (shown as "fed" in the yearly line) tracks how much of the
  last couple of days they had food in them. Health, immunity, fertility, and
  how hungry the band looks all use it.
- People **eat when hungry and at camp**. In shortage they hold back to a fair
  share, by their warmth and their band's sharing norm.
- **Foragers snack** on what they find; **hunters eat their fill at the kill**
  and carry the rest home.
- Infants live on nursing (mother, or another nursing woman).
- Not modeled yet: **a shared-meal custom** (eating together at the fire). Right
  now everyone eats on their own hunger. **[call]** Left for later; it fits
  with ritual/custom work.

### Hunting cadence comes from the body
- The "2 days between hunts" rule is gone. People hunt when they need meat,
  aren't exhausted, injured, or sick, and there's daylight for it.

### Culture norms **[call: a starting set of two]**
- Norms are beliefs each person holds, spread by talk and picked up by children
  watching adults (children now absorb the beliefs of those they learn from).
- **valor** (cautious ↔ daring): learned from how risky hunts turn out. A clean
  kill of something dangerous says daring pays; someone hurt says it doesn't;
  failing without harm is neutral. A death to an animal pushes hard toward
  caution, most for those who loved the dead.
- **sharing**: strengthens in hard times and after starvation deaths, loosens in
  easy times.
- Every band (ours and each neighbor) starts with its own shade of these.
  Partners from other bands bring theirs: cultural diffusion.
- Results: bands drift differently. Sharing ranged 0.40 to 0.93 across seeds
  depending on history; valor stays moderate.
- I first had valor and caution as separate norms that only ever rose, so they
  saturated near 1.0. Merging them into one axis with evidence both ways fixed it.

### Personal valuations (no global constants in risk decisions)
- **Injury cost**: lower for the resilient, higher in a cautious culture.
- **Value of own life**: much higher for parents of children under 10; lower
  for the bold, in a daring culture, and past 50.
- **Glory pull**: boldness × the band's valor.
- Danger beliefs are now pure beliefs ("how likely someone gets hurt"); all the
  personal and cultural weighting happens in the costs.

### Fixes along the way
- Crash when a neighbor band dwindled to nobody: such bands now vanish from the
  region ("The X band is no more").
- Founders started with ~2 days of food, which caused artificial hunger splits
  in year 0. They now start with a few days' stores.
- "Strangers" for crowding = people one barely knows (affinity < 0.1).

### Results (6 seeds × 100 years)
- Mammoth deaths ~10 per century (was 20-45). Mammoth kills still happen, mostly by
  big parties.
- Splits: 0-3 per century. One band of six died out (year 46). That's within
  "survivable but not safe".
- **Winter hunger is now the main pressure** (~20 starvation deaths per century),
  and it suppresses fertility in hard winters. That's the pressure
  drying/storage discoveries are meant to answer.
- Speed: 7-17 s per 100 years.

Tests: 9/9 passing.

## Step 3: disease rework 🟡 IN PROGRESS (stopped here: usage limit)

### Built
- **Distinct diseases** (`Anthro.Sim/People/Disease.cs`): cold, fever, the flux,
  wound rot, bad meat, lung sickness. Each has contagiousness, growth, harm,
  immunity after recovery, and extra danger for children.
- **Spread only through real contact** (`Simulation.Contact`): conversations,
  caring for the sick (healers get exposed), hunting together, families
  sleeping together.
- **Sources:** the flux from **camp filth** (builds with people and days in one
  place, resets when the band moves); bad meat; wound rot from injuries; lung
  sickness as a complication when someone already sick is chilled (worst for
  the old); colds and fevers arrive from **neighbor outbreaks** via visitors,
  plus a small chance of fever from butchering.
- Immunity is per disease and fades at each disease's own rate.
- A fouled camp lowers how good staying feels, and catching the flux makes
  people restless, so filth drives camp moves.

### Open problem at the stop point
- First calibration had the flux killing everyone (604 deaths across 6 seeds).
  Recalibrated to 17.
- But **starvation (399) and cold (190) deaths jumped ~3×** compared with the end
  of step 2. Most likely cause: sick people rest instead of foraging, and
  families sleeping together spreads illness through households, so food
  production drops. Seed 2 shows nourishment falling to ~62% by year 40-50
  before a crash. **Next step: confirm with the profiler and families view,
  then fix structurally** (e.g., whether sick people still eat from stores
  fairly, and how much illness actually cuts work).
- Colds and fevers caused almost no deaths, so neighbor outbreaks may be too rare.
- **Tests were not re-run after step 3.** Run `dotnet test` first.

## Not started
- Step 4: the discovery system (experiment, accidents, teaching and losing
  knowledge, the Healing skill in the web).

## Mortality realism pass (after step 3)

- Fair recount: the step-2 vs step-3 "tripling" was mostly a counting error
  (deaths under 5 had been left out of the step-2 count).
- **Deaths now show their chain:** "died of fever at 3, weakened by hunger and
  cold". Each person keeps a fading ~4-week ledger of what's harming them; the
  biggest contributor is the cause, the others are "weakened by".
- Cold and hunger now work mostly **through immunity**. Direct freezing only in
  severe cold; direct starvation only when body reserves are gone.
- **Body reserves (fat):** burned when the belly is empty, built when food is
  plentiful (autumn fattening). Nourishment = reserves.
- **Buffered lactation:** milk only fails when the mother is badly starved.
- **Parents feed all their children under 14,** and parents with hungry kids
  eat less in the field and carry food home.
- **Huddling:** sleeping with family cuts cold stress.
- **Endemic illness:** colds (worse in winter, worse for small children),
  childhood gut infections, warm-season fevers.
- **Immunity costs energy** (stronger immunity = slightly higher food needs), so
  selection on immunity has a trade-off.
- **Food work is weighed by learned yield per hour.** People had been hunting 2× more
  than foraging at 1/10 the return.
- Hungry people socialize less; food eaten in the field counts toward foraging yield.
- **Gathering rate** raised to a realistic range, and now **density-dependent**
  (Holling type II): thin patches, leftovers and winter all yield less per hour;
  nuts and grain are richer than greens.
- **Winter foresight:** people remember the worst of each winter (strongly after
  a famine), the memory spreads, and in late summer/autumn it drives stockpiling
  of nuts and grain sized to the winter they expect.
- Debug views (opt-in): `--families`, `--kids`, `--profile`; environment
  variables `ANTHRO_DEBUG_DEATHS=1` and `ANTHRO_DEBUG_SEASONS=1`.

### Finding that needs a design decision
The valley has an **emergent winter carrying capacity of ~15-25 people** with
the starting knowledge. Bands founded at 30 get cut down by early-spring
famines, then hold steady. Starvation is still ~95% of attributed deaths:
the famines are real (camp food at zero, everyone's reserves gone).
That's the intended pressure for storage/drying discoveries, but it
contradicts real forager death patterns (illness ~70%) and may feel harsh.
See chat for options.

Tests: 9/9 passing.

## Real-scale world (approved: real-size range, 200 m ecology cells)
- Map is now 100×100 cells at 200 m = **20 km × 20 km**, the band's real home
  range. Every distance in the sim is written in meters via
  `WorldMap.FromMeters(...)`: gatherers range ~4 km, explorers ~7 km, hunters
  ~10 km, deer move ~3 km/day, neighbors "visit" within ~3 km.
- Plant food per cell is a single calibration knob: `WorldGen.PlantDensity`.
- **When hungry, everything except food work, eating, rest, care and talk
  fades** (people were knapping and fetching flint while starving).
- Result: bands settle at ~20-27 people after early famines. Starvation is
  still the main attributed cause. The binding limits are the **late-spring
  hungry gap** and **spoilage** (20-30% of gathered food rots), which is
  exactly what cooking, drying and storage address. **Stopped tuning here to
  build those.**
- Open oddity: starvation deaths are now mostly adults 15-49; parents may be
  sacrificing too much for their children. Check after cooking and storage.

Tests: 9/9 passing.

## Daylight, body maturity, household sharing, need-bent limits
- **Sleep follows seasonal daylight:** sunrise/sunset by season (summer
  5:00-21:00, winter 8:00-16:00). People sit up ~3 hours by the fire after dark,
  sleep until sunrise, and only start a trip with 6+ hours of light left. Hunts
  end at dark.
- **No age gates:** capability comes from a growth curve (full by ~16) times
  strength (hunting) or dexterity (knapping). Play fades as children grow.
- **Hunting limits bend with desperation:** a starving band sends out people
  who are hurt, sick or tired.
- **Household sharing:** in hard times a parent takes about a fair share per
  household mouth, feeds hungry children first from it (warm parents give
  nearly all of it), and eats what's left.
- Result: 0% of deaths are children 5-14 now, but adults 15-49 are 68% of
  (mostly starvation) deaths. Children self-serve from the shared store all day
  while adults are out working, so adults come home to nothing. Proposed fix:
  household food ownership (see chat).

Tests: 9/9 passing.

## Social fabric (first version)
Code: `Anthro.Sim/Social.cs` (part of Simulation), `People/Household.cs`.
- **Households own food.** Gathered food goes to the gatherer's hearth. Meat
  is split between the hunters' households (killer double) and everyone else,
  by the band's sharing norm; receivers trust and like the hunters more.
  Families eat from their own store; hungry children of the household are fed
  first (warm adults give nearly everything).
- **Orphans are taken in:** grandparents, grown siblings, aunts/uncles first,
  else the warmest adult fondest of them.
- **Asking for food** (once a day at most): the giver weighs warmth, sharing
  norm, fondness, trust, kinship and what they can spare. Giving builds trust;
  refusal stings in proportion to what they could have spared.
- **Trust** is separate from fondness. Both fade toward neutral without contact
  (fondness has a higher floor for family). Trust shapes hunt recruiting,
  bonds and ties.
- **Theft** (a decision at most once a day): real desperation (reserves) × low
  warmth × weak sharing norm × nerve, **deterred by how busy the camp is**. If
  seen: victims' trust and fondness collapse, the thief loses standing, and
  hot tempers can lead to a **fight** (loser hurt, rarely killed; families take
  sides).
- **Failed hunts in hard times** dent trust in whoever led them.
- **Discontent** builds slowly (over a season or more) from hardship, crowding
  and distrust. Only lasting discontent leads to proposals to leave.
- **Splits are dynamic:** households decide whole; each adult judges whether
  the leaving group is big enough for them (the bold accept smaller groups). A
  bold person can **strike out** with just their family to a neighbor band.
- **Returning:** after a year or more away, people come back if they're more
  bound to the band than to their new one (bonds count for less with people
  they distrust). Neighbor bands' members build familiarity over time too.

### Bugs found and fixed while building
- Theft keyed to an empty stomach (fired at every meal): crime waves and
  band collapse. Now keyed to reserves and rate-limited.
- Return loop: people left and came straight back. Fixed with bonds in new bands,
  trust-weighted ties, and time away before returning.
- Departing families' children were briefly "orphaned" and adopted.

### Results (6 seeds × 80 years)
- Splits 0-4, strike-outs 0-17, returns 3-34, caught thefts ~1-2 a year,
  fights ~0.2-0.4 a year, no fight deaths in this batch.
- **Still open:** starvation remains ~93% of attributed deaths, mostly adults;
  2 of 6 bands died out. The social layer distributes scarcity but doesn't
  create food. Cooking, drying and storage are next.

Tests: 9/9 passing.

## Strife v2: relationship-driven theft, severity, peace norm, raids
- Theft victims chosen by relationship: never kin, rarely people the thief is
  fond of, most readily a distrusted family with plenty (especially one that
  refused them). Propensity also scales with resentment and the peace norm.
- **Severity** of a caught theft = amount vs the family's needs × how hungry
  they already were × how hard the times are. Reactions scale with it; betrayal
  by a friend hurts more; bystanders take sides by fondness.
- **Peace norm** (third norm): sets how easily disputes become fights and how
  lethal they get. Killings push most people toward peace (most of all those
  who loved the dead); successful raids push raiders away from it.
- **Raids** on visibly better-off households in hard times: an instigator
  (temper, boldness, resentment, hunger, low peace) rallies people who each weigh
  joining; the hoarders' kin and friends defend; the stronger side carries it.
- Bug: children could end up alone in a household (parents dead). Now any child
  without an adult at their hearth is taken in, checked daily. The early
  "grave thefts" and raids were mostly people raiding these orphans.
- Results (8 seeds × 80 y): 7/8 bands survive, ~0.3-0.8 caught thefts a year,
  rare fights, **no killings or raids**. Starvation still ~90% of deaths (69%
  adults).

## Cooking, hearth & preservation (built; state at compaction)
- New: `Activities/HearthActivities.cs` (Cuisine rules, Cook, GatherWood, Preserve).
  FoodStore pools: Meat, Plants, Stored, Cooked, Dried. Households have Fuel
  (firewood) and SoftFoodUntil. Cells have Wood (forests most). Interest.Hearth,
  Skill.Cooking.
- Roasting (fire.make) works now: cooking raises food value (meat ×1.15, plants
  ×1.1, nuts/grain ×1.15), and cooked meat can't make people sick. Better stages
  (hot stones, boiling, oven, bread, pot) raise gains; boiling/pots make soft food
  (toddlers wean, elders digest better; elders get 20% less from raw food). Bone
  grease turns bones into keeping fat.
- Firewood: each hearth burns ~0.8/day in summer up to 2.5/day in winter. No fuel
  means no cooking and no warmth (fire protection is per family now).
- Preservation (needs dry.keep): drying/racks/smoking turn surplus perishables
  into Dried food; storage stages slow spoilage; cold caches keep winter meat;
  pemmican.
- Winter snow: travel ×1.5 and harder chases unless snowshoes (which then help).
- Discovery web: new Cooking skill + store.cold, dry.pemmican, hide.snowshoe
  → 25 skills, 106 stages (page rebuilt).
- Runner: `--grant id,id,...` gives founders knowledge (testing only).

### Experiment (6 seeds × 80 y)
| condition | survived | notes |
|---|---|---|
| baseline, PlantDensity 0.8 | 3/6 | firewood chore tipped the energy budget |
| + preservation | 3/6 | never any surplus to preserve |
| + cooking | 5/6 | helps |
| everything | 5/6 | small pops |
| **PlantDensity 1.6** | 6/6 | pops 2-23; starvation 84%, illness 7%, cold 6% |
| **PlantDensity 2.4** (currently set) | 5/6 + 1 crash | pops 12-74 |

- Diagnosis: deaths are real spring famines (hungry gap). The land was 2-4×
  poorer than real temperate forager carrying capacity (0.1-0.2/km² → 40-80
  people on 400 km²), so there was never surplus to preserve.
- **A segmentation fault crashed one run** (PlantDensity 2.4, seed 2 or so).
  Must reproduce and fix.

### Next steps
1. Reproduce and fix the crash.
2. Settle PlantDensity against the real-density target (40-80 people sustained);
   re-run the A/B/C/D tech comparison at that level.
3. Then the discovery system (experiment, accidents, teaching, loss).
4. Calibration pass against real targets (death causes/ages, work hours, band
   sizes, split frequency).
5. Later: deeper social layer (more conflict causes, remembered events, skill
   reputation), couple separation, culture as customs, immunity/medicine
   additions, god & belief.

## Crash fix, leave/return loop, multi-band map (2026-10-02/03)

### Crash (was "segfault")
- Not a segfault: a NullReferenceException in `MakeChildOf` (father null).
- Root cause: the hourly loop steps a snapshot of the band. When a parent left
  mid-hour (strike-out triggered inside someone's Socialize), their children left
  too but were still in the snapshot; later that hour each child chose a new
  activity, and `HomeOf` created a household for them back in the old band. From
  then on they existed in two places (two bands, double births, impossible health
  averages, people who never died).
- Fix: skip anyone who left or died earlier in the hour (`SteppedAt` / `LeftDay`).
  `MoveFood` also dropped cooked and dried food; fixed.
- Test `EveryoneIsInExactlyOnePlace` checks the bookkeeping daily for 12 years.

### Leave/return loop
- People living with (stub) neighbor bands kept full-strength bonds to the old
  band forever (bonds only faded for band members), so they always came back.
  Fixed with bond fading while apart and prospects in the return decision; this
  was then superseded by the multi-band rewrite below.

### Multi-band map (user decision: nobody leaves the map)
- New `Region` owns the world, clock, animals and every band. `Simulation` is now
  one band on the map (its activity code is unchanged). Neighbor stub bands
  (off-map wandering, no food/disease/births, removed at 65, topped up to 22) are
  gone.
- Map 100 → 200 cells (40 km). Animal pools scale with area so per-km² density
  is unchanged. 4 bands at start (founders 30 + three of 18-30), ≥ 8 km apart.
- Moving between bands is always walking somewhere real:
  - **Visit** (new activity): adults go to another band's camp for 3-10 days to
    see kin/friends, or (unpaired, no one suitable at home) to court. Hosts feed
    guests. Longing grows with time since the last visit. At the end they may
    stay if they'd be better off there.
  - **Pairing across bands**: whoever loses less moves (ties, both bands'
    prospects, both bands' residence customs).
  - **Bands camped together** (< 3 km): families may join the other band
    (ties + prospects), after at least a year where they are.
  - **Strike-out**: a bold person's household leaves only if relief + what's
    waiting beats the ties left behind; it joins a known band where they'd be
    better off, or sets off alone as a new group on the map.
  - **Splits** found a new real band 4-15 km away, carrying customs and tools.
  - Discontent starts fresh on arrival (it's about life in a band).
- Runner prints the founding band's story and one `map │` line per year (every
  band's size, births, deaths). `--grant` applies to everyone on the map.
- Speed: GatherWood/GatherFlint lookups cached, near-camp cells cached per camp,
  plants regrow in parallel across cores, no string building when profiling is
  off, argmax without sorting. ~2× faster (≈1.1 s per simulated year with ~190
  people). GPU doesn't fit this kind of branchy agent code. Bigger win is running
  many seeds at once on the 24 cores (experiment script now runs 12 in parallel).
- Tests: 11 passing (added `EveryoneLivesOnTheMap`).

### Seen in first multi-band runs (to calibrate)
- Background mortality far too low when people are fed: ~280 people with 0-2
  deaths a year (real foragers ~25/1000/yr, mostly children's illnesses).
- Bands split often (4 → 11 in 20 years), sizes 11-57.

## Food calibration, overnight session (2026-10-03), stopped at usage limit
Done (builds clean; tests not re-run since the fishing/reserves changes):
- First 12-seed run of the multi-band map: populations exploded (100 → 1,200-2,500)
  because land richness 2.4 was tuned when one band used only the area round its
  camp. Sweeps: 0.6 collapses, 1.0 ≈ 80-100 people, 1.4 ≈ 200-320, 2.0 explodes.
- Hunting was 1-3% of food. Causes and fixes:
  - Animal numbers 30-70× too low: species now use real densities per km²
    (deer 4, boar 2, hares 10, mammoth 0.05, wolves 0.025); regional pools derive
    from density × map area.
  - Hunt learning counted old wounds and ignored meat eaten at the kill: fixed.
  - Wooden spears range 8 → 12 m (Schöningen throwing spears).
  - Yield memory was biased by fast pessimistic updates (true hunt return 0.28/h,
    believed 0.03/h). Activity yields are now an even-handed long-run average;
    the fast pessimistic update stays for danger and beliefs.
  - Hunting now 20-45% of food.
- Firewood checked: ~1.8 h/household/day, within the real range. Not a bug.
- Story starts in late summer (Clock.StartAt; climate set at creation).
- Fishing: new known-at-start stage fish.grope; river/marsh fish stocks with
  slow resident regrowth plus spring/autumn runs; Fish activity, Skill.Fishing,
  method efficiencies (hand < spear < line < trap < weir/net in runs); fish
  spoil like meat. Web now 107 stages, page rebuilt.
- Wolves: only starving packs attack, and only people out alone near them, or
  small children at a hearth with the fire out.
- Childhood illness: chest infections after other sickness all year for babies
  and the old (cold amplifies); weaning-age gut infections; maternal protection
  for nursed infants. Fitted knobs Simulation.ChestRisk / WeaningRisk (runner
  --chest / --weaning). Runner also has --plants, and reports child mortality.
- Body reserves: max now 18-42 days of own needs for adults (scaled by age),
  nourishment falls below 40% of capacity (Physiology.MaxReserve/Nourishment).
  Diagnosis: 142 of 153 starvation deaths were in spring, mostly adults 15-49,
  because reserves were only 5-9 days.

Latest result (40 y, chest 0.03, weaning 4):
- plants 1.0: ~90-115 people, infant 7-18%, under-15 30-44% (near real), but
  starvation still 84% of deaths.
- plants 1.4: ~200-320 people, child mortality too low (5% / 13-19%), starvation 73%.

Next:
1. Find why starvation still dominates after the reserve fix (check spring
   deaths by age again; spring fish runs only help with weirs/nets; sharing
   between households in the hungry gap).
2. Settle plants (~1.2) + ChestRisk/WeaningRisk so the map holds 160-320 with
   illness the main killer; re-run tests; re-run the tech comparison.
3. Then social rework stages 0-3 (plan agreed in chat), then discovery.
- Reverted wooden spear range to 8 m (thrusting): throwing is the spear.light
  unlock in the design. Hunting still 35-45% of food at plants 1.2 (true return
  0.23/h), so the densities and the learning fix did the work.
- Note: storage, preservation, spear fishing, weirs, etc. all exist but are
  unlocks, and the discovery system isn't built, so normal runs only have
  starting knowledge (hand fishing, no drying, no caches).
- TODO hunting: 8 m 'range' really means the last rush; real thrust ~2 m. Model the rush, or add ambush and drive methods.

## Calibration continued (2026-10-04)
- Lean-season memory now covers winter into spring (the real hungry gap); the
  lesson lands in early summer; autumn stockpiling aims to cover two seasons.
  Topic renamed lean:winter → lean:season.
- Starvation damage only while not eating (stomach < 0.3), not while a thin
  person is eating again.
- Fertility: replaced the "no conception if satiety < 0.5 / child under 2"
  cutoffs with conception chance × age curve × nourishment³ × nursing
  suppression (fades as the youngest child reaches 3).
- Childbirth: serious complications now kill about half the time (~1% of
  births, as among foragers).
- Illness course: care and rest now multiply the body's own response instead of
  adding a fixed amount (which let cared-for children beat any infection on day
  one; case fatality was ~0.1% for everything). Severities set from simulated
  courses against untreated case fatality: flux harm 0.015 (~2-3% of toddler
  cases), lung sickness 0.07 (~15% toddlers, ~28% at 70), fever unchanged
  (~5% toddlers).
- Calibrated defaults: PlantDensity 1.4, ChestRisk 0.001, WeaningRisk 2.5,
  FeverRate 0.0003. Runner knobs --plants --chest --weaning --fever.
- 60 y: infant 20-28%, under-15 33-39%, births ~50/1000, deaths ~30/1000,
  map 250-330, illness the top killer.
- 100 y: populations keep growing (520-670) and starvation returns to ~49% of
  deaths. Missing density-dependent checks: intergroup violence (often ~15% of
  adult forager deaths) and crowding-driven contagion. Revisit land richness
  after the social rework adds conflict between bands; don't fake it here.
- Tests: 11 passing.
- Technology comparison (calibrated world, 4 seeds × 60 y, last-20-year mean pop):
  baseline ~280 (starvation 39%), preservation ~285, cooking ~280 (34%),
  fishing tech ~370 (27%), everything ~330 (29%). Fishing matters most.
- Preservation fixes: dried food now counts as keeping food for the stockpile
  drive; preserving is pulled by any fresh food that will rot before it's eaten.
  Effect on starvation still unclear across seeds (sharing between households
  looks like the real gap: households starve while others hold food).
- Warmth: with their own fire out, people sit at a lit hearth of kin or a close
  friend. Fires were out ~40% of household-days.
- Spears: thrusting reach now 2.5 m (spear ~2 m held partway + lunge). Hunts can
  drive herds into marsh/river (needs ~6 people, slower animals easier) when
  creeping close is hopeless. Hunting falls to ~5-15% of food with thrusting
  spears; granting throwing spears cuts starvation 47% → 32% (6 seeds).
  Ambush is still TODO with the hunting-methods pass.

## Social rework stages 0-1 (2026-10-04)
Built (Society.cs, People/Deeds.cs, Activities/SocialActs.cs):
- Deeds (ask, give, refuse, take, confront, attack, return) as the unit of memory,
  gossip and learning. Perception: those awake in camp notice with some chance;
  people an act is done to face-to-face always know; a take from a hearth is only
  noticed by whoever is about. Witnesses remember (40 deeds max, a year).
- Innate: grievance (anger at a pinned harm, ×(1 + hunger of one's children),
  cools over weeks, slower for the hot-tempered); guilt toward kin and friends;
  fear of the stronger. Trust is now only moved by deeds seen or heard of.
- Learning in context: "act:<act>:<relation>[:seen|unseen]" expectations in
  rations; consequences carry the deed they answer (refusals, confrontations,
  beatings credit the earlier take); watchers learn vicariously; gossip passes
  remembered deeds in conversation; all topics also spread through the existing
  belief copying. Newborns take on their mother's expectations.
- Cultural priors per band (taboo on taking, generosity, retaliation, violence),
  with personal variation.
- Decider options: Ask / Take when the hearth is short; Confront / Attack when
  angry at someone in camp. Owners give or refuse by choice (warmth, the asker's
  visible need, kinship, fondness, the sharing norm, learned reciprocity, their own
  family's need, anger). Confronted takers choose to give back or stand firm;
  accusers choose whether to escalate. Fights keep their physics. Hunters'
  households hand round a kill by the same giving choice.
- Removed: AskForFood, ConsiderTheft, Fight's scripted reactions, ConsiderRaid
  (raids return in stage 2 via invite), BlameFailedHunt, the ShareMeat formula.
- Founding: elders are parents of a founding adult and live at their hearth;
  teenagers are older children of a couple (nobody starts alone).
- **Big bug found**: Eat was offered whenever the camp had food but only drew from
  one's own hearth. People with empty hearths kept choosing to eat and starved
  without asking or foraging. Fixed: eating only with food at home. Starvation
  fell from ~50% to ~12% of deaths (illness now ~60%, as among real foragers).
  This was likely the hidden cause of the long-running "spring starvation".
- Nursing suppression of conception now fades over 4 years (forager births ~4
  years apart); births had climbed to ~55/1000 once starvation stopped.
- 60 y check (6 seeds) after the Eat fix: illness 60% of deaths, starvation 16%,
  infants 18-29%, under-15 33-45%; but populations grow to 380-690. Land richness
  lowered 1.4 → 1.0 (interim; the starvation bug had been holding numbers down).
  Final setting after stage 3 adds conflict between bands.

## Discovery system (2026-10-04)
- docs/annotate-sim.py adds a machine-readable "sim" block to all 97 discoverable
  stages: during (activities), materials (at hand near camp), pressures, interest,
  skill, trait, way (settled / settled-long), rate. Knowledge prerequisites are the
  refs already in "needs" (DiscoveryWeb.CanLearn). Loader reads it.
- Inventing.cs: each working hour, a person may hit on a stage that can be found
  during that activity, if they know its prerequisites, its materials are at hand
  and the way of life fits. Chance = DiscoveryRate × rate × pressure (0.2-2) ×
  curiosity × aptitude × interest × skill × trait (each 0.5-1.5) × 1.5 if it has
  an accident. Chronicle notes accidents and firsts on the map; Region.DiscoveryLog.
- Pressures: hunger, cold, winter, dark, spoilage (household rot lately), flight
  (stalks failing), danger (wolves seen), death (recent death), carry (big hauls).
  Materials: terrain, clay, ochre, cave, reeds, nuts, grain, fishrun, flint (8 km),
  game, wolves, snow, bone/hide/sinew/fat (band stores), glut (household).
- Spreading: PassOn in conversation (both ways), children watching someone work,
  and visits, so ideas cross bands when people meet. Lost when the last knower dies.
- Pacing (design decision for the user): DiscoveryRate 2e-5 gave 89 stages in 40 y
  (bows in year 4); 1.6e-6 gave ~50 different stages on the map in 50 y; set to
  6e-7 so god nudges have room to matter. Runner --discovery overrides, and the
  run summary lists discoveries.

## Social stage 2 (group acts) and stage 3 (between bands), 2026-10-04
- Stage 2, JointActs.cs: Rally. An angry or hungry person calls others to take a
  hearth's food by force; each weighs joining (own hunger × the hearth's plenty,
  anger at it, bond and trust with the caller, what seen takes have brought,
  guilt, peace norm, temper). Others weigh defending (household, kin and fondness,
  anger at the raiders, fondness for them). The clash is physical (DoAttack pairs);
  if the raiders prevail each takes a share (DoTake). The raided hold grievances
  against everyone who came. Hunts, camp moves and splits were already proposals
  weighed person by person, so they stay.
- Stage 3:
  - BandGrievance: a harm from someone of another band is also laid at that band's
    door (half strength), spreads by gossip, cools slowly (1%/day); gifts from a
    band's people soften it. Hosts feeding guests on visits are Give deeds.
  - Encounters on shared ground (Region daily): bands within 8 km whose forage or
    fishing patches overlap meet now and then (likelier the more they share). Each
    person weighs hostility (anger at the band or person, hunger × reliance on the
    ground, temper vs fondness, guilt, peace). Usually they talk (fondness, sickness,
    gossip, beliefs and know-how pass), sometimes it comes to blows. Territory
    emerges from this, not from a rule.
  - Raids (Decider option Raid → DoWarRaid): anger at a band within known range, vs
    fear, peace, young children; each person weighs joining. They fall on the camp at
    first light; ~60% of the other camp wakes to defend (home advantage ×1.4). Deeds
    happen in the raided band, so its people feel and remember them and its anger
    turns on the raiders. Winners carry off food.
  - Perception fix: whoever an act is done to face to face always knows of it, even
    from another band.
- Speed: conversations pass 6 random beliefs (weighted up to keep the pace), deed
  memory has a lookup set, topic names cached, awake/sick/working lists cached per
  hour, discovery rolls first against the best case. ~4 s per simulated year with
  ~120-150 people (social + discovery cost is spread across systems).
- First stage-3 runs: worlds diverged (one seed 202 fights / 38 killings in the
  founding band, another 16 / 1), but violence was 14-62% of all deaths: a feud
  loop with no brakes. Dampeners, all through choices: fear of the enemy (their
  strength vs. the likely party, more for parents of young children, less for the
  bold) in calling and joining raids; revenge taken cools band anger; a camp near a
  band people are angry at feels unsafe (camp-move value); temper weighs less when
  strangers meet on shared ground. Result: violence 4-13% of deaths, raids 0-19 per
  world in 60 years. Realistic range for foragers.

## Social stage 4 (couples, leaders) and stage 5 (evolved values), 2026-10-04
- Family.cs. Jealousy (innate): noticing one's partner warming to someone else
  stirs anger at the rival and a little hurt toward the partner; what follows uses
  the same confront/fight choices. Separation: a faded bond plus someone free and
  close may lead a person to leave (against young children, warmth × family drive,
  anger at the partner); children stay with the mother; the left one resents both.
  First version gave 100+ breakups per band in 60 y (partner bonds faded like any
  other); now sharing a hearth keeps the bond up nightly, children weigh more, and
  leaving is a rarer daily decision.
- Leadership as learned trust: "follow:<id>" expectations. After a camp move, each
  person's next forage trips are compared with how foraging went before the move;
  after a hunt, its yield against their usual. Votes on moves and joining hunts
  lean on it; reputations spread like any belief.
- Stage 5: six drives per person (food, safety, family, standing, comfort,
  company), 0-1, inherited from both parents with drift (like body traits),
  founders seeded from a per-band mix, shifted by experience (hunger → food, wounds
  → safety, a kin death → family), copied culturally (children from those who raise
  and teach them, adults a little from talk). Each drive scales the options it
  stands behind (food work, comfort, company, care, standing/safety for fights and
  raids). Runner prints each big band's drive averages and key norms at the end.
- Drives fix: one-way experience shifts plus daily "easing" terms drove food,
  safety and family drives to 0 within decades (people stopped valuing food;
  starvation 30-51%). Now each person has BaseDrives, their settled character,
  formed at birth from both parents' current drives and in childhood from the
  adults they learn from; adult experience (hunger, wounds, kin deaths, talk) moves
  drives away from it and they relax back (~0.3%/day). Culture still accumulates
  across generations; nothing ratchets.
- Teen fertility: conception chance 20% of peak at 16 rising to full by 22
  (forager first births ~19). Tests trimmed (suite 21 min → 3 min).
- State after all five stages (6 worlds × 60 y): map 245-406 people; illness the
  top killer; starvation 14-25%; violence 7-13% of deaths; raids 0-12; couple
  breakups 0-45 per founding band; infants 20-35%, under-15 30-51%; 17-28 stages
  discovered per world and spread between bands. Bands diverge in drives (0.29-0.68),
  sharing (0.31-0.55), peace (0.53-0.75) and how badly they expect a seen take to go.

### Open items
- Land richness final calibration (now 1.0; populations slightly above the
  160-320 target).
- Speed (~4 s per simulated year at ~150 people; spread across systems).
- Hunting methods: ambush (drives are in).
- Discovery pacing is a design decision for the user (DiscoveryRate 6e-7).
- Remaining authored lessons: the sharing/valor/peace "BandLearns" hooks (hard
  times → sharing, big kills → valor, deaths by animals → caution) could become
  emergent from learned outcomes too.

## Overnight 2 (2026-10-05): learning fixes, audit A-F, mental maps, hunting tactics
### Learning and theft
- Each consequence counts once per person answering a deed (Deed.AnsweredBy); the
  ratchet was every refusal re-crediting the same old take. Consequences learn at
  the same rate as the gain. Deeds carry Payoff (gain + consequences so far);
  watchers learn toward it when they see a consequence (not one-sided costs, and
  not the bare gain the moment they see a take).
- Takes are answered on the spot: a witness close to the loss decides to confront
  now (ConfrontValue: anger, temper, learned confront payoff vs guilt and fear).
- Unseen takes: the family notices missing food next day and suspects someone
  (known takers first, else the least trusted). Suspicion is a grievance; right or
  wrong, it has consequences.
- Taking is valued by the family's actual thinning (satiety), not a low store.
### Audit
- A: sharing/valor/peace norms retired with all BandLearns hooks. Sharing → what
  giving brings (minus what refusing costs); peace → learned attack payoff + safety
  drive; valor → the standing drive.
- One felt scale (Simulation.Felt): learned payoffs and guilt are added to act
  options in food-work units. Before, expected costs were multipliers and could
  never make violence look bad.
- B: joining raids/rallies = own value + pull of the asker vs best alternative;
  defending, escalating, hostile-vs-friendly meetings compare against the
  alternative; pairing = each the other's best available choice.
- C: separation on occasions (talk with someone free; a fight between partners);
  the other must want them back; pair bonds count double and deepen with years;
  breaking up hurts both sides' fondness; resentment lowers desire. Whom children
  stay with is a choice (closeness + a band custom formed from what parents did).
  Raided camps wake by dogs and party noise.
- D: carrying capacity (~12 rations × strength × maturity; baskets ×1.3/1.6, net
  bag +0.2): takes, raids, foraging, fishing and kills brought home.
- E: Prestige removed. Standing = learned following ("follow:<id>"); receivers of
  a hunter's meat come to follow them; votes and influence use it. BigKills is a
  plain record for narration.
- Founding violence priors: in-band N(-3, 0.7), toward outsiders less costly.
  Winning a fight is no longer its own +1 reward. A rally clash could pick the same
  person twice ("Kanor attacked Kanor"): fixed, and DoAttack refuses self-targets.
- Giving: kin altruism (feeding one's kin is felt as good), reciprocity credits the
  full return, refusing keeps the food (gain) and its costs come back later.
- Fear of a target's backing (kin and friends present); grudges feed discontent so
  feuds end with people leaving (fission), as among real foragers.
- Children inherit their parents' settled character (BaseDrives) not their moods.
### Mental maps (People/MentalMap.cs, Wayfinding.cs)
- Each person's map: places (plant type, what was there, best known, wood, fish,
  flint), game sightings (14 days), other camps. Founders know their home range
  (2-8 km by age); people see ~400 m around where they go and animals within 1 km;
  camp news updates what's left at places people just used; conversation, visits
  and encounters pass places, sightings and camps (more with stories / named places
  / ground maps). Capacity 400 places (+400 named places, +400 carried maps).
- Foraging, firewood and fishing choose from known places by expected yield for
  the walk; nothing worth it → explore. Visits, raids and strike-outs only go to
  camps people know of; a camp that has moved is found empty.
- Cost of imperfect knowledge, measured: foraging 0.235 vs 0.30 rations/hour with
  the old omniscient choice (−20%).
### Hunting
- Hunts go after herds someone saw in the last 3 days; nobody has → go looking.
  Tracking from the sighting: chance falls with how far the herd has moved, rises
  with skill (and dogs).
- Tactics: Stalk, Ambush (wait at water), Drive (into wet ground, 4+), Surround
  (discovered, 6+ open ground), Persistence (discovered), Fire drive (burning the
  land; scares herds around). The party votes by learned payoff per tactic and
  animal; bands start with habits (physics-informed: stalking with thrusting spears
  rarely works); curiosity explores.
- Founders' expectation of hunting set to its measured return (0.25/h): the old
  0.6 placeholder made a band starve chasing prey it now has to find.
- Web: + Wayfinding tile (names, sketch maps, carried maps, star paths) and Hunting
  tactics tile → 27 skills, 117 stages.
### Hunting and foraging bugs found with the maps
- A searching party never stopped "searching": after spotting a herd it kept
  re-choosing on arrival, walking on for days; its members starved mid-hunt (19 of
  20 starvation deaths in one world). Fixed (searching cleared once a target is set).
- Tactic choice started from random band habits; stalking with a 2.5 m thrusting
  spear almost never works, so bands starved while unlearning it. Starting habits now
  reflect what works with thrusting spears (stalk ~0.05, ambush ~0.3, drive ~0.35
  rations/hour), varied per band; curiosity tries other tactics.
- Camp news: back from the land, people tell what's left at the place they used, so
  others don't all head for the same picked-over patch.
- Imperfect knowledge costs ~20% of foraging returns vs the old omniscient choice
  (0.235 vs 0.30 rations/hour). Realistic.
### Audit F
- Band.ComingOfAge (custom, ~15 ± 1.2 per band, inherited by splits): pairing,
  joining raids, defending, voting use Grown(p). Body maturity still governs physical
  capacity.
### Discovery effects (82 of 103 discoverable stages now act directly)
- New Trapping activity (snares 0.4, deadfalls 0.5, spring traps 0.7 rations/hour
  where small game has been seen; catches come out of the warren).
- Warmth: lean-tos, huts, pit houses, built hearths, scraped/sewn hides, awls,
  needles. Shoes: 10% faster travel. Axes: 1.5× firewood loads. Built hearths burn
  20% less wood. Heat-treated flint: +30% tools; hafting: tools last longer.
- Foraging: plant lore +15%; nut processing, pounding, mortars for nuts; grain
  harvesting, tight baskets, grinding slabs for grain. Fishing: harpoons; canoes
  double fishing reach. Weapons: darts, fletched and backed bows. Bone points +10%.
  Game-trail knowledge helps tracking.
- Wolves: torches, dogs (lurking, pups, camp dogs) raise deterrence.
- Stories (+30%) and counting (+10%) help passing on know-how; shared rites and
  burial ease discontent and grief; counting, fired clay and coiled pots cut
  spoilage; seasonal-return / long / year-round camps make staying worth more; ochre
  adornment adds to desirability; star paths lengthen journeys.
- Grain toward farming: once a band knows clearing, wild grain stands within 2 km
  of a camp kept 30+ days grow richer each week (up to 1.5×; 2× with scattering
  seed; 2.5× with selection); untended stands drift back to wild.
- Stepping stones only (no direct effect, others build on them): fire.hot,
  fire.lamp, wood.polish, bask.sealed, pot.pinch, bone.hook, dog.pup partly.
  FLAGGED for the belief layer: ochre.hand, ochre.paint, ochre.figure, pot.pit,
  fire.lamp (art, rites; meaning comes with religion).
### Mortality calibration
- Accidents out on the land (falls, drowning; hunting twice as risky):
  AccidentRate 1e-5 per hour out. ChestRisk 0.003, WeaningRisk 4 (fitted).
  PlantDensity 1.3 (now that knowledge is imperfect and loads are limited).
- 30 y, 6 worlds: pop 157-255; starvation 0-6%; violence 0-22% of deaths; killings
  ≤0.07 per band-year; seen takes ≤1 per band-year; splits 0.12-0.16; infants
  21-40%, under-15 30-52%; births ~48/1000 vs deaths 17-35 (still growing).
### Long run (60 y, 6 worlds, richness 1.3)
- No crashes. Violence 0-17% of deaths, starvation 1-6%, killings ≤0.03 per
  band-year, 32-42 different stages discovered per world.
- Populations keep growing ~2.5-3%/yr (≈140 → 300-580): deaths 16-31/1000 vs
  births ~46. Real foragers grew ~0%. Testing richness 1.0 vs 1.3 over 60 y.
### Speed
- Telling places compared every recent sighting against every one the listener
  had (~30k comparisons per conversation). Sightings are now keyed by herd.
  Conversation 24 → 5 µs; a 3-year run 17.3 → 10.7 s.
### Final calibration of the night
- Richness 1.0 vs 1.3 over 60 y: both keep growing (1.0: 290-600; 1.3: 330-830)
  with little starvation. Food isn't the brake; children survived too often
  (under-15 deaths 20-40% vs forager ~45-50%) because well-fed children beat
  infections. Refit: ChestRisk 0.005, WeaningRisk 6 → under-15 37-61% (mean ~46%),
  infants 28-45%; 40-y map 175-288, growth ~1.5%/yr (from ~2.5-3%). Defaults now
  PlantDensity 1.0, ChestRisk 0.005, WeaningRisk 6, AccidentRate 1e-5.

## Open / flagged (not fixed tonight, with reasons)
- Population still grows ~1.5%/yr. Next lever is grounded fertility: nursing
  suppression fading over 4 years may be slightly short (forager birth spacing
  3.5-4 y for surviving children); and births rise after infant deaths (real).
  Worth a dedicated demography pass with life tables rather than more knob-turning.
- Breakups ~0.01-0.05 per band-year (forager divorce is commoner). Needs
  occasions beyond attraction: provisioning failures, childlessness, conflict.
  Left for the next social pass to avoid re-creating the flip-flop loop.
- Violence share runs high (15-40%) in a few worlds mainly because total deaths
  are low; killings per band-year are within target. Will settle with demography.
- Wolves attack on a 25%/day roll when conditions hold (animal behavior; fine for
  now). Raid waking uses a chance (perception).
- Remaining global weights inside formulas (audit G): many reduced or routed
  through drives/learned values; the rest documented as authored structure.
- Belief-layer stages (ochre hand/paint/figure, pit firing, lamps) wait for the
  god/religion work; stepping-stone stages have no direct effect by design.
- Speed: ~3.5 s per simulated year at ~200 people on one core; 12 parallel runs
  slow each other. Decisions (~2.3 µs) and daily upkeep are the main costs now.
- Hunting returns with thrusting spears and real tracking: ~0.09 rations/hour
  (hunting mostly by ambush/drive). Discovered projectiles raise it.

## STATE AT COMPACTION (2026-10-05)
- All 11 tests pass. Defaults: PlantDensity 1.0, ChestRisk 0.005, WeaningRisk 6,
  AccidentRate 1e-5, DiscoveryRate 6e-7. Web: 27 skills, 117 stages (+ Wayfinding,
  Hunting tactics); annotate with docs/annotate-sim.py, rebuild page with
  docs/build-spec.py.
- Runner: --all (every band's chronicle), --plants --chest --weaning --fever
  --accident --discovery --grant, ANTHRO_DEBUG_HUNTS / _DEATHS / _SEASONS env vars.
- Experiment scripts in the old session scratchpad (check20.sh, compare.sh,
  metrics.py per band-year metrics vs targets). Recreate if missing: run 6 seeds
  with --all, count chronicle lines per band-year.
- Recent results (6 worlds, 30-40 y): pop 175-288; starvation 0-8%; violence
  mostly 0-20% of deaths; killings ≤0.07/band-year; under-15 deaths ~46%;
  growth ~1.5%/yr.
- NEXT (agreed order): demography pass (population growth, then breakups and
  violence share follow from it) → god layer. User decides discovery pacing.

## Demography audit and food/ecology rework (2026-10-05, after compaction)
### Balance target (user)
- Without a god, humans on the map persist but advance slowly; single bands of
  ~30 can and do die out. The god speeds innovation, adds story/drama, rescues,
  improves odds. Baseline: near-zero long-run growth, real lean seasons, slow and
  uneven discovery.
### Audit findings (12 worlds x 60 y, scratchpad Demog tool: life tables, food accounting)
- Populations grew 1.5% → 3%/yr with no ceiling (800-1150 people by year 90-107).
- Everyone fed ~1.0 all year; food in 1.25-1.6x need; hunting 70-110% of need.
- Cause 1: off-map "regional pool" 4-8x the map refilled hunted herds every spring
  (map deer hunted to ~0, ~3,000 deer/yr killed from the reservoir).
- Cause 2: buffers added during the "starvation" era (which was mostly the Eat bug)
  were never removed.
- Cause 3: satiety saturates at 1 above 40% fat, so fertility (satiety^3) and
  immunity never felt mild leanness. TFR 5.9-7.5 (real 4.5-6.2); l(15) 0.62-0.80.
- Accidents (falls/drowning) ~30% of deaths (real 5-10%): exposure grew since calibration.
- Per-hour returns are realistic (forage 0.4-0.6, hunt 0.33, fish 0.35-0.5 rations/h).
- Discoveries: ~40 stages known by ~85% of everyone by year 60; per-hour returns
  barely moved. PassOn is one-shot per conversation (8%), so crafts spread like gossip.
### Changes made
- Climate: daily temperature curve (seasonal means unchanged), lapse rate 6.5 C/km
  (authored: elevation unit = 500 m), snow (0.6 cm/freezing day x wetness, 1%/day
  settling, 1.5 cm/degree-day melt; woods hold half), yearly rain districts (6x6
  grid, AR(1)), nut mast (alternating), berry/grain years, fish run strength.
- Cells: local temperature, snow, wetness; plant growth by local warmth and rain;
  foraging under snow limited (SnowAccess). Camp weather = camp cell.
- Ecology rewritten: closed map, no off-map pool. Herds have Condition from local
  forage (authored Need 0.45, dormant forage 0.3, 2%/day); thin animals die;
  births late spring by condition and crowding (1-(N/K)^3); natural death rates
  from field survival (minus predation). Deer migrate within the map (altitudinal);
  mammoths leave the map in winter and return (same animals, unhunted while away).
  Wolves: hunger in weeks, ~4 kg/day, up to 3 chases a day choosing easy prey,
  find prey within ~4 km (authored), pairs disperse to found packs. Extinction is
  permanent (user). Hare meat 2 → 1 ration.
- Body: fecundity from fat (full at 60% of max) and weight trend (Ellison);
  immunity weakens with real undernutrition (fat < 30%).
- Food valuation: food work's base pull scales with room for more (fresh food
  days at home, camp meat vs how long meat keeps: 4 d warm, 20 frozen, 60 cached);
  hunt value subtracts meat already in camp.
### Results so far
- Ecology alone, no people (6 worlds x 50 y): deer 2,000-6,200 fluctuating,
  hares boom/bust, boar track masts, wolves 5-40 (extinct in 1/6).
- Ecology with people (before the valuation fix): pops flat at 31-180; but bands
  killed 1,000-1,800 deer/yr (10-18 per person), wiping out deer/boar in ~half the
  worlds within 30-50 y. Valuation fix aims at this; run in progress.
### Hunting fix (same day, iterated over 9 test rounds of 12 worlds x 60 y)
- Overkill causes found and fixed, in order:
  1. Food work had a fixed base pull regardless of food on hand → scaled by room
     (fresh-food days at home; for hunting, all camp food vs how long meat keeps:
     4 d warm, 20 frozen, 60 cold-cached).
  2. Hunt party value ignored meat already in camp → subtracts it.
  3. Meat craving = <3 days of meat at the hearth (always on in warm weather) →
     craving follows the hearth's recent animal-food share (full ≤10%, none ≥40%).
  4. Searching was nearly free (look once, go home) → parties roam up to 8 h
     scanning new ground; empty days are now real, so returns fall with scarcity.
  5. Hunted herds learn to be shy of people (Shy: +0.05 per failed stalk, +0.1 per
     kill, fades ~0.3%/day; cuts approach chance up to 60%).
- Ecology fixes found along the way: wolf kills compensatory up to the natural
  death rate (Culled); wolves pick prey by encounter (meat-weighted share of what's
  near) instead of always the meatiest; wolves feed on a carcass for days (hunger
  floor -1.5); conception set at the autumn rut by condition, spring condition only
  trims calf survival. Accident rate 1e-5 → 2.5e-6/h (fitted to 1.5-2.5 deaths per
  1,000/yr); accidents now ~5% of deaths.
- New test LandLeftAloneKeepsItsAnimals (12 tests, all pass).
### Results (v9: 12 worlds x 60 y)
- Deer 5,000-6,000, boar ~2,700, wolves 29-40 in ALL 12 worlds at year 35-60.
- Populations 25-193 at year 60 (11/12 persisting well, one dwindling); density
  0.015-0.12/km² (real temperate 0.05-0.3). Growth ~0-1.3%/yr.
- Hunting 16-60% of food; per-hour returns 0.12-0.5.
- Infant mortality 17-33% (real 15-25%); l(15) 0.41-0.75 (real 0.55-0.65).
- TFR still 5.4-7.2 (real 4.5-6.2); first birth ~19; spacing 3.7-3.9 y.
- Starvation now 20-55% of deaths in most worlds, including adults 15-44: lean
  winters/springs push the poorest 10% of people to ~0 fed while band averages
  stay 0.75-0.85 → famine is unevenly shared. Breakups still rare.
### Next
- Famine distribution (sharing in crisis, fallback foods, winter stores, moving
  camp) — discuss with user. Then TFR re-check, discovery spread for the "slow
  without god" target, dogs as real animals (later), breakups, speed.

## Famine pass (item 1 of the user's list; 2026-10-05 evening)
User's list, in order: 1 famine, 2 births, 3 discovery spread, 4 breakups,
5 dogs as real animals, 6 speed. (Also noted: band life churns a lot — bad-blood
splits every few years, hundreds of family moves; band sizes 4-26 vs real 25-30.)
### Diagnosis (death logs, ANTHRO_DEBUG_DEATHS / _SEASONS)
- Most starvation deaths were whole camps out of food in late winter/spring, not
  unequal sharing; also lone people in 1-person hearths and orphaned infants.
- Bands shattered into 1-5 person groups: "hard times" splits skipped the
  minimum-group-size check; rejoining only happened when two camps came within 3 km.
- Stores ran out by spring; early-spring land near camp is bare.
### Changes
- Hard-times splits no longer found bands below what members judge viable; the
  family instead joins a known band (anyone in hard times; only the bold strike out).
- SeekCompany (weekly): bands below the size their adults think viable consider
  joining known bands nearby; ConsiderJoining adds a "too small here" pull scaled
  by hardship.
- Demand sharing: givers will share down to the asker's level (SpareFor), not
  only what's beyond 2 days; asking/refusal accounting use the same.
- Inner bark: forest fallback food in winter/spring (~0.2 rations/h × skill),
  only when nothing better at the patch; depletes (20 rations per forest cell,
  regrows 5%/yr) so camps strip the woods near them.
- Births (item 2): Coale-Trussell natural-fertility age schedule after 22
  (0.94/0.86/0.70/0.36/0.05 for 25-29…45-49), fertile to 49; secondary sterility
  (2.5% per birth + complication risk).
### Results
- Starvation share of deaths (years 1-30): ~38% → ~31% (sharing/splits) → ~16%
  (bark). Tiny bands 3-4 per world (from many more); splits now 6-14 people.
- With bark, populations grew ~2%/yr again (100 → 150-200 by year 30) because
  TFR was 6-7; deer hunted out in 1/12. Fertility schedule fix in test (v14).

## Overnight 2026-10-05/06 (user away; queue in memory file anthro-overnight-queue)
### Done since the famine pass
- Inner bark: removed, then restored as desperation-only food (hearth <1 day of food AND
  body fat <50%; 0.12 rations/h × skill; 10 rations per forest cell, regrows 5%/yr).
- Splitting rework: discontent adds Overwork (food-work hours/day, strain from 7 h, full at
  11 h; Person.Workload EMA). Personal feuds (Strife dominant) move one family, never split
  the band. Group too small to stand alone doesn't found a band; in hard times anyone may
  join a known band; a family can't hop bands within a year of arriving. "Too small" pull in
  ConsiderJoining only in hard times.
- Tracking: AnimalGroup.Trail (8 daily points); tracks legible 2 d dry / 3 d marsh / 6 d snow;
  hunt Track follows the trail (1-3 km/h by skill, ×1.3 snow) or goes cold (lead dropped);
  people notice fresh tracks within 300 m (EcologySystem.TrailsNear index); gossip about game
  is blurred ~700 m. Sightings usable up to 5 days old.
- Foresight reworked (user asked if hard-coded): no calendar window. Learned per-season
  leanness beliefs lean:spring..winter (worst of fed/fat each season); the urge = expected
  leanness of next two seasons × patience × store shortfall; it adds equally to any work
  whose food keeps (nuts/grain; meat/fish only with dry.keep). MeatKeepsDays is physical
  only; hunt value adds the hearth's store shortfall for those who can dry.
- Births: Coale-Trussell schedule; secondary sterility; fecundity × workload (-5.5%/h above 4).
- Crowd disease: CampAir — each contagious sick person exposes everyone at camp 0.15 h/day.
- Discovery spread: Person.Learning progress; talk gives +0.15 (crafts capped at 40% by talk;
  13 talk-learned stages with During=Socialize learn fully); watching gives +0.35; hearing of
  something makes reinventing it up to 3× likelier. Result: techniques now 5-90% known, uneven.
- Breakups: Person.Provided (food brought home EMA); each season partners weigh strain =
  provider gap vs same-sex peers ×0.8 + childless ≥4 y ×0.5 against the bond.
- Dogs as animals (Dogs.cs): pups from wolves nearby (dog.pup), breed with dog.camp, eat
  scraps/spoilage (0.3 rations/dog/day), starve, get eaten in famine (8 rations), guard
  (DogGuard), wake camp in raids, help tracking with dog.hunt. None acquired yet in tests.
- Peoples/camps (Tribe.cs): one people to start; splits make camps of the same people
  ("Tor of Kesh"); joining another people costs; schism yearly check (RiftWith = bad blood
  + years apart + custom drift; ≥1 → 10%/yr × ...); MaxTribes = 5 (labelled design rule).
  Region.Create(kinCamps:n) adds kin camps of the same people. Lone wanderers allowed (bold).
- Speed: hunt target choice HashSet (hunting 7.9 → 2.5 s per 8 y); trail index.
### Findings
- One lone camp of 30 is demographically non-viable: few unpaired adults → TFR 1.8-2.4,
  first births at 25-39 → worlds dwindle to 7-20 (v20). With 2 kin camps (~75 people) pops
  hold 30-95 for 30-40 y, but one world still collapsed to 1 (v21). User must choose start.
- Mortality still too high generally (v18: e0 15-40, l15 0.29-0.72); starvation 25-50% of
  deaths in bigger worlds; lung sickness top killer of children.
### Later overnight
- Tech → bigger groups (6 worlds × 50 y, 20 food techs granted vs none, kinCamps 2): starvation
  1-5% of deaths with tech vs 14-59% without; people 88-159 vs 26-126; largest camps 38-54 vs
  21-46. But the people spreads into 7-10 camps by year 20-30 (player-management concern).
- Child mortality: ChestRisk 0.005→0.003, WeaningRisk 6→4 (old values fitted to always-fed
  children). Lactational suppression lasts 3 y (fat) to 5 y (thin mothers).
  v22 (3 kin camps, 80 y): all 12 worlds persist (58-211 at y80); e0 27-45, l15 0.53-0.71,
  TFR 4.6-7.4; starvation still 30-60% of recorded deaths (attribution: hunger-weakened
  disease deaths are labelled starvation when that harm dominates).
- Dogs: litters of 2-4; dogs move with families between camps (Region.TakeDogs); starving
  dogs barely breed. With dog.* granted, dogs persist and swing 5-50 with food. Nobody
  discovered dog.pup in 80 y × 12 worlds (slow discovery by design).
- Lone wanderers allowed again (bold, not desperate); being alone pulls +2 toward joining;
  own people's camps always count as known (they found loners never rejoined otherwise).
- Speed: parallel grazer update with per-group RNG (deterministic; eco groups 6.2 → 1.2 s
  per 12 y); wolves use the spatial index. Hunting target choice 3× faster earlier.
- Audit (docs/AUDIT-HARDCODE.md): fixed knowledge leaks (camp sites from the leader's map;
  fishing gated on knowing a fishing place; wood search near camp; discovery materials and
  pups from sightings); calendar rules → conditions (day length curve; fuel, colds, fevers,
  persistence hunting and discovery pressures from temperature/day length); wolf attacks
  and taking pups now derived from state instead of fixed dice.
- Inter-camp war found: camps of one people treated each other as outsiders (weakest
  violence taboo) → raids in years 1-2 killing up to a third of a world. Fixed: same-people
  camps judged by real relation (kin/friend/neutral); raids on own people's camps weighed
  as Neutral; starting kin camps share the founders' culture and know each other (40% of
  adult pairs acquainted). New test CampsBelongToAPeople (13 tests).

## STATE AT COMPACTION (2026-10-06 morning)
- Violence: fights too lethal (36 killings / 15 y in one 70-person world, mostly in-camp food
  quarrels in hungry springs). Changed: loserWound 0.1-0.5 → 0.05-0.3 (× 0.5+strength);
  grievance toward a target discounted by their injury; raids on own-people camps weighed by
  CampRelation (most common relation to their adults). v28 running to test.
- Pairing: in small worlds young women 0-25% partnered while men 85-100% (monogamy + losses of
  men). User: monogamy needn't be hard-coded → plural marriage as learned custom (queued).
- Gender roles: emergent (traits, interests; nursing/pregnancy cost of hunting); only biological
  sex rules hard-coded (fertility ages, nursing).
- Next steps and gotchas: see memory file anthro-overnight-queue.md (mirrored here):
  1 analyse v28; 2 famine attribution/brakes; 3 plural marriage; 4 rest of audit; 5 tests + docs.
- Runner: --kin N (kin camps), map line shows dogs. Demog tool in session scratchpad
  (args: seed years [kinCamps] [grant,list]).

## 2026-10-06 day → 2026-10-07 overnight (after compaction)

### Starvation: diagnosed, then fixed by behaviour (not by tuning)
- Diagnosis (weekly trace of the worst world): nearly all starvation in 3-4 bad springs; nobody hunted
  in winter (trips needed 6 h of daylight; store-up pull for hunting only with drying), hunger ignored
  stores until <6 days left, no rationing, 150% "feasting" applied to winter nuts, snow blocks roots
  until ~spring day 18. Babies die because mothers' milk fails when the mothers starve (realistic).
- Fixes: stores judged against the learned lean stretch (incl. rest of this season); worry pulls to
  work; rationing spreads stores over the stretch while the land isn't keeping up (floor ½, strictness
  now a learned custom); store-up pull applies to hunting/fishing whenever meat will keep (frozen);
  trips sized to daylight (3 h gathering/fishing, 4 h hunting); eating past need only for food that
  would rot or true surplus; drying 3× faster + seeing rot urges preserving.
- Cross-camp food asking: hungry people visit camps where they have ties and ask; hosts give or
  refuse by their learned giving per relation; gifts carried home; refusals → grievance at that camp
  → raids. In use: 1,000-5,800 asks per world per 40 y, ~20-30% answered.

### Plural marriage (learned custom, biology fixed)
- Person.Spouses / Wed / Unwed; ConsiderAnotherSpouse (desire incl. provider standing, own learned
  view "wed:more", spouses' hurt), LearnFromPluralMarriages (yearly: hearth fed vs camp, own children,
  strife). Biology at the decision: a co-husband stakes paternity, a co-wife stakes provisioning;
  jealousy scales with what's at stake. Result: polygyny now more common than polyandry (5-7 vs 2-4
  hubs per world); every people so far comes to disapprove (views −0.1 to −0.8).

### Fights (Fights.cs, user-approved design)
- Intent per fighter (show / hurt / kill) = learned bound for that relation (`bound:kin…outsider`,
  seeded per people) + rage (scaled by grievance on the scale of a life) + a little desperation −
  love, kinship, guilt. Exchanges (≤3), resolve vs fear, yielding/fleeing, onlookers join (only if
  willing to hurt) or make peace (`norm:peacemaking`), morale breaks (`norm:honour`), weapons,
  pursuit only by those meaning to kill, fallen not struck except by them, badly wounded can't fight,
  anger spent after fighting it out, killings shift the culture's bound (feuds can normalise killing).
- Iterations (all logged in chronicle/debug): 15-dead pile-ups → onlooker restraint → hunger no longer
  drives kill intent → gang-theft loop (929/yr; rally cooldown 7 d, taking cools anger) → outnumbered
  families give way → killing blows were landing on people already badly hurt (29 of 41) → fixed.
- Result (v57, 10 worlds × 40 y): 94 violent deaths (old system 146): 7 worlds ~0, 3 violent (17-50),
  every killing by someone who meant to kill. Blood-feud chains appear in violent peoples.

### Settled life (#6)
- Fish runs follow river warmth (Cell.WaterTemp), not calendar days.
- Investment holds a camp: stores too heavy to carry, houses (hut/pit), tended grain, hand stencils.
  Moving leaves behind what can't be carried (short moves allow up to 4 trips; caches keep half);
  each voter weighs their own family's loss.
- Threat: after a raid, splitting off needs more people and bigger camps look safer.
- Gatherings (Gatherings.cs): seasonal feast in plenty binds near-strangers (ritual, rites, ochre,
  story multiply it), lowering crowding strain.
- Grinding now matters: raw nuts 0.75, leached 1.0, pounded ×1.3, mortar ×1.15; wild grain 0.7 until
  ground (slab ×1.3). Pit-fired pots keep stores; ochre.hand adds attachment; ochre.paint/figure bind
  gatherings; forage.lore = healing plants (care for the sick 1.6× as effective).

### Customs (#5, Customs.cs): rationing, demand (when asking is ok), elders' voice, hunting prestige —
seeded per people, learned/spread. Decision audit: docs/AUDIT-DECISIONS.md.

### Discovery pace: DiscoveryRate 6e-7 → 1e-7. Without help ~3 stages widely known per 40 y (target 1-3).

### Tech review (controlled: founders granted each tile, 3-4 seeds × 15 y, vs none)
- Large (+35 to +58 people): spear-thrower, bow, fishing, bone (harpoon), better spears, shelter, traps.
- Moderate (+15 to +30): wood, baskets, grain, dogs, hide, drying, cordage, fire, cooking, hunting
  tactics, burial, storage. None/negative: wayfinding (mobility, not food), grinding (fixed since),
  forage lore (medicine added since), camp, ochre, pottery, language (social/other kinds of effect).
- Settling toolkit at start (v37t/v57t): pops 2-3× (150-280), fish 63-85% of food, but 9-19 camps:
  density rises, concentration doesn't yet.

### Other
- Region.AddKinCamps: other peoples start with kin camps too (single-camp peoples weren't viable).
- Tests: +MarriagesAreMutualAndAmongTheLiving, +PeoplesStartWithTheirOwnCultures (15 total).
- Diagnostics: ANTHRO_DEBUG_FIGHTS (every fight + killing blows), ANTHRO_DEBUG_KILLS, ANTHRO_CHRON
  (regex chronicle), ANTHRO_TRACE (weekly spring trace), ANTHRO_OFF (feature switches for ablation).
- Reflection vs the original plan: docs/REFLECTION.md.

### Final validation (late overnight): a population regression found and fixed
- A 20-world head-to-head (seeds 11-30, 40 y, 3 kin camps) of the old build (v37) vs tonight's
  (v57) showed final population halved (1,261 → 608) and births −35%, with starvation unchanged.
  Ablations on 20 worlds each: moving costs (stores/houses/loss-weighing), feasts, nut/grain processing,
  fish runs: no effect. Customs off: recovered. Split: **demand sharing** was the cause: adults spent
  1.2 h/day asking (vs 0.2), because asking kept a baseline pull and the custom opened asking to
  families with 3+ days of food. Asking moves food, doesn't make it: everyone thinner, fewer births.
- Fix: asking/taking driven only by need (no baseline); the custom sets how hungry one must be before
  asking, and asking stays for families under 2 days of food. Asking now 0.22 h/day.
- **Final (v61) vs old (v37), same 20 worlds:** population 1,100 vs 1,261; births 2,861 vs 2,989;
  starvation 1,570 vs 1,395; violent deaths 137 vs 397; violent worlds 2 vs 7.
  Tech start (4 worlds): ~7 camps (was 12), main camp holds 50% of people (was 31%), population
  128 vs 208. Two peoples (4 worlds): 73-110 at year 40.
- Lesson: compare builds on ≥20 worlds; 6-10 worlds can't detect 20-30% effects.

## 2026-10-07 day: base-sim readiness (state at compaction)
- Speed +18% (hunt target Outlook hoisted; per-hour decision caches AdultsNow/MemberById; allocation-free gossip).
  ~3 s per simulated year; a 150-y world ≈ 8 min; real-time target has ~15× headroom.
- Fights end of themselves (exhaustion feeds fear; loop guard 40 only). Violence seeds wider.
  Cultural conflict forms: `norm:sides` (taking sides → brawls/feuds), `norm:duel` (one-on-one, anger
  spent more, honour to winner), Ambush (attack a lone person away from camp; unseen; family blames a
  suspect), side-taking feeds gang joining. 20 worlds × 40 y: conflict in 15, killings in 10, forms vary.
- Gang-theft cooldown removed (no regression). Gatherings driven by a growing urge + plenty.
- Weirs are built structures at river cells (Cell.Weir; ~12 person-h; decay unless used); count in
  Investment. Tech-start worlds: camps stay ~5 y at a site; ~90 weirs.
- Tech audit: docs/TECH-SETTLEMENT.md (settlement forms; effect kinds; usage verified: resilience
  techs cut bad springs 23 → 1-9).
- 150-y runs found: rationing unlearned by summer spoilage (fixed: only rotted stores unteach it; still
  drifts to ~0.3), peoples never splitting (rift now uses full culture distance). After fixes: median pop
  at y150 84 (was 46), 1 of 20 extinct, 3 of 20 worlds with 2 peoples.
- Next: rationing drift, rerun tests, light two-peoples check, remaining hard-code items, readiness review.

## 2026-10-07 evening: rationing analysis
- Why it drifted (20 worlds × 50 y, lesson counters): old rule raised the custom whenever anyone got
  thin (0.1/season) and lowered it whenever stores rotted while nobody got thin (0.05). Down lessons
  outnumbered up 85k to 23k, and 68% of downs came from families that never had to budget at all.
  Balance point of those two flows = 0.35, which is exactly where it settled (0.32).
- Fix (`Customs.LearnRationing`): only seasons where the family actually had to budget teach anything.
  Budgeted + ran out + went thin → pinch harder (0.1). Budgeted + stores rotted → pinched for nothing,
  as strong as the share of the store that rotted that season (0.1 × rot share). Old rule kept behind
  `ANTHRO_OFF=rationlearn`. Result: custom holds ~0.55 (worlds 0.30–0.70) instead of falling to 0.3.
  Better storage (less rot) now makes budgeting worth more, so the custom should rise with it.
- But rationing barely matters yet: budgeting families hold back only 3–5 rations/person a season
  (need ≈ 80), and 74–83% of those budgeting in winter/spring run out anyway. Keeping food is very
  unequal (winter median 14/person, mean 76, top 10% 248). Ablations (never ration vs always ration
  hard): pop 823 vs 709, starvation 1731 vs 1597. The trade is fewer starvation deaths for fewer births,
  and it is within world-to-world noise.
- World-rule gap found: stored and dried food rot at the same rate in deep winter as in summer
  (0.4%/day, ~30% a season with no storage tech). Hoarding families lose more to rot in winter (27 per
  person) than everyone saves by rationing. Proposed: make rot temperature-dependent (not changed yet).
- Diagnostics: `ANTHRO_DEBUG_RATION` (per-household season budgeting lines); `ANTHRO_OFF=norationing`
  (strict 0) / `rationing` (strict 1).
- Tests: 15/15 pass on current code.
- Noise check on 20 fresh worlds (seeds 61-80): the rationing fix costs ~13% population on both seed
  sets (1018 → 882): holding the custom higher trades starvation deaths for fewer births while stores
  are small. Decision pending with the user (keep / learn from rationing's cost / fix winter rot first).

## 2026-10-07 evening: gang thefts, restlessness, marriage reviews (build v77)
- Gang thefts (taking a hearth's food by force) ran hourly in some worlds (one world: 3,836 in 12 y,
  500 in one autumn, mostly summer/autumn plenty). Causes: (1) the learned "taking pays" expectation and
  the side-taking custom were added regardless of any reason, so after a few successes people raided
  while full and at peace; (2) the leader's fear ignored their own wounds and weighed defenders weakly,
  so the beaten tried again the same hour.
  Fix (`JointActs.RallyValue`, `RallyFear`): a motive (anger at the family, or hunger with food over
  there) scales what taking brought before; side-taking applies to joiners only; fear = defenders'
  might vs one's own (as defenders weigh raiders) + own wounds. Switches `rallymotive`, `rallyfear`.
- Restlessness now comes from any discontent (feuds, hunger, overwork, crowding), not crowding alone
  (`restless` switch). Small effect alone (gang −5-10%, pop slightly up).
- Marriage reviews are event-driven: a spouse takes stock after a season that left them thin, and
  couples childless for 4+ years take stock when a child is born in camp; a plural marriage is judged by
  the camp at births in it and when its hub has a hungry season. No calendar, no dice.
- Results, 20 worlds:
  | | gang fights | violent deaths | pop | 2 peoples left |
  |---|---|---|---|---|
  | one people 50 y, v72 | 15,165 | 305 | 692 | |
  | one people 50 y, v77 | 614 | 122 | 871 | |
  | two peoples 40 y, v71 | 32,192 | 547 | 1,090 | 8 |
  | two peoples 40 y, v77 | 3,049 | 359 | 1,503 | 8 |
  Two peoples: raids between peoples rose 564 → 979; ambushes steady; killings in 18/20 worlds. One
  people: killings in 12/20 worlds, fight counts vary 0-351 per world (cultural).
- Tests 15/15 pass on v77.

## 2026-10-07: rationing fixed at the root (build v78)
- Keeping food (nuts, grain, dried) now rots slower in the cold: Q10 ≈ 2, rates as before at 20 °C
  (summer), ~0.44× at 8 °C, ~0.19× at −4 °C (`coldrot` switch). Hand-set: base rate, Q10 = 2.
- Rationing lesson is two-way: ran out with stores to spread → pinch harder (by what there was to
  spread); pinched and still have more than the lean time ahead needs, or watched the store rot →
  ease off; budgeted and came through → no change.
- 20 worlds × 50 y: pop 871 → 1,096 (lesson only) → 1,429 (both); starvation unchanged (~2,000);
  winter rot 28 → 6 per person; rationing settles ~0.33 (0.12–0.49 by world), lower where stores are big.
- Median winter stores unchanged (~14 per person) and ~3/4 of budgeting families still run out: the
  limit is now how much people put by, not rot (open question; also a natural lever for the god).

## 2026-10-07: elders and hunting customs (build v80)
- Elders' voice now learns from moves: at a camp move, how much more the old than the young voted for
  it is noted; a month on, people judge the move by how they eat. Moves the old pushed that paid off
  (or the young carried against them that went badly) raise the elders' weight; the reverse lowers it.
  Moves voted down teach nothing. Neutral on population; ends 0.45-1.56 by world. `elderslearn` switch.
- Hunting prestige tied to the share of food from hunting was tried and removed: it fed on itself
  (less prestige → less hunting → less meat → less prestige), fell to 0.1-0.27 everywhere, cost 27% of
  population and doubled gang thefts (hungrier). Ablations confirmed. It stays a cultural custom
  (inherited, spread by talk) — a natural lever for the god.
- Weir build/decay rates stay hand-set (physical facts), disclosed in AUDIT-HARDCODE.md.

## 2026-10-07: readiness review (build v80; 20 worlds × 150 y, 20 two-peoples × 40 y, 10 tech-start × 40 y)
| # | Requirement | Status |
|---|---|---|
| 1 | Persist, but survival hard | ✅ median pop 70 → 136 at y150 (slow growth ~0.45%/y); 0/20 extinct, 2 worlds near collapse (8, 21 people); starvation 8,612 deaths. Watch: a little easier than before (was 1/20 extinct). |
| 2 | Slow advance without help | ✅ median 4 stages known by y40, 10.5 by y150. |
| 3 | Tech → bigger, more settled camps | ✅ to the forager ceiling: tech-start main camps 40-110 people, ~6 y at a site, 135 weirs; towns wait for agriculture by design. |
| 4 | Food and starvation understood | ✅ rationing explained and fixed at the root. Open: families put little by (median ~2 weeks into winter). Rationing drifts to 0.12 by y150 (it doesn't pay with small stores). |
| 5 | Ecology stable | ✅ deer/boar/rabbit/wolf medians stable or up over 150 y; deer depleted in 2 high-pop worlds (hunting pressure). |
| 6 | Cultures vary for logical reasons | ✅ violence by culture: one people killings 14/20 worlds; two peoples 18/20, raids between peoples 465, gang thefts 32k → 3k. |
| 7 | Nothing hard-coded that should be learned | ✅ remaining hand-set numbers are physical/grounded and disclosed (AUDIT-HARDCODE.md). |
| 8 | Player scale (3-5 peoples) | ❌ no people split in 150 y (0/20; was 3/20): less bad blood now that the gang loop is gone, customs drift together. |
| 9 | Stable over generations | ✅ no runaway or collapse trend; cultures don't converge to extremes (except rationing drifting low). |
| 10 | Fast enough | ✅ ~2.8 s per simulated year at ~70 people. |
Tests 15/15 pass on v80.
