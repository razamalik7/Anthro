# Anthro: Review (2026-10-04)

Where we are against the original vision, what's left, in what order, and an audit
of everything still hard-coded or unnatural.

---

## 1. The vision (from PLANNING.md)

Started from one question: **how would isolated cultures develop without
colonization?** Would people naturally discover the same things (destiny) or not
(chance), and how alike or different would they become?

The game: you're a **new god** who can't act directly. You shape nature, nudge
people at turning points, shape some births, speak through a prophet, and are
**interpreted, often wrongly**: religion forms from misreading you. Progress comes
through a **discovery web** whose conditions have to converge (the core puzzle).
There are many kinds of "advanced". People are **minds** you can inspect. Grounded
biology with speculative outcomes. Worlds are procedural and not Earth (alien and
fantasy fauna in scope). Arc toward modern-ish across islands that eventually
meet.

**Pillars:**
1. Convergence, not unlocks.
2. Many kinds of advanced.
3. Influence, not control.
4. People are minds.
5. Discover alongside them.

**Principle:**
- Nothing scheduled, nothing global.
- World facts are grounded in real data.
- Valuations come from the person and their culture.

**Target:** a real game by about summer 2027, solo plus AI. One island is the
floor; the fuller vision is the stretch.

---

## 2. Where we are

| Area | Status | Notes |
|---|---|---|
| World & ecology | **Built, calibrated** | 40 km map, real animal densities, plants with seasons, rivers with fish runs, regional pools, animal fear |
| Bodies, disease, demography | **Built, calibrated** | Every death has a cause; child mortality, birth spacing, fat reserves and fertility match forager data |
| Food economy | **Built, mostly calibrated** | Foraging, hunting (thrusting + drives), fishing, cooking, preservation, storage, firewood |
| People / minds | **Built** | Traits, interests, skills, learned expectations, memory of deeds, grievances, drives (stage 5) |
| Social layer | **Built (stages 0-5)** | Primitives + learning, group force, raids/feuds/encounters between bands, jealousy, separation, learned leadership, evolving values |
| Multi-band world | **Built** | Every band real; splits, loners, visits, intermarriage |
| Discovery engine | **Built** | 97 stages annotated; found during work; spread by talk, watching and visits; lost with the last knower |
| Discovery *effects* | **Partial: 30 of 97** | Cooking, drying, storage, weapons, fishing, hide/shelter, snowshoes. **No effect yet:** language, cordage, baskets, traps, bone, wood, grinding, dog, burial, ochre, pottery, grain, camp patterns, fire.* |
| Language tile | **Not functioning** | Should govern how fast knowledge and beliefs spread |
| **God layer** | **Not started** | Favor, prayers, omens, powers, branch-point nudges, blessed births, prophet, reputation, last chances |
| **Belief / religion** | **Not started** | Only three abstract norms exist (sharing, valor, peace) |
| Evolution | **Partial** | Inheritance with drift, culturally shaped attraction. No trait pathways, no compressed-evolution mechanic |
| Domestication / agriculture | **Not started** | Stages exist in the web only |
| Hierarchy, economy, trade | **Not started** | Gifts and learned leadership only |
| Islands, sailing, first contact | **Not started** | One 40 km map |
| Alien world / fauna | **Not started** | Earth-like temperate valley |
| Unity presentation | **Not started** | Assets bought |
| Codex, notifications, mind view, save/load | **Not started** | Chronicle exists and can feed notifications |
| Speed | **Okay for headless** | ~4 s per simulated year at ~150 people. Real time needs headroom for more people and the god layer |

### Honest assessment

The substrate is now deep and fairly true to life. Worlds genuinely diverge:
peaceful vs. feuding bands, sharing vs. taking cultures, different values. That
already answers part of the founding question.

**But the game itself, the god layer, hasn't been started**, and it's what makes
this a game rather than a simulation. The first milestone said "4-5 god powers,
belief forming from your actions", and neither exists. With about 8 months to
the target and art as the expected bottleneck, the next stretch should shift
from deepening the simulation to the player's side of it.

---

## 3. What's left, in priority order

### P0: make it a game (next)
1. **God layer v1 (headless):**
   - Favor from prayer, rites and offerings.
   - Prayers as requests to answer or ignore.
   - Belief and doubt that spread socially.
   - First powers: place resources and animals, omens, branch-point nudges,
     blessed births.
   - Reputation (kind or cruel), last chances, fading.
2. **Religion by misinterpretation:** people link omens and events to recent
   deeds using the same association learning the social layer already has
   ("the sky burned after Ka took food: the god punishes taking"). Beliefs
   become customs and taboos through the same priors and gossip.
3. **Language tile with real effects:** teaching and gossip rates depend on
   language stages, and stories carry beliefs.

### P1: make the web a puzzle
4. **Effects for the remaining 67 stages**, so convergence matters:
   - Cordage → nets, snares, hafting.
   - Baskets → gathering capacity.
   - Traps → steady small game.
   - Dog → hunting and alarms.
   - Pottery → boiling and storage.
   - Grain → a farming path.
   - Camp patterns → staying longer.
   - Burial and ochre → belief and cohesion.
5. **Discovery depth:** OR routes and variants, a deliberate "tinker/experiment"
   activity for curious people, real accident events (and omens can steer
   people toward them).
6. **Codex and hints** (the player's guidance).

### P2: make it visible
7. **Unity vertical slice early** (simple shapes): one band, people doing verbs,
   chronicle notifications, the mind view, god powers on the map. Art is the
   bottleneck, so start soon.
8. **Speed for real time:** profile with the god layer in; target a full
   generation in about 20 minutes at 1×.
9. **Save/load.**

### P3: breadth (stretch, in dependency order)
10. Domestication (dog first), then agriculture routes.
11. Hierarchy, economy and trade (gifts → exchange → specialists).
12. Islands, sailing, first contact.
13. Evolution pathways and the compressed-evolution mechanic; alien fauna.
14. Final calibration pass (land richness, populations, discovery pacing).

---

## 4. Hard-code / unnatural audit

Grouped by how much each goes against "nothing scheduled, nothing global;
valuations come from people". Severity: **High** = an authored outcome or rule;
**Medium** = a global constant where a person or culture should decide; **Low** =
a world fact or a deliberate design knob (fine, but documented).

### A. Authored lessons: High
The band is *told* what to learn by code, instead of learning from outcomes:
- `Simulation.cs:323-324` Hard times push the sharing norm up; good times push it
  down slightly.
- `Simulation.cs:615` A starvation death raises sharing.
- `Simulation.cs:638` A death by an animal lowers valor.
- `Simulation.cs:974` Big kills raise valor.

The three norms (sharing, valor, peace) are now partly redundant with the learned
act expectations (giving, taking, attacking), and several choices read both.

**Fix:** retire the norms as separate levers. Sharing becomes what people expect
giving to bring; peace becomes what they expect attacking to bring; valor becomes
the learned standing that bold hunts and fights earn. Remove the BandLearns hooks.

### B. Fixed bars on choices: High
Joining, escalating or pairing is decided by comparing a score to a constant,
instead of comparing it to what else the person could do:
- Raid joining, defending, war parties > 0.6 (`JointActs.cs:65, 73, 138`)
- Hostility when meeting on shared ground > 0.6 (`JointActs.cs:195`)
- Escalating a confrontation > 0.4 (`Society.cs:485`)
- Pairing > 0.35 (`Visit.cs:94` and Socialize)
- Separation gap > 0.3 (`Family.cs:50`)

**Fix:** compare against the person's best alternative, as hunt recruitment
already does (`Decider.BestAlternative`). One rule everywhere: people do the
thing that beats what else they'd do.

### C. Behavior on a dice timer: Medium-High
- `Family.cs:50` Separation is a 0.5%/day roll. **Should** be a choice when an
  occasion arises (a fight, a new attachment, a hungry season).
- `Family.cs:27` Jealousy is noticed 15% of the time. Fine as perception, but it
  should use the same witness model as deeds.
- `JointActs.cs:146` 60% of a raided camp wakes. Should depend on dogs, watch
  customs and the time of the raid (later stages).
- `Simulation.cs:997` Wolf attack 25%/day when the conditions hold. Acceptable
  (animal behavior), but could follow pack hunger.

### D. Fixed amounts: Medium
- A take is at most 30% of a store (`Society.cs:442`); raids seize 60% or 50%
  (`JointActs.cs:93, 165`).
- "Spare" is anything beyond two days of food (`Society.cs`).

**Fix:** physical carrying capacity per person (about 20-30 kg, roughly 10-15
rations; more with baskets and nets, which also gives baskets an effect), and
"spare" should come from the household's own learned lean-season expectation.

### E. Prestige formula: Medium
- `Hunt.cs:419` Killer prestige increases by meat/100.

Standing should be how others regard someone (learned per viewer, like trust),
not a global number. The new learned-leadership expectations ("follow:<id>")
should replace it.

### F. Age gates: Medium
About 34 places use fixed ages (14 or 16 for adult choices, 6 for witnessing). Some
already use `Physiology.Maturity`.

**Fix:** body maturity for physical things, and a cultural "grown up" that bands
can differ on (initiation rites could set it later).

### G. Hand-set weights inside valuations: Medium (partly inevitable)
- `Decider.cs`, `Society.cs`, `JointActs.cs` contain many coefficients (0.3, 0.5,
  ...).
- Stage 5 drives now scale whole categories per person, so the *balance between*
  food, safety, family, standing, comfort and company is personal and evolves.
- The weights *within* a formula are still global.

**Fix later:** let drives and learned expectations carry more of each formula.
Accept that some structure has to be authored, and keep it documented.

### H. Knowledge omniscience and shortcuts: Medium
- `Region.KnownRange` (15 km): everyone knows where every band within 15 km is
  camped. Should come from visits, encounters and gossip.
- `Inventing.cs` materials "at hand" checks the land within 3 km around camp,
  whether or not anyone has been there. Should come from place memory.
- Raids resolve instantly (no walk, no being seen on the way).
- Children stay with the mother on separation (`Family.cs`): an authored custom.
  Should be a choice shaped by culture (residence norm style).

### I. World facts and calibration knobs: Low (legitimate, keep documented)
- Cooking gains, fishing efficiencies, shelter protection, weapon range/power,
  disease biology, fish stocks: world facts, loosely grounded. Add source notes.
- Fitted to real targets: `PlantDensity` (1.0), `ChestRisk`, `WeaningRisk`,
  `FeverRate`.
- Pacing: `DiscoveryRate` (6e-7), a game-design decision for you.
- Founding culture priors (taboo on taking, generosity, retaliation, violence,
  drive mix) are random per band, which is the intended "culture before the story
  starts".

### J. Things that are fine as authored
- Perception rules, innate feelings (anger, guilt, fear, jealousy), physics of
  fights and food: this is the "thin biology" layer we decided to author.
- Starting knowledge, the discovery web content and its annotations: authored
  world content by design.

### Suggested audit order
1. **A + B** (authored lessons; constant bars, replaced by best alternative):
   most directly against the principle, and small to do.
2. **D + E** (carrying capacity; standing as others' regard).
3. **C + H** (choices on occasions instead of dice; knowledge from memory).
4. **F** (maturity and cultural adulthood).
5. **G** (ongoing, as systems get touched).

---

## 5. Recommendation

Spend about 1-2 days on audit items A + B (cheap, and they close the biggest gaps
with the principle), then **move to the god layer** (P0). The god layer needs
belief and association machinery that's mostly already there in the social layer.
Start a Unity slice soon after, because art is the bottleneck. Discovery effects
(P1) can then be filled in alongside the god work, since many powers are about
nudging discoveries.
