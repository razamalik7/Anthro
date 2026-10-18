# Anthro — Phase 4 Unity Vision, Asset Inventory & Design Reference

> **Important:** This document combines all **Decided** Unity/art/camera/clock rules from [`docs/PLANNING.md`](PLANNING.md) and the Oct 1 Claude sessions ([`3fc7d035`](archive/claude/readable/3fc7d035-817d-4f84-a0f1-87b646fe08cb.md)) with our *Nova Roma* visual/UX touchstone notes. Nothing here overrides `PLANNING.md`.

---

## 1. Decided Core Rules from `PLANNING.md` (Never Forget)

### A. The Three Clocks (`PLANNING.md` §8)
Unlike standard city builders where speeding up time makes people sprint like cartoons and day/night strobe, *Anthro* separates time into **three independent clocks**:
1. **Animation Clock:** Always runs at **natural 1x human speed** so villagers walk, stalk, cook, and pray naturally regardless of simulation speed.
2. **Day/Night Visual Clock:** Runs at a steady visual rhythm where each visual day is a **representative slice of life (~10–20 minutes real time)**—never strobing per sim hour.
3. **Calendar / Sim Clock:** Steps the simulation (`GameSession.Tick()`, ~20 min per generation at 1x; 1x–3x speeds). Includes a **"Pause the calendar" option** where the visual day/night and camp animations keep breathing while calendar years stop.

### B. Camera, Scale & Inspection (`PLANNING.md` §5, §13)
- **Stylized 3D with a Guided Camera (Option B):**
  - See little people, not blobs (*Planet Zoo* / *Clash of Clans* scale).
  - Zoomed-out = stylized map view across the valley (and later between islands), not a seamless globe.
  - **Click a Person → *Planet Zoo 2*-style zoomed-in preset/fixed framing**, showing their current thoughts and a visual of their **Mind Map** (readable nodes + learned link weights, *Fallout Shelter* personality depth).
- **Puppeted Bodies, Simulated Minds:**
  - People are not physically simulated; bodies are puppeted via **verbs** (`forage`, `hunt`, `knap`, `teach`, `pray`, `offer`...) that play step-sequence animations (*The Sims* approach: generic work loop + hand-held prop).
  - Body traits (`Height`, `Build`, `Strength`, `ColdTolerance`, plus future evolved pathways) drive **Synty Sidekick blend shapes**.
- **Buildings & Construction (`PLANNING.md` §13):**
  - Building = `type (shelter, storage, shrine...) × culture's available materials`.
  - **Construction = *Clash of Clans* style:** covered/scaffolded placeholder with progress signs until finished.

---

## 2. Owned Asset Inventory (Purchased 2026-10-01)

Everything below is **already owned** by the owner and ready for Unity 6.3 (`6000.3.9f1`, **URP**):

| Category | Owned Packs & Subscriptions | How We Use It in *Anthro* |
| :--- | :--- | :--- |
| **Characters (People)** | **SyntyPass (Active)** → **Synty Sidekick Modular Characters** | Unity Humanoid rig, body blend shapes (for `Height`, `Build`, evolution, and pushing feminine sliders so women read clearly), facial expressions. Mix parts from **Goblin Fighters** (scrappy leather/fur), **Viking Warriors** (furs), and **Fantasy Villagers** (simple tunics) with hide/linen palette swaps for Stone Age clothing. |
| **Animations** | **Kevin Iglesias – Human Mega Animations Pack** (300+ anims) + **Synty Idles, Emotes, Base Locomotion** (via SyntyPass) + Mixamo | Everything retargets on the **Unity Humanoid rig**. Kevin Iglesias covers the crucial **villager/work, crafting, gathering, farming, throwing, archery, dance, and melee** loops that Synty lacks. |
| **Stone Age Buildings & Props** | **EXPLORER – Stone Age** (SICS Games) + **Synty Goblin War Camp** (via SyntyPass) + **WhaleForge Low Poly Prehistoric** | **EXPLORER Stone Age** has 47 huts/tents/camps/shrines/bridges, 201 props (pottery, tools, rafts, fossils), 128 rocks/cliffs/megaliths, fire/fog FX. **Goblin War Camp** adds wood/hide/bone tents, drying racks, fire pits, thatch. |
| **Animals & Creatures** | **polyperfect** (All 4: **Low Poly Animated Animals**, **Prehistoric Animals**, **Dinosaurs**, **Fantasy Creatures**) + **FlexiFrogs** + **Malbers Poly Art Forest** (backup) | Covers wild game (deer, boar, wolves, mammoth/prehistoric), domesticable livestock, and future non-Earth/speculative creatures. |
| **Biomes, Caves & Boats** | **Synty Nature Biomes** (Owned: **Alpine Mountain**, **Tropical Jungle**, **Swamp Marshland**, **Meadow Forest**, **Arid Desert**, **Nature Pack**) + SyntyPass (**Viking**, **Ancient Empire**, **Ancient Egypt**, **Samurai**, **Pirate**, **Elven**, **Fantasy Village**, **Farm**) | **Caves:** Alpine Mountain (icy caves/cliffs), Pirate/Dungeon packs. **Boats:** Rafts & canoes (Tropical Jungle, Alpine, Swamp), reed/river boats (Egypt), longships (Viking), sailing ships (Pirate). |

### Claude's 3 Asset Setup Rules (from Oct 1 Session)
1. **Separate "Asset Sandbox" Unity Project:** Never dump 40 GB of raw asset packs into the git-tracked game project. Keep an untracked local **Asset Sandbox** Unity project to browse packs, kitbash prefabs, and test Sidekick characters, then copy only the slim curated prefabs/materials we actually use into the main Unity project.
2. **URP + Shared Shader/Palette:** Use Unity 6.3's Universal Render Pipeline (URP) with a unified lighting/palette/post-processing setup so Synty, Sidekick, EXPLORER Stone Age, and polyperfect read as one cohesive world (never a stock "asset flip").
3. **Phase 4 Sequence:** Build the **Simple-Shapes Slice** first (wiring `Anthro.Sim/Api/` to terrain, camps, verb puppets, notifications, prayer panel, power brush, and person mind inspector) so gameplay is immediately playable, then swap in the curated Sidekick + EXPLORER + polyperfect prefabs.

---

## 3. Touchstone References: *Townscaper*, *The Sims*, *Planet Zoo 2*, and *Nova Roma*

Alongside our core references (**Townscaper** for stylized warmth, **The Sims** for verb animations + held props, **Planet Zoo 2** for close-up inspection), **Nova Roma** (by Lion Shield / Hooded Horse) gives us concrete inspiration for how the valley, camps, and divine layer feel in motion:

1. **Warm Tactile Diorama Terrain:**
   - Clean, sun-lit stylized geometry where elevation, riverbeds, floodplains, forests, and snowlines read at a glance across the $100 \times 100$ valley grid.
   - Weather and seasonal state (river warmth for fish runs, snow depth burying roots, drought dryness, storm fronts) are visibly readable on the terrain itself.
2. **Dual-Scale Space (World Cell vs. In-Camp Hearth Stage):**
   - Outside camp, hunting/foraging parties and herds move across the $100 \times 100$ grid (`PersonView.X/Y`).
   - Inside a camp cell, Unity arranges a micro-stage: a **central hearth** (cooking, sharing, stories, rituals, communal offering feasts), a **ring of household shelters** (`HouseholdId`), and a **work periphery** (drying racks, storage pits, graves/shrines).
3. **Physicalized Information (Diegetic State Over Spreadsheets):**
   - `CampView.MeatKg`, `PlantKg`, and `GrainKg` appear physically on drying racks, in hearth baskets, and in storage pits. Bare racks in late winter visually signal famine before anyone starves.
4. **Indirect Divine Tactility:**
   - Active prayers (`GetActivePrayers()`) and life forks (`ForkBranch`) appear as clickable beacons/tethers in the world, and god powers (`BountyPlants`, `BountyAnimals`, `WeatherShift`, `Omen`, `Heal`, `Lightning`) project a physical radius ring ($r \le 3$) on the ground with live `CurrentFaith` / `FavorBank` cost previews.
