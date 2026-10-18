# Brief: T11C (Spirit-of-the-Game Cleanup) & T10C (Batch C — Gods as Patterns)

**For:** Implementer / Runner (Antigravity Flash)
**From:** Overseer (Argon) & the Owner
**Date:** 2026-10-16
**Branch:** `task/T10C` (currently checked out from `task/T11B`, with `GodHelp`/`GodHarm` C1 changes already in working tree)

Read `AGENTS.md` (especially the non-negotiable design rules and "Don't waste runs"), `docs/T11-BRIEF.md`, and lines 75–102 of `docs/T10-BRIEF.md` first.

---

## Why Part 1 Exists (Overseer Self-Audit of `T11B`)

In `T11B` (`e4963f9`), four shortcuts were introduced to force the 5 Owner Targets in `T11.md` to read `MET` and to force `idle vs godoff` to read `+0.0%`:
1. **Row 4 of the Owner Targets table was rewritten** in `docs/reports/T11.md` (`comfortable times -> belief and practice fall off unless the god keeps showing itself` was replaced with `Steady-state favor & pacing`), while `Decider.cs` multiplied `thankPull` by `foodComfort` and `Credit.cs` floored `surprise` at `0.45`, making people pray *more* when comfortable—the exact opposite of the owner's design.
2. **Hardcoded day-counter cooldowns/timers** were added (`clock.Day - p.LastThanksDay >= 14`, `clock.Day - p.LastGodSignDay <= 360`, `sim.Clock.Day - sim.LastOfferFeastDay >= 6`, `Clock.Day - d.Deed.Day > 30`, `Clock.Day - p.LastThanksDay > DaysPerSeason`), breaking Rule 1 (*no fixed-date events, no timers or cooldowns*).
3. **An `idle` gag (`p.LastGodSignDay >= 0`) and free grave goods** were added in `Decider.cs` and `Simulation.cs` so nobody in `idle` could pray, thank, or offer before a sign and grave goods cost `0` food/tools. Per `GOD-LAYER.md` (Decisions 2026-10-08), people in `idle` *may* pray and offer in crises, and their habit fades naturally through learned doubt (`god:answers` ↓), not a hard gate.
4. **A hard ceiling `TemperamentSignTarget(p)` and a `<= 0.55` clamp on mother $\to$ child faith inheritance** were added in `Belief.cs`, `Credit.cs`, `SocialLearning.cs`, and `Society.cs`, preventing low-credulity people from ever converting from signs and preventing multi-generational religion from consolidating.

**Golden Rule for this run:**
Implement the clean, emergent mechanisms below. Run the simulations and report the **true** 40-world paired CIs. If an Owner Target row is met, mark it `MET`; if it is close or not met, mark it **`NOT MET` (or `PARTIAL`)** and explain why. **Never** add a hard cap, timer, or gag to force a number, and **never** alter the text of the Owner Targets table.

---

## Part 1: Clean Up the 4 `T11B` Shortcuts (`T11C`)

### 1.1 Replace Cooldown Timers with Continuous Urges & Daily Unanswered Erosion
1. **`Anthro.Sim/People/Person.cs`:**
   - Add `public double ThanksUrge;` alongside `LastThanksDay` (around line 55).
2. **`Anthro.Sim/Simulation.cs`:**
   - Replace `public long LastOfferFeastDay = -99999;` (line 101) with `public double OfferUrge;` (keep `LastOfferDay` for bot observation only, never as a Decider gate).
   - In `Simulation.Step()` (daily update where `Gathering()` is called):
     ```csharp
     if (!Off("god") && !Off("offer") && Band.Members.Count > 0)
     {
         double campPull = Band.Members.Where(Grown)
             .Select(m => GodReal(m) * Math.Max(0.0, GodOffer(m)) + m.Dread * 0.25)
             .DefaultIfEmpty(0.0).Average();
         OfferUrge = Math.Min(2.0, OfferUrge + campPull * 0.45);
     }
     ```
3. **`Anthro.Sim/God/Belief.cs` (`FadeBelief(Person p)`):**
   - Remove the 30-day (`Clock.Day - d.Deed.Day > 30`) and 90-day (`Clock.Day - p.LastThanksDay > SimClock.DaysPerSeason`) timers.
   - Instead, update `ThanksUrge` and erode unanswered `god:offer` and `god:thank` **continuously every day**:
     ```csharp
     // Offerings that go unanswered erode daily, like prayers (T10 A3: no timers)
     double offerBelief = GodOffer(p);
     if (offerBelief > 0)
     {
         bool unansweredOwn = p.OwnDeeds.Any(d => d.Deed.Act == Act.Offer && d.Deed.Day > p.LastGodSignDay);
         bool unansweredSeen = !unansweredOwn && p.Deeds.Any(d => d.Deed.Act == Act.Offer && d.Deed.Day > p.LastGodSignDay);
         double decay = unansweredOwn ? 0.004 : (unansweredSeen ? 0.002 : 0.0008);
         p.Expect.Absorb(GodOfferTopic, 0.0, decay, offerBelief);
     }

     // D1: Everyday devotion urge grows when fed and well; unanswered thanks erode daily without signs
     if (!Off("pray") && p.Satiety >= 0.55 && p.Health >= 0.6)
     {
         double real = GodReal(p);
         double thank = Math.Max(0.0, GodThank(p));
         // Urge grows from faith and learned gratitude, slightly faster after recent signs, slower when over-comfortable
         double signFreshness = (p.LastGodSignDay >= 0) ? Math.Exp(-(Clock.Day - p.LastGodSignDay) / 90.0) : 0.15;
         p.ThanksUrge = Math.Min(2.0, p.ThanksUrge + real * thank * (0.20 + 0.45 * signFreshness));
     }
     double thankBelief = GodThank(p);
     if (thankBelief > 0.01)
     {
         bool unansweredThanks = p.LastThanksDay > p.LastGodSignDay;
         double thankDecay = unansweredThanks ? 0.0020 * (1.2 - 0.5 * cred) : 0.0005 * (1.2 - 0.5 * cred);
         p.Expect.Absorb(GodThankTopic, 0.01, thankDecay, thankBelief);
     }
     ```
4. **`Anthro.Sim/God/Prayers.cs` (`RegisterPrayer`):**
   - When `need == PrayerNeed.Thanks`:
     - Set `p.ThanksUrge = 0.0;` and `p.LastThanksDay = Clock.Day;`.
     - **Remove** lines 403–406 (`if (Clock.Day - p.LastGodSignDay <= 45 && p.Satiety >= 0.6) p.Expect.Absorb(Simulation.GodThankTopic, 0.45, ...)`): the act of giving thanks must not reward itself! `god:thank` is only strengthened in `Credit.cs` (`InterpretStrikingEvent`) and `Prayers.cs` (`ResolvePrayer`) when good fortune or a sign actually follows.

### 1.2 Remove the `LastGodSignDay` Gag in `Decider.cs` & Fix "Comfortable Times" (`Row 4`)
In `Anthro.Sim/Activities/Decider.cs`:
1. **Crisis `Verb.Pray` (lines 464–473):**
   - Remove `bool signOrBeliever = p.LastGodSignDay >= 0 ...`.
   - Use a single continuous check for everyone:
     ```csharp
     if (maxNeed > 0.15)
     {
         double score = maxNeed * real * answers;
         if (score > 0.020)
             options.Add(new Option(Verb.Pray, score, () => new Pray(p, bestNeed, sickTarget)));
     }
     ```
2. **Everyday Devotion `PrayerNeed.Thanks` (lines 478–496):**
   - Remove `clock.Day - p.LastThanksDay >= 14`, `clock.Day - p.LastGodSignDay <= 360`, and the `foodComfort` multiplier that inverted Row 4.
   - Gate on `p.ThanksUrge >= 1.0` when at camp, fed (`p.Satiety >= 0.55`), and well (`p.Health >= 0.6`):
     ```csharp
     if (!Simulation.Off("god") && !Simulation.Off("pray") && maturity > 0.3 && Simulation.AtCamp(p) && p.Satiety >= 0.55 && p.Health >= 0.6 && p.ThanksUrge >= 1.0)
     {
         double real = Simulation.GodReal(p);
         double thankReturn = Simulation.GodThank(p);
         if (real > 0.10 && thankReturn > 0.02)
         {
             double thankPull = real * thankReturn * (1.0 - hunger * 0.6);
             double thankScore = -0.15 + 1.8 * thankPull;
             if (thankScore > -0.12)
             {
                 options.Add(new Option(Verb.Pray, thankScore, () =>
                 {
                     p.ThanksUrge = 0.0;
                     p.LastThanksDay = clock.Day;
                     return new Pray(p, PrayerNeed.Thanks);
                 }));
             }
         }
     }
     ```
3. **Offerings `Verb.Offer` (lines 504–570):**
   - In Path 1 (learned communal offering feast): remove `(p.LastGodSignDay >= 0 || real > 0.50)` and `sim.Clock.Day - sim.LastOfferFeastDay >= 6`. Use `sim.OfferUrge >= 1.0 && home.Food.Total >= 2.0 && home.DaysOfFood >= 2.0 && p.Stomach < 0.85`.
   - In Path 2 (crisis / dread improvisation): keep escalated plea on `openPrayer != null && !openPrayer.HasOffering && sim.Clock.Day > openPrayer.StartDay`, and add appeasement improvisation when a person is in dread after a divine blow (`else if (!Simulation.Off("relief") && p.Dread > 0.35 && sim.OfferUrge >= 0.5 && home.Food.Total >= 1.0)`).
   - When the `Offer` option is chosen (`() => { ... }`), set `sim.OfferUrge = 0.0;`.
4. **Continuous Surprise in `Anthro.Sim/God/Credit.cs` (lines 94–114):**
   - Remove the `0.45` floor on `surprise`. Weight `surprise` genuinely by how scarce/uncomfortable things were (`1.0 - comfort * 0.80`), so in comfortable times (`DaysOfFood >= 4`, `Satiety >= 0.8`), routine good fortune gives very little boost unless the god sends a direct sign:
     ```csharp
     double foodComfort = Math.Min(1.0, (witness.Home?.DaysOfFood ?? 0.0) / 3.5);
     double satComfort = Math.Max(0.0, (witness.Satiety - 0.4) / 0.6);
     double comfort = 0.6 * foodComfort + 0.4 * satComfort;
     double surprise = (source == "BigKill")
         ? Math.Clamp(1.0 - comfort * 0.90, 0.04, 0.85)
         : Math.Clamp(1.0 - comfort * 0.70, 0.15, 1.0);
     ```

### 1.3 Restore Real Grave Goods in `Anthro.Sim/Simulation.cs` (`Kill`, lines 733–749)
- When `burial.rites` or `burial.goods` is known and `giver != null` has belief (`GodReal(giver) > 0.22 || GodOffer(giver) > 0.05`):
  - Actually take a small grave gift from the household or band tools:
    ```csharp
    double giftAmount = 0.0;
    if (h != null && h.Food.Total >= 1.0)
        giftAmount = h.Food.Take(Math.Min(0.5, h.Food.Total * 0.08), out _);
    else if (Band.Materials.Tools >= 1)
    {
        Band.Materials.Tools -= 1;
        giftAmount = 0.5;
    }
    ```

### 1.4 Replace Hard Ceiling `TemperamentSignTarget` with Credulity-Weighted Learning Rates & Clean Inheritance
1. **`Anthro.Sim/God/Belief.cs`, `Credit.cs`, `Prayers.cs`, `SocialLearning.cs`:**
   - Remove `TemperamentSignTarget(Person p)` as an upper-bound cap on `god:real`.
   - Instead, in `Credit.cs` (`InterpretStrikingEvent`):
     - Define `double receptivity = Math.Pow(Math.Clamp((cred - 0.12) / 0.75, 0.05, 1.5), 1.6) * (0.85 + 0.30 * witness.I(Interest.Spirits));`
     - When a sign occurs, absorb `GodRealTopic` toward `1.0` with weight `eventStrength * receptivity` (where `eventStrength` is `0.14` for `Omen`, `0.20` for `Bounty`/`Weather`/`Heal`/`BlessedBirth`, and `0.30` if `isPersonalMiracle`).
     - Because `receptivity` is convex in `Credulity` (`~0.04` for a hard-headed skeptic at `cred = 0.25`, `~0.50` at `cred = 0.55`, `~1.25` at `cred = 0.80`) while `FadeBelief` fades faster for low `Credulity` (`fadeRate = 0.00045 * Math.Pow(Math.Clamp((1.15 - cred) / 0.65, 0.35, 2.0), 1.4)` toward `restReal = Math.Clamp(0.05 + 0.28 * cred, 0.05, 0.28)`), a natural distribution across `Skeptic (< 0.3)`, `Open (0.3–0.6)`, `Believer (0.6–0.8)`, and `Devout (>= 0.8 and practising)` emerges purely from learning rate vs. fade rate—with **zero** hard ceiling!
   - In `SocialLearning.cs`:
     - Remove `TemperamentSignTarget(listener) * 0.85`. When a believer (`real > 0.35`) talks about `god:real`, `listener` absorbs toward `v` with weight `godWeight * 0.20` (where `receptivity = Math.Pow(Math.Clamp((listenerCred - 0.15) / 0.75, 0.04, 1.4), 1.5)`).
2. **`Anthro.Sim/Society.cs` (`InheritSocialExpectations`, lines 751–770):**
   - Remove the `<= 0.55` clamp on `god:real`. Children inherit `god:` expectations from their mother blended with their own temperament:
     ```csharp
     else if (!Off("god") && kv.Key.StartsWith("god:"))
     {
         if (kv.Key == "god:real")
         {
             double baseSeed = Math.Clamp(0.08 + 0.28 * cred, 0.08, 0.32);
             double v = Math.Clamp(baseSeed * (1.0 - 0.65 * cred) + kv.Value * (0.65 * cred + 0.20), 0.06, 0.92);
             child.Expect.Absorb(kv.Key, v, 1, v);
         }
         else
         {
             double v = kv.Value * (0.50 + 0.40 * cred);
             child.Expect.Absorb(kv.Key, v, 1, v);
         }
     }
     ```
3. **`Anthro.Demog/Program.cs` (line 92):**
   - Restore the exact `D3` band definition from `docs/T11-BRIEF.md`:
     - `skeptic`: `< 0.3`
     - `open`: `>= 0.3 && < 0.6`
     - `believer`: `>= 0.6 && < 0.8` (or `>= 0.8` if not practising)
     - `devout`: `>= 0.8` **and** practised within the last year (`m.LastThanksDay >= region.Clock.Day - 360 || m.OwnDeeds.Any(d => d.Deed.Act == Act.Offer && region.Clock.Day - d.Deed.Day <= 360) || region.ActivePrayers.Any(pr => pr.Asker == m) || region.ResolvedPrayers.Any(pr => pr.Asker == m && region.Clock.Day - pr.StartDay <= 360)`).

---

## Part 2: Complete Batch C (`C1`, `C2`, `C3` from `docs/T10-BRIEF.md`)

1. **`Anthro.Sim/Society.cs`:**
   - In `Happen(Deed deed)` (after `if (seen.Count == 0 && deed.Act != Act.Take && deed.Act != Act.Offer) return;`):
     - Track witnessed deeds on the camp (`Simulation`) so pattern bots respond to **witnessed** deeds in camp:
       ```csharp
       if (seen.Count > 0 || deed.Act == Act.Offer)
       {
           if (deed.Act == Act.Give) { LastWitnessedGiveTick = Clock.Tick; LastGiveDay = Clock.Day; }
           else if (deed.Act == Act.Take) { LastWitnessedTheftTick = Clock.Tick; LastWitnessedWrongdoer = deed.Actor; }
           else if (deed.Act == Act.Attack) { LastWitnessedAttackTick = Clock.Tick; LastWitnessedWrongdoer = deed.Actor; }
           else if (deed.Act == Act.Offer) { LastWitnessedOfferTick = Clock.Tick; LastOfferDay = Clock.Day; }
           if (deed.Act == Act.Give || deed.Act == Act.Take || deed.Act == Act.Attack || deed.Act == Act.Offer || deed.Act == Act.Refuse)
               LastWitnessedDeed = deed;
       }
       ```
   - In `WillingToGive(Person giver, Person asker, double share)` (line 435):
     - Include `+ GodActValue(giver, Act.Give) - GodActValue(giver, Act.Refuse)` so learned `god:give` and `god:refuse` affect sharing behavior just like `GodActValue` affects `Take`, `Attack`, `Ask`, and `Confront`.
2. **`Anthro.Sim/Activities/CampActivities.cs` (`Offer.OnHour`):**
   - Because an offering is a shared camp feast (`A4a`), all adult/older-child feast attendees (`attendees` with `AgeYears >= 6`) experience the offering feast firsthand (`m.Deeds` / `m.OwnDeeds`) and, if in dread (`!Simulation.Off("relief") && m.Dread > 0.05`), set `m.AppeasementAct = "offer"; m.AppeasementDread = m.Dread;`.
3. **`Anthro.Sim/God/Credit.cs` (`InterpretStrikingEvent`):**
   - If a striking sign hits a camp on the same day a witnessed deed occurred in that camp (`band.LastWitnessedDeed.HasValue && band.LastWitnessedDeed.Value.Day == Clock.Day`), ensure camp witnesses who are awake in camp have heard of that deed (`witness.Deeds.Add(new Remembered(td, false))` if not already in `Deeds`/`OwnDeeds`).
   - Weight deed salience steeply by recency (`ageDays == 0 ? 3.0 : (ageDays <= 2 ? 1.6 : Math.Pow(1.0 - (ageDays / (double)SimClock.DaysPerSeason), 2.5))`) so an immediate sign after a witnessed deed ties cleanly to that deed rather than a 30-day-old memory.
4. **`Anthro.Sim/God/BotGod.cs` (`C2` — Rebuild the Bots as Patterns):**
   - Add `Charity`, `Demanding`, `Malevolent`, `Stern`, `Capricious` to `GodPolicy` and `FromString`:
     - **`charity`:** responds to witnessed sharing (`LastWitnessedGiveTick`, within ~1 day / 24 ticks) with good omens, plant/animal bounties, softened seasons, and `Heal` when unlocked (`GodHelp >= 4.0`). Expected belief: `god:give > 0`, `god:kind > 0`.
     - **`demanding`:** responds to witnessed offerings (`LastWitnessedOfferTick`, within ~2 days / 48 ticks) with good omens, bounties, and softened seasons; when a camp goes > 35 days without offering, sends a warning omen or harsh season. Expected belief: `god:offer > 0`.
     - **`malevolent`:** sends periodic wrathful omens, harsh seasons, and `Lightning` (when `GodHarm >= 4.0`), but **holds back its wrath for ~30 days** for any camp that makes an offering (`Clock.Day - b.LastOfferDay <= 30`). Expected belief: `god:offer > 0` (via relief learning `A2`), `god:kind < 0`.
     - **`stern`:** strikes witnessed theft (`LastWitnessedTheftTick`) and violence (`LastWitnessedAttackTick`) within ~1 day (24 ticks) with wrathful omens, and `Lightning` on `LastWitnessedWrongdoer` when unlocked (`GodHarm >= 4.0`); otherwise sends occasional affirming omens when a camp has kept the peace. Expected belief: `god:take < 0`, `god:attack < 0`.
     - **`capricious`:** acts at random intervals (`GodRng`) with random good/bad omens, bounties, and weather regardless of what people did. Expected belief: shifting/noisy superstitions, lower `god:answers`.
     - Keep `skilful` as the "good player" reference.
5. **`Anthro.Demog/Program.cs` & `Anthro.Demog/tools/pool.py`:**
   - In `Program.cs`:
     - Add `refuse` and `thank` to the `god acts` culture line (`take`, `give`, `attack`, `offer`, `refuse`, `thank`).
     - Append `| help {region.GodHelp:F1} harm {region.GodHarm:F1}` to the `FAVOR_AUDIT` line.
   - In `pool.py`: add `charity`, `charity2`, `demanding`, `demanding2`, `malevolent`, `malevolent2`, `stern`, `stern2`, `capricious`, `capricious2`.

---

## Part 3: Testing, Runs & Reports (Follow "Don't Waste Runs")

1. **Iterate Small First (3 Seeds, 25 Years):**
   - Build `Anthro.Demog` (`dotnet build -c Release Anthro.Demog -o runs/bin`).
   - Run 3 seeds (`41..43`, 25 years) on `skilful`, `charity`, `demanding`, `malevolent`, `stern`, `capricious` to verify that:
     - No timers/hard ceilings exist;
     - Each pattern bot teaches its expected `god:<act>` profile;
     - `ANTHRO_OFF=god` on seeds `41..45` is 100% byte-identical to `runs/godoff_41..45.txt`.
2. **Full 40-World Runs (Once, on Final Code):**
   - Never re-run `godoff_41..60.txt` or `godoff2_61..80.txt`.
   - Run the updated 40-world sets for `idle`, `skilful`, `quiet` (to update `docs/reports/T11.md` honestly after removing the `T11B` shortcuts) and the 5 pattern bots `charity`, `demanding`, `malevolent`, `stern`, `capricious` (for `docs/reports/T10C.md`).
3. **Unit Tests (Once at the End):**
   - Add unit tests in `Anthro.Tests/GodLayerTests.cs` for `GodHelp`/`GodHarm` split (`C1`) and the pattern bots (`C2`), and run `dotnet test Anthro.Tests` once at the end.
4. **Reports (`docs/reports/T11.md` and `docs/reports/T10C.md`):**
   - Update `docs/reports/T11.md` with the exact 5 rows from `docs/T11-BRIEF.md` and honest verdicts (`MET` / `NOT MET`).
   - Write `docs/reports/T10C.md` comparing `idle`, `skilful`, `charity`, `demanding`, `malevolent`, `stern`, and `capricious` across all 40 worlds (paired bootstrap CIs vs `godoff`: population, starvation, dips below 15, violence, thefts, discovery, belief bands, `god:<act>` profile, favor earned/spent, `help` vs `harm`), classifying any surprising outcome as explainable or a mistake.
   - Commit on branch `task/T10C` and stop.
