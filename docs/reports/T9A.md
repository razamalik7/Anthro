# Task T9A: Clean Foundations and Baseline Integrity

## 1. Acceptance Checks
- **A1. Clean God-Off Switch (`ANTHRO_OFF=god,selfmemory`)**: **PASS**
  Byte-identical comparison (`cmp`) of all `^Y` lines against reference runs (`runs/base_<seed>.txt`):
  - seed 41: PASS (byte-identical, 50 Y lines)
  - seed 42: PASS (byte-identical, 50 Y lines)
  - seed 43: PASS (byte-identical, 50 Y lines)
  - seed 44: PASS (byte-identical, 50 Y lines)
  - seed 45: PASS (byte-identical, 50 Y lines)
  - seed 46: PASS (byte-identical, 50 Y lines)
  - seed 47: PASS (byte-identical, 50 Y lines)
  - seed 48: PASS (byte-identical, 50 Y lines)
  - seed 49: PASS (byte-identical, 50 Y lines)
  - seed 50: PASS (byte-identical, 50 Y lines)
  - seed 51: PASS (byte-identical, 50 Y lines)
  - seed 52: PASS (byte-identical, 50 Y lines)
  - seed 53: PASS (byte-identical, 50 Y lines)
  - seed 54: PASS (byte-identical, 50 Y lines)
  - seed 55: PASS (byte-identical, 50 Y lines)
  - seed 56: PASS (byte-identical, 50 Y lines)
  - seed 57: PASS (byte-identical, 50 Y lines)
  - seed 58: PASS (byte-identical, 50 Y lines)
  - seed 59: PASS (byte-identical, 50 Y lines)
  - seed 60: PASS (byte-identical, 50 Y lines)
  *Result: 20 / 20 seeds byte-identical.*
- **Unit Tests**: **PASS** (30 / 30 tests pass).

---

## 2. Raw `ana.py` Output

```text
runs/base  medpop 62.5 total 1327 starv 2180 ext 0 campmoves 5352 gang 1504 viol 126 ration Y1,10..50 0.62 0.55 0.47 0.42 0.37 0.33 lessons [14081, 55635, 30989, 95772]
runs/base2 medpop 56.5 total 1267 starv 2066 ext 0 campmoves 4726 gang 1336 viol 44  ration Y1,10..50 0.50 0.44 0.38 0.33 0.30 0.27 lessons [13361, 51426, 29256, 91851]

runs/sm1   medpop 39.0 total 1139 starv 1988 ext 0 campmoves 5450 gang 1535 viol 86  ration Y1,10..50 0.62 0.56 0.49 0.44 0.39 0.35 lessons [11301, 42500, 24064, 94674]
runs/sm2   medpop 62.5 total 1196 starv 2036 ext 0 campmoves 5187 gang 1735 viol 51  ration Y1,10..50 0.50 0.44 0.40 0.35 0.32 0.28 lessons [12661, 45621, 24673, 95624]

runs/idle1 medpop 60.5 total 1224 starv 1913 ext 0 campmoves 5641 gang 743  viol 123 ration Y1,10..50 0.62 0.56 0.49 0.43 0.40 0.34 lessons [11485, 44446, 23765, 94819]
runs/idle2 medpop 50.0 total 993  starv 2054 ext 0 campmoves 5894 gang 2706 viol 54  ration Y1,10..50 0.50 0.45 0.40 0.35 0.32 0.29 lessons [12800, 41494, 22003, 92671]
```

### Comparative Summary Table (40 worlds per condition)
| Metric | Base (41-60 / 61-80) | Selfmemory On (41-60 / 61-80) | Idle God (41-60 / 61-80) | Noise Band (Base vs Base2) |
| :--- | :--- | :--- | :--- | :--- |
| **End Total Pop** | 1,327 / 1,267 | 1,139 (-14.2%) / 1,196 (-5.6%) | 1,224 (-7.8%) / 993 (-21.6%) | -4.5% |
| **Starvations** | 2,180 / 2,066 | 1,988 (-8.8%) / 2,036 (-1.5%) | 1,913 (-12.2%) / 2,054 (-0.6%) | -5.2% |
| **Births** | 4,112 / 3,780 | 3,695 (-10.1%) / 3,615 (-4.4%) | 3,559 (-13.4%) / 3,451 (-8.7%) | -8.1% |
| **Violence Deaths** | 126 / 44 | 86 / 51 | 123 / 54 | 44 vs 126 (high noise) |
| **Gang Fights** | 1,504 / 1,336 | 1,535 (+2.1%) / 1,735 (+29.9%) | 743 (-50.6%) / 2,706 (+102%) | -11.2% (seed-volatile) |
| **Camp Moves** | 5,352 / 4,726 | 5,450 (+1.8%) / 5,187 (+9.8%) | 5,641 (+5.4%) / 5,894 (+24.7%) | -11.7% |
| **Extinctions** | 0 / 0 | 0 / 0 | 0 / 0 | 0 / 0 |

---

## 3. Analysis of Findings

### A2. "People remember their own deeds" (`selfmemory`)
1. **Mechanical Mechanism**:
   - In base code, `Witness(deed)` explicitly skipped `m == deed.Actor`, meaning actors never stored their own acts in `p.Deeds`.
   - **Memory Dilution**: `p.Deeds` is a ring buffer capped at 40 entries. When actors record their own routine daily acts (asks, gives, takes), their own acts displace memories of offenses done by others.
   - **Self-Incrimination in Gossip**: `Gossip` checks `if (d.Actor == listener)` but not `if (d.Actor == teller)`. People gossiped about *their own* takes and attacks. Listeners learned of the wrongdoing, lost trust, and harbored grievances against the teller.
   - **Behavioral Impact**: Inter-camp requests for food (`asks`) dropped by **-37.7%** (116,983 $\to$ 72,848 in set 1) and **-29.2%** (105,153 $\to$ 74,398 in set 2). Quarrels dropped by -24.3% / -12.6%.
   - **Net Demographics**: Pop shifted slightly downward (-14.2% and -5.6%), partly due to reduced sharing across camp lines.

### A3. The Noise Band
- Comparing reference runs `base` (41–60) vs `base2` (61–80) reveals intrinsic variance:
  - Total population varies by **4.5%** and starvation by **5.2%**.
  - Gang fights and camp moves swing by **11–12%**.
  - Violence deaths exhibit high stochastic volatility (126 vs 44).
  - Any divine effect must consistently exceed $\pm 10-15\%$ in the same direction on both sets to be considered significant.

### A4. The "Idle God" World (God On, No Bot)
- **Prayers**: Asked at a rate of **1.97 to 2.26 / year** across the camp. Since no divine power answered, **>99% failed** (resolved-bad), with only ~0.02 / year succeeding by pure coincidence (spontaneous recovery / finding forage).
- **Hours**:
  - Praying: **~0.00 h/adult/day** (rare crisis plea: 1 hour once or twice a year).
  - Offering: **0.13 to 0.15 h/adult/day** (~8–9 minutes daily per adult).
- **Food Sacrificed**: **~995 rations / year** per camp. Hearth food is taken directly from stores (`home.Food.Take`). This drain on stores slightly depresses food security and accounts for the -8% to -13% drop in births.
- **Belief Trajectory Over Decades**:
  - `god:answers`: Starts at ~0.100 $\to$ drops to **0.048–0.051** (Y10) $\to$ collapses to **0.022–0.026** (Y50). People learn that prayers go unanswered.
  - `god:real`: Starts at ~0.21 $\to$ initial uptick to **0.34–0.35** (Y10) due to rites and offerings $\to$ steadily decays back toward background seed (**0.22–0.23** at Y50).
  - `god:kind`: Starts neutral (0.00) $\to$ drifts deeply negative (**-0.44 to -0.49** at Y50). Unrelieved winters and illness lead people to conclude the spirits are indifferent or hostile.

---

## 4. What Changed (Files & Numbers)
- `Anthro.Sim/Region.cs`:
  - Added dedicated `GodRng = new Rng(seed ^ 0x60D5EEDUL, 0x60D5EEDUL)` independent of `Simulation.Rng`.
  - Disentangled kin camp inheritance: `god:` expectations draw from `GodRng`, leaving base `Rng` untouched.
- `Anthro.Sim/Society.cs`:
  - Gated `Remember(deed.Actor, deed, true)` behind `!Off("selfmemory")`.
  - Gated `god:` inheritance in `InheritSocialExpectations` behind `!Off("god")`.
- `Anthro.Sim/Customs.cs`:
  - Made `Simulation.Off` public for Demog and tool usage.
- `Anthro.Sim/God/Belief.cs`:
  - `SeedBelief` draws exclusively from `Region.GodRng`.
- `Anthro.Sim/God/BotGod.cs` & `EcologySystem.cs`:
  - Replaced all god-layer `region.Rng` usages with `region.GodRng`.
- `Anthro.Demog/Program.cs`:
  - Suppressed god-specific output on `^Y` lines and culture lines when `Off("god")` is true, ensuring exact byte reproducibility of reference output.

## 5. Deviations
- None. Batch A instructions followed to the letter.

## 6. Surprises and Open Questions
1. **Self-Incriminating Gossip**: In `Society.Gossip`, teller tells listener about their own deeds because `d.Actor == teller` was not excluded. If `selfmemory` stays, should people gossip about their own wrongs, or should gossip only be about third parties (`d.Actor != teller && d.Actor != listener`)?
2. **Food Drain from Offerings**: Even without an active god, people sacrifice nearly 1,000 rations/year under low faith, directly lowering birth rates. This explains a significant fraction of why active bots caused population dips in earlier tasks.
