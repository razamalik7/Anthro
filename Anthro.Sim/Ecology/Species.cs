using Anthro.Sim.World;

namespace Anthro.Sim.Ecology
{
    /// <summary>
    /// A species is described by what it physically is and how it lives. How hunts and populations
    /// turn out comes from these properties meeting people's skills and weapons, never from a
    /// per-species outcome number.
    /// </summary>
    public sealed class Species
    {
        public string Name = "";
        public string Plural = "";
        public int GroupMin, GroupMax;

        // ---- Population ----
        /// <summary>
        /// How many the land can carry per km² (real field densities). These are all the animals
        /// there are: migratory herds leave the map for part of the year and come back, but there is
        /// no reserve beyond it.
        /// </summary>
        public double DensityPerKm2;
        /// <summary>Young raised per animal per year when well fed and far below what the land can carry.</summary>
        public double BirthRate;
        /// <summary>
        /// Share dying each year of age, disease and accident, whatever the weather. Field survival
        /// rates include predation; wolves here do their own killing, so this is the rest.
        /// </summary>
        public double DeathRate;
        /// <summary>Share of herds that migrate (leave the map when it can't feed them).</summary>
        public double MigratoryFraction;

        // ---- Feeding ----
        /// <summary>How good each kind of ground is to feed on (0-1), by terrain: grazers on open ground, browsers in woods.</summary>
        public double[] Feeds = new double[5];
        /// <summary>Snow depth (cm) at which it can no longer get at its food: deer struggle past knee-deep; mammoths sweep it aside.</summary>
        public double SnowLimit = 50;
        /// <summary>Fattens on fallen nuts (boar on acorns and beechmast): a mast year feeds them, a poor one doesn't.</summary>
        public bool EatsMast;

        public double FeedsOn(Terrain t) => Feeds[(int)t];

        // ---- Body and behavior ----
        /// <summary>How much damage it takes to kill, in units of one good spear hit.</summary>
        public double Size;
        /// <summary>How early it notices danger. Flight distance ≈ wariness × 60 m, more when afraid.</summary>
        public double Wariness;
        /// <summary>How likely it turns and fights when wounded or cornered.</summary>
        public double Aggression;
        /// <summary>How hard it is to run down.</summary>
        public double Speed;

        public bool Predator;
        public bool Huntable;
        public double Meat;
        public int Hides, Sinew, Bone;

        public static readonly Species Deer = new Species
        {
            Name = "deer", Plural = "deer", GroupMin = 6, GroupMax = 14,
            // Red deer move between summer high ground and the valley floor in winter: within the map, not off it.
            DensityPerKm2 = 4, BirthRate = 0.45, DeathRate = 0.06,
            Feeds = new double[] { 0, 0.6, 1.0, 0.8, 0.5 }, SnowLimit = 50,
            Size = 1.5, Wariness = 0.6, Aggression = 0.05, Speed = 0.6,
            Huntable = true, Meat = 40, Hides = 1, Sinew = 2, Bone = 3,
        };

        public static readonly Species Boar = new Species
        {
            Name = "boar", Plural = "boar", GroupMin = 3, GroupMax = 7,
            DensityPerKm2 = 2, BirthRate = 1.2, DeathRate = 0.3,
            Feeds = new double[] { 0, 0.7, 0.4, 1.0, 0.4 }, SnowLimit = 40, EatsMast = true,
            Size = 2.0, Wariness = 0.4, Aggression = 0.6, Speed = 0.4,
            Huntable = true, Meat = 30, Hides = 1, Sinew = 1, Bone = 2,
        };

        public static readonly Species Rabbit = new Species
        {
            Name = "rabbit", Plural = "rabbits", GroupMin = 20, GroupMax = 40,
            DensityPerKm2 = 10, BirthRate = 2.5, DeathRate = 0.75,
            Feeds = new double[] { 0, 0.3, 1.0, 0.3, 0.6 }, SnowLimit = 70,
            Size = 0.3, Wariness = 0.8, Aggression = 0, Speed = 0.7,
            Huntable = true, Meat = 1,   // a hare: ~2 kg of meat, about a day's food
        };

        public static readonly Species Mammoth = new Species
        {
            Name = "mammoth", Plural = "mammoths", GroupMin = 4, GroupMax = 8,
            DensityPerKm2 = 0.05, BirthRate = 0.1, DeathRate = 0.04, MigratoryFraction = 1.0,
            Feeds = new double[] { 0, 0.6, 1.0, 0.3, 0.4 }, SnowLimit = 100,
            Size = 6, Wariness = 0.35, Aggression = 0.8, Speed = 0.3,
            Huntable = true, Meat = 400, Hides = 3, Sinew = 6, Bone = 12,
        };

        public static readonly Species Wolf = new Species
        {
            Name = "wolf", Plural = "wolves", GroupMin = 5, GroupMax = 8,
            DensityPerKm2 = 0.025, BirthRate = 0.8, DeathRate = 0.3,
            Feeds = new double[] { 0, 0.4, 0.7, 1.0, 0.6 },
            Size = 1.2, Wariness = 0.5, Aggression = 0.7, Speed = 0.7,
            Predator = true,
        };

        public static readonly Species[] All = { Deer, Boar, Rabbit, Mammoth, Wolf };
    }
}
