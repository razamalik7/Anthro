using System;
using Anthro.Sim.Core;

namespace Anthro.Sim.World
{
    /// <summary>
    /// The weather. Temperature follows the sun through the year and falls with height (about
    /// 6.5 °C per km, as real air does), so the hills green later and hold snow longer than the
    /// valley floor. Each year brings its own swing: a hard or mild winter, a warm or cool year, a
    /// pattern of rain that leaves some districts lush and others dry, a good or poor nut mast, berry
    /// crop and fish run. Bad years tend to come in runs (droughts persist; a big mast is usually
    /// followed by a poor one).
    /// </summary>
    public sealed class Climate
    {
        /// <summary>Standard deviations from a normal winter. Above 1 is a hard winter.</summary>
        public double WinterSeverity { get; internal set; }
        public double YearWarmth { get; internal set; }

        /// <summary>This year's crops relative to an average year (1): the nut mast, berries, wild grain, and the fish runs.</summary>
        public double Mast { get; internal set; } = 1;
        public double Berries { get; internal set; } = 1;
        public double Grain { get; internal set; } = 1;
        public double FishRun { get; internal set; } = 1;

        /// <summary>Added by the god's weather power; reset each season.</summary>
        public double SeasonOverride { get; set; }

        /// <summary>
        /// The map's elevation is relative (river 0, ridges ~1.5); this is how many meters one unit is.
        /// Authored: a valley floor rising 500-900 m to its ridges, typical of temperate river valleys.
        /// </summary>
        public const double MetersPerElevation = 500;
        const double LapsePerMeter = 0.0065;

        // The year's temperature curve on the valley floor: a mean and a swing, peaking at midsummer.
        // Seasonal means come to about 8 °C in spring and autumn, 20 in summer and -4 in winter.
        const double MeanTemp = 8, Swing = 13.3;
        const int Midsummer = SimClock.DaysPerSeason + SimClock.DaysPerSeason / 2;

        // Rain: a coarse grid of districts, each with this year's wetness (1 is a normal year).
        const int RainGrid = 6;
        double[,] _rain = Ones();
        double _logMast, _logRainBias;

        static double[,] Ones()
        {
            var g = new double[RainGrid, RainGrid];
            for (int i = 0; i < RainGrid; i++) for (int j = 0; j < RainGrid; j++) g[i, j] = 1;
            return g;
        }

        public void NewYear(Rng rng)
        {
            WinterSeverity = rng.Normal();
            YearWarmth = rng.Normal(0, 1.5);
            // Wet and dry years persist somewhat across the whole region, and differ district by district.
            // (Year-to-year rainfall in temperate lands varies by about 15-25%.)
            _logRainBias = 0.5 * _logRainBias + rng.Normal(0, 0.1);
            for (int i = 0; i < RainGrid; i++)
                for (int j = 0; j < RainGrid; j++)
                    _rain[i, j] = Math.Exp(0.5 * Math.Log(_rain[i, j]) + _logRainBias + rng.Normal(0, 0.15));
            // Nut trees mast together across a region, and a big mast year is usually followed by a lean one.
            _logMast = -0.4 * _logMast + rng.Normal(0, 0.6);
            Mast = Math.Exp(_logMast);
            Berries = Math.Exp(rng.Normal(0, 0.3));
            Grain = Math.Exp(rng.Normal(0, 0.25));
            FishRun = Math.Exp(rng.Normal(0, 0.4));
        }

        /// <summary>The temperature on the valley floor on a given day of the year.</summary>
        public double TemperatureOn(int dayOfYear)
        {
            double t = MeanTemp + Swing * Math.Cos(2 * Math.PI * (dayOfYear - Midsummer) / SimClock.DaysPerYear);
            // A hard winter is a cold midwinter, not a cold summer.
            double winterness = Math.Max(0, -Math.Cos(2 * Math.PI * (dayOfYear - Midsummer) / SimClock.DaysPerYear));
            // (Averaged over the winter, a severity of 1 makes it 5 °C colder.)
            return t - WinterSeverity * 5 / 0.9 * winterness + YearWarmth + SeasonOverride;
        }

        /// <summary>The temperature at a height on the map on a given day.</summary>
        public double TemperatureAt(int dayOfYear, double elevation) =>
            TemperatureOn(dayOfYear) - Math.Max(0, elevation) * MetersPerElevation * LapsePerMeter;

        /// <summary>The season's average temperature on the valley floor.</summary>
        public double TemperatureC(Season season)
        {
            int start = (int)season * SimClock.DaysPerSeason;
            double sum = 0;
            for (int d = 0; d < SimClock.DaysPerSeason; d += 5) sum += TemperatureOn(start + d);
            return sum / (SimClock.DaysPerSeason / 5);
        }

        /// <summary>How wet this year is at a place on the map (1 is normal), smoothly between districts.</summary>
        public double Wetness(double x, double y, int mapSize)
        {
            double gx = Math.Clamp(x / mapSize * (RainGrid - 1), 0, RainGrid - 1.001), gy = Math.Clamp(y / mapSize * (RainGrid - 1), 0, RainGrid - 1.001);
            int i = (int)gx, j = (int)gy;
            double fx = gx - i, fy = gy - j;
            return _rain[i, j] * (1 - fx) * (1 - fy) + _rain[i + 1, j] * fx * (1 - fy) + _rain[i, j + 1] * (1 - fx) * fy + _rain[i + 1, j + 1] * fx * fy;
        }
    }
}
