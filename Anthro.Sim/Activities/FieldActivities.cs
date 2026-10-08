using System;
using System.Collections.Generic;
using System.Linq;
using Anthro.Sim.People;
using Anthro.Sim.World;

namespace Anthro.Sim.Activities
{
    /// <summary>Walk out → gather → walk back. Yield depends on the patch, skill, age and interest.</summary>
    public sealed class Forage : Activity
    {
        public const double FallbackPerHour = 0.4;

        enum Phase { Out, Gather, Back }

        Phase _phase;
        Cell? _patch;
        int _travel, _left, _hours;
        double _gathered, _storable, _eatenHere;

        public Forage(Person p) : base(p) { }
        public override Verb Verb => Verb.Forage;

        public override void Begin(Simulation sim)
        {
            // Curious people sometimes try somewhere farther afield; that's how the band learns its land.
            if (sim.Rng.Chance(P.M(MindTrait.Curiosity) * 0.25))
            {
                _patch = null;
                for (int i = 0; i < 20 && _patch == null; i++)
                {
                    double reach = WorldMap.FromMeters(7000);
                    int x = (int)(sim.Band.CampX + sim.Rng.Range(-reach, reach)), y = (int)(sim.Band.CampY + sim.Rng.Range(-reach, reach));
                    if (sim.World.InBounds(x, y) && sim.World[x, y].PlantCapacity > 0) _patch = sim.World[x, y];
                }
                P.Nudge(Interest.Plants, 0.002);
            }
            else
            {
                // From the places they know: what they expect there this season, for the walk.
                // Someone stocking up for winter goes for food that keeps.
                bool stocking = Decider.Foresight(sim, P) > 0.15;
                double reach = WorldMap.FromMeters(6000);
                var best = new List<(double score, int key)>();
                foreach (var kv in P.Map.Places)
                {
                    var note = kv.Value;
                    if (note.Plant == PlantFood.None || stocking && !Cell.Storable(note.Plant)) continue;
                    var (x, y) = MentalMap.FromKey(kv.Key);
                    double d = WorldMap.Distance(x, y, sim.Band.CampX, sim.Band.CampY);
                    if (d > reach) continue;
                    double expect = P.Places[(int)sim.Clock.Season].TryGetValue(kv.Key, out var yielded) ? yielded * 4 : sim.ExpectedPlants(note) * Cell.Richness(note.Plant);
                    if (expect < 0.3) continue;
                    best.Add((expect / (1 + d * WorldMap.CellMeters / 1000 * 0.3), kv.Key));
                }
                best.Sort((a, b) => b.score.CompareTo(a.score));
                if (best.Count > 0)
                {
                    var (px, py) = MentalMap.FromKey(best[sim.Rng.Range(0, Math.Min(6, best.Count))].key);
                    _patch = sim.World[px, py];
                }
                else
                {
                    // Nothing they know of seems worth it: go and look somewhere new.
                    for (int i = 0; i < 20 && _patch == null; i++)
                    {
                        double r = WorldMap.FromMeters(5000);
                        int x = (int)(sim.Band.CampX + sim.Rng.Range(-r, r)), y = (int)(sim.Band.CampY + sim.Rng.Range(-r, r));
                        if (sim.World.InBounds(x, y) && sim.World[x, y].PlantCapacity > 0) _patch = sim.World[x, y];
                    }
                }
            }
            _travel = _patch == null ? 1 : Math.Max(1, sim.TravelHours(WorldMap.Distance(_patch.X, _patch.Y, sim.Band.CampX, sim.Band.CampY), P));
            _left = _travel;
            _phase = Phase.Out;
            Step = "walking out";
        }

        public override void Hour(Simulation sim)
        {
            _hours++;
            P.Fatigue = Inheritance.Clamp01(P.Fatigue + 0.04);
            switch (_phase)
            {
                case Phase.Out:
                    if (--_left > 0) return;
                    _phase = Phase.Gather;
                    _left = Math.Max(1, Math.Min(4 + (int)(P.M(MindTrait.Patience) * 3), sim.Clock.DaylightLeft - _travel));
                    Step = _patch == null ? "searching" : "gathering";
                    return;
                case Phase.Gather:
                    if (_patch != null) GatherHour(sim, _patch);
                    // Full hands (or a full basket) end the gathering.
                    if (_gathered >= Physiology.CarryCapacity(P, P.AgeYears(sim.Clock))) _left = 1;
                    if (--_left > 0) return;
                    _phase = Phase.Back;
                    _left = _travel;
                    Step = "walking back";
                    return;
                default:
                    if (--_left > 0) return;
                    Finish(sim);
                    return;
            }
        }

        bool _looked;

        void GatherHour(Simulation sim, Cell patch)
        {
            if (!_looked) { sim.LookAround(P, patch.X, patch.Y); _looked = true; }
            P.X = patch.X; P.Y = patch.Y;
            double age = P.AgeYears(sim.Clock);
            double ageFactor = age < 14 ? age / 14 : age > 55 ? 0.7 : 1;
            // Holling type II: returns rise with how dense the food is and level off where it's everywhere
            // (you can only pick and carry so fast). A thinned patch means more searching per mouthful.
            // At full density an average forager makes ~1 ration an hour: within field measurements of
            // 1,000-3,000 kcal/hour.
            double handling = 1.1 * (0.5 + P.S(Skill.Foraging)) * (1 + 0.3 * P.I(Interest.Plants)) * ageFactor;
            var k = P.Knows;
            // Knowing plants well, and ways of getting more out of hard foods.
            if (k.Contains("forage.lore")) handling *= 1.15;
            if (patch.Plant == PlantFood.Nuts)
                // Nuts are hard work raw (cracking, and bitter kinds are useless, though sweet ones only need cracking); leaching the bitterness
                // out makes them food, pounding them to meal makes them a staple (acorn peoples lived on it).
                handling *= Simulation.Off("nuts") ? (k.Contains("forage.nuts") ? 1.25 : 1) * (k.Contains("grind.pound") ? 1.1 : 1) * (k.Contains("grind.mortar") ? 1.1 : 1)
                    : (k.Contains("forage.nuts") ? 1.0 : 0.75) * (k.Contains("grind.pound") ? 1.3 : 1) * (k.Contains("grind.mortar") ? 1.15 : 1);
            if (patch.Plant == PlantFood.WildGrain)
                // Wild grass seed is a poor food until it can be ground: pounding helps, a grinding slab
                // makes flour of it.
                handling *= (k.Contains("grain.harvest") ? 1.4 : 1) * (k.Contains("bask.tight") ? 1.1 : 1)
                            * (Simulation.Off("nuts") ? (k.Contains("grind.slab") ? 1.15 : 1) : k.Contains("grind.slab") ? 1.3 : k.Contains("grind.pound") ? 0.9 : 0.7);
            // Under snow, only what can be dug or reached counts.
            double density = patch.PlantStock * patch.SnowAccess;
            double rate = handling * density / (density + 2.0) * Cell.Richness(patch.Plant) / 0.8;
            double take = Math.Min(patch.PlantStock, rate);
            patch.PlantStock -= take;
            // Famine food: in the lean months someone from a hearth with almost nothing left, already
            // getting thin, strips the inner bark of pine or birch to dry and pound (Scandinavian bark
            // bread, the Adirondack "bark eaters"). Slow work for little (~300 kcal an hour) and the
            // woods near camp are soon stripped: it buys days, not seasons.
            var season = sim.Clock.Season;
            if (take < 0.05 && patch.Terrain == Terrain.Forest && patch.Bark > 0 && (season == Core.Season.Winter || season == Core.Season.Spring)
                && sim.HomeOf(P).DaysOfFood < 1 && Physiology.BodyFat(P, age) < 0.5)
            {
                double bark = Math.Min(patch.Bark, 0.12 * (0.5 + P.S(Skill.Foraging)) * ageFactor);
                patch.Bark -= bark;
                take += bark;
            }
            // A hungry forager eats some of what they find on the spot.
            double need = sim.DailyNeed(P);
            // A parent with hungry children at home eats only what they must and carries the rest back.
            double snackBelow = P.HasHungryDependents(sim.Clock) ? 0.5 - 0.4 * P.M(MindTrait.Warmth) : 0.5;
            if (P.Stomach < snackBelow && need > 0)
            {
                double eat = Math.Min(take, (0.8 - P.Stomach) * need);
                P.Stomach += eat / need;
                _eatenHere += eat;
                take -= eat;
            }
            _gathered += take;
            if (Cell.Storable(patch.Plant)) _storable += take;
            P.Practice(Skill.Foraging, 1);
        }

        void Finish(Simulation sim)
        {
            P.X = sim.Band.CampX; P.Y = sim.Band.CampY;
            var home = sim.HomeOf(P);
            home.Food.AddPlants(_storable, keeps: true);
            sim.ThisYear.Gathered += _gathered;
            P.ProvidedToday += _gathered;
            home.Food.AddPlants(_gathered - _storable, keeps: false);
            // What they ate on the spot counts: it's food the trip brought in.
            double perHour = (_gathered + _eatenHere) / Math.Max(1, _hours);
            lock (Activities.HuntParty.DebugEnds)
            {
                var d = Activities.HuntParty.DebugEnds;
                d["~forage-hours"] = d.TryGetValue("~forage-hours", out var fh) ? fh + _hours : _hours;
                d["~forage-gather-hours"] = d.TryGetValue("~forage-gather-hours", out var gh) ? gh + (_hours - 2 * _travel) : _hours - 2 * _travel;
                d["~forage-rations"] = d.TryGetValue("~forage-rations", out var fr) ? fr + (int)(_gathered + _eatenHere) : (int)(_gathered + _eatenHere);
                d["~forage-trips"] = d.TryGetValue("~forage-trips", out var ft) ? ft + 1 : 1;
                d["~forage-travel-hours"] = d.TryGetValue("~forage-travel-hours", out var th) ? th + 2 * _travel : 2 * _travel;
            }
            // A trip well below what they'd come to expect leaves people restless.
            if (perHour < P.Expect.Get(Verb.Forage, FallbackPerHour) * 0.7) P.Restless = true;
            P.Expect.Learn(P, Verb.Forage, perHour, FallbackPerHour);
            sim.JudgeMove(P, perHour);
            if (_patch != null)
            {
                P.RememberPlace(_patch.X, _patch.Y, sim.Clock.Season, perHour);
                P.Map.See(_patch, sim.Clock.Day);
                sim.CampNews(P, _patch);
            }
            if (perHour > FallbackPerHour) P.Nudge(Interest.Plants, 0.005);
            Done = true;
        }
    }

    /// <summary>A trip to the hills for flint. Without flint there are no new tools.</summary>
    public sealed class GatherFlint : Activity
    {
        int _hoursLeft;
        Cell? _source;

        public GatherFlint(Person p) : base(p) { }
        public override Verb Verb => Verb.GatherFlint;

        public override void Begin(Simulation sim)
        {
            _source = sim.World.FlintCells.OrderBy(c => WorldMap.Distance(c.X, c.Y, sim.Band.CampX, sim.Band.CampY)).FirstOrDefault();
            _hoursLeft = _source == null ? 1 : 2 + 2 * sim.TravelHours(WorldMap.Distance(_source.X, _source.Y, sim.Band.CampX, sim.Band.CampY), P);
            Step = "walking to the flint";
        }

        public override void Hour(Simulation sim)
        {
            P.Fatigue = Inheritance.Clamp01(P.Fatigue + 0.05);
            if (--_hoursLeft > 0) return;
            if (_source != null)
            {
                sim.Band.Materials.Flint += 3 + sim.Rng.Range(0, 3);
                P.Nudge(Interest.StoneEarth, 0.01);
            }
            Done = true;
        }
    }
}
