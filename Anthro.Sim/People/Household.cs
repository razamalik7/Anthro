using System.Collections.Generic;
using System.Linq;

namespace Anthro.Sim.People
{
    /// <summary>
    /// A family hearth: a couple (or a single adult) and the children they're raising. Plant food
    /// belongs to the household that gathered it; families eat from their own store and share with
    /// others when asked.
    /// </summary>
    public sealed class Household
    {
        public readonly int Id;
        public readonly List<Person> Members = new List<Person>();
        public readonly FoodStore Food = new FoodStore();
        /// <summary>Firewood for the hearth, in days of a cooking fire. When it runs out, the fire goes out: no cooking, no warmth.</summary>
        public double Fuel = 6;
        /// <summary>Until this day the household has soft food (soups, mush) that toddlers and toothless elders can live on.</summary>
        public long SoftFoodUntil = -1;
        public bool FireLit => Fuel > 0;
        /// <summary>Food that has rotted lately (fades over about ten days): what makes people think about keeping food.</summary>
        public double SpoiledRecent;
        /// <summary>Keeping food (nuts, grain, dried food) gone bad lately, over about a season: stores held too long.</summary>
        public double StoresSpoiledRecent;
        /// <summary>
        /// This season's budgeting, which is what teaches the rationing custom: whether the family had to
        /// budget at all, the keeping food they had when they started to, the food they held back by it,
        /// the keeping food that rotted anyway, and whether they ran out regardless.
        /// </summary>
        public bool HadToBudget, RanOut;
        public double BudgetStart, HeldBack, StoresRotted, StoresKept;

        public Household(int id) { Id = id; }

        public IEnumerable<Person> Adults(Core.SimClock clock) => Members.Where(m => m.AgeYears(clock) >= 14);

        /// <summary>Whether any grown member of the household knows a stage.</summary>
        public bool Knows(string stageId, Core.SimClock clock) => Adults(clock).Any(a => a.Knows.Contains(stageId));

        /// <summary>Days of food per mouth in the household store.</summary>
        public double DaysOfFood => Members.Count == 0 ? 0 : Food.Total / (Members.Count * 0.9);
        /// <summary>Food the hearth has been bringing in per day lately (about a month's memory).</summary>
        public double Income => (Food.AnimalIn + Food.PlantIn) * 0.03;
    }
}
