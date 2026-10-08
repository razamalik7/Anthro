using System;
using System.Collections.Generic;
using System.Linq;
using Anthro.Sim.Core;
using Anthro.Sim.People;
using Anthro.Sim.World;

namespace Anthro.Sim.Activities
{
    /// <summary>
    /// How well each known way of fishing works, per hour of an average fisher at a well-stocked
    /// stretch of river. Weirs and nets come into their own when a run fills the river.
    /// </summary>
    public static class FishingMethods
    {
        public static double Efficiency(Person p, bool run) => Efficiency(p, run, null);

        /// <summary>
        /// How well someone fishes here. A weir is a thing built across a particular stretch, not a skill
        /// one carries: it only works where one stands.
        /// </summary>
        public static double Efficiency(Person p, bool run, Cell? spot)
        {
            var k = p.Knows;
            double e = 0;
            if (k.Contains("fish.grope")) e = 0.35;                    // groping, stranded fish, shellfish
            if (k.Contains("fish.spear")) e = Math.Max(e, 0.7);
            if (k.Contains("fish.line")) e = Math.Max(e, 0.9);
            if (k.Contains("fish.trap")) e = Math.Max(e, 1.0);
            if (spot != null && spot.Weir >= 0.5) e = Math.Max(e, (run ? 2.5 : 0.8) * spot.Weir);
            if (k.Contains("fish.net")) e = Math.Max(e, run ? 3.0 : 1.4);
            if (k.Contains("bone.harpoon")) e = Math.Max(e, 1.1);
            return e;
        }

        public static bool CanFish(Person p) => Efficiency(p, false) > 0;
    }

    /// <summary>
    /// Walk to the river → fish → walk back. The catch depends on how many fish are in that stretch
    /// (a run fills it; heavy fishing empties it), the methods the fisher knows, their skill and
    /// patience, and ice in deep winter. Fish spoil like meat.
    /// </summary>
    public sealed class Fish : Activity
    {
        public const double FallbackPerHour = 0.3;

        enum Phase { Out, Fishing, Back }

        Phase _phase;
        Cell? _spot;
        int _travel, _left, _hours;
        double _caught, _eatenHere;

        public Fish(Person p) : base(p) { }
        public override Verb Verb => Verb.Fish;

        public override void Begin(Simulation sim)
        {
            // The fishing spots they know, by the fish they expect there, for the walk.
            var known = new List<(double score, int key)>();
            foreach (var kv in P.Map.Places)
            {
                double fish = sim.ExpectedFish(kv.Value);
                if (fish < 1) continue;
                var (x, y) = MentalMap.FromKey(kv.Key);
                double d = WorldMap.Distance(x, y, sim.Band.CampX, sim.Band.CampY);
                if (d > WorldMap.FromMeters(P.Knows.Contains("wood.canoe") ? 12000 : 6000)) continue;
                known.Add((fish / (1 + d * WorldMap.CellMeters / 1000 * 0.3), kv.Key));
            }
            known.Sort((a, b) => b.score.CompareTo(a.score));
            if (known.Count > 0)
            {
                var (fx, fy) = MentalMap.FromKey(known[sim.Rng.Range(0, Math.Min(4, known.Count))].key);
                _spot = sim.World[fx, fy];
            }
            else _spot = null;
            _travel = _spot == null ? 1 : Math.Max(1, sim.TravelHours(WorldMap.Distance(_spot.X, _spot.Y, sim.Band.CampX, sim.Band.CampY), P));
            _left = _travel;
            _phase = Phase.Out;
            Step = "walking to the river";
        }

        public override void Hour(Simulation sim)
        {
            _hours++;
            P.Fatigue = Inheritance.Clamp01(P.Fatigue + 0.03);
            switch (_phase)
            {
                case Phase.Out:
                    if (--_left > 0) return;
                    _phase = Phase.Fishing;
                    _left = Math.Max(1, Math.Min(3 + (int)(P.M(MindTrait.Patience) * 4), sim.Clock.DaylightLeft - _travel));
                    Step = _spot == null ? "looking for fish" : "fishing";
                    return;
                case Phase.Fishing:
                    if (_spot != null) FishHour(sim, _spot);
                    if (_caught >= Physiology.CarryCapacity(P, P.AgeYears(sim.Clock))) _left = 1;
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

        void FishHour(Simulation sim, Cell spot)
        {
            if (!_looked) { sim.LookAround(P, spot.X, spot.Y); _looked = true; }
            P.X = spot.X; P.Y = spot.Y;
            double age = P.AgeYears(sim.Clock);
            double ageFactor = age < 14 ? age / 14 : age > 60 ? 0.7 : 1;
            bool run = spot.FishStock > spot.FishCapacity * 1.5;
            // Someone who knows how builds a weir across a stretch that has none (a dozen hours' work, more
            // hands make it quicker); one that stands gets mended by whoever fishes there.
            if (P.Knows.Contains("fish.weir") && spot.Weir < 1 && spot.FishCapacity > 0)
            {
                spot.Weir = Math.Min(1, spot.Weir + 0.08 * (0.5 + P.B(BodyTrait.Strength)));
                if (spot.Weir < 0.5) { P.Practice(Skill.Fishing, 0.5); Step = "building a weir"; return; }
            }
            else if (spot.Weir > 0) spot.Weir = Math.Min(1, spot.Weir + 0.02);
            double handling = FishingMethods.Efficiency(P, run, spot) * (0.5 + P.S(Skill.Fishing)) * (0.8 + 0.4 * P.M(MindTrait.Patience)) * ageFactor;
            // Under ice, fish are few and hard to reach.
            if (sim.CampCell.Temperature < 0) handling *= 0.4;
            // Holling type II, as with plants: a thinned stretch means more waiting per fish.
            double rate = handling * spot.FishStock / (spot.FishStock + 20);
            double take = Math.Min(spot.FishStock, rate);
            spot.FishStock -= take;
            double need = sim.DailyNeed(P);
            if (P.Stomach < 0.4 && need > 0 && take > 0)
            {
                double eat = Math.Min(take, (0.7 - P.Stomach) * need);
                P.Stomach += eat / need;
                _eatenHere += eat;
                take -= eat;
            }
            _caught += take;
            P.Practice(Skill.Fishing, 1);
        }

        void Finish(Simulation sim)
        {
            P.X = sim.Band.CampX; P.Y = sim.Band.CampY;
            sim.HomeOf(P).Food.AddMeat(_caught);
            if (_spot != null) { P.Map.See(_spot, sim.Clock.Day); sim.CampNews(P, _spot); }
            sim.ThisYear.Fished += _caught + _eatenHere;
            P.ProvidedToday += _caught;
            double perHour = (_caught + _eatenHere) / Math.Max(1, _hours);
            P.Expect.Learn(P, Verb.Fish, perHour, FallbackPerHour);
            if (perHour > FallbackPerHour) P.Nudge(Interest.Water, 0.005);
            Done = true;
        }
    }
}
