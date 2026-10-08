using System;

namespace Anthro.Sim.People
{
    /// <summary>
    /// Bodies, not dice. Nobody dies from a random roll: health falls because of hunger, cold,
    /// wounds, sickness or childbirth, and an aging body holds less health, so the old die of
    /// the same things the young survive.
    /// </summary>
    public static class Physiology
    {
        /// <summary>The most health a body can hold. Full until about 40, then a slow decline to nothing in the late 80s.</summary>
        public static double MaxHealth(double age) => age < 40 ? 1 : Math.Max(0, 1 - Math.Pow((age - 40) / 46.0, 2));

        /// <summary>How grown a body is: 0 at birth, full by about 16.</summary>
        public static double Maturity(double age) => Math.Max(0, Math.Min(1, (age - 1) / 15));

        /// <summary>Strong enough to keep up on a hunt and handle a spear.</summary>
        public static double HuntCapacity(Person p, double age) => Maturity(age) * (0.6 + 0.6 * p.B(BodyTrait.Strength));

        /// <summary>Steady enough hands for knapping.</summary>
        public static double CraftCapacity(Person p, double age) => Maturity(age) * (0.6 + 0.6 * p.B(BodyTrait.Dexterity));

        /// <summary>
        /// How well someone fights off sickness today: their inherited immunity, weak in infancy and
        /// old age, worn down by hunger.
        /// </summary>
        /// <summary>
        /// How many days of their own food needs a body can carry as fat. A healthy adult forager
        /// carries roughly 6-12 kg of fat, four to eight weeks of food (people survive 45-60 days
        /// without eating). Small children carry far less, and the old somewhat less.
        /// </summary>
        public static double MaxReserve(Person p, double age)
        {
            double adult = 18 + 24 * p.B(BodyTrait.Build);
            double byAge = age < 15 ? 0.25 + 0.75 * age / 15 : age > 60 ? 0.8 : 1;
            return adult * byAge;
        }

        /// <summary>
        /// How well nourished someone is (0-1): full until their reserves fall below about 40% of
        /// what they can carry, then falling as the body thins.
        /// </summary>
        public static double Nourishment(Person p, double age) => Math.Min(1, p.Reserve / (0.4 * MaxReserve(p, age)));

        /// <summary>
        /// How much food someone can carry in one trip, in rations: a grown forager manages roughly
        /// 20-25 kg (about 12 rations) by hand and sling, more with a bundle or basket, more again with
        /// a woven basket or a net bag. Strength and growth scale it.
        /// </summary>
        public static double CarryCapacity(Person p, double age)
        {
            var k = p.Knows;
            double gear = k.Contains("bask.woven") ? 1.6 : k.Contains("bask.bundle") ? 1.3 : 1;
            if (k.Contains("cord.net")) gear += 0.2;
            if (k.Contains("cord.twine")) gear += 0.1;   // tying bundles
            if (k.Contains("cord.rope")) gear += 0.1;    // tumplines and carrying straps
            return 12 * (0.6 + 0.4 * p.B(BodyTrait.Strength)) * Math.Max(0.2, Maturity(age)) * gear;
        }

        /// <summary>How much of the fat this body can carry it has now (0-1).</summary>
        public static double BodyFat(Person p, double age) => Math.Min(1, p.Reserve / Math.Max(0.01, MaxReserve(p, age)));

        /// <summary>
        /// How readily a woman's body would conceive this month, from her energy: ovulation follows
        /// both how much fat she carries and whether she's gaining or losing (Ellison's work on
        /// forager women: a lean season's weight loss of a kilo or two measurably lowers
        /// conceptions, long before anyone is starving). Full when well padded and holding steady.
        /// Heavy daily work lowers it too, even in women who aren't thin (Jasienska and Ellison: the
        /// ovarian hormones of farm and forager women fall with workload): a woman out getting food
        /// ten hours a day is about a third less likely to conceive than one out four hours.
        /// </summary>
        public static double Fecundity(Person p, double age)
        {
            double padding = Math.Min(1, BodyFat(p, age) / 0.6);
            // Losing about a kilo a month (~0.1 of a day's food a day) roughly thirds the chance.
            double balance = Math.Max(0.3, Math.Min(1, 1 + p.WeightTrend / 0.15));
            double work = Math.Max(0.5, 1 - 0.055 * Math.Max(0, p.Workload - 4));
            return padding * balance * work;
        }

        public static double ImmuneStrength(Person p, double age)
        {
            double byAge = age < 5 ? 0.4 + 0.12 * age : age > 50 ? Math.Max(0.3, 1 - (age - 50) / 50) : 1;
            // Hunger and cold both wear the body's defenses down: this is how famine and winter mostly kill.
            // Real undernutrition weakens defenses; being a little lean doesn't (fat below ~30% starts to tell).
            double fed = age < 2 ? p.Satiety : Math.Min(p.Satiety, Math.Min(1, BodyFat(p, age) / 0.3));
            return p.B(BodyTrait.Immunity) * byAge * (0.5 + 0.5 * fed) * (1 - Math.Min(0.5, p.ColdStress * 0.5));
        }
    }
}
