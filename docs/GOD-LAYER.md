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
