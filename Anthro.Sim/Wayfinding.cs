using System;
using System.Collections.Generic;
using System.Linq;
using Anthro.Sim.Core;
using Anthro.Sim.Ecology;
using Anthro.Sim.People;
using Anthro.Sim.World;

namespace Anthro.Sim
{
    /// <summary>
    /// Knowing the land. Everyone carries a mental map (<see cref="MentalMap"/>) built from what
    /// they've seen and been told; what people do out on the land is chosen from it, not from the
    /// land as it really is. Founders know their home range from a lifetime there; children learn it
    /// by going along and being told. Map-making stages (named places, ground maps, carried maps,
    /// star paths) let people hold more, tell more, and tell of farther places.
    /// </summary>
    public sealed partial class Simulation
    {
        static readonly double SeeRadius = WorldMap.FromMeters(400);
        static readonly double GameSeeRadius = WorldMap.FromMeters(1000);
        static readonly double CampSeeRadius = WorldMap.FromMeters(1500);
        static readonly double TrackSeeRadius = WorldMap.FromMeters(300);

        /// <summary>How many places someone can keep in mind: more with named places and carried maps; less for the young.</summary>
        int MapCapacity(Person p)
        {
            double cap = 400;
            if (p.Knows.Contains("wayfind.names")) cap += 400;
            if (p.Knows.Contains("wayfind.carry")) cap += 400;
            return (int)(cap * Math.Min(1, 0.3 + p.AgeYears(Clock) / 25));
        }

        /// <summary>Taking in the land around where someone is: what grows, wood, fish, flint; game nearby; other camps in view.</summary>
        public void LookAround(Person p, double x, double y)
        {
            long day = Clock.Day;
            using (Profiler.Measure("look cells")) foreach (var c in World.Around(x, y, SeeRadius)) p.Map.See(c, day);
            foreach (var g in Ecology.Near(x, y, GameSeeRadius)) p.Map.Sight(g, day);
            // Fresh tracks crossing the ground they're on tell of animals not far off, a day or more ago.
            using (Profiler.Measure("look tracks"))
            foreach (var t in Ecology.TrailsNear(x, y, TrackSeeRadius))
            {
                if (p.Map.Game.TryGetValue(t.G.Id, out var known) && known.Day >= Math.Max(day - 1, t.Day)) continue;
                if (!EcologySystem.Legible(World[(int)t.X, (int)t.Y], day - t.Day)) continue;
                p.Map.Game[t.G.Id] = new Sighting { Species = t.G.Species, GroupId = t.G.Id, Count = t.G.Count, X = t.X, Y = t.Y, Day = t.Day };
            }
            foreach (var b in Region.Bands)
                if (b != this && !b.Extinct && WorldMap.Distance(b.Band.CampX, b.Band.CampY, x, y) < CampSeeRadius)
                    p.Map.Camps[b.Id] = (b.Band.CampX, b.Band.CampY, day);
        }

        /// <summary>
        /// A lifetime on this land: founders know the country around camp (wider for the older),
        /// with what they last saw there spread over the past year, and where the other bands they've
        /// met over the years are camped.
        /// </summary>
        internal void KnowHomeRange(Person p)
        {
            double age = p.AgeYears(Clock);
            if (age < 4) return;
            double radius = WorldMap.FromMeters(2000 + Math.Min(age, 50) * 120);
            int stride = age < 14 ? 3 : 2;
            int cx = (int)Band.CampX, cy = (int)Band.CampY, r = (int)Math.Ceiling(radius);
            for (int x = cx - r; x <= cx + r; x += stride)
                for (int y = cy - r; y <= cy + r; y += stride)
                {
                    if (!World.InBounds(x, y) || WorldMap.Distance(cx, cy, x, y) > radius) continue;
                    var c = World[x, y];
                    p.Map.See(c, Clock.Day);
                    if (p.Map.Places.TryGetValue(MentalMap.Key(x, y), out var note)) note.LastDay = Clock.Day - Rng.Range(0, SimClock.DaysPerYear);
                }
            foreach (var flint in World.FlintCells)
                if (WorldMap.Distance(cx, cy, flint.X, flint.Y) < WorldMap.FromMeters(10000)) p.Map.See(flint, Clock.Day);
            p.Map.Fade(Clock.Day, MapCapacity(p));
        }

        /// <summary>
        /// Telling someone about places: a few of the most useful things the teller knows (game seen
        /// lately, good ground for the season, fishing spots, other camps). Named places let people
        /// tell more; ground maps let them tell of places far off.
        /// </summary>
        public void TellPlaces(Person teller, Person listener)
        {
            var k = teller.Knows;
            int n = 2 + (k.Contains("lang.story") ? 1 : 0) + (k.Contains("wayfind.names") ? 2 : 0) + (k.Contains("wayfind.sketch") ? 3 : 0);
            double reach = k.Contains("wayfind.sketch") ? double.MaxValue : WorldMap.FromMeters(8000);
            double hx = Band.CampX, hy = Band.CampY;
            long day = Clock.Day;
            var season = Clock.Season;

            // Game seen in the last few days is the most urgent news.
            foreach (var s in teller.Map.Game.Values)
            {
                if (n <= 0) break;
                if (day - s.Day > 3 || WorldMap.Distance(s.X, s.Y, hx, hy) > reach) continue;
                if (listener.Map.Game.TryGetValue(s.GroupId, out var known) && known.Day >= s.Day) continue;
                // Told, not seen: "deer down by the north marsh" — near enough to look, not a spot to walk to.
                var told = s;
                told.X = Math.Max(0, Math.Min(World.Size - 1, s.X + Rng.Normal(0, WorldMap.FromMeters(700))));
                told.Y = Math.Max(0, Math.Min(World.Size - 1, s.Y + Rng.Normal(0, WorldMap.FromMeters(700))));
                listener.Map.Game[s.GroupId] = told;
                n--;
            }
            foreach (var kv in teller.Map.Camps)
            {
                if (n <= 0) break;
                if (listener.Map.Camps.TryGetValue(kv.Key, out var known) && known.day >= kv.Value.day) continue;
                listener.Map.Camps[kv.Key] = kv.Value;
                n--;
            }
            if (n <= 0 || teller.Map.Keys.Count == 0) return;
            // Then good places: a handful of the teller's places looked over, the best told.
            // A handful of the teller's places come to mind; the most useful get told.
            var keys = teller.Map.Keys;
            var mind = new List<KeyValuePair<int, PlaceNote>>(12);
            for (int i = 0; i < 12; i++)
            {
                int key = keys[Rng.Range(0, keys.Count)];
                var (x, y) = MentalMap.FromKey(key);
                if (WorldMap.Distance(x, y, hx, hy) <= reach) mind.Add(new KeyValuePair<int, PlaceNote>(key, teller.Map.Places[key]));
            }
            mind.Sort((a, b) => Worth(b.Value, season).CompareTo(Worth(a.Value, season)));
            for (int i = 0; i < Math.Min(n, mind.Count); i++)
            {
                var kv = mind[i];
                if (listener.Map.Places.TryGetValue(kv.Key, out var mine) && mine.LastDay >= kv.Value.LastDay) continue;
                var (x, y) = MentalMap.FromKey(kv.Key);
                var note = listener.Map.Note(x, y, kv.Value.LastDay);
                note.Plant = kv.Value.Plant; note.PlantSeen = kv.Value.PlantSeen; note.PlantSeenDay = kv.Value.PlantSeenDay;
                note.PlantBest = Math.Max(note.PlantBest, kv.Value.PlantBest);
                note.WoodSeen = kv.Value.WoodSeen; note.FishSeen = kv.Value.FishSeen; note.FishBest = Math.Max(note.FishBest, kv.Value.FishBest);
                if (note.FishBest > 0) listener.Map.KnowsFishing = true;
                note.Flint |= kv.Value.Flint;
            }
            // (Forgetting what's crowded out happens weekly, not in every conversation.)
        }

        /// <summary>
        /// Camp news: back from the land, someone says where they've been and what's left there.
        /// Everyone in camp who knows the place hears it.
        /// </summary>
        public void CampNews(Person teller, Cell c)
        {
            int key = MentalMap.Key(c.X, c.Y);
            foreach (var m in Band.Members)
            {
                if (m == teller || !m.Map.Places.TryGetValue(key, out var note)) continue;
                if (c.PlantCapacity > 0) { note.PlantSeen = c.PlantStock; note.PlantSeenDay = Clock.Day; }
                note.WoodSeen = c.Wood;
                if (c.FishCapacity > 0) note.FishSeen = c.FishStock;
                note.LastDay = Clock.Day;
            }
        }

        /// <summary>
        /// Where someone leading a group off would make camp: the best ground they know of between
        /// <paramref name="minDistance"/> and <paramref name="maxDistance"/> from here, judged by the food
        /// they remember near it (plants at their best, fish), game they've seen lately, and away from
        /// camps they know of. If they know no such place, they set off into country they don't know.
        /// </summary>
        internal Cell ChooseCampSite(Person leader, double minDistance, double maxDistance)
        {
            var map = leader.Map;
            var candidates = new List<Cell>();
            foreach (var key in map.Keys)
            {
                var (x, y) = MentalMap.FromKey(key);
                double d = WorldMap.Distance(x, y, Band.CampX, Band.CampY);
                if (d < minDistance || d > maxDistance) continue;
                var c = World[x, y];
                if (c.Terrain == Terrain.Forest || c.Terrain == Terrain.Grassland) candidates.Add(c);
            }
            if (candidates.Count == 0) return Region.FindCampSite(Rng, Band.CampX, Band.CampY, minDistance, maxDistance, this);
            if (candidates.Count > 40) candidates = candidates.OrderBy(_ => Rng.NextDouble()).Take(40).ToList();
            double near = WorldMap.FromMeters(3000), gameNear = WorldMap.FromMeters(5000), crowd = WorldMap.FromMeters(5000);
            double Score(Cell c)
            {
                double food = 0;
                foreach (var kv in map.Places)
                {
                    var (x, y) = MentalMap.FromKey(kv.Key);
                    if (WorldMap.Distance(x, y, c.X, c.Y) > near) continue;
                    food += kv.Value.PlantBest * Cell.Richness(kv.Value.Plant) + kv.Value.FishBest * 0.5;
                }
                foreach (var s in map.Game.Values)
                    if (s.Species.Huntable && WorldMap.Distance(s.X, s.Y, c.X, c.Y) < gameNear) food += s.Count * s.Species.Meat * 0.1;
                foreach (var kv in map.Camps)
                    if (kv.Key != Id && WorldMap.Distance(kv.Value.x, kv.Value.y, c.X, c.Y) < crowd) food *= 0.3;
                return food;
            }
            return candidates.OrderByDescending(Score).First();
        }

        /// <summary>How useful a remembered place seems this season (for telling and choosing).</summary>
        internal static double Worth(PlaceNote n, Season season) =>
            n.PlantBest * Cell.SeasonFactor(n.Plant, season) * Cell.Richness(n.Plant) + n.FishBest * 0.5 + (n.Flint ? 2 : 0);

        /// <summary>
        /// How much plant food someone expects at a place they know: what they saw there if it was
        /// recent; otherwise the best they've known it to hold, as the season would have it now.
        /// </summary>
        public double ExpectedPlants(PlaceNote n)
        {
            if (n.Plant == PlantFood.None) return 0;
            if (Clock.Day - n.PlantSeenDay < 7) return n.PlantSeen;
            double peak = Cell.PeakSeasonFactor(n.Plant);
            return peak <= 0 ? 0 : n.PlantBest * Cell.SeasonFactor(n.Plant, Clock.Season) / peak;
        }

        /// <summary>Fish someone expects at a spot: what they saw lately, or what they know it holds (a run fills it in spring and autumn).</summary>
        public double ExpectedFish(PlaceNote n)
        {
            if (n.FishBest <= 0) return 0;
            if (Clock.Day - n.LastDay < 5) return n.FishSeen;
            bool runSeason = Clock.Season == Season.Spring || Clock.Season == Season.Autumn;
            return n.FishBest * (runSeason ? 1 : 0.4);
        }
    }
}
