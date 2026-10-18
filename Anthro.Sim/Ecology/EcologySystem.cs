using System;
using System.Collections.Generic;
using System.Linq;
using Anthro.Sim.Core;
using Anthro.Sim.World;

namespace Anthro.Sim.Ecology
{
    /// <summary>
    /// The animals of the map. Every herd, pack and warren there is lives here: nothing restocks
    /// the map from beyond it. Each group feeds where it stands (grazing that greens with local
    /// warmth and rain, buried under snow in winter) and keeps a body condition from it; groups
    /// move to better ground and away from people; thin animals die in hard winters and bear fewer
    /// young, and young are fewer as the land fills up. Migratory herds leave the map when it can no
    /// longer feed them and come back as it greens: the same animals, unseen and unhunted while away.
    /// </summary>
    public sealed class EcologySystem
    {
        readonly List<AnimalGroup> _groups = new List<AnimalGroup>();
        readonly List<AnimalGroup> _away = new List<AnimalGroup>();
        readonly HashSet<Species> _gone = new HashSet<Species>();
        int _nextId;
        /// <summary>The map's area in km².</summary>
        double _areaKm2;
        int _size;

        /// <summary>How many the map can carry: the species' real density over the map's area.</summary>
        int Capacity(Species sp) => (int)Math.Round(sp.DensityPerKm2 * _areaKm2);

        // Feeding (authored, in the units of Condition): a group needs grazing of about 0.45 to hold its
        // condition; winter forage (dry grass, twigs, bark) is about 0.3 of summer's; condition moves
        // ~2% a day toward what the grazing supports, so a winter on poor ground takes a fat animal thin.
        const double Need = 0.45, Dormant = 0.3, ConditionRate = 0.02;
        /// <summary>Below this condition animals start dying of hunger and cold.</summary>
        const double Starving = 0.25;
        /// <summary>The day young are born (late spring), and the autumn rut when they're conceived.</summary>
        const int BirthDay = 50, RutDay = SimClock.DaysPerSeason * 2 + 30;

        public IReadOnlyList<AnimalGroup> Groups => _groups;

        /// <summary>This year's births and deaths by species and cause ("deer born", "deer starved", "deer old", "deer wolves", "deer hunted"), for reporting.</summary>
        public readonly Dictionary<string, int> Tally = new Dictionary<string, int>();
        public void Count(Species sp, string what, int n) { if (n == 0) return; string k = sp.Name + " " + what; lock (Tally) Tally[k] = (Tally.TryGetValue(k, out var v) ? v : 0) + n; }

        /// <summary>The map's groups of a species, plus any away for the season.</summary>
        public int Total(Species sp) => _groups.Where(g => g.Species == sp).Sum(g => g.Count);
        public int Away(Species sp) => _away.Where(g => g.Species == sp).Sum(g => g.Count);

        public IEnumerable<AnimalGroup> Living(Species sp) => _groups.Where(g => g.Species == sp && !g.Gone);

        public void Populate(Region sim)
        {
            _size = sim.World.Size;
            _areaKm2 = Math.Pow(_size * WorldMap.CellMeters / 1000, 2);
            // The land starts somewhat below what it could carry.
            foreach (var sp in Species.All)
            {
                int left = (int)Math.Round(Capacity(sp) * 0.8);
                while (left > 0)
                {
                    int size = Math.Min(left, sim.Rng.Range(sp.GroupMin, sp.GroupMax + 1));
                    left -= size;
                    var c = GoodGround(sim.World, sim.Rng, sp);
                    var g = new AnimalGroup(_nextId++, sp, size, c.X, c.Y) { Migratory = sim.Rng.Chance(sp.MigratoryFraction) };
                    g.Rng = sim.Rng.Fork((ulong)g.Id);
                    _groups.Add(g);
                }
            }
            Index();
        }

        /// <summary>
        /// Whether tracks of this age can still be read here: a day or two on dry ground, a few days in
        /// mud and marsh, most of a week in snow (which is why winter is tracking season).
        /// </summary>
        public static bool Legible(Cell c, long age) => age <= (c.Snow > 3 ? 6 : c.Terrain == Terrain.Marsh || c.Terrain == Terrain.River ? 3 : 2);

        /// <summary>How well a species can feed at a place today (0-1).</summary>
        public static double Forage(Species sp, Cell c, Climate climate, Season season)
        {
            double green = Math.Max(Dormant, c.Growth);
            double food = sp.FeedsOn(c.Terrain) * green;
            // Boar fatten on fallen nuts from autumn into winter, as much as the year's mast gives.
            if (sp.EatsMast && (season == Season.Autumn || season == Season.Winter) && c.Plant == PlantFood.Nuts)
                food += 0.4 * Math.Min(1.5, climate.Mast);
            // Browse in the woods stands above the snow, and less snow gets under the trees.
            return food * Math.Max(0, 1 - c.GroundSnow / sp.SnowLimit);
        }

        public void DailyUpdate(Region sim)
        {
            var season = sim.Clock.Season;
            var climate = sim.World.Climate;
            var world = sim.World;
            // Grazers and browsers each look after themselves, so they're updated side by side on all the
            // processor's cores; each draws on its own chance stream, so the result is the same every run.
            var grazers = _groups.Where(g => !g.Gone && !g.Species.Predator).ToList();
            var leaving = new System.Collections.Concurrent.ConcurrentBag<AnimalGroup>();
            using (Profiler.Measure("eco groups"))
                System.Threading.Tasks.Parallel.ForEach(grazers, g => { if (Graze(sim, g, season, climate, world)) leaving.Add(g); });
            foreach (var g in leaving.OrderBy(g => g.Id)) Leave(sim, g);
            // Wolves follow the herds as they now stand.
            foreach (var g in _groups)
            {
                if (g.Gone || !g.Species.Predator) continue;
                g.Fear = Math.Max(0, g.Fear - 0.04);
                g.Condition = Math.Max(0, Math.Min(1, 1.2 - g.Hunger));
                using (Profiler.Measure("eco follow")) FollowPrey(sim, g);
                AgeAndMove(sim, g, sim.Rng);
            }

            using (Profiler.Measure("eco packs")) foreach (var pack in _groups.Where(g => g.Species.Predator && !g.Gone).ToList()) PackHunts(sim, pack);
            using (Profiler.Measure("eco away")) AwayDaily(sim, season);

            if (sim.Clock.DayOfYear == BirthDay) Births(sim);
            // Whether a female conceives depends on how fat she is at the rut, when she's at her best.
            if (sim.Clock.DayOfYear == RutDay)
                foreach (var g in _groups.Concat(_away)) g.Pregnant = Math.Min(1, g.Condition / 0.6);
            foreach (var sp in Species.All)
                if (!_gone.Contains(sp) && Total(sp) + Away(sp) == 0)
                {
                    _gone.Add(sp);
                    sim.Chronicle.Write(sim.Clock, Importance.Major, $"No {sp.Plural} are left anywhere.");
                }
            using (Profiler.Measure("eco index"))
            {
                _groups.RemoveAll(g => g.Gone || g.Away);
                _away.RemoveAll(g => g.Gone);
                Index();
            }
        }

        /// <summary>A grazing group's day: feeding, condition, choosing ground, deaths, moving. True if it leaves the map today.</summary>
        bool Graze(Region sim, AnimalGroup g, Season season, Climate climate, WorldMap world)
        {
            var sp = g.Species;
            var rng = g.Rng ?? sim.Rng;
            double toCamp = NearestCamp(sim, g.X, g.Y);
            g.Fear = Math.Max(0, g.Fear - 0.04);
            g.Shy *= 0.997;
            if (toCamp < WorldMap.FromMeters(1500)) g.Fear = Math.Min(1, g.Fear + 0.03);
            var here = world[(int)g.X, (int)g.Y];
            double food = Forage(sp, here, climate, season);
            g.Condition = Math.Max(0, Math.Min(1, g.Condition + ConditionRate * (food - Need)));
            bool leaves = false;
            if (g.Leaving) leaves = AtEdge(g);
            else if (WorldMap.Distance(g.X, g.Y, g.TargetX, g.TargetY) < WorldMap.FromMeters(300) || food < Need
                     || rng.Chance(0.05) || (g.Fear > 0.3 && toCamp < WorldMap.FromMeters(3000)))
                ChooseRange(sim, g, season, rng);
            // Hunger and cold kill the thinnest first.
            if (g.Condition < Starving)
            {
                int died = Math.Min(g.Count, rng.Poisson(g.Count * (Starving - g.Condition) * 0.05));
                g.Count -= died; Count(sp, "starved", died);
            }
            if (!leaves) AgeAndMove(sim, g, rng);
            return leaves;
        }

        /// <summary>Deaths of age, disease and accident (less what wolves already culled), then the day's walk, leaving a trail.</summary>
        void AgeAndMove(Region sim, AnimalGroup g, Rng rng)
        {
            var sp = g.Species;
            int old = Math.Min(g.Count, rng.Poisson(g.Count * sp.DeathRate / SimClock.DaysPerYear));
            int spared = Math.Min(old, g.Culled);
            g.Culled -= spared; old -= spared;
            g.Count -= old; Count(sp, "old", old);
            if (g.Away) return;
            g.Trail.Add((g.X, g.Y, sim.Clock.Day));
            if (g.Trail.Count > 8) g.Trail.RemoveAt(0);
            Step(g, WorldMap.FromMeters(sp.Predator ? 6000 : 3000));
        }

        // ---- Coming and going --------------------------------------------------------------------

        bool AtEdge(AnimalGroup g) => g.X < 1 || g.Y < 1 || g.X > _size - 2 || g.Y > _size - 2;

        void Leave(Region sim, AnimalGroup g)
        {
            g.Leaving = false; g.Away = true; g.LeftDay = sim.Clock.Day; g.Fear = 0;
            _away.Add(g);
            if (_away.Count(a => a.Species == g.Species) == 1 && g.Species.Huntable)
                sim.Chronicle.Write(sim.Clock, Importance.Notable, $"The {g.Species.Plural} began leaving the valley.");
        }

        /// <summary>
        /// Herds away for the season winter somewhere a little kinder than what they left (that's why
        /// they went), but it's the same winter: they thin there too, more in a hard year, and the
        /// weakest die. They come back to the edge they left by once the land there can feed them again.
        /// </summary>
        void AwayDaily(Region sim, Season season)
        {
            bool first = true;
            var climate = sim.World.Climate;
            double elsewhere = season == Season.Winter ? Dormant + 0.1 - 0.05 * Math.Max(0, climate.WinterSeverity)
                             : season == Season.Autumn ? Need + 0.05 : 0.8;
            foreach (var g in _away)
            {
                g.Condition = Math.Max(0, Math.Min(1, g.Condition + ConditionRate * (elsewhere - Need)));
                int starved = g.Condition < Starving ? Math.Min(g.Count, sim.Rng.Poisson(g.Count * (Starving - g.Condition) * 0.05)) : 0;
                g.Count -= starved; Count(g.Species, "starved", starved);
                int old = Math.Min(g.Count, sim.Rng.Poisson(g.Count * g.Species.DeathRate / SimClock.DaysPerYear));
                g.Count -= old; Count(g.Species, "old", old);
                if (sim.Clock.Day - g.LeftDay < 20) continue;
                double sum = 0;
                for (int i = 0; i < 6; i++)
                {
                    var c = NearbyCell(sim.World, sim.Rng, g.X, g.Y, WorldMap.FromMeters(4000));
                    sum += Forage(g.Species, c, sim.World.Climate, season);
                }
                if (sum / 6 < Need * 1.2) continue;
                g.Away = false;
                g.TargetX = g.X; g.TargetY = g.Y;
                _groups.Add(g);
                if (first && g.Species.Huntable && !_groups.Any(o => o != g && o.Species == g.Species && o.Migratory))
                    sim.Chronicle.Write(sim.Clock, Importance.Notable, $"The {g.Species.Plural} came back into the valley.");
                first = false;
            }
            _away.RemoveAll(g => !g.Away);
        }

        /// <summary>
        /// Young are born in late spring: as many as were conceived at the autumn rut, fewer surviving
        /// their first weeks when their mothers come out of the winter starved, and fewer as the land
        /// fills up.
        /// </summary>
        void Births(Region sim)
        {
            foreach (var sp in Species.All)
            {
                double fullness = (Total(sp) + Away(sp)) / (double)Math.Max(1, Capacity(sp));
                // Crowding bites hard only near what the land can carry (large mammals breed near their
                // best until the range is nearly full).
                double room = Math.Max(0, 1 - Math.Pow(fullness, 3));
                foreach (var g in _groups.Concat(_away).Where(g => g.Species == sp && !g.Gone).ToList())
                {
                    double fed = g.Pregnant * (0.5 + 0.5 * Math.Min(1, g.Condition / 0.4));
                    int born = sim.Rng.Poisson(g.Count * sp.BirthRate * fed * room);
                    g.Count += born; Count(sp, "born", born);
                    // Young wolves leave in pairs to start packs of their own, somewhere not far off.
                    while (sp.Predator && g.Count > sp.GroupMax)
                    {
                        g.Count -= 2;
                        var c = NearbyCell(sim.World, sim.Rng, g.X, g.Y, WorldMap.FromMeters(15000));
                        _groups.Add(new AnimalGroup(_nextId++, sp, 2, c.X, c.Y) { Condition = g.Condition, Rng = sim.Rng.Fork((ulong)_nextId) });
                    }
                    // A herd grown too big splits.
                    while (g.Count > sp.GroupMax * 2)
                    {
                        int part = sp.GroupMax;
                        g.Count -= part;
                        var split = new AnimalGroup(_nextId++, sp, part, g.X, g.Y) { Migratory = g.Migratory, Condition = g.Condition, Away = g.Away, LeftDay = g.LeftDay, Shy = g.Shy, Rng = sim.Rng.Fork((ulong)_nextId) };
                        (g.Away ? _away : _groups).Add(split);
                    }
                }
            }
        }

        /// <summary>Spawn an animal group at a location (used by divine bounty or migrations).</summary>
        public AnimalGroup SpawnGroup(Species sp, double x, double y, int count, Region sim)
        {
            var g = new AnimalGroup(_nextId++, sp, count, x, y)
            {
                Condition = 1.0,
                Rng = sim.GodRng.Fork((ulong)_nextId)
            };
            _groups.Add(g);
            Index();
            return g;
        }

        // ---- Where the animals are (for who can see them) -----------------------------------------

        const int Bucket = 25;
        readonly Dictionary<int, List<AnimalGroup>> _buckets = new Dictionary<int, List<AnimalGroup>>();
        readonly Dictionary<int, AnimalGroup> _byId = new Dictionary<int, AnimalGroup>();

        void Index()
        {
            _buckets.Clear(); _byId.Clear();
            foreach (var list in _trails.Values) list.Clear();
            foreach (var g in _groups)
            {
                if (g.Gone) continue;
                int k = (int)(g.X / Bucket) * 1000 + (int)(g.Y / Bucket);
                if (!_buckets.TryGetValue(k, out var list)) _buckets[k] = list = new List<AnimalGroup>();
                list.Add(g);
                _byId[g.Id] = g;
                foreach (var t in g.Trail)
                {
                    int tk = (int)(t.X / TrailBucket) * 1000 + (int)(t.Y / TrailBucket);
                    if (!_trails.TryGetValue(tk, out var tl)) _trails[tk] = tl = new List<(AnimalGroup, double, double, long)>();
                    tl.Add((g, t.X, t.Y, t.Day));
                }
            }
        }

        // Where trails run (as of this morning), so people passing can notice tracks without checking every herd.
        const int TrailBucket = 5;
        readonly Dictionary<int, List<(AnimalGroup G, double X, double Y, long Day)>> _trails = new Dictionary<int, List<(AnimalGroup, double, double, long)>>();

        /// <summary>Points of animal trails within a distance of a place: which herd, where, and on what day it passed.</summary>
        public IEnumerable<(AnimalGroup G, double X, double Y, long Day)> TrailsNear(double x, double y, double radius)
        {
            int r = (int)Math.Ceiling(radius / TrailBucket);
            int bx = (int)(x / TrailBucket), by = (int)(y / TrailBucket);
            for (int i = bx - r; i <= bx + r; i++)
                for (int j = by - r; j <= by + r; j++)
                    if (_trails.TryGetValue(i * 1000 + j, out var list))
                        foreach (var t in list)
                            if (!t.G.Gone && !t.G.Away && WorldMap.Distance(t.X, t.Y, x, y) <= radius) yield return t;
        }

        public AnimalGroup? ById(int id) => _byId.TryGetValue(id, out var g) && !g.Gone && !g.Away ? g : null;

        /// <summary>Animal groups within a distance of a point (as of this morning's positions).</summary>
        public IEnumerable<AnimalGroup> Near(double x, double y, double radius)
        {
            if (_byId.Count == 0) Index();
            int bx = (int)(x / Bucket), by = (int)(y / Bucket);
            for (int i = bx - 1; i <= bx + 1; i++)
                for (int j = by - 1; j <= by + 1; j++)
                    if (_buckets.TryGetValue(i * 1000 + j, out var list))
                        foreach (var g in list)
                            if (!g.Gone && !g.Away && WorldMap.Distance(g.X, g.Y, x, y) <= radius) yield return g;
        }

        /// <summary>A hunt, a near miss or a kill frightens the herd.</summary>
        public void Startle(AnimalGroup g, double amount) => g.Fear = Math.Min(1, g.Fear + amount);

        // ---- Moving --------------------------------------------------------------------------------

        /// <summary>
        /// Picks where to go: the best feeding within reach, kept away from people when frightened.
        /// A migratory herd that finds nothing within a long walk that could feed it heads off the map.
        /// </summary>
        void ChooseRange(Region sim, AnimalGroup g, Season season, Rng rng)
        {
            var sp = g.Species;
            var climate = sim.World.Climate;
            // Hungry animals look farther: herds come down from snowbound hills to the valley floor.
            bool hungry = Forage(sp, sim.World[(int)g.X, (int)g.Y], climate, season) < Need;
            double reach = WorldMap.FromMeters(sp.Predator ? 6000 : hungry ? 12000 : 5000);
            double Score(Cell c)
            {
                double s = Forage(sp, c, climate, season) - WorldMap.Distance(g.X, g.Y, c.X, c.Y) * WorldMap.CellMeters / 1000 * 0.01;
                if (g.Fear > 0.3) s -= g.Fear * Math.Max(0, 1 - NearestCamp(sim, c.X, c.Y) / WorldMap.FromMeters(3000));
                return s;
            }
            Cell best = sim.World[(int)g.X, (int)g.Y];
            double bestScore = Score(best), bestFood = Forage(sp, best, climate, season);
            for (int i = 0; i < 10; i++)
            {
                var c = NearbyCell(sim.World, rng, g.X, g.Y, reach);
                double s = Score(c);
                if (s > bestScore) { best = c; bestScore = s; }
                bestFood = Math.Max(bestFood, Forage(sp, c, climate, season));
            }
            if (g.Migratory && bestFood < Need * 0.8)
            {
                // Nothing here will carry them through: off to wherever the herd goes in hard times.
                g.Leaving = true;
                double dl = g.X, dr = _size - 1 - g.X, dt = g.Y, db = _size - 1 - g.Y, m = Math.Min(Math.Min(dl, dr), Math.Min(dt, db));
                g.TargetX = m == dl ? 0 : m == dr ? _size - 1 : g.X;
                g.TargetY = m == dt ? 0 : m == db ? _size - 1 : g.Y;
                return;
            }
            g.TargetX = best.X; g.TargetY = best.Y;
        }

        /// <summary>
        /// Wolves go after what they come across within scenting range (a few km), a meatier herd more
        /// often than a warren, but in proportion to what's about: when deer get scarce, most of
        /// what the pack runs into is boar and hares, and the deer get a respite (real wolves' diets
        /// track what's common). Where nothing is about, they roam their territory looking.
        /// </summary>
        void FollowPrey(Region sim, AnimalGroup pack)
        {
            double range = WorldMap.FromMeters(4000);
            var near = Near(pack.X, pack.Y, range).Where(g => !g.Species.Predator && g.Species.Huntable && g.Species.Size < 4).ToList();
            if (near.Count > 0)
            {
                double total = near.Sum(g => g.Species.Meat), pick = sim.Rng.Range(0, total);
                foreach (var g in near)
                {
                    pick -= g.Species.Meat;
                    if (pick <= 0) { pack.TargetX = g.X; pack.TargetY = g.Y; return; }
                }
            }
            var c = NearbyCell(sim.World, sim.Rng, pack.X, pack.Y, WorldMap.FromMeters(10000));
            pack.TargetX = c.X; pack.TargetY = c.Y;
        }

        /// <summary>
        /// Wolves hunt like people do: get close before the prey bolts. Hunger is counted in weeks
        /// without food (1 = a week; wolves can go one or two); a wolf eats ~4 kg (about 2 rations) a
        /// day, so a deer feeds a pack of six for about three days.
        /// </summary>
        void PackHunts(Region sim, AnimalGroup pack)
        {
            pack.Hunger = Math.Min(1.5, pack.Hunger + 1.0 / 7);
            if (pack.Hunger < 0.2) return;
            // Wolves test herd after herd through the day, picking out the ones that look easiest:
            // thin, tired or unwary animals. Every chase alarms the herd.
            double Chance(AnimalGroup g) => 0.6 * Math.Max(0, 1 - g.FlightDistance / 120)
                * (pack.Species.Speed / (pack.Species.Speed + g.Species.Speed) * 2) * (1.5 - g.Condition * 0.5);
            var near = Near(pack.X, pack.Y, WorldMap.FromMeters(1500)).Where(g => !g.Species.Predator && g.Species.Huntable && g.Species.Size < 4).ToList();
            for (int attempt = 0; attempt < 3 && near.Count > 0; attempt++)
            {
                // The chase goes to whichever herd at hand looks most worth it: meat times the odds.
                var prey = near.OrderByDescending(g => g.Species.Meat * Chance(g)).First();
                double chance = Chance(prey);
                Startle(prey, 0.15);
                if (!sim.Rng.Chance(chance)) continue;
                prey.Count--; Count(prey.Species, "wolves", 1);
                // Wolves pick out the weak: within reason, this one wasn't long for this world anyway.
                if (prey.Culled < prey.Count * prey.Species.DeathRate) prey.Culled++;
                // They stay with the carcass and feed on it for days (a pair can get over a week from a deer).
                pack.Hunger = Math.Max(-1.5, pack.Hunger - prey.Species.Meat / (2.2 * Math.Max(1, pack.Count)) / 7);
                break;
            }
            if (pack.Hunger > 1.2 && sim.Rng.Chance(0.05))
            {
                pack.Count--;
                if (pack.Gone) sim.Chronicle.Write(sim.Clock, Importance.Notable, "The last of the wolf pack starved.");
            }
        }

        /// <summary>Distance to the closest camp of any band.</summary>
        static double NearestCamp(Region region, double x, double y)
        {
            double best = double.MaxValue;
            foreach (var b in region.Bands)
                if (b.Band.Members.Count > 0) best = Math.Min(best, WorldMap.Distance(x, y, b.Band.CampX, b.Band.CampY));
            return best;
        }

        static void Step(AnimalGroup g, double maxCells)
        {
            double dx = g.TargetX - g.X, dy = g.TargetY - g.Y;
            double d = Math.Sqrt(dx * dx + dy * dy);
            if (d < 0.01) return;
            double s = Math.Min(maxCells, d) / d;
            g.X += dx * s; g.Y += dy * s;
        }

        /// <summary>A place where this species feeds well (for placing the first animals).</summary>
        static Cell GoodGround(WorldMap map, Rng rng, Species sp)
        {
            Cell best = map[rng.Range(0, map.Size), rng.Range(0, map.Size)];
            for (int i = 0; i < 6; i++)
            {
                var c = map[rng.Range(0, map.Size), rng.Range(0, map.Size)];
                if (sp.FeedsOn(c.Terrain) > sp.FeedsOn(best.Terrain)) best = c;
            }
            return best;
        }

        static Cell NearbyCell(WorldMap map, Rng rng, double x, double y, double radius)
        {
            int cx = (int)Math.Max(0, Math.Min(map.Size - 1, x + rng.Range(-radius, radius)));
            int cy = (int)Math.Max(0, Math.Min(map.Size - 1, y + rng.Range(-radius, radius)));
            return map[cx, cy];
        }
    }
}
