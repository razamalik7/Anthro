using System;
using System.Linq;
using Anthro.Sim.People;
using Anthro.Sim.World;

namespace Anthro.Sim.Activities
{
    /// <summary>
    /// What cooking and preserving yield, by what the cook knows. These are world facts about food:
    /// cooking unlocks energy (most for starchy roots and grain), kills what rotten meat carries, and
    /// soft cooked food lets toddlers and toothless elders eat.
    /// </summary>
    public static class Cuisine
    {
        /// <summary>How much more food value cooking gets out of meat, raw plant food, and nuts/grain.</summary>
        public static (double meat, double plants, double stored) Gain(Person cook)
        {
            var k = cook.Knows;
            double meat = 1.0, plants = 1.0, stored = 1.0;
            if (k.Contains("fire.make")) { meat = 1.15; plants = 1.1; stored = 1.15; }     // roasting
            if (k.Contains("cook.hotstone")) { meat = 1.2; plants = 1.2; stored = 1.2; }
            if (k.Contains("cook.boil")) { meat = 1.25; plants = 1.3; stored = 1.3; }
            if (k.Contains("cook.oven")) { plants = Math.Max(plants, 1.45); stored = Math.Max(stored, 1.3); }
            if (k.Contains("cook.bread")) stored = Math.Max(stored, 1.5);
            if (k.Contains("cook.pot")) { meat += 0.05; plants += 0.05; stored += 0.05; }
            return (meat, plants, stored);
        }

        public static bool MakesSoftFood(Person cook) => cook.Knows.Contains("cook.boil") || cook.Knows.Contains("cook.pot");

        /// <summary>How well stored food keeps: 0 (none) to 3 (raised caches).</summary>
        public static int StorageLevel(Household h, Core.SimClock clock) =>
            h.Knows("store.raised", clock) ? 3 : h.Knows("store.pit", clock) || h.Knows("store.basket", clock) ? 2 : h.Knows("store.cache", clock) ? 1 : 0;

        /// <summary>
        /// Firewood a hearth burns in a day: cooking, plus keeping warm, more the colder it is (about
        /// 0.8 on a warm summer day, 2.5 at a few degrees below freezing).
        /// </summary>
        public static double DailyBurn(double temperatureC) => 0.8 + Math.Max(0, 20 - temperatureC) * 0.07;
    }

    /// <summary>
    /// Cooking at the family hearth: turns raw food into cooked food the household will eat over the
    /// next day or so. Takes the cook's time and the hearth's firewood.
    /// </summary>
    public sealed class Cook : TimedActivity
    {
        public Cook(Person p) : base(p, 2) { Step = "cooking"; }
        public override Verb Verb => Verb.Cook;
        protected override double FatiguePerHour => 0.01;

        protected override void OnHour(Simulation sim) => P.Practice(Skill.Cooking, 1);

        protected override void OnEnd(Simulation sim)
        {
            var home = sim.HomeOf(P);
            if (!home.FireLit) { Step = "no fire to cook on"; return; }
            var food = home.Food;
            double target = Math.Max(0, home.Members.Count * 0.9 * 1.5 - food.Cooked);
            double capacity = 8 * (0.5 + P.S(Skill.Cooking));
            double todo = Math.Min(target, capacity);
            var (gm, gp, gs) = Cuisine.Gain(P);
            double meat = FoodStore.TakeFrom(ref food.Meat, todo);
            double plants = FoodStore.TakeFrom(ref food.Plants, todo - meat);
            double stored = FoodStore.TakeFrom(ref food.Stored, todo - meat - plants);
            double cooked = meat * gm + plants * gp + stored * gs;
            food.Cooked += cooked;
            sim.ThisYear.CookingGain += cooked - (meat + plants + stored);
            if (Cuisine.MakesSoftFood(P)) home.SoftFoodUntil = sim.Clock.Day + 1;

            // Boiling cracked bones renders their grease: a rich, keeping fat from what was thrown away.
            if (P.Knows.Contains("cook.grease") && sim.Band.Materials.Bone > 0)
            {
                int bones = Math.Min(4, sim.Band.Materials.Bone);
                sim.Band.Materials.Bone -= bones;
                food.Dried += bones * 1.5;
            }
            home.Fuel = Math.Max(0, home.Fuel - 0.3);
            P.Nudge(Interest.Hearth, 0.004);
        }
    }

    /// <summary>A trip for firewood: to the nearest place with dead wood lying about, and back with a load.</summary>
    public sealed class GatherWood : Activity
    {
        int _hoursLeft, _work;
        Cell? _source;

        public GatherWood(Person p) : base(p) { }
        public override Verb Verb => Verb.GatherWood;

        public override void Begin(Simulation sim)
        {
            // The nearest place they know with deadwood lying about; failing that, what's visible around camp.
            Cell? best = null;
            double bestKey = double.MaxValue;
            foreach (var kv in P.Map.Places)
            {
                if (kv.Value.WoodSeen < 1) continue;
                var (x, y) = MentalMap.FromKey(kv.Key);
                double d = WorldMap.Distance(x, y, sim.Band.CampX, sim.Band.CampY);
                if (d > WorldMap.FromMeters(3000)) continue;
                double key = d - kv.Value.WoodSeen * 0.05;
                if (key < bestKey) { bestKey = key; best = sim.World[x, y]; }
            }
            // Nowhere they know of: look for dead wood in the nearest woods they can see from camp.
            _source = best ?? sim.World.Around(sim.Band.CampX, sim.Band.CampY, WorldMap.FromMeters(2000))
                .Where(c => c.Terrain == Terrain.Forest).OrderBy(c => WorldMap.Distance(c.X, c.Y, sim.Band.CampX, sim.Band.CampY)).FirstOrDefault();
            _work = 3;
            _hoursLeft = _source == null ? 1 : _work + 2 * sim.TravelHours(WorldMap.Distance(_source.X, _source.Y, sim.Band.CampX, sim.Band.CampY), P);
            Step = "fetching firewood";
        }

        public override void Hour(Simulation sim)
        {
            P.Fatigue = Inheritance.Clamp01(P.Fatigue + 0.05);
            if (--_hoursLeft > 0) return;
            if (_source != null)
            {
                double axe = P.Knows.Contains("wood.axe") ? 1.5 : 1;
                double load = Math.Min(_source.Wood, _work * 1.5 * axe * (0.6 + P.B(BodyTrait.Strength) * 0.6) * Physiology.Maturity(P.AgeYears(sim.Clock)));
                _source.Wood -= load;
                sim.LookAround(P, _source.X, _source.Y);
                sim.CampNews(P, _source);
                sim.HomeOf(P).Fuel += load;
                P.Nudge(Interest.Hearth, 0.002);
            }
            Done = true;
        }
    }

    /// <summary>
    /// Drying, smoking, and pounding food into keeping-food. Only for those who know how; it turns
    /// food that would rot in days into food that keeps for months.
    /// </summary>
    public sealed class Preserve : TimedActivity
    {
        public Preserve(Person p) : base(p, 3) { Step = "drying food"; }
        public override Verb Verb => Verb.Preserve;

        public static bool CanPreserve(Person p) => p.Knows.Contains("dry.keep");

        protected override void OnHour(Simulation sim) => P.Practice(Skill.Cooking, 0.5);

        protected override void OnEnd(Simulation sim)
        {
            var home = sim.HomeOf(P);
            var food = home.Food;
            var k = P.Knows;
            bool smoking = k.Contains("dry.smoke") && home.FireLit;
            Step = smoking ? "smoking food" : "drying food";
            // Keep a couple of days' fresh food for eating; preserve the rest.
            double keepFresh = home.Members.Count * 0.9 * 2;
            double surplus = Math.Max(0, food.Perishable - keepFresh);
            // Slicing meat thin and laying it out (the sun and wind do the rest): a few rations' worth an
            // hour for a practised hand, about twice that with racks to hang it on.
            double rate = (k.Contains("dry.rack") ? 30 : 15) * (0.5 + P.S(Skill.Cooking));
            double todo = Math.Min(surplus, rate);
            double meat = FoodStore.TakeFrom(ref food.Meat, todo);
            double plants = FoodStore.TakeFrom(ref food.Plants, todo - meat);
            double efficiency = smoking ? 0.95 : k.Contains("dry.rack") ? 0.85 : 0.75;
            // Pemmican: dried meat pounded with fat keeps longest and packs the most food.
            double pemmican = k.Contains("dry.pemmican") ? 1.1 : 1.0;
            food.Dried += meat * efficiency * pemmican + plants * efficiency;
            if (smoking) home.Fuel = Math.Max(0, home.Fuel - 0.3);
            P.Nudge(Interest.Hearth, 0.004);
        }
    }
}
