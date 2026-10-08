using System;

namespace Anthro.Sim.Core
{
    public enum Season { Spring, Summer, Autumn, Winter }

    /// <summary>The calendar clock. One tick is one in-game hour.</summary>
    public sealed class SimClock
    {
        public const int HoursPerDay = 24;
        public const int DaysPerSeason = 90;
        public const int SeasonsPerYear = 4;
        public const int DaysPerYear = DaysPerSeason * SeasonsPerYear;

        public long Tick { get; private set; }
        public int Hour => (int)(Tick % HoursPerDay);
        public long Day => Tick / HoursPerDay;
        public int DayOfYear => (int)(Day % DaysPerYear);
        public int Year => (int)(Day / DaysPerYear);
        public Season Season => (Season)(DayOfYear / DaysPerSeason);
        public int DayOfSeason => DayOfYear % DaysPerSeason;
        /// <summary>
        /// Hours of daylight today: following the sun smoothly through the year, about 16 at
        /// midsummer and 8 at midwinter (a mid-latitude valley, ~45°).
        /// </summary>
        public double DayLength => 12 + 4 * Math.Cos(2 * Math.PI * (DayOfYear - (DaysPerSeason + DaysPerSeason / 2)) / DaysPerYear);
        public int Sunrise => (int)Math.Round(12.5 - DayLength / 2);
        public int Sunset => (int)Math.Round(12.5 + DayLength / 2);

        public bool IsDaylight => Hour >= Sunrise && Hour < Sunset;

        /// <summary>Hours of light left today (0 after sunset).</summary>
        public int DaylightLeft => Math.Max(0, Sunset - Hour);

        /// <summary>
        /// Bedtime: people sit up by the fire for a few hours after dark, then sleep until sunrise.
        /// </summary>
        public bool IsNight => Hour >= Math.Min(23, Sunset + 3) || Hour < Sunrise;

        public void Advance() => Tick++;

        public static Season SeasonOf(long day) => (Season)((int)(day % DaysPerYear) / DaysPerSeason);

        /// <summary>Sets the calendar before anything has happened (the story's first day).</summary>
        public void StartAt(Season season, int dayOfSeason) => Tick = ((long)season * DaysPerSeason + dayOfSeason) * HoursPerDay;

        public override string ToString() => $"Year {Year}, {Season} day {DayOfSeason + 1}";
    }
}
