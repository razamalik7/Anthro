# Anthro: Design Doc (planning v0)

How to read this:
- **Decided** = explicitly agreed by you.
- **(proposed)** = Claude's suggestion, waiting for your yes/no.
- **Open** = not discussed or not settled.

---

## 1. Why this game exists

Started from wondering how Native and other cultures might have developed
without colonization: whether people naturally discover certain things and
progress in certain ways (destiny vs. chance), and how varied and how similar
isolated cultures could become. An anthropological + biological evolution sim
about how cultures grow and advance, and how that shapes humanity itself.

## 2. Pillars (proposed: one-line summary of everything below)

1. **Convergence, not unlocks.** Big moments happen when many conditions line up.
2. **Many kinds of advanced.** No one right path; industrial is one shape of many.
3. **Influence, not control.** You shape the world and are interpreted, often wrongly.
4. **People are minds.** Individuals with traits, interests and beliefs drive everything.
5. **Discover alongside them.** The player learns how humanity works by playing.

---

## 3. Player role (Decided)

- A god who **just became a god** and is figuring it out (story frame).
- Mostly **cannot interact directly**; shapes the world and nudges.
- Base powers: place animals, natural features, resources; nudges; prophet;
  pre-birth shaping; life branch-point nudges.
- **Big events** (earthquakes, plagues, climate shifts) exist but are sparing,
  limited, and some unlock later.
- Powers **unlock** via belief (count and strength), how you're perceived
  (malevolent vs. benevolent gods get different options), and story progression.
- **Atheism weakens you.**
- God progression and society progression are separate but linked through religion.

**Prophet / vessel**
- Fully obedient vessel: gets your message **right**. The people who hear it may
  drift from your intent.
- **Start with contextual menu options.** Free typing later, with an LLM as a
  **translator only** (text → structured sim commands + sermon text).

**Shaping individuals**
- Pre-birth: personality, talents, physical traits.
- Frequency: **a few blessed births per generation, more with stronger belief**;
  prophet lineages / devout families may offer more.
- Life branch points: notification when someone hits a life-shaping fork; you can
  push one way.

**Open:** exact limits on big powers · god skill tree visible or hidden · rival /
false gods

## 4. Progression: the discovery web (Decided)

- Discoveries are **authored tiles** (fire, cordage, windmill, steam engine...),
  arranged as a **non-linear scatter/web**, not a tree.
- Each tile has condition rows: **materials, knowledge, pressure, person**. Rows
  accept **alternatives (OR)**, so cultures arrive by different routes and keep
  that flavor.
- All rows met → **possible, not guaranteed**; rolls when a qualifying person
  does related activity; must **spread** by teaching or it dies with them.
- **Contingent over convergent:** many paths up the same mountain, some dead ends.
- **Many kinds of advanced** (Pandora-style non-industrial included). Some
  branches may be **not 100% real**.
- Getting conditions to converge is **the core puzzle**.
- The web gets **drawn out** as its own design step.
- **Map format (Decided):** tiles are big skills; each has a description,
  how it can happen (alternatives in text), and stages inside. All lines
  solid, meaning "plays a part in."
- **Starting knowledge (Decided):** fire, knapping, wooden spear, foraging,
  language. **Language is a starting tile** whose stages govern how knowledge
  and beliefs spread (teaching by words, stories carrying beliefs).
- **Stages are real steps (Decided direction):** each stage inside a tile has
  its own requirements and its own unlocks. Routes/variants (e.g. sinew vs.
  plant cordage) unlock different things; having both unlocks more.
- **Discovery web v1 APPROVED (2026-10-01), incl. known stages shaping
  wood + windbreaks:** 24 skills, 96 stages (added Trapping, Bone & antler,
  Woodworking, Shelter, footwear, torches/lamps). Source of truth: `docs/discovery-web.json` (will feed the
  sim). Page: `docs/milestone-1-spec.html`, rebuilt by `python build-spec.py`.
  Map shows skills as lanes, stages placed by steps from start.
- **Accidents (Decided direction):** a general mechanism. Some stages are
  accident-prone under certain situations. The player can omen someone toward
  an accident for a free jump. Exact rules proposed in chat 2026-10-01. Omen as hint tool and the favor economy also Decided.

**Success:** dimensions + milestones, no single score. Any flavor can keep
advancing with clearly-forward unlocks. A maxed flavor gets acknowledged as
perfected.
- Dimensions (proposed): mastery of nature, knowledge, wellbeing, reach,
  complexity, continuity.

## 5. People & minds

**Decided**
- Each person's mind is a **map** of beliefs, learned things, traits, interests,
  disposition, stats, abilities.
- It gates which tiles they can trigger **and** drives their choices, learning
  ML-style.
- Clicking a person shows current thoughts and a visual of their mind.
- Personality depth ~ **Fallout Shelter**. Players will have favorites.
- People are not physically simulated (no Isaac / physics RL); bodies are
  puppeted, minds are simulated.

**Decided (was proposed)**
- Mind = a **learned graph** (readable nodes, learned link weights). Learning
  from experience, teaching, copying. Not a deep net, so it stays explainable
  and drawable.
- Layers: traits (fixed) · interests (slow) · skills (practice) · beliefs (fast) ·
  relationships · state (moment) · culture (shared, generational).
- Choices: need × expected result + interest + trait pulls + social pull −
  culture/taboo + noise.
- Traits change **how people learn** (cautious = weights bad outcomes;
  stubborn = updates slowly; sociable = copies more).
- Behavior expressed through **verbs** (forage, hunt, knap, teach, experiment,
  pray...) mapped to animations; the Sims approach to natural-looking people.

### Activities (Decided)
- Verbs are **step sequences** you can watch (hunt: gather party → track →
  stalk → strike → chase/finish → butcher & carry → share). Each step has its
  own odds and animation. Animals run their own loop (graze, watch, flee,
  fight back). Group roles by trait; kids learn by watching.

### ML stance (Decided, revisitable)
- ML = small live learners in minds (prediction-error updates, association,
  social copying) + evolution. No neural nets, no training, no GPU.
- **Keep open:** may revisit if we ever want a different human-mind system.
- Possible later: LLM prophet translator; optimization as a dev-time balancing
  tool.

### Band strength / deterrence (Decided idea, details TBD)
- A band-level strength metric (armed, healthy adults; fire; later
  fences/structures) decides whether predators **lurk** at the edges or
  **attack**. Dog domestication needs the lurking zone.

### Favor economy (Decided 2026-10-02)
- **Favor** comes from acts of belief: prayer (steady), rituals (burst),
  offerings, sacrifice (big; animal = a bit cruel, human = very cruel and
  innocent deaths cost believers).
- **Prayers are requests** shown as notifications. Answer (spend) to grow
  belief; ignore and doubt grows. Coincidences give credit or blame for free.
- **Cap** on favor grows with believers; **faith decays** without signs;
  **doubt spreads** socially (atheism).
- **Reputation** (kind <-> cruel, Red Dead honor-style) is a second meter,
  never spent: kind = blessings cheaper, cruel = wrath cheaper + more fear
  offerings.
- **No baseline trickle.** Low belief spirals. **Last chances:** in a crisis a
  lapsed believer may pray, bringing a *last ember* of favor to answer it;
  desperate miracles give outsized belief. ~3 chances, then the god **fades**
  (loss). Extinction is the other loss.
- Shared rites (any, incl. ancestor rites) give band cohesion; losing them
  makes a band fragile.
- Placeholder costs: omen 5, fork 10, land 20/40, animals 30, weather 60/
  season, blessed birth 40 (+ per-generation limit). Start pool ~50.

### Traits & interests (Decided 2026-10-02)
- **Mind (11):** curiosity, boldness, sociability, patience, openness,
  temper, warmth, credulity, charisma, aptitude, resilience.
- **Body (6):** strength, dexterity, endurance, cold tolerance, height,
  build (stocky holds heat but eats more). Body traits are where evolution
  lives; shown via Sidekick blend shapes.
- Inherited: parents' average + drift, occasional bigger jump; blessed
  births set them.
- **Mate selection included:** relationship + attraction + availability; kin
  excluded; attraction partly **cultural** (shared preferences), giving
  sexual selection. Tasteful: pair bonds and children only.
- **Interests (10):** stone & earth, wood, plants, animals, water, fire,
  people, spirits, sky, art. Science/philosophy later. Childhood weighs most;
  enjoyment and events shift them slowly.

### Sim structure added during build (Approved 2026-10-02)
1. **Regional populations + migration:** the valley is one corner of a region;
   species breed region-wide, arrive each spring at a random natural rate,
   migratory ones leave in autumn; a dangerous valley is avoided for years;
   kills drain the region.
2. **Animal fear:** hunted herds get warier and avoid camp.
3. **Physical hunting:** weapon range vs. flight distance, hits vs. size,
   wounded animals charge. Weapons from the discovery web change these.
4. **Bodies and disease:** no random death rolls; every death traces to a
   cause. Aging lowers capacity. Infants depend on nursing.
5. **Neighboring bands** wander the region; visitors bring partners.
6. **Courtship** through socializing; attraction partly cultural.
7. **Prestige** as a motive.

### Principle: nothing scheduled, nothing global (Decided 2026-10-02)
- No fixed-time events or schedules. Rhythms (meals, hunting cadence,
  gatherings) come from need, the body, and culture.
- Two kinds of numbers:
  - **World facts** (spoilage, animal size, disease biology) are grounded in
    real data where possible and tuned against realism targets.
  - **Valuations** (fear vs. hunger, glory, risk) come from the person
    (traits, situation) and their culture, never one global constant.

## 6. Society systems

**Decided as in scope** (deep-system status marked)
- Food system / agriculture: **near-universal, deep system, many routes**
- Domestication: **near-universal, deep system**
- Language: **near-universal, deep system**
- Hunter-gatherer life
- Hierarchies, economies, technologies
- Religion, beliefs, customs (religion forms by **misinterpreting the god**)

**Open:** how each works mechanically.

## 7. Biology & evolution (Decided)

- Base species: **Homo sapiens**.
- **Grounded rules, speculative outcomes:** traits evolve only under real-ish
  selection pressure over real-ish time; results can look alien.
- A **predetermined set of trait pathways** we design (e.g. amphibious, hairy,
  gliding).

## 8. Time & scale (Decided)

- **Variable speed + lineages.** Fast through quiet stretches, slow at key
  moments; follow families across generations. **No eras.**
- Start with **one band**, expand to **multiple societies on islands /
  mini-continents**, which discover each other and eventually sail.
- Arc toward modern-ish (steam, electricity in scope).

### Clocks (Decided)
- **Animation** clock: always natural speed.
- **Day/night** clock: visual rhythm; each visual day is a representative
  slice of life (length ~10–20 min, proposed).
- **Calendar** clock: ~20 min per generation at 1x; speed 1x–3x.
- **Pause the calendar** option: day/night keeps running, years stop.
- **Compressed evolution:** a trait appears in one odd child ("feathers,
  stage 1") and spreads over 10–20 generations. No deep-time skips in play.

## 9. World (Decided)

- **Invented / procedurally generated** worlds.
- **Not Earth.** Humanity evolving in alien-ish environments: dinosaurs and
  fantasy creatures are in scope (and can be domestication candidates).
- **Natural consequences** (proposed scope): IN = predator-prey populations,
  plant regrowth, overhunting, soil exhaustion, disease in dense settlements.
  OUT / faked = real climate & fluid sim; big climate events are scripted
  effects with simple rules.

## 10. Story, lore, discovery (Decided)

- Mysterious, vague lore (No Man's Sky style); present, never dominating.
- Leaning: an **old god** left hints, which explains why you're ahead of the people.
- Loose **prewritten characters** appear at points (Hades / Elden Ring style).
- **No objectives or waypoint markers.** Guidance = codex + lore hints + the
  player's own common sense. Hints say loosely *what*; the challenge is making
  it happen.
- Learning real anthropology through play is a goal.

## 11. Feel & loop (Decided)

- **Garden + puzzle + drama**, drama the smallest.
- A typical 10 minutes: notice a problem (cold, hungry) → check codex/hints →
  shape the world (place clay) → wait/watch → a person engages → branch-point
  notification → nudge → discovery or failure. Plus belief/prophet/story beats.
- Luck is OK in small doses, but **bad outcomes shouldn't feel unearned**. Most
  tragedies should trace back to something the player could have seen coming.

## 12. Failure & restart (Decided)

- Hard loss: **extinction**, maybe **losing all believers**.
- Soft failure: society locked in a bad direction (e.g. stagnant authoritarian);
  you must change a lot, even regress, to move forward.
- On extinction: mold new humans in the same world or start fresh. Wait time sets
  what remains: recent ruins (Rome) vs. fossils (dinosaurs).

## 13. Look & camera

**Decided:** see little people, not blobs (Planet Zoo / Clash of Clans scale) ·
zoom out to map and travel to other islands · click person/event for close-up ·
stylized art.

**Decided:** stylized **3D with a guided camera** (Option B); zoomed-out =
stylized map view, not a seamless globe. Main reason: evolving bodies are far
cheaper in 3D.
- **Click a person → Planet Zoo 2-style zoomed-in view.** A preset/fixed camera
  framing is fine.
- References: **Townscaper** for stylization; **The Sims** for how animation and
  people are handled.
- People: **stylized but human** (chunkier OK). Animations can be a bit
  videogamey as long as they feel natural, not janky.

**Assets on hand:** all Synty biome/nature packs + many animated animals.
Willing to buy more.

**Proposed asset direction**
- Commit to the **Synty POLYGON look**; everything matches what's owned.
- People: **Synty Sidekick** modular characters (Unity Humanoid rig, body
  blend shapes, facial expressions); a strong fit for evolving bodies.
- Animation: everything on the **Unity Humanoid rig**, so packs from any source
  retarget. Candidates: Mixamo (free), Synty ANIMATION packs, Kevin Iglesias
  Villager / Crafting animations. Craft-specific actions = generic work loop +
  hand-held prop.
- Make it our own: custom palette, lighting, post-processing so it doesn't read
  as stock Synty.
- **Buildings (proposed):** building = type (shelter, storage, shrine...) ×
  culture's available materials, so architecture diverges like tech does.
  Assets as **modular kits per material**. **Construction (Decided):** Clash
  of Clans style: covered/scaffolded placeholder with slight progress signs
  until finished.
- **Decided:** SyntyPass for Sidekick + library during development
  (~$360 to summer); resubscribe for later substantial updates. Ask Synty:
  download method, any non-commercial exception.
- **Animals (Decided):** **polyperfect** for all animals (style synergy +
  domestic species): Low Poly Animated Animals, Prehistoric Animals,
  Dinosaurs, Fantasy Creatures. Owned Malbers Poly Art Forest set = backup
  (wild only, different style).
- **Synty library covers (proposed, verify contents):** primitive structures
  (Goblin War Camp, Adventure, Explorer Kit), organic/Pandora-style (Elven
  Realm, Enchanted Forest), farming/villages (Farm, Fantasy Village, Viking,
  Town), civilizations (Egypt, Ancient Empire, Samurai, Fantasy Kingdom),
  sailing (Pirate), industrial (Western Frontier, Construction), domestication
  (Dog Pack, Farm), creatures (Kaiju, Werewolf), children (Kids Pack?), FX and
  Icons. No true Stone Age pack: kitbash. **Skip Poly Universal Pack.**
- **Animation (Decided):** Synty's animation packs are locomotion, idles,
  sword/bow combat, emotes only; **no work/crafting/gathering/farming**. So
  **Kevin Iglesias Human Mega Animations Pack** is still needed (300+:
  villager/work, basic motions, throwing, archer, dance, melee, soldier).
  Synty Idles + Emotes + Base Locomotion come with SyntyPass. Mixamo fills gaps.
- **Owned (purchased 2026-10-01):** SyntyPass (active from Oct 1) ·
  polyperfect Low Poly Animated Animals, Dinosaurs, Prehistoric Animals,
  Fantasy Creatures · Kevin Iglesias Human Mega Animations Pack · WhaleForge
  Low Poly Prehistoric Pack · EXPLORER Stone Age · FlexiFrogs. Plus previously:
  Synty biome/nature packs, Malbers Poly Art Forest set.
- **Building coverage audit (2026-10-01):**
  - Synty's only real primitive kit = **Goblin War Camp** (wood/hide/bone,
    tents, log walls, thatch, stilts, fire pits; some goblin-specific spikes/
    skulls/siege). Viking Realm = modular timber halls + boats. Elven Realm =
    modular organic (Pandora route). Fantasy Village = storybook village.
    Farm Pack = *modern* farm (crops useful only). Explorer Kit = modern
    explorer (useless). **No Synty livestock** (only anthropomorphic Fantasy
    Village animals). **No Synty steampunk/industrial** beyond Western.
  - **EXPLORER Stone Age** ($53): 47 buildings (huts, tents, camps, shrines,
    towers, bridges), 201 props (pottery, tools, boats, rafts, fossils), 128
    rocks/cliffs/megaliths, 8 humanoid characters + 15 anims, fire/fog FX.
    Most direct first-milestone coverage.
  - **WhaleForge Prehistoric** ($35): caves turned out NOT unique; now
    optional (only painted cave walls are unique; decals can do that).
  - **Poly Universal: skip (Decided).** Synty library covers buildings/farm
    enough. Steam-era gap accepted for now (stretch goal).
  - **Caves in Synty:** Alpine Mountain (owned: icy caves, stalactites),
    Pirate Realm (cave floors/walls/pillars), Dungeon Pack (caverns), Dwarven
    Dungeon Map (caverns, mushroom grotto).
  - **Boats in Synty:** rafts + canoes (Tropical Jungle, owned), canoes
    (Alpine, Swamp, owned), Egypt barges/boats, Viking boats x3, Pirate
    rowboat + 9 modular ships. Covers raft to sailing ship.
  - **Primitive clothing:** no Synty tribal/caveman character pack exists.
    Candidates: Sidekick parts are cross-pack compatible, so Goblin Fighters
    (scrappy leather/fur?) and Viking Warriors (furs) parts on human bodies;
    Fantasy Villagers for simple tunics; POLYGON palette-texture swaps to
    recolor clothing to hide/linen tones. Non-Sidekick POLYGON characters
    mostly have clothing baked into one mesh, so parts can't be moved onto
    Sidekick bodies without Blender work.
  - **Female characters** read masculine in Synty style. Test pushing
    Sidekick's feminine body + facial blend shapes (free starter pack).
- **Gaps found:** prehistoric **clothing for Sidekick people** (no tribal
  Sidekick pack) · **prehistoric children** (Synty Kids Pack is modern kids).
- **Low-poly style (Decided).** Risk = asset-flip look; fix = one shared
  shader, palette, lighting + strong UI identity.
- **SyntyPass note:** subscription licence forbids substantial development
  after cancelling, so it must stay active until release (~$480/yr). Decide at
  the Unity visual phase, not now. First
  milestone structures (fire pit, windbreak, lean-to, tent, drying rack,
  storage pit, kiln) kitbashed from nature packs; buy later (Synty Fantasy
  Village / Ancient Egypt / Viking; a Stone Age pack once verified).
- To verify before buying: children & elders, body-part add-ons (fur, webbing),
  enough "primitive" clothing.

## 14. Tech (Decided)

- Unity 6.3 for presentation; sim is plain C# (netstandard2.1, C# 9), no Unity
  references, runs headless.
- ML = learning inside minds (no GPU needed). LLM only as optional prophet
  translator / flavor text, later.

---

## 15. Production & content stance (Decided)

- **Timeline: until ~summer 2027** (about 9 months), aiming for a real,
  complete game. **One island = floor, fuller vision = stretch.** Architect for
  everything, build in dependency order, ship what's solid.
- Headless MVP code: **~2 weeks**; tuning continues alongside Unity work.
- **Solo developer + AI.** Ambitious scope is fine; content (art/animation) is
  the likely bottleneck more than code, so plan art to be AI/procedural-friendly.
- **Player only creates natural things.** People build everything man-made.
- **Conflict is fully in:** dictatorships, wars, slaughter can happen, but are
  always **avoidable with good play** (conditioning for peace, managing first
  contact well).
- **Conquest, sacrifice, and slavery exist.** Framing through consequences:
  - Innocent/good people dying → you lose believers, points, powers.
  - Conquerors, murderers, slavers grow **arrogant**, which leads to bad outcomes.
  - Not all wars are equal (e.g. defensive wars weigh less than conquest).

## 16. Not yet discussed (later, not needed for first milestone)

- **Family, reproduction, gender roles, aging, death:** emergent or modeled?
- **Disease** beyond natural consequences.
- **Trade & contact** between societies: how it works.
- **UI:** codex, notifications, mind-map view, info overlays.
- **Sound & music.**
- **Save/load, platform** (PC only?), mod support.
- **Name** (Anthro is a working title).

## 17. Build order (proposed, depends on dependency chain)

```
World + ecology → People (needs, minds, verbs) → Knowledge & teaching
  → Discovery tiles → God powers → Belief/religion → Prophet
  → Lineages/genetics → Evolution → Language → Hierarchy/economy
  → Multi-society contact            (story/codex layered throughout)
```

**First milestone (Decided):** one band (~30), small generated valley, simple
ecology, full mind model, ~10 tiles (fire → cordage → storage → pottery → bow),
4–5 god powers, belief forming from your actions. Headless first, then Unity
with simple shapes.

## Social rework: primitives + learning (DECIDED 2026-10-04)
Society should emerge from people, not from designed events.
- **Primitives**: ask, give, refuse, take, confront, attack, shun/avoid, tell (gossip),
  invite (generalized hunt recruitment). No "steal"/"raid" verbs; the chronicle labels
  deeds after the fact.
- **Thin authored biology**: perception (who notices, how news spreads), innate
  feelings (anger at a loss pinned on someone, stronger when your kids are hungry;
  fear when hurt; attachment and guilt toward kin/friends), physics of food, fights
  and injury, drives (food, safety, family, standing, comfort).
- **Learning**: each person learns what acts lead to, in context (act × relation to
  target × watched), from their own results (consequences carry the deed that caused
  them, for credit), from watching others, and from gossip (existing belief spreading).
  Trust is a learned prediction of how someone treats you.
- **Cultural priors**: founders inherit their band's expectations about taking,
  sharing and retaliation (varied per band), not blank slates.
- **Group acts**: one "invite to a joint act" primitive for hunts, raids, camp moves,
  splits, war.
- **Between bands**: same primitives; group identity as context; grievance
  generalizes to the offender's band; outsiders in remembered places = pinned loss
  (territory); gifts + learned reciprocity = alliances.
- **Stages**: 0 foundations → 1 food & conflict in a band → discovery system →
  2 group acts → 3 between bands (raids, feuds, war, alliances) → 4 pairing &
  jealousy, leadership, splitting → 5 evolved per-person drive weights.
