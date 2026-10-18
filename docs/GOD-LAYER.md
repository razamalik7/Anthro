# God layer v1 — design (PROPOSED, awaiting owner approval)

Sources: `PLANNING.md` §3 (player role), §5 favor economy, §6 religion by misinterpretation; `REVIEW.md` P0.
Everything here runs on machinery that already exists: learned expectations, deeds remembered by witnesses,
gossip, inheritance, the decision formula, the chronicle. Nothing is scheduled; no trickle.

## 1. Belief lives in people
Each person holds learned topics (`People/Expectations.cs`), spread by talk (`SocialLearning`, automatic for
any topic), passed mother → child and to new kin camps (add the `god:` prefix to
`Society.InheritSocialExpectations` and `Region.AddKinCamps`):

| Topic | Range | Meaning |
|---|---|---|
| `god:real` | 0-1 | that there is a power that hears them (the believer count = people with > 0.5) |
| `god:answers` | 0-1 | expected chance a plea is answered (learned from their own and witnessed pleas) |
| `god:kind` | -1..1 | expected temper: gifts vs punishments seen |
| `god:<act>` | -2..2 | what the god is believed to think of an act (`god:take`, `god:give`, `god:attack`, `god:hunt`, …): learned by misinterpretation (§5) |

Seed per people (like `Customs.SeedCustoms`): `god:real` low (~N(0.2, 0.1)); vague spirits, nothing known.
**Faith fades without signs**: `god:real` relaxes slowly toward its seed when nothing striking happens (an
expectation that isn't fed decays, the same way danger fear calms). **Doubt** = low `god:real`, spread by talk
from doubters like any belief (atheism weakens the god because unbelievers don't pray or offer).

## 2. Acts of belief (new verbs, chosen by the Decider)
- **Pray** (1 h, at camp, seen by others): value = need beyond one's means (hunger with little food, own or a
  child's sickness, grief after a death, fear after an attack) × `god:real` × `god:answers`, against the best
  alternative. A prayer **is a request** (§3).
- **Offer** (give up food/goods at a place): value = belief × expected favour return; offerings sized by the
  giver's means (what it costs them is what it's worth).
- **Rite** (group, at gatherings, needs `lang.ritual`): a joint act (reuse `Gatherings.cs` / joint acts), bursts
  of favor; shared rites already give cohesion.
Favor = what these acts give, weighted by the doer's belief. **Cap** = Σ believers' `god:real` × k. No baseline
trickle: low belief spirals down.

With no god acting, pleas go unanswered → `god:answers` falls → praying stops. So the no-god baseline stays
the base sim (acceptance check for every task).

## 3. Prayers as requests
A plea creates a `Prayer {who, need, place, day}` shown to the player. It stays open **while the need lasts**
(the hunger, the sickness, the danger), not on a timer. It resolves when the need ends:
- need met after the plea (by the player's power **or by chance**) → the asker and witnesses learn
  `god:answers` ↑, `god:real` ↑ (coincidences give credit for free);
- need not met (person dies, starves on, gives up) → `god:answers` ↓; the grieving may turn to doubt.

## 4. Powers v1 (player only creates natural things)
| Power | Acts through | Cost (placeholder) |
|---|---|---|
| **Omen** — a sign in the sky/land at a place (light, birds, a burning tree) | witnesses; pure meaning, feeds §5 | 5 |
| **Bounty** — plants or animals flourish in an area | cell stocks / `Ecology/EcologySystem.cs` groups | 20 plants · 30 animals |
| **Weather** — soften or harshen a season | `World/Climate.cs` (winter severity, rain, mast) | 60 / season |
| **Blessed birth** — shape an unborn child's traits | `Simulation.NewChild` trait inheritance | 40, a few per generation |
| **Fork nudge** — push someone at a life branch point (marry/leave/fight/join a raid) | the person's choice in Decider/Family | 10 |
No direct healing, feeding or killing: the god shapes the world, people act. Big events (plague, quake)
are later.

## 5. Credit and blame by misinterpretation
When something striking happens — an omen, a plea answered, a bounty found, a hard winter, a sudden death,
a lightning-struck tree — each witness looks at the deeds they remember (`Person.Deeds`, last season) and
ties the event to the most salient one (recent, strongly felt, involving people they care about or resent).
Good event → `god:<that act>` up; bad → down; weighted by belief and credulity (`MindTrait.Credulity`).
Example: the sky burns the day after Ka took food → "the god punishes taking" (`god:take` −).
In the Decider, a believer adds `Felt(god:<act>) × god:real` to that act's value: beliefs change behaviour,
spread by talk, are inherited — that's the seed of religion (later: `god:` lessons hardening into `norm:`
customs and taboos, prophet).

## 6. Reputation, last embers, loss
- **Reputation** (kind ↔ cruel, never spent): what the god's acts did to people (bounty, eased winters =
  kind; harsh weather, deaths = cruel). Kind: blessings cheaper; cruel: wrath cheaper, fear offerings bigger.
- **Last embers**: when favor is ~0 and believers are few, in a crisis a lapsed believer may still plead; an
  answer gives outsized belief. About 3 ignored embers → the god **fades** (loss). Extinction is the other loss.

## 7. Open points for v1 (proposed answers)
- Limits on big powers: favor cost + reputation; Weather at most one season ahead. · Skill tree: hidden in
  v1. · Rival/false gods: not in v1.

## Hand-set numbers (to disclose)
Power costs and start pool (~50, from PLANNING); favor per act; cap factor k; belief learning rates; fading
rate. All behind `ANTHRO_OFF` switches (`god`, `pray`, `offer`, `rite`, `credit`, …).

## Decisions 2026-10-08 (owner + Claude, after reviewing T0-T8)
- **Testing switch:** `ANTHRO_OFF=god` must reproduce the pre-god game exactly; the god layer has its own RNG.
  "People remember their own deeds" is a base-game change with its own switch (`selfmemory`), analysed
  separately; leaning toward keeping it.
- **Idle god:** with no god acting, people may still pray and offer (realistic); measured, not hidden.
- **Prayers** end by outcomes, not clocks: need met while alive (power or luck) = answered, weighted by how
  surprising the relief was; asker/child dies = failed; unanswered pleas erode `god:answers` daily (faster
  when desperate) until the person gives up; signs and blows while a plea is open count. No grief pleas for
  now. Need is continuous severity, not hard gates.
- **Nudges** add weight to the decision the person is already making (belief × credulity, maybe favor
  tier); no extra dice; the UI shows how close the decision is. Following a nudge is itself a lesson.
- **Kind/cruel is the god's style, not the player's skill.** A kind god can harm (dependence), a cruel one
  can help (fear curbs theft), if it emerges and is explainable. Gods are judged on several dimensions, not
  population. A separate "skilful" bot is what should improve survival odds.
- **Reputation-unlocked direct acts (v1: Heal, Lightning):** unlocked by strong kind/cruel reputation plus
  enough believers; expensive, limited per generation, deterministic, natural events; the most striking
  events for credit and blame (smiting the innocent costs believers; healing a killer may teach that the god
  favours killers). More such acts later.

## Decisions 2026-10-12 (owner + Claude, after reviewing T9C)
- **A god's character is a pattern, not a label.** What it responds to and how (rewarding sharing, demanding
  offerings, destroying until appeased, punishing wrongdoing, acting at random); people learn those patterns,
  truly or falsely, and that is their religion. Kind/cruel never means success or failure.
- **Two measures:** help vs harm (what the god did to people; drives Heal/Lightning unlocks) and what people
  believe the god wants (the `god:<act>` profile per people).
- **Learning from relief:** besides "good followed X", people learn "bad stopped after X" (dread that lifts after
  an appeasing act). This is how fear offerings arise under a malevolent god.
- **Where offering comes from:** mainly as an escalated plea in a desperate, unanswered crisis; grave goods
  (with burial rites) make it likelier; a small cultural seed varies by people. Sharing food teaches "the god
  likes generosity" (`god:give`), not offering.
- **No offering timer:** offerings stay open until a sign; hope erodes daily, like prayers.
- **Skilful bot:** audit its favor before tuning. Not merging to master until this round is smoothed.
- **Offerings are shared feasts (2026-10-12):** most offered food is eaten by those present (bonding like a
  gathering); only a small part is burned or left for the god, and favor comes from that part.
- **Favor pacing target (2026-10-12, for a faithful band of ~30, scaling with believers):** small acts (omen,
  nudge) every week or two; medium (bounty) a few times a year; large (weather, heal, lightning, blessed birth)
  about once a year or rarer. Cap and income rates are set to hit this pace.

## Decisions 2026-10-13 (owner + Claude, after M2)
- **Everyday devotion:** believers pray and give thanks in good times too; a learned habit (`god:thank`). Easy
  times may thin out devotion, emergently (surprise-weighted credit), never by rule.
- **Favor bank leaks instead of capping:** favor above what believers sustain decays slowly; no hard clip.
- **Belief is living:** strength (skeptic / open / believer / devout, as reporting bands) and content (`god:<act>`,
  kind, answers) per person; diversity from temperament, experience and personal shocks; the devout teach more
  strongly. One generation can take people from "nothing" to "something's out there"; a full religion (shared,
  stable content and practice) takes generations. Belief fades within ~a generation when the god goes quiet.
- **Targets after ~25 years with a responsive god:** most open or believer, ~10-20% devout, a lasting skeptic
  minority ~10-20%; belief content never unanimous and different between camps.
- **Pacing scales with faith:** new god ~1 small act a month; faithful band ~weekly small, a few medium a year,
  ~1 large a year.
