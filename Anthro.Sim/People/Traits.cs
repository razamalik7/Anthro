using System;
using Anthro.Sim.Core;

namespace Anthro.Sim.People
{
    public enum MindTrait { Curiosity, Boldness, Sociability, Patience, Openness, Temper, Warmth, Credulity, Charisma, Aptitude, Resilience }

    public enum BodyTrait { Strength, Dexterity, Endurance, ColdTolerance, Height, Build, Immunity }

    public enum Interest { StoneEarth, Wood, Plants, Animals, Water, Fire, People, Spirits, Sky, Art, Healing, Hearth }

    public enum Skill { Foraging, Hunting, Knapping, Fire, Crafting, Caring, Teaching, Cooking, Fishing }

    /// <summary>
    /// What a person cares about, and how much: the weights behind their choices. Inherited from
    /// both parents with small changes, shaped by what happens to them, and copied from the people
    /// around them, so each band grows its own way of deciding.
    /// </summary>
    public enum Drive { Food, Safety, Family, Standing, Comfort, Company }

    public static class TraitCounts
    {
        public static readonly int Mind = Enum.GetValues(typeof(MindTrait)).Length;
        public static readonly int Body = Enum.GetValues(typeof(BodyTrait)).Length;
        public static readonly int Interests = Enum.GetValues(typeof(Interest)).Length;
        public static readonly int Skills = Enum.GetValues(typeof(Skill)).Length;
        public static readonly int Drives = Enum.GetValues(typeof(Drive)).Length;
    }

    public static class Inheritance
    {
        /// <summary>Parents' average plus drift, with an occasional bigger jump (a new variation).</summary>
        public static double Blend(Rng rng, double mother, double father)
        {
            double drift = rng.Chance(0.03) ? rng.Normal(0, 0.25) : rng.Normal(0, 0.08);
            return Clamp01((mother + father) / 2 + drift);
        }

        public static double Founder(Rng rng) => Clamp01(rng.Normal(0.5, 0.15));

        public static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
    }
}
