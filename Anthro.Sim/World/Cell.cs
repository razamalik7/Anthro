using System;
using Anthro.Sim.Core;

namespace Anthro.Sim.World
{
    public enum Terrain { River, Marsh, Grassland, Forest, Hills }

    [Flags]
    public enum Feature { None = 0, Flint = 1, Clay = 2, Ochre = 4, Cave = 8, Reeds = 16 }

    public enum PlantFood { None, Greens, Roots, Berries, Nuts, WildGrain }

    /// <summary>One ~50 m square of the valley.</summary>
    public sealed class Cell
    {
        public int X, Y;
        public Terrain Terrain;
        public double Elevation;
        public Feature Features;
        public PlantFood Plant;
        public double PlantCapacity;
        /// <summary>What the cell holds left wild; tending near camps raises <see cref="PlantCapacity"/> above it.</summary>
        public double WildCapacity;
        public double PlantStock;
        /// <summary>Dead wood lying about, in days of a cooking fire. Forests hold the most; it builds back slowly.</summary>
        public double Wood, WoodCapacity;
        /// <summary>
        /// Fish in this stretch of water, in rations. Rivers hold a modest resident stock (about 100 kg
        /// of fish on a 200 m stretch), which breeds back slowly; spring and autumn runs fill them for
        /// a few weeks.
        /// </summary>
        public double FishStock, FishCapacity;
        /// <summary>
        /// A fish weir built across this stretch (0 none, 1 standing). People build it; floods and rot
        /// wear it down unless it's used and mended.
        /// </summary>
        public double Weir;
        /// <summary>The river's warmth: it follows the air slowly (weeks behind).</summary>
        public double WaterTemp = 4;

        public bool Has(Feature f) => (Features & f) != 0;

        /// <summary>How much of a plant's capacity the season supports. Above 1 is a glut.</summary>
        public static double SeasonFactor(PlantFood plant, Season season)
        {
            switch (plant)
            {
                case PlantFood.Greens: return Pick(season, 1.0, 0.8, 0.4, 0.05);
                case PlantFood.Roots: return Pick(season, 0.6, 0.8, 1.0, 0.3);
                case PlantFood.Berries: return Pick(season, 0.1, 1.2, 0.8, 0.0);
                case PlantFood.Nuts: return Pick(season, 0.0, 0.2, 1.5, 0.1);
                case PlantFood.WildGrain: return Pick(season, 0.0, 0.6, 1.3, 0.0);
                default: return 0;
            }
        }

        /// <summary>The highest share of capacity this plant reaches in any season.</summary>
        public static double PeakSeasonFactor(PlantFood plant) =>
            Math.Max(Math.Max(SeasonFactor(plant, Season.Spring), SeasonFactor(plant, Season.Summer)),
                     Math.Max(SeasonFactor(plant, Season.Autumn), SeasonFactor(plant, Season.Winter)));

        /// <summary>
        /// How much food value a unit of this plant carries relative to an average plant food. Nuts and
        /// grain are dense; greens are mostly water.
        /// </summary>
        public static double Richness(PlantFood plant) => plant switch
        {
            PlantFood.Nuts => 1.4, PlantFood.WildGrain => 1.3, PlantFood.Roots => 1.0, PlantFood.Berries => 0.8, PlantFood.Greens => 0.5, _ => 0,
        };

        /// <summary>Nuts and grain keep for months; everything else spoils within days.</summary>
        public static bool Storable(PlantFood plant) => plant == PlantFood.Nuts || plant == PlantFood.WildGrain;

        static double Pick(Season s, double spring, double summer, double autumn, double winter) =>
            s == Season.Spring ? spring : s == Season.Summer ? summer : s == Season.Autumn ? autumn : winter;

        public static double WoodCapacityOf(Terrain t) => t switch
        {
            Terrain.Forest => 40, Terrain.Marsh => 4, Terrain.Hills => 4, Terrain.Grassland => 2, _ => 0,
        };

        /// <summary>
        /// Inner bark that could be peeled here, in rations: the few suitable trees (pine, birch, elm)
        /// in a patch of woods. A peeled tree dies, so it comes back only as young trees grow, a few
        /// percent a year: camps soon strip the woods near them.
        /// </summary>
        public double Bark;
        public const double BarkCapacity = 10;

        /// <summary>Snow lying here, in cm.</summary>
        public double Snow;
        /// <summary>How wet this year is here (1 is a normal year), set each new year from the rain pattern.</summary>
        public double Wet = 1;
        /// <summary>Today's temperature here.</summary>
        public double Temperature;

        /// <summary>
        /// How much of what grows here can be got at under the snow: digging for roots or reaching
        /// berries gets hard past a hand's depth and near hopeless past knee-deep.
        /// </summary>
        public double SnowAccess => Math.Max(0.1, 1 - GroundSnow / 45);

        /// <summary>Snow on the ground under cover: woods catch much of it in their branches.</summary>
        public double GroundSnow => Terrain == Terrain.Forest ? Snow * 0.5 : Snow;

        /// <summary>
        /// How well plants are growing here today: warmth (nothing grows near freezing; the hills
        /// green weeks after the valley) and rain (a dry year stunts everything but the marsh).
        /// </summary>
        public double Growth => Math.Max(0, Math.Min(1, Temperature / 8)) * Water;

        double Water => Terrain == Terrain.Marsh || Terrain == Terrain.River ? 1 : Math.Max(0.3, Math.Min(1.3, Math.Sqrt(Wet)));

        /// <summary>
        /// A day of weather and growth: snow builds on cold days (more in a wet year) and melts on
        /// warm ones; plant stock drifts toward what the season, the local warmth and rain, and the
        /// year's crop support; fish runs come up the rivers in spring and autumn, big or small by year.
        /// </summary>
        public void DailyRegrow(Climate climate, int dayOfYear)
        {
            var season = (Season)(dayOfYear / SimClock.DaysPerSeason);
            int dayOfSeason = dayOfYear % SimClock.DaysPerSeason;
            Temperature = climate.TemperatureAt(dayOfYear, Elevation);
            // About 0.6 cm of fresh snow on an average freezing day, settling ~1% a day as it packs down;
            // melting ~1.5 cm per degree above zero. A normal winter leaves ~30 cm on the valley floor.
            if (Temperature < 0) Snow = Snow * 0.99 + 0.6 * Wet;
            else if (Snow > 0) Snow = Math.Max(0, Snow - 1.5 * Temperature);

            if (FishCapacity > 0)
            {
                // Fish come up the rivers to spawn as the water warms through the spring (about 5-11 °C)
                // and as it cools again in autumn (about 12-5 °C): early or late, long or short, as the year goes.
                double before = WaterTemp;
                WaterTemp += (Temperature - WaterTemp) * 0.05;
                bool warming = WaterTemp > before;
                bool run = warming ? WaterTemp >= 5 && WaterTemp <= 11 : WaterTemp <= 12 && WaterTemp >= 5;
                if (Simulation.Off("fishrun")) run = season == Season.Spring && dayOfSeason >= 10 && dayOfSeason < 40 || season == Season.Autumn && dayOfSeason >= 20 && dayOfSeason < 50;
                double peak = FishCapacity * 4 * climate.FishRun;
                if (run && FishStock < peak) FishStock += (peak - FishStock) * 0.1;      // fish coming up the river
                else if (!run && FishStock > FishCapacity) FishStock -= (FishStock - FishCapacity) * 0.05;      // the run moves on
                else if (!run) FishStock += FishStock * 0.004 * (1 - FishStock / FishCapacity) + FishCapacity * 0.0005; // residents breed back slowly
            }
            if (Weir > 0) Weir = Math.Max(0, Weir - 0.003);
            if (Wood < WoodCapacity) Wood = Math.Min(WoodCapacity, Wood + WoodCapacity * 0.004);
            if (Terrain == Terrain.Forest && Bark < BarkCapacity) Bark = Math.Min(BarkCapacity, Bark + BarkCapacity * 0.05 / SimClock.DaysPerYear);
            if (PlantCapacity <= 0) return;
            // Roots keep underground whatever the weather; everything else needs warmth and water to grow.
            double local = Plant == PlantFood.Roots ? Water : Growth;
            double year = Plant switch { PlantFood.Nuts => climate.Mast, PlantFood.Berries => climate.Berries, PlantFood.WildGrain => climate.Grain, _ => 1 };
            // Autumn's nuts, grain and late berries ripened over the summer: a cold autumn day doesn't undo them.
            if ((season == Season.Autumn || season == Season.Winter) && Plant != PlantFood.Greens && Plant != PlantFood.Roots) local = Math.Max(local, 0.8 * Water);
            double target = PlantCapacity * SeasonFactor(Plant, season) * local * year;
            PlantStock += PlantStock < target ? (target - PlantStock) * 0.05 : (target - PlantStock) * 0.03;
        }
    }
}
