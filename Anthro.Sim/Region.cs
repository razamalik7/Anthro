using System;
using System.Collections.Generic;
using System.Linq;
using Anthro.Sim.Core;
using Anthro.Sim.Discovery;
using Anthro.Sim.Ecology;
using Anthro.Sim.People;
using Anthro.Sim.World;

namespace Anthro.Sim
{
    /// <summary>
    /// The whole map: one world, one clock, one set of animals, and every band living on it. Every
    /// band is simulated the same way. People move between bands only by walking there: visiting,
    /// pairing with someone from another band, leaving to join a band they have ties with, or
    /// setting off on their own. Nobody ever leaves the map. <see cref="Step"/> advances one hour.
    /// </summary>
    public sealed partial class Region
    {
        /// <summary>Cells across: 40 km. Big enough for several bands at real forager densities.</summary>
        public const int MapSize = 200;

        /// <summary>How far away people keep track of where other bands are camped.</summary>
        public static readonly double KnownRange = WorldMap.FromMeters(15000);

        public readonly Rng Rng;
        public readonly SimClock Clock = new SimClock();
        /// <summary>Events that belong to the land rather than to a band (animals, bands appearing and vanishing).</summary>
        public readonly Chronicle Chronicle = new Chronicle();
        public readonly WorldMap World;
        public readonly EcologySystem Ecology = new EcologySystem();
        public readonly DiscoveryWeb? Web;

        /// <summary>Bands living on the map now.</summary>
        public readonly List<Simulation> Bands = new List<Simulation>();
        /// <summary>Every band that has ever lived here, including those that are gone.</summary>
        public readonly List<Simulation> AllBands = new List<Simulation>();
        /// <summary>The band the story starts with.</summary>
        public Simulation Founders { get; private set; } = null!;

        public event Action<IReadOnlyList<(Simulation band, YearStats year)>>? YearEnded;

        /// <summary>Every discovery made on the map: (year, band, stage id).</summary>
        public readonly List<(int year, string band, string stage)> DiscoveryLog = new List<(int, string, string)>();

        internal readonly Names Names = new Names();
        internal int NextPersonId;
        int _nextBandId;
        /// <summary>Pairs of bands camped within sight of each other's fires (by band id, smaller first).</summary>
        readonly HashSet<(int, int)> _together = new HashSet<(int, int)>();
        /// <summary>Bands that lived at some point this year, so each reports its year even if it ended.</summary>
        readonly List<Simulation> _reporting = new List<Simulation>();

        Region(ulong seed, DiscoveryWeb? web)
        {
            Rng = new Rng(seed);
            Web = web;
            // The story opens in late summer: food is plentiful and autumn's stockpiling is ahead,
            // as it would be for bands that have been living here for generations.
            Clock.StartAt(Season.Summer, 60);
            World = WorldGen.Generate(Rng.Fork(1), MapSize);
            NewWeatherYear();
            Ecology.Populate(this);
        }

        /// <summary>
        /// A new map with the founding camp: one people of about thirty. Other camps, and in time other
        /// peoples, come from it (pass otherBands to start with unrelated peoples already on the map).
        /// </summary>
        public static Region Create(ulong seed, DiscoveryWeb? web = null, int bandSize = 30, int otherBands = 0, int kinCamps = 0)
        {
            var region = new Region(seed, web);
            region.Founders = region.FoundBand(bandSize);
            region.AddKinCamps(region.Founders, kinCamps);
            // Other peoples already on the map: each its own culture, with its own related camps.
            for (int i = 0; i < otherBands; i++) region.AddKinCamps(region.FoundBand(region.Rng.Range(18, 31)), kinCamps);
            foreach (var b in region.Bands)
                foreach (var p in b.Band.Members.Where(p => p.AgeYears(region.Clock) >= 16))
                    foreach (var other in region.Bands)
                        if (other != b && region.Rng.Chance(0.6))
                            p.Map.Camps[other.Id] = (other.Band.CampX, other.Band.CampY, region.Clock.Day);
            return region;
        }

        /// <summary>
        /// Other camps of a founding camp's people nearby: the families they marry into and visit. One
        /// people, one culture: the same customs and expectations (each person their own variation), and
        /// they know each other.
        /// </summary>
        void AddKinCamps(Simulation founders, int n)
        {
            for (int i = 0; i < n; i++)
            {
                var camp = FoundBand(Rng.Range(18, 26));
                camp.Tribe = founders.Tribe;
                Tribes.Remove(Tribes[Tribes.Count - 1]);
                camp.Band.Name = camp.CampName(camp.Band.Members.Where(camp.Grown).OrderByDescending(m => m.AgeYears(Clock)).FirstOrDefault());
                var f = founders.Band;
                camp.Band.ResidenceNorm = f.ResidenceNorm; camp.Band.ComingOfAge = f.ComingOfAge; camp.Band.ChildrenFollowMother = f.ChildrenFollowMother;
                System.Array.Copy(f.AttractionMind, camp.Band.AttractionMind, f.AttractionMind.Length);
                System.Array.Copy(f.AttractionBody, camp.Band.AttractionBody, f.AttractionBody.Length);
                var elders = f.Members.Where(founders.Grown).ToList();
                foreach (var p in camp.Band.Members)
                {
                    var model = elders[Rng.Range(0, elders.Count)];
                    foreach (var kv in model.Expect.Snapshot())
                        if (kv.Key.StartsWith("act:") || kv.Key.StartsWith("tactic:") || kv.Key.StartsWith("wed:") || kv.Key.StartsWith("bound:") || kv.Key.StartsWith("norm:") || kv.Key.StartsWith("god:"))
                            p.Expect.Absorb(kv.Key, kv.Value + Rng.Normal(0, 0.1), 1, kv.Value);
                }
            }
            var tribe = Bands.Where(b => b.Tribe == founders.Tribe).ToList();
            foreach (var a in tribe)
                foreach (var b in tribe)
                {
                    if (a == b) continue;
                    foreach (var p in a.Band.Members.Where(a.Grown))
                        foreach (var o in b.Band.Members.Where(b.Grown))
                            if (Rng.Chance(0.4)) p.AddAffinity(o, Rng.Range(0.1, 0.35));
                }
        }

        public bool Extinct => Bands.Count == 0;

        public IEnumerable<Person> People => Bands.SelectMany(b => b.Band.Members);

        public void Step()
        {
            ApplyGodCommands();
            if (Clock.Hour == 0) using (Profiler.Measure("daily")) Daily();
            foreach (var band in Bands.ToList())
                if (!band.Extinct) band.StepHour();
            Clock.Advance();
            if (Clock.Tick % (SimClock.HoursPerDay * SimClock.DaysPerYear) == 0) EndYear();
        }

        public void RunYears(int years)
        {
            long end = Clock.Tick + (long)years * SimClock.HoursPerDay * SimClock.DaysPerYear;
            while (Clock.Tick < end && !Extinct) Step();
        }

        void Daily()
        {
            int dayOfYear = Clock.DayOfYear;
            var climate = World.Climate;
            // Each cell has its own weather and regrows on its own, so the map is shared out across the processor's cores.
            using (Profiler.Measure("daily plants"))
                System.Threading.Tasks.Parallel.For(0, World.Size, x => { for (int y = 0; y < World.Size; y++) World.Cells[x, y].DailyRegrow(climate, dayOfYear); });
            using (Profiler.Measure("daily ecology")) Ecology.DailyUpdate(this);
            // Untended grain stands drift back toward wild.
            if (Clock.Day % 30 == 0)
                foreach (var c in World.All())
                    if (c.PlantCapacity > c.WildCapacity && c.WildCapacity > 0) c.PlantCapacity -= (c.PlantCapacity - c.WildCapacity) * 0.05;
            foreach (var band in Bands.ToList()) band.Daily();
            using (Profiler.Measure("daily meetings")) Meetings();
            using (Profiler.Measure("daily encounters")) Encounters();
            foreach (var gone in Bands.Where(b => b.Extinct).ToList())
            {
                Bands.Remove(gone);
                Chronicle.Write(Clock, Importance.Notable, $"The {gone.Band.Name} band is no more.");
            }
        }

        void EndYear()
        {
            var stats = new List<(Simulation, YearStats)>();
            foreach (var band in _reporting) stats.Add((band, band.EndYear()));
            _reporting.Clear();
            _reporting.AddRange(Bands);
            NewWeatherYear();
            ConsiderSchisms();
            YearEnded?.Invoke(stats);
        }

        /// <summary>A new year's weather: the climate draws the year, and each place learns how wet its district is.</summary>
        void NewWeatherYear()
        {
            World.Climate.NewYear(Rng);
            foreach (var c in World.All()) c.Wet = World.Climate.Wetness(c.X, c.Y, World.Size);
        }

        // ---- Bands -----------------------------------------------------------------------------

        Simulation FoundBand(int size)
        {
            var band = NewBand();
            band.Tribe = NewTribe("", null);
            var camp = CampSites(band.Rng, 40).FirstOrDefault(c => Bands.All(b => b == band || b.Band.Members.Count == 0
                           || WorldMap.Distance(c.X, c.Y, b.Band.CampX, b.Band.CampY) > WorldMap.FromMeters(8000)))
                       ?? CampSites(band.Rng, 40).First();
            band.Found(size, camp.X, camp.Y);
            band.Tribe.Name = band.Band.Name;
            band.LastKinContact = Clock.Day;
            return band;
        }

        /// <summary>A new, empty band on the map. Whoever creates it fills it.</summary>
        internal Simulation NewBand()
        {
            int id = _nextBandId++;
            var band = new Simulation(this, id, Rng.Fork((ulong)id * 7919 + 13));
            Bands.Add(band);
            AllBands.Add(band);
            _reporting.Add(band);
            return band;
        }

        /// <summary>Random forest and grassland spots, best camps first.</summary>
        internal IEnumerable<Cell> CampSites(Rng rng, int samples)
        {
            var list = new List<Cell>();
            for (int i = 0; i < samples * 4 && list.Count < samples; i++)
            {
                var c = World[rng.Range(0, World.Size), rng.Range(0, World.Size)];
                if (c.Terrain == Terrain.Forest || c.Terrain == Terrain.Grassland) list.Add(c);
            }
            return list.OrderByDescending(c => CampScore(c.X, c.Y));
        }

        /// <summary>
        /// Where a group setting off on its own makes camp: the best spot they find within a few
        /// days' walk, away from other bands' camps if they can manage it.
        /// </summary>
        internal Cell FindCampSite(Rng rng, double fromX, double fromY, double minDistance, double maxDistance, Simulation leaving)
        {
            var spots = new List<Cell>();
            for (int i = 0; i < 200 && spots.Count < 30; i++)
            {
                double angle = rng.Range(0, Math.PI * 2), d = rng.Range(minDistance, maxDistance);
                int x = (int)(fromX + Math.Cos(angle) * d), y = (int)(fromY + Math.Sin(angle) * d);
                if (!World.InBounds(x, y)) continue;
                var c = World[x, y];
                if (c.Terrain == Terrain.Forest || c.Terrain == Terrain.Grassland) spots.Add(c);
            }
            if (spots.Count == 0) return World[(int)fromX, (int)fromY];
            double Crowd(Cell c) => Bands.Where(b => b != leaving && b.Band.Members.Count > 0)
                .Count(b => WorldMap.Distance(c.X, c.Y, b.Band.CampX, b.Band.CampY) < WorldMap.FromMeters(5000));
            return spots.OrderBy(Crowd).ThenByDescending(c => CampScore(c.X, c.Y)).First();
        }

        internal double CampScore(double x, double y)
        {
            double plants = World.Around(x, y, WorldMap.FromMeters(3000)).Sum(c => c.PlantStock);
            double game = Ecology.Groups.Where(g => !g.Gone && g.Species.Huntable && WorldMap.Distance(g.X, g.Y, x, y) < WorldMap.FromMeters(5000))
                .Sum(g => g.Count * g.Species.Meat * 0.1);
            double water = World.Around(x, y, WorldMap.FromMeters(1000)).Any(c => c.Terrain == Terrain.River) ? 1.2 : 0.8;
            return (plants + game) * water;
        }

        public Simulation? BandOf(Person p) => Bands.FirstOrDefault(b => b.Band.Members.Contains(p));

        public Simulation? BandById(int id) => Bands.FirstOrDefault(b => b.Id == id);

        // ---- Moving between bands ---------------------------------------------------------------

        /// <summary>
        /// Some people walk from one band to another. They make a hearth of their own there (with
        /// their partner, if they're joining one), carrying whatever food their emptied hearths held.
        /// </summary>
        internal void Move(List<Person> people, Simulation from, Simulation to, Person? partner = null)
        {
            TakeDogs(people.Count, from, to);
            var carried = from.Leave(people);
            to.Welcome(people, carried, partner);
        }

        /// <summary>Dogs go where their people go: a share for a family that leaves, all of them when the camp empties.</summary>
        static void TakeDogs(int leaving, Simulation from, Simulation to)
        {
            if (from.Band.Dogs == 0 || from.Band.Members.Count == 0) return;
            int dogs = leaving >= from.Band.Members.Count ? from.Band.Dogs : (int)Math.Round(from.Band.Dogs * leaving / (double)from.Band.Members.Count);
            if (dogs == 0) return;
            to.Band.DogsFed = (to.Band.DogsFed * to.Band.Dogs + from.Band.DogsFed * dogs) / (to.Band.Dogs + dogs);
            from.Band.Dogs -= dogs; to.Band.Dogs += dogs;
        }

        /// <summary>Whole households move together, hearth, food and all.</summary>
        internal void MoveHouseholds(List<Household> homes, Simulation from, Simulation to)
        {
            var people = homes.SelectMany(h => h.Members).ToList();
            TakeDogs(people.Count, from, to);
            from.LetHouseholdsGo(homes, people);
            to.TakeInHouseholds(homes, people);
        }

        // ---- Bands meeting ------------------------------------------------------------------------

        public bool Together(Simulation a, Simulation b) => _together.Contains(Key(a, b));

        static (int, int) Key(Simulation a, Simulation b) => a.Id < b.Id ? (a.Id, b.Id) : (b.Id, a.Id);

        /// <summary>
        /// Bands whose camps come within sight of each other's fires spend time together: their people
        /// talk, court, pass on sicknesses, and some decide they'd rather live with the other band.
        /// Nothing brings them together but where each band chooses to camp.
        /// </summary>
        void Meetings()
        {
            _together.RemoveWhere(k => !Bands.Any(b => b.Id == k.Item1) || !Bands.Any(b => b.Id == k.Item2));
            for (int i = 0; i < Bands.Count; i++)
                for (int j = i + 1; j < Bands.Count; j++)
                {
                    var a = Bands[i];
                    var b = Bands[j];
                    if (a.Extinct || b.Extinct) continue;
                    double d = WorldMap.Distance(a.Band.CampX, a.Band.CampY, b.Band.CampX, b.Band.CampY);
                    var key = Key(a, b);
                    if (d < WorldMap.FromMeters(3000) && a.Tribe == b.Tribe) a.LastKinContact = b.LastKinContact = Clock.Day;
                    if (!_together.Contains(key) && d < WorldMap.FromMeters(3000))
                    {
                        _together.Add(key);
                        a.Chronicle.Write(Clock, Importance.Notable, $"The {b.Band.Name} band made camp nearby.");
                        b.Chronicle.Write(Clock, Importance.Notable, $"The {a.Band.Name} band made camp nearby.");
                        a.ConsiderJoining(b);
                        if (!a.Extinct && !b.Extinct) b.ConsiderJoining(a);
                    }
                    else if (_together.Contains(key) && d > WorldMap.FromMeters(5000))
                    {
                        _together.Remove(key);
                        a.Chronicle.Write(Clock, Importance.Detail, $"The {b.Band.Name} band moved on.");
                        b.Chronicle.Write(Clock, Importance.Detail, $"The {a.Band.Name} band moved on.");
                    }
                }
        }

        /// <summary>
        /// Bands whose foraging grounds overlap sometimes meet out on the land: the more ground they
        /// share, the likelier. What happens is up to the two people who meet.
        /// </summary>
        void Encounters()
        {
            for (int i = 0; i < Bands.Count; i++)
                for (int j = i + 1; j < Bands.Count; j++)
                {
                    var a = Bands[i];
                    var b = Bands[j];
                    if (a.Extinct || b.Extinct || Together(a, b)) continue;
                    if (WorldMap.Distance(a.Band.CampX, a.Band.CampY, b.Band.CampX, b.Band.CampY) > WorldMap.FromMeters(8000)) continue;
                    int shared = a.ForagePatches.Count(c => b.ForagePatches.Contains(c)) + a.FishSpots.Count(c => b.FishSpots.Contains(c));
                    if (shared == 0 || !Rng.Chance(Math.Min(0.5, shared * 0.05))) continue;
                    var pa = a.Band.Members.Where(p => p.Alive && p.AgeYears(Clock) >= 14 && p.Expect.Has("Forage")).ToList();
                    var pb = b.Band.Members.Where(p => p.Alive && p.AgeYears(Clock) >= 14 && p.Expect.Has("Forage")).ToList();
                    if (pa.Count == 0 || pb.Count == 0) continue;
                    a.MeetOnSharedGround(pa[Rng.Range(0, pa.Count)], pb[Rng.Range(0, pb.Count)], b, shared);
                }
        }

        // ---- Checks -----------------------------------------------------------------------------

        /// <summary>
        /// Bookkeeping check for tests: everyone alive is in exactly one band, and each band's
        /// households list exactly its members whose home they are.
        /// </summary>
        public void CheckPlaces()
        {
            var seen = new HashSet<Person>();
            foreach (var band in Bands)
                foreach (var m in band.Band.Members)
                {
                    if (!seen.Add(m))
                        throw new InvalidOperationException($"{m.Name}#{m.Id} is in more than one band (day {Clock.Day}): "
                            + string.Join(", ", Bands.Where(b => b.Band.Members.Contains(m)).Select(b => b.Band.Name)));
                    if (!m.Alive) throw new InvalidOperationException($"{m.Name} is dead but still in the {band.Band.Name} band");
                }
            foreach (var band in Bands) band.CheckHouseholds();
        }
    }
}
