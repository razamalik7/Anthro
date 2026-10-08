using System;
using System.Collections.Generic;
using System.Linq;
using Anthro.Sim.Core;

namespace Anthro.Sim.People
{
    /// <summary>
    /// A household's food, kept by kind because each spoils at a different rate:
    /// raw meat, raw plant food, keeping-food (nuts, grain), cooked food, and dried food.
    /// </summary>
    public sealed class FoodStore
    {
        public double Meat, Plants, Stored, Cooked, Dried;
        /// <summary>Average age of the raw meat, in days. Past a few days it makes people sick (unless cooked).</summary>
        public double MeatAge;

        public double Total => Meat + Plants + Stored + Cooked + Dried;
        public double Raw => Meat + Plants + Stored;
        public double Perishable => Meat + Plants + Cooked;

        /// <summary>
        /// Food brought to this hearth over the past month or so, animal (meat and fish) and plant:
        /// what the family has been living on lately.
        /// </summary>
        public double AnimalIn, PlantIn;

        /// <summary>The share of animal food in what the hearth has been getting lately (a typical mix if nothing yet).</summary>
        public double AnimalShare => AnimalIn + PlantIn < 1 ? 0.3 : AnimalIn / (AnimalIn + PlantIn);

        public void AddPlants(double rations, bool keeps)
        {
            if (rations <= 0) return;
            if (keeps) Stored += rations; else Plants += rations;
            PlantIn += rations;
        }

        public void AddMeat(double rations)
        {
            if (rations <= 0) return;
            AnimalIn += rations;
            MeatAge = Meat * MeatAge / (Meat + rations);
            Meat += rations;
        }

        /// <summary>Eat cooked food first, then what spoils fastest. Reports how much was raw meat and how much raw food overall.</summary>
        public double Take(double amount, out double rawMeat, out double raw)
        {
            double taken = TakeFrom(ref Cooked, amount);
            rawMeat = TakeFrom(ref Meat, amount - taken);
            taken += rawMeat;
            double plants = TakeFrom(ref Plants, amount - taken);
            taken += plants;
            taken += TakeFrom(ref Dried, amount - taken);
            double stored = TakeFrom(ref Stored, amount - taken);
            taken += stored;
            raw = rawMeat + plants + stored;
            return taken;
        }

        public double Take(double amount, out double rawMeat) => Take(amount, out rawMeat, out _);

        public static double TakeFrom(ref double pool, double want)
        {
            double t = Math.Min(pool, Math.Max(0, want));
            pool -= t;
            return t;
        }

        /// <summary>
        /// Raw meat rots in days unless it's freezing (and keeps well buried in snow, if they know
        /// to); greens and roots in about a week; cooked food in a few days. Nuts, grain and dried
        /// food keep for months, longer the better they're stored, and far longer in the cold: the moulds,
        /// rot and insects that spoil them slow by about half for every 10 °C cooler (a Q10 of about 2,
        /// the usual figure for decay), so a winter cache keeps where a summer one goes bad. The rates
        /// below are for a warm summer day (20 °C).
        /// </summary>
        public void DailySpoil(double temperatureC, int storageLevel, bool coldCaches, double care = 1)
        {
            Meat *= temperatureC < 0 ? (coldCaches ? 0.995 : 0.97) : 0.8;
            MeatAge = Meat < 0.5 ? 0 : MeatAge + (temperatureC < 0 ? 0.2 : 1);
            Plants *= 0.92;
            Cooked *= 0.85;
            double warmth = Simulation.Off("coldrot") ? 1 : Math.Pow(2, (temperatureC - 20) / 10);
            Stored *= 1 - (0.004 - storageLevel * 0.001) * care * warmth;
            Dried *= 1 - (0.003 - storageLevel * 0.0007) * care * warmth;
            AnimalIn *= 0.97; PlantIn *= 0.97;
        }
    }

    public sealed class Materials
    {
        public int Hides, Sinew, Bone, Flint;
        public double Tools;
    }

    public sealed class Band
    {
        public string Name = "";
        public readonly List<Person> Members = new List<Person>();
        public readonly List<Person> Dead = new List<Person>();
        public readonly List<Household> Households = new List<Household>();

        /// <summary>All the food in camp, across households.</summary>
        public double FoodTotal => Households.Sum(h => h.Food.Total);
        public double MeatTotal => Households.Sum(h => h.Food.Meat);
        public readonly Materials Materials = new Materials();
        /// <summary>The band's dogs, and how well fed they've been lately (0-1).</summary>
        public int Dogs;
        public double DogsFed = 1;
        public double CampX, CampY;

        /// <summary>Waste around camp. Builds with people and days in one place; moving to a fresh site leaves it behind.</summary>
        public double Filth;

        /// <summary>
        /// The band's custom about who moves when a couple spans two bands: +1 the woman joins the man's
        /// people, -1 the man joins hers. Starts with no custom; forms from what couples actually do.
        /// </summary>
        public double ResidenceNorm;

        /// <summary>
        /// The band's custom about whom children stay with when a couple parts: above 0, the mother;
        /// below, the father. Starts slightly mother-side (nursing); moves with what couples do.
        /// </summary>
        public double ChildrenFollowMother = 0.3;

        /// <summary>
        /// When this band counts someone as grown: able to pair, to join a raid, to stand for a
        /// hearth. A custom, not a rule of the body; bands differ, and rites could move it.
        /// </summary>
        public double ComingOfAge = 15;

        /// <summary>When someone in the band last died.</summary>
        public long LastDeathDay = -1000;
        /// <summary>Hunts lately that failed because the animals bolted before anyone got close (fades over weeks).</summary>
        public double StalkFailures;

        /// <summary>
        /// What this band finds attractive, by trait. Culture, not biology: it drifts with what
        /// the band admires, and it's how sexual selection pushes evolution.
        /// </summary>
        public readonly double[] AttractionMind = new double[TraitCounts.Mind];
        public readonly double[] AttractionBody = new double[TraitCounts.Body];

        public IEnumerable<Person> Adults(SimClock clock) => Members.Where(p => p.IsAdult(clock));

        /// <summary>
        /// How dangerous the band looks to a predator: healthy, armed adults plus fire.
        /// Predators lurk when this is high and attack when it's low.
        /// </summary>
        public double Strength(SimClock clock) =>
            3 + Members.Where(p => { var a = p.AgeYears(clock); return a >= 14 && a < 60; })
                .Sum(p => p.Health * (1 - p.Injury) * (0.5 + p.B(BodyTrait.Strength)));

        public bool AnyoneKnows(string stageId) => Members.Any(p => p.Knows.Contains(stageId));
    }
}
