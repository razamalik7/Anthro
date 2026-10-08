using System;
using System.Collections.Generic;
using Anthro.Sim.Ecology;
using Anthro.Sim.World;

namespace Anthro.Sim.People
{
    /// <summary>What someone remembers about a spot on the land.</summary>
    public sealed class PlaceNote
    {
        public PlantFood Plant;
        /// <summary>Plant food they saw there, and when.</summary>
        public double PlantSeen;
        public long PlantSeenDay;
        /// <summary>The most plant food they've known it to hold (what they judge it by in other seasons).</summary>
        public double PlantBest;
        public double FishBest;
        public double WoodSeen;
        public double FishSeen;
        public bool Flint;
        /// <summary>When they last saw it or heard about it. Old, unvisited notes fade.</summary>
        public long LastDay;
    }

    /// <summary>A herd or pack someone saw: what, how many, where, when.</summary>
    public struct Sighting
    {
        public Species Species;
        public int GroupId;
        public int Count;
        public double X, Y;
        public long Day;
    }

    /// <summary>
    /// What one person knows of the land: places with food, wood, fish and flint; where they've seen
    /// game lately; where other bands are camped. Built from what they've seen with their own eyes and
    /// what others have told them; it fades for places they haven't been to or heard of in years.
    /// Everything people do out on the land is chosen from this, not from the land as it really is.
    /// </summary>
    public sealed class MentalMap
    {
        public readonly Dictionary<int, PlaceNote> Places = new Dictionary<int, PlaceNote>();
        /// <summary>The same places as a list, for picking a few at random (talk) without walking them all.</summary>
        public readonly List<int> Keys = new List<int>();
        /// <summary>The latest sighting of each herd or pack they've seen lately, by group.</summary>
        public readonly Dictionary<int, Sighting> Game = new Dictionary<int, Sighting>();
        /// <summary>Other bands' camps they know of: band id → where, and when they knew it.</summary>
        public readonly Dictionary<int, (double x, double y, long day)> Camps = new Dictionary<int, (double, double, long)>();
        /// <summary>Whether they know of anywhere with fish (a river or marsh is not soon forgotten).</summary>
        public bool KnowsFishing;

        public static int Key(int x, int y) => x * 1000 + y;
        public static (int x, int y) FromKey(int key) => (key / 1000, key % 1000);

        public PlaceNote Note(int x, int y, long day)
        {
            int k = Key(x, y);
            if (!Places.TryGetValue(k, out var n)) { Places[k] = n = new PlaceNote(); Keys.Add(k); }
            n.LastDay = Math.Max(n.LastDay, day);
            return n;
        }

        /// <summary>Takes in what's at a cell they can see.</summary>
        public void See(Cell c, long day)
        {
            if (c.PlantCapacity <= 0 && c.Wood < 1 && c.FishCapacity <= 0 && !c.Has(Feature.Flint)) return;
            var n = Note(c.X, c.Y, day);
            if (c.PlantCapacity > 0)
            {
                n.Plant = c.Plant; n.PlantSeen = c.PlantStock; n.PlantSeenDay = day;
                // Judged against the season: a bare patch in winter can be known as a good one.
                double f = Cell.SeasonFactor(c.Plant, Core.SimClock.SeasonOf(day));
                double peak = Cell.PeakSeasonFactor(c.Plant);
                if (f > 0.05) n.PlantBest = Math.Max(n.PlantBest * 0.98, c.PlantStock / f * peak);
            }
            n.WoodSeen = c.Wood;
            if (c.FishCapacity > 0) { n.FishSeen = c.FishStock; n.FishBest = Math.Max(n.FishBest * 0.98, c.FishStock); KnowsFishing = true; }
            if (c.Has(Feature.Flint)) n.Flint = true;
        }

        public void Sight(AnimalGroup g, long day)
        {
            Game[g.Id] = new Sighting { Species = g.Species, GroupId = g.Id, Count = g.Count, X = g.X, Y = g.Y, Day = day };
        }

        /// <summary>Forgets what's stale: game seen more than a couple of weeks ago, places not thought of in years, beyond what they can hold.</summary>
        public void Fade(long day, int capacity)
        {
            var stale = new List<int>();
            foreach (var kv in Game) if (day - kv.Value.Day > 14) stale.Add(kv.Key);
            foreach (var k in stale) Game.Remove(k);
            if (Places.Count <= capacity) return;
            var old = new List<KeyValuePair<int, PlaceNote>>(Places);
            old.Sort((a, b) => a.Value.LastDay.CompareTo(b.Value.LastDay));
            for (int i = 0; i < old.Count - capacity; i++) Places.Remove(old[i].Key);
            Keys.Clear();
            Keys.AddRange(Places.Keys);
        }
    }
}
