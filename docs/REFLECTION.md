# Reflection: are we still building the game we planned? (2026-10-07)

Measured against `PLANNING.md` (the plan agreed before code) and `REVIEW.md` (2026-10-04).

## Where we've been true to the plan

**People are minds (pillar 4).** The plan specified choices as *need × expected result + interest +
trait pulls + social pull − culture + noise*, in a readable learned graph, without neural nets. That
is exactly what's built. The worry that decisions are "fixed formulas fed by learned knowledge" is
that design, working as intended. The work since has been moving what was wrongly fixed (cultural
choices) into what's learned:
- what each act brings;
- how lean each season gets;
- plural marriage;
- how far violence goes toward whom;
- honour and peacemaking;
- rationing, asking, elders' voice, hunting prestige.

**Nothing scheduled, nothing global.** This principle has been actively enforced:
- Calendar rules are now driven by conditions: day length, firewood by temperature, disease by
  cold, and (tonight) fish runs by river warmth.
- Valuations come from the person and their people's culture.
- Two audit documents keep score: `AUDIT-HARDCODE.md` and `AUDIT-DECISIONS.md`.

**The social rework** (primitives + learning, cultural priors per band, grievance generalising to
the offender's band, gifts building alliances) is built and keeps deepening. Tonight added fights
with intent, onlookers, peacemaking, routs, and blood feuds.

**Every death has a cause, and tragedies trace back to something visible.** Famines come from
specific springs and specific choices. Feuds come from specific killings. This is what the plan
asked for: "bad outcomes shouldn't feel unearned."

**Start with one band, grow into several societies.** Built: peoples and camps, schisms, a cap of
5 peoples.

**Conflict is fully in, and varies by culture.** Yes, and now with texture.

**Learning real anthropology through play.** Calibration is grounded in real forager data:
- demography and fertility schedules (Coale–Trussell);
- Hadza work hours;
- winter lean seasons;
- storage leading to sedentism (acorn and fish economies);
- respiratory infection as the top killer;
- polygyny far more common than polyandry, and why.

## Where we've drifted

1. **The god layer hasn't been started.** This is the biggest gap.
   - The first milestone was "one band, ~10 tiles, full mind model, 4–5 god powers, belief forming
     from your actions."
   - The 2026-10-04 review put the god layer first (P0).
   - Since then every session has deepened the simulation instead: demography, ecology, food,
     camps, marriage, fights. Each was a real problem, and most were your direct calls, but the
     game the player plays doesn't exist yet. Favor, prayers, omens, blessed births, prophets,
     reputation, the "last ember": none of it is in code.
2. **Religion by misinterpretation hasn't been started.** Burial, ritual and ochre currently act as
   cohesion bonuses, not as beliefs about a god. The learning machinery to build it already exists:
   linking events to recent deeds is the same thing as learning what acts bring.
3. **The scope of the discovery web grew.** "~10 tiles" became 27 tiles and 144 stages. Each now
   has an effect and measured data, but it's far more to balance than milestone 1 called for.
4. **"Not Earth."** The plan wanted an alien-ish world with fantasy creatures. Everything has been
   grounded in Earth data (deer, boar, wolves, mammoth, a temperate valley). That's the right way to
   calibrate, but there's no pattern yet for grounding invented species. The likely approach: by
   analogy to a real body plan and ecology, with numbers derived from that.
5. **Evolution and the compressed-evolution mechanic** aren't built. Trait inheritance with drift
   is all there is. The "accelerated evolution" you mentioned is still only a plan.
6. **Off-map regional populations** were approved on 10-02, then replaced by a closed map at your
   request. That was a deliberate, sound change; noting it because the planning doc still lists it.
7. **No Unity yet.** The plan said art would be the bottleneck and to start a simple-shapes Unity
   slice early. The headless simulation is far past MVP; the visual side hasn't begun.
8. **Player-facing constraints arrived late.** The "1–2 camps you really play" and "3–5 peoples
   max" limits came from playing the numbers forward, not from the plan. They're good constraints,
   and they argue for building the player's view (people → camps → individuals) before more
   simulation depth.

## What this suggests

The simulation is now deep, causal and mostly honest. It answers part of the founding question
("how varied could isolated cultures become?") already: peoples differ in violence, sharing,
marriage, rationing and luck. The risk is that it keeps absorbing all the effort. Each sim session
finds real problems, so there's always a reason to do one more.

**Recommendation:** after one more calibration pass on tonight's systems (violence level and the
settling pull), switch the main effort to the **god layer v1 (headless)**, in this order:
1. Favor and belief.
2. Prayers as requests.
3. Omens, placing resources and animals, branch-point nudges.
4. Religion through misinterpretation, using the existing learned-expectation machinery.

Keep a short weekly simulation-tuning slot alongside it. Then start the **Unity simple-shapes
slice**, since art is the bottleneck.
