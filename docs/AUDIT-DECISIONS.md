# Decision audit: what's written, what's learned

Every choice a person makes is scored by a formula, and they do the best-scoring option. That is
fine as long as the formula describes **how a body, the world, or plain reasoning works**. It is
not fine when the formula quietly decides **what a culture values**. That part should be learned
(from what happens to people), spread by talk, and seeded differently per people.

Classes:

- **P — physics/biology.** How bodies and the world work (spoilage, hunger, fertility, strength). Fixed is right.
- **D — decision mechanics.** Plain arithmetic of choosing (compare options, spread stores over days, follow the freshest trail). Fixed is right.
- **L — already learned.** The culture-shaped part already comes from experience and talk.
- **C — cultural choice baked in.** Should become a learned custom.
- **W — hand-set weight.** A number that sets strength or pace. Disclosed; candidate for per-person or per-people variation.

Status as of 2026-10-06.

## Getting food

| Decision | Formula | Class | Notes |
|---|---|---|---|
| Which food work | need × learned food-per-hour × interest | D + L | Yields are learned per person. Weights 0.25 (room) and 1.5 (hunger) are W. |
| Hunting target, tracking | freshest sighting or trail; expected value | D + L | Tactics and animal danger are learned. |
| Store for lean times | stores vs the lean stretch they expect | D + L | Leanness is learned from having been thin. |
| Rationing | spread stores over the lean stretch while the land isn't keeping up | D + L | **Floor of half portions is C/W** → make "how tightly a hearth rations" a learned habit. |
| Eating past need | only food that would rot, or a true surplus | P | |
| Drying | surplus that would rot; urge from foresight and from seeing food rot | D + P | Work rate is W (now 15 per session, 30 with racks). |
| Daylight for trips | 3 h for gathering/fishing, 4 h for hunting, 6 h for long trips | W | |

## Sharing, taking, fighting

| Decision | Formula | Class | Notes |
|---|---|---|---|
| Give / refuse / ask | learned outcome for that relation + innate kin altruism, warmth, guilt + need | L | Seeded per people. |
| When asking is acceptable | only when hunger > 0.3 and under 2 days of food | **C** | Demand-sharing cultures ask freely; others only in need → learn it. |
| Steal | learned outcome (seen / unseen) − guilt, × how thin the family is | L | |
| Confront / attack | anger × temper + learned outcome − guilt − fear | L + W | **How far a fight goes is not modelled yet** → the new fight design. |
| Gang food theft (rally) | anger + hunger × plenty, learned outcome, fear | L | Brawl resolution is mechanics, rebuilt by the fight design. |
| Raid another camp | grievance at that camp + learned outcome − fear | L + W | Thresholds (anger > 1) are W. |
| Sharing a kill | each hunter's hearth hands out by learned giving | L | |

## Family

| Decision | Formula | Class | Notes |
|---|---|---|---|
| Who pairs | mutual best choice: fondness, attraction (camp's tastes), provider standing | L + W | Attraction tastes drift per camp. |
| Incest avoidance | no close kin | P | Biology (avoidance of those raised together). |
| Where a couple lives | weighing ties, prospects, residence custom | L | Custom forms from practice. |
| Who keeps the children | closeness + custom | L | Custom forms from practice. |
| Plural marriage | desire + learned view − biological stakes | L + P | |
| Jealousy | innate; scaled by what the body stakes | P | |
| Breakups | provider gap, childlessness vs bond | P + W | Could become cultural (some peoples divorce easily). Low priority. |

## Camp life

| Decision | Formula | Class | Notes |
|---|---|---|---|
| Moving camp | proposal, vote weighted by charisma + **age** + following | **C** | Whether elders' voices count more is cultural (gerontocracy vs not) → learn it. |
| Splitting | discontent = hunger + crowding + overwork + feuds | **C/W** | **Crowding tolerance (60) and overwork threshold (7 h) are cultural/social-tech** → tie to social tech and settled life (#6). |
| Joining another camp | ties, prospects, hardship | D + W | |
| Visiting | longing for ties, courting, hunger | D + W | 90-day longing is W. |
| Asking another camp for food | worry × ties × learned outcome of asking there | L | |
| Hunting earns standing | hunt counts 40% toward standing | **C** | Whether hunting is prestigious is cultural → learn it from admiration. |
| Drive weights | food, comfort, company, family, standing, safety | P | Per-person, inherited. |
| Hunger crowds out the rest | non-essentials fade with hunger | P | |

## Conversion list (C items)

1. Rationing strictness → learned habit. **Done 2026-10-07** (`norm:rationing` in Customs.cs; going hungry late in a season teaches it, watching rationed food rot unteaches it).
2. When asking is acceptable → learned. **Done** (`norm:demand`; answered asks teach it, refusals unteach it).
3. Elders' weight in camp decisions → learned custom. **Done** (`norm:elders`, seeded per people and spread by talk; no experience signal yet).
4. Crowding tolerance and overwork threshold → social tech and settled life. **Partly done.** Gatherings (Gatherings.cs) bind near-strangers, so crowding falls with ritual, rites and ochre. The thresholds themselves are still W.
5. Hunting prestige → learned. **Done** (`norm:hunting`, seeded and spread; no admiration signal yet).
6. How far violence goes, toward whom → **Done** (Fights.cs):
   - `bound:<relation>` per people;
   - `norm:honour` and `norm:peacemaking`;
   - learned from killings (AfterKilling).

All of these start from a random seed per people (a hand-set range, disclosed in code) and are passed
to children and to kin camps.
