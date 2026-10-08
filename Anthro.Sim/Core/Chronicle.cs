using System;
using System.Collections.Generic;

namespace Anthro.Sim.Core
{
    public enum Importance { Detail, Notable, Major }

    public sealed class ChronicleEntry
    {
        public int Year;
        public Season Season;
        public int DayOfSeason;
        public Importance Importance;
        public string Text = "";

        public override string ToString() => $"Year {Year}, {Season}: {Text}";
    }

    /// <summary>The story log. Unity will read the same entries to raise notifications.</summary>
    public sealed class Chronicle
    {
        readonly List<ChronicleEntry> _entries = new List<ChronicleEntry>();

        public IReadOnlyList<ChronicleEntry> Entries => _entries;

        public event Action<ChronicleEntry>? Written;

        public void Write(SimClock clock, Importance importance, string text)
        {
            var entry = new ChronicleEntry
            {
                Year = clock.Year,
                Season = clock.Season,
                DayOfSeason = clock.DayOfSeason,
                Importance = importance,
                Text = text,
            };
            _entries.Add(entry);
            Written?.Invoke(entry);
        }
    }
}
