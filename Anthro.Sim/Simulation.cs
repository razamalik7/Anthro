using System;
using System.Collections.Generic;
using System.Linq;
using Anthro.Sim.Activities;
using Anthro.Sim.Core;
using Anthro.Sim.Discovery;
using Anthro.Sim.Ecology;
using Anthro.Sim.People;
using Anthro.Sim.World;

namespace Anthro.Sim
{
    public sealed class YearStats
    {
        public int Year;
        public int Births;
        public int Population;
        public double AvgSatiety, AvgHealth, Valor, Caution, Sharing;
        public int SickDays, Couples;
        public double Gathered, Hunted, Fished, Eaten, Spoiled, Needed, Shared, CookingGain;
        /// <summary>Asking other camps for food: hearths asked, how many gave, and how much was carried home.</summary>
        public int GuestAsks, GuestGifts; public double GuestFood;
        /// <summary>Food left behind on moves (what could not be carried).</summary>
        public double Abandoned;
        /// <summary>Fights and the deaths in them, by kind: quarrel, gang (taking a hearth's food), raid, encounter.</summary>
        public readonly Dictionary<string, int> FightsBy = new Dictionary<string, int>(), FightDeathsBy = new Dictionary<string, int>();
        public int FireOutDays;
        /// <summary>Rationing lessons this year (diagnostic): up/down, and whether the family had actually rationed that season.</summary>
        public int RationUp, RationDown, RationHeld, RationNone;
        public int Refusals, Thefts, Fights, Raids;
        public int PrayersAsked, PrayersResolvedGood, PrayersResolvedBad;
        public double FavorGain, FoodOffered;
        public readonly Dictionary<Activities.Verb, long> Hours = new Dictionary<Activities.Verb, long>();
        internal int Samples;
        public readonly Dictionary<string, int> Deaths = new Dictionary<string, int>();
        public readonly Dictionary<string, int> Kills = new Dictionary<string, int>();
    }

    /// <summary>
    /// One band living on the map, and everything that happens inside it. The world, the clock and
    /// the animals belong to the <see cref="Region"/>, which steps every band each hour. Everything
    /// is driven by a single seed, so the same seed always replays the same history.
    /// </summary>
    public sealed partial class Simulation
    {
        public readonly Region Region;
        public readonly int Id;
        public readonly Rng Rng;
        public SimClock Clock => Region.Clock;
        /// <summary>This band's story.</summary>
        public readonly Chronicle Chronicle = new Chronicle();
        public WorldMap World => Region.World;
        public EcologySystem Ecology => Region.Ecology;
        public readonly Band Band = new Band();
        public DiscoveryWeb? Web => Region.Web;

        /// <summary>
        /// People from other bands who are here to talk: guests who walked over to visit, and the
        /// people of a band camped nearby who are sitting about their own fires.
        /// </summary>
        public IReadOnlyList<Person> Visitors
        {
            get
            {
                if (_visitorsTick == Clock.Tick) return _visitors;
                _visitorsTick = Clock.Tick;
                _visitors.Clear();
                foreach (var other in Region.Bands)
                {
                    if (other == this) continue;
                    bool near = Region.Together(this, other);
                    foreach (var m in other.Band.Members)
                    {
                        if (m.Current is Visit v) { if (v.Host == this && v.Arrived) _visitors.Add(m); }
                        else if (near && m.AgeYears(Clock) >= 14 && (m.Current is Socialize || m.Current is Rest)) _visitors.Add(m);
                    }
                }
                return _visitors;
            }
        }
        readonly List<Person> _visitors = new List<Person>();
        long _visitorsTick = -1;

        /// <summary>Who each adult would like to go and visit today, and how much (worked out once a day).</summary>
        internal readonly Dictionary<Person, (Simulation band, double pull)> VisitPulls = new Dictionary<Person, (Simulation, double)>();
        /// <summary>Those whose visit pull is mostly hunger: they'd go to ask for food.</summary>
        internal readonly HashSet<Person> VisitForFood = new HashSet<Person>();

        /// <summary>Best plant patches near camp, refreshed daily so foragers don't each scan the map.</summary>
        public readonly List<Cell> ForagePatches = new List<Cell>();

        public YearStats ThisYear { get; private set; } = new YearStats();
        public event Action<YearStats>? YearEnded;

        readonly HashSet<Species> _killedBefore = new HashSet<Species>();
        long _campSince;
        bool _wolvesSeenThisSeason, _hungerReportedThisSeason;
        /// <summary>How hard life has been lately: hunger, smoothed over roughly a season.</summary>
        double _hardship;

        static readonly string[] DefaultKnown = { "fire.make", "knap.flake", "knap.blade", "spear.wood", "forage.rounds", "fish.grope", "lang.speech", "wood.shape", "shelter.brush", "wayfind.heart", "hunt.stalk", "hunt.ambush", "hunt.drive" };

        internal Simulation(Region region, int id, Rng rng)
        {
            Region = region;
            Id = id;
            Rng = rng;
        }

        /// <summary>A new map from this seed; returns the founding band.</summary>
        public static Simulation Create(ulong seed, DiscoveryWeb? web = null, int bandSize = 30) =>
            Region.Create(seed, web, bandSize).Founders;

        /// <summary>Advances the whole map an hour (every band, not just this one).</summary>
        public void Step() => Region.Step();

        /// <summary>Runs the whole map (every band, not just this one).</summary>
        public void RunYears(int years) => Region.RunYears(years);

        public void CheckPlaces() => Region.CheckPlaces();

        public bool Extinct => Band.Members.Count == 0;

        /// <summary>An hour of everyone's life in this band.</summary>
        internal void StepHour()
        {
            foreach (var p in Band.Members.ToList())
            {
                // Someone who died or left earlier this hour is no longer here; someone who arrived
                // from a band that already had its hour has had theirs.
                if (!p.Alive || p.SteppedAt == Clock.Tick || p.LeftDay == Clock.Day && !Band.Members.Contains(p)) continue;
                p.SteppedAt = Clock.Tick;
                Metabolize(p);
                if (p.Current != null && p.AgeYears(Clock) >= 14) ThisYear.Hours[p.Current.Verb] = (ThisYear.Hours.TryGetValue(p.Current.Verb, out var h) ? h : 0) + 1;
                if (p.Current is Forage || p.Current is Hunt || p.Current is Fish || p.Current is Trap || p.Current is GatherWood) p.WorkToday++;
                if (p.Current == null || p.Current.Done)
                {
                    using (Profiler.Measure("decide")) p.Current = Decider.Choose(this, p);
                    using (Profiler.Measure("begin ", p.Current.Verb)) p.Current.Begin(this);
                }
                using (Profiler.Measure("hour ", p.Current.Verb)) p.Current.Hour(this);
                if (p.Alive && p.Current != null && Band.Members.Contains(p)) TryDiscover(p);
                if (p.Alive && p.Current != null && Band.Members.Contains(p)) MaybeAccident(p);
            }
        }

        // ---- Founding ------------------------------------------------------------------------

        /// <summary>A band that has been living on this land a while, at the start of the story.</summary>
        internal void Found(int size, double campX, double campY)
        {
            Band.Name = Region.Names.Next(Rng);
            for (int i = 0; i < TraitCounts.Mind; i++) Band.AttractionMind[i] = Rng.Normal(0, 0.2);
            for (int i = 0; i < TraitCounts.Body; i++) Band.AttractionBody[i] = Rng.Normal(0, 0.2);
            Band.CampX = campX; Band.CampY = campY;
            Band.ComingOfAge = Math.Max(13, Math.Min(18, Rng.Normal(15, 1.2)));

            // A typical forager band: about a quarter couples, a few elders and young adults, the rest children.
            int couples = Math.Max(1, (int)Math.Round(size * 0.25));
            int elders = Math.Max(1, size / 10);
            int youths = Math.Max(1, size / 10);
            var parents = new List<(Person mother, Person father, double motherAge)>();
            for (int i = 0; i < couples; i++)
            {
                double womanAge = Rng.Range(18, 38);
                var woman = NewFounder(womanAge, Sex.Female);
                var man = NewFounder(Math.Max(16, womanAge + Rng.Range(-2, 8)), Sex.Male);
                // Couples who've been together since their late teens.
                Person.Wed(woman, man, Clock.Day - (long)(Math.Max(0, womanAge - 18) * SimClock.DaysPerYear));
                woman.AddAffinity(man, 0.6); man.AddAffinity(woman, 0.6);
                NewHousehold(woman, man);
                parents.Add((woman, man, womanAge));
            }
            // Elders are the parents of one of the couples and live at their hearth; teenagers are the
            // older children of a couple. Nobody starts out alone.
            for (int i = 0; i < elders; i++)
            {
                var elder = MakeStranger(Rng.Range(48, 63), Rng.Chance(0.5) ? Sex.Female : Sex.Male);
                var (mother, father, _) = parents[i % parents.Count];
                var child = Rng.Chance(0.5) ? mother : father;
                if (elder.Sex == Sex.Female) child.Mother = elder; else child.Father = elder;
                elder.Children.Add(child);
                elder.AddAffinity(child, 0.5); child.AddAffinity(elder, 0.5);
                Join(elder);
                MoveInto(elder, child.Home!, false);
            }
            for (int i = 0; i < youths; i++)
            {
                var (mother, father, motherAge) = parents.OrderByDescending(x => x.motherAge).ElementAt(i % parents.Count);
                if (motherAge < 32) { NewChild(mother, father, Rng.Range(0, Math.Max(1, Math.Min(13, motherAge - 16)))); continue; }
                NewChild(mother, father, Rng.Range(14, Math.Min(20, motherAge - 16)));
            }
            while (Band.Members.Count < size)
            {
                var (mother, father, motherAge) = parents[Rng.Range(0, parents.Count)];
                NewChild(mother, father, Rng.Range(0, Math.Max(1, Math.Min(13, motherAge - 16))));
            }

            KnitTogether(Band.Members);
            foreach (var m in Band.Members) KnowHomeRange(m);
            SeedCulture(Band.Members);
            SeedSocialCulture(Band.Members);
            // A band that has been living here a while: a few days of food in each hearth.
            foreach (var h in Band.Households)
            {
                h.Food.AddMeat(1.3 * h.Members.Count);
                h.Food.Plants = 1.3 * h.Members.Count;
                h.Food.Stored = 2 * h.Members.Count;
            }
            Band.Materials.Flint = 6; Band.Materials.Tools = 25;
            RefreshForagePatches();
            Chronicle.Write(Clock, Importance.Major,
                $"The {Band.Name} band, {Band.Members.Count} people, made camp in a {World[(int)Band.CampX, (int)Band.CampY].Terrain.ToString().ToLower()} by the river.");
        }

        Person NewFounder(double ageYears, Sex sex)
        {
            var p = MakeStranger(ageYears, sex);
            Join(p);
            return p;
        }

        /// <summary>Someone not (yet) of this band: founders, and the people of neighboring bands.</summary>
        Person MakeStranger(double ageYears, Sex sex)
        {
            var p = new Person(Region.NextPersonId++, Region.Names.Next(Rng), sex, Clock.Day - (long)(ageYears * SimClock.DaysPerYear));
            for (int i = 0; i < p.Mind.Length; i++) p.Mind[i] = Inheritance.Founder(Rng);
            for (int i = 0; i < p.Body.Length; i++) p.Body[i] = Inheritance.Founder(Rng);
            for (int i = 0; i < p.Interests.Length; i++) p.Interests[i] = Inheritance.Clamp01(Rng.Normal(0.35, 0.15));
            SeedSkills(p, ageYears);
            foreach (var id in StartingKnowledge()) p.Knows.Add(id);
            return p;
        }

        Person NewChild(Person mother, Person father, double ageYears)
        {
            var p = MakeChildOf(mother, father, ageYears);
            Join(p);
            return p;
        }

        Person MakeChildOf(Person mother, Person father, double ageYears)
        {
            var p = new Person(Region.NextPersonId++, Region.Names.Next(Rng), Rng.Chance(0.5) ? Sex.Female : Sex.Male, Clock.Day - (long)(ageYears * SimClock.DaysPerYear));
            p.Mother = mother; p.Father = father;
            mother.Children.Add(p); father.Children.Add(p);
            for (int i = 0; i < p.Mind.Length; i++) p.Mind[i] = Inheritance.Blend(Rng, mother.Mind[i], father.Mind[i]);
            for (int i = 0; i < p.Body.Length; i++) p.Body[i] = Inheritance.Blend(Rng, mother.Body[i], father.Body[i]);
            for (int i = 0; i < p.Interests.Length; i++)
                p.Interests[i] = Inheritance.Clamp01((mother.Interests[i] + father.Interests[i]) / 2 * 0.4 + Rng.Normal(0.2, 0.1));
            for (int i = 0; i < p.Drives.Length; i++) p.Drives[i] = p.BaseDrives[i] = Inheritance.Blend(Rng, mother.BaseDrives[i], father.BaseDrives[i]);
            SeedSkills(p, ageYears);
            p.AddAffinity(mother, 0.5); p.AddAffinity(father, 0.4);
            mother.AddAffinity(p, 0.6); father.AddAffinity(p, 0.5);
            InheritSocialExpectations(p, mother);
            foreach (var id in StartingKnowledge()) p.Knows.Add(id);
            return p;
        }

        void SeedSkills(Person p, double age)
        {
            // Late summer: people are in decent condition, part way to their autumn fat.
            p.Reserve = p.LastReserve = Physiology.MaxReserve(p, age) * 0.6;
            double grown = Inheritance.Clamp01(age / 25);
            p.Skills[(int)Skill.Foraging] = grown * Rng.Range(0.4, 0.9);
            p.Skills[(int)Skill.Hunting] = age >= 14 ? grown * Rng.Range(0.2, 0.8) * (0.5 + p.M(MindTrait.Boldness)) : 0;
            p.Skills[(int)Skill.Knapping] = age >= 12 ? grown * Rng.Range(0.1, 0.7) : 0;
            p.Skills[(int)Skill.Fire] = grown * 0.5;
            p.Skills[(int)Skill.Caring] = age >= 14 ? Rng.Range(0.2, 0.6) : 0;
            p.Skills[(int)Skill.Teaching] = age >= 20 ? Rng.Range(0.1, 0.5) : 0;
            p.Skills[(int)Skill.Cooking] = grown * Rng.Range(0.2, 0.6);
            p.Skills[(int)Skill.Fishing] = grown * Rng.Range(0.1, 0.5);
        }

        /// <summary>
        /// Everyone is born into the band's starting culture. Stages discovered later have to be
        /// taught (next slice).
        /// </summary>
        void Join(Person p)
        {
            foreach (var id in StartingKnowledge()) p.Knows.Add(id);
            p.X = Band.CampX; p.Y = Band.CampY;
            Band.Members.Add(p);
            // Children live with their mother (or father); anyone else starts a hearth of their own.
            var parent = new[] { p.Mother, p.Father }.FirstOrDefault(x => x != null && x.Home != null && Band.Members.Contains(x));
            if (parent != null && p.AgeYears(Clock) < 14) MoveInto(p, parent.Home!, false);
            else NewHousehold(p);
        }

        IEnumerable<string> StartingKnowledge() => Web != null ? Web.KnownAtStart.Select(s => s.Id) : DefaultKnown;

        // ---- Daily ---------------------------------------------------------------------------

        /// <summary>
        /// About how many days fresh meat keeps here now: a few in warm weather; weeks frozen; months in
        /// cold caches. (Follows the spoilage rates: a fifth a day warm, 3% frozen, 0.5% cached.) Meat
        /// that will be dried keeps for months; that is weighed through the store shortfall instead.
        /// </summary>
        public double MeatKeepsDays => CampCell.Temperature >= 0 ? 4 : Band.AnyoneKnows("store.cold") ? 60 : 20;

        static readonly string[] _leanTopics = { "lean:spring", "lean:summer", "lean:autumn", "lean:winter" };
        /// <summary>The belief "how lean does this season get" (0 = plenty, 1 = starving).</summary>
        public static string LeanTopic(Season s) => _leanTopics[(int)s];

        /// <summary>The ground the camp stands on: its weather is the band's weather.</summary>
        public Cell CampCell => World[(int)Band.CampX, (int)Band.CampY];

        internal void Daily()
        {
            if (Extinct) return;
            var season = Clock.Season;
            double temp = CampCell.Temperature;

            if (Clock.DayOfSeason == 0 && Environment.GetEnvironmentVariable("ANTHRO_DEBUG_SEASONS") != null && Band.Members.Count > 0)
                Console.Error.WriteLine($"SEASON Y{Clock.Year} {Clock.Season,-6} pop {Band.Members.Count,3} | meat {Band.MeatTotal,5:F0} plants {Band.Households.Sum(h => h.Food.Plants),5:F0} stored {Band.Households.Sum(h => h.Food.Stored),5:F0} | avg reserve {Band.Members.Average(m => m.Reserve):F1} | lean-season beliefs {string.Join("/", Enum.GetValues(typeof(Season)).Cast<Season>().Select(s => Band.Members.Average(m => m.Expect.Get(LeanTopic(s), 0.2)).ToString("F2")))} | plant stock near camp {ForagePatches.Sum(c => c.PlantStock):F0}");
            if (Clock.DayOfSeason == 0)
            {
                _wolvesSeenThisSeason = false;
                _hungerReportedThisSeason = false;
                NewBudgetSeason();
                if (season == Season.Winter && World.Climate.WinterSeverity > 1)
                    Chronicle.Write(Clock, Importance.Notable, "A hard winter set in.");
            }

            double before = Band.FoodTotal;
            foreach (var h in Band.Households)
            {
                double had = h.Food.Total, kept = h.Food.Stored + h.Food.Dried;
                // Keeping count of stores and sealing them in pots: less goes bad.
                double care = (h.Knows("lang.count", Clock) ? 0.9 : 1) * (h.Knows("pot.fired", Clock) ? 0.95 : 1) * (h.Knows("pot.coil", Clock) ? 0.85 : 1) * (h.Knows("pot.pit", Clock) ? 0.9 : 1);
                h.Food.DailySpoil(temp, Cuisine.StorageLevel(h, Clock), h.Knows("store.cold", Clock), care);
                h.SpoiledRecent = h.SpoiledRecent * 0.9 + (had - h.Food.Total);
                h.StoresSpoiledRecent = h.StoresSpoiledRecent * 0.99 + (kept - h.Food.Stored - h.Food.Dried);
                h.StoresRotted += kept - h.Food.Stored - h.Food.Dried;
                h.StoresKept += kept;
                // Down to less than a day's food while budgeting, with the land not keeping up: the budget failed.
                if (h.HadToBudget && h.Food.Total < h.Members.Count * 0.9 && h.Income < h.Members.Count * 0.9) h.RanOut = true;
                h.Fuel = Math.Max(0, h.Fuel - Cuisine.DailyBurn(temp) * (h.Knows("fire.hearth", Clock) ? 0.8 : 1));
                if (!h.FireLit) ThisYear.FireOutDays++;
            }
            DailyDogs(Math.Max(0, before - Band.FoodTotal));
            ThisYear.Spoiled += before - Band.FoodTotal;
            using (Profiler.Measure("daily bonds")) DailyBonds();
            foreach (var m in Band.Members) ThisYear.Needed += DailyNeed(m);
            Band.Materials.Tools *= Band.AnyoneKnows("knap.haft") ? 0.998 : 0.997;   // hafted tools last longer
            Band.StalkFailures *= 0.97;

            Band.Filth = Band.Filth * 0.98 + Band.Members.Count * 0.002;
            using (Profiler.Measure("daily sleep")) SleepingTogether();
            CampAir();
            // Someone in camp who knows healing plants makes care for the sick count for more.
            _healers = Band.AnyoneKnows("forage.lore");
            using (Profiler.Measure("daily bodies")) foreach (var p in Band.Members.ToList()) DailyBody(p, temp);
            if (Band.Members.Count > 0)
            {
                ThisYear.Samples++;
                ThisYear.AvgSatiety += Band.Members.Average(m => m.Satiety);
                ThisYear.AvgHealth += Band.Members.Average(m => m.Health);
                ThisYear.SickDays += Band.Members.Count(m => m.Illness != null);
            }
            foreach (var p in Band.Members) p.AteRotten = false;
            if (Band.Members.Count > 0)
            {
                _hardship += (1 - Prospects - _hardship) * 0.02;
                double times = Prospects;
            }
            foreach (var p in Band.Members.Where(m => m.DueDay == Clock.Day).ToList()) GiveBirth(p);
            using (Profiler.Measure("daily conceive")) foreach (var p in Band.Members.ToList()) MaybeConceive(p);
            using (Profiler.Measure("daily visits")) UpdateVisitPulls();
            if (Clock.Day % 7 == 0) TendGrain();
            if (Clock.Day % 7 == 0) SeekCompany();
            Gathering();
            if (Clock.Day % 7 == 0) foreach (var m in Band.Members) m.Map.Fade(Clock.Day, MapCapacity(m));
            using (Profiler.Measure("daily wolves")) WolvesNearCamp();
            using (Profiler.Measure("daily patches")) RefreshForagePatches();
            ReportHunger();
        }

        /// <summary>
        /// An hour of living: the stomach empties (faster when working hard), and nourishment
        /// follows whether there's food in it. Infants live on their mother's milk, or another
        /// nursing woman's.
        /// </summary>
        void Metabolize(Person p)
        {
            double age = p.AgeYears(Clock);
            double effort = p.Current switch
            {
                Sleep _ => 0.7, Rest _ => 0.8, Socialize _ => 0.9, Eat _ => 0.9,
                Hunt _ => 1.6, Forage _ => 1.2, Fish _ => 1.1, Trap _ => 1.1, GatherFlint _ => 1.3, GatherWood _ => 1.3, _ => 1.0,
            };
            // Burn from the stomach first, then from the body's reserves.
            double burn = effort / 24;
            double fromStomach = Math.Min(p.Stomach, burn);
            p.Stomach -= fromStomach;
            p.Reserve = Math.Max(0, p.Reserve - (burn - fromStomach));
            // A well-filled stomach slowly lays down reserves, up to what this body can carry.
            double maxReserve = Physiology.MaxReserve(p, age);
            if (p.Stomach > 0.5 && p.Reserve < maxReserve)
            {
                double store = Math.Min(0.02, p.Stomach - 0.5);
                p.Stomach -= store;
                p.Reserve += store;
            }
            if (age >= 2)
            {
                p.Satiety = Physiology.Nourishment(p, age);
                return;
            }
            {
                var nurse = p.Mother != null && p.Mother.Alive && Band.Members.Contains(p.Mother) ? p.Mother
                    : Band.Members.FirstOrDefault(w => w.Sex == Sex.Female && w.Children.Any(c => c != p && c.Alive && c.AgeYears(Clock) < 2));
                // A nursing mother keeps her milk up by drawing on her own body; it only fails when she's badly starved.
                double milk = nurse == null ? 0 : Math.Min(1, nurse.Satiety / 0.4);
                double own = Physiology.Nourishment(p, age);
                bool softFood = p.Home != null && p.Home.SoftFoodUntil >= Clock.Day;
                double fed = age < 1 ? milk : softFood ? Math.Max(milk, own) : (milk + own) / 2;
                p.Satiety += (fed - p.Satiety) * 0.02;
            }
        }

        /// <summary>
        /// Walking hours for a distance in cells. Deep winter snow slows everyone down, unless they
        /// have snowshoes.
        /// </summary>
        public int TravelHours(double cells, Person p)
        {
            int hours = WorldMap.TravelHours(cells);
            // Snow past ankle-deep makes for slow, heavy walking.
            bool snow = CampCell.Snow > 15;
            if (snow && !p.Knows.Contains("hide.snowshoe")) hours = (int)Math.Ceiling(hours * 1.5);
            // Shoes spare the feet on long walks over rough or frozen ground.
            if (p.Knows.Contains("hide.shoe")) hours = Math.Max(0, (int)Math.Round(hours * 0.9));
            return hours;
        }

        /// <summary>Rations a day this person needs: by age and size, more when pregnant or nursing.</summary>
        public double DailyNeed(Person p)
        {
            double age = p.AgeYears(Clock);
            if (age < 1) return 0;
            double need = age < 2 ? 0.1 : age < 14 ? 0.2 + 0.3 * age / 14 : 0.5;
            if (p.DueDay >= 0) need += 0.1;
            if (p.Children.Any(c => c.Alive && c.AgeYears(Clock) < 2)) need += 0.15;
            // Bigger bodies eat more; so does a strong immune system, which is why immunity doesn't simply max out.
            return need * 2 * (0.85 + 0.3 * p.B(BodyTrait.Build) * p.B(BodyTrait.Height)) * (1 + 0.15 * (p.B(BodyTrait.Immunity) - 0.5));
        }

        /// <summary>
        /// A day in a body. Each harm is booked against its cause, so a death is always explained
        /// by what actually wore the person down.
        /// </summary>
        bool _healers;

        void DailyBody(Person p, double temp)
        {
            double age = p.AgeYears(Clock);
            // Each harm is booked against its cause in a ledger that fades over a few weeks, so a death
            // is attributed to whatever really wore the person down, not just the last day's blow.
            foreach (var key in p.HarmLedger.Keys.ToList()) p.HarmLedger[key] *= 0.93;
            var harm = p.HarmLedger;
            void Hurt(string cause, double amount)
            {
                if (amount <= 0) return;
                p.Health -= amount;
                harm[cause] = (harm.TryGetValue(cause, out var h) ? h : 0) + amount;
            }

            // Outright starvation only when the body's reserves are all but gone. Before that,
            // short rations do their damage by weakening people against sickness.
            // Wasting does its damage while the body is going without, not while someone thin is eating again.
            if (p.Satiety < 0.15 && p.Stomach < 0.3) Hurt("starvation", (0.15 - p.Satiety) * 0.2);
            // What they've usually been bringing home lately (a few months' memory).
            p.Provided += (p.ProvidedToday - p.Provided) * 0.01;
            p.ProvidedToday = 0;
            // The usual working day lately (a month or two's memory).
            p.Workload += (p.WorkToday - p.Workload) * 0.02;
            p.WorkToday = 0;
            // Whether the body's been gaining or losing lately (about a month's memory).
            p.WeightTrend += (p.Reserve - p.LastReserve - p.WeightTrend) / 30;
            p.LastReserve = p.Reserve;
            // Going hungry makes people care more about food; a wound, more about safety.
            if (p.Satiety < 0.4) p.Shift(Drive.Food, 0.0005);
            if (p.Injury > 0.3) p.Shift(Drive.Safety, 0.0005);
            // Over months, people settle back toward who they are.
            for (int i = 0; i < p.Drives.Length; i++) p.Drives[i] += (p.BaseDrives[i] - p.Drives[i]) * 0.003;

            double cold = Math.Max(0, (Shelter.ComfortTemp(Band, p) - temp) / 20.0 - p.B(BodyTrait.ColdTolerance) * 0.3 - p.B(BodyTrait.Build) * 0.1);
            if (age < 5 || age > 60) cold *= 1.5;
            // Sleeping close to family shares body heat; small children sleep against their parents.
            if (HasSleepingFamily(p)) cold *= 0.6;
            p.ColdStress = cold;
            // Cold mostly kills by weakening people against sickness; it freezes outright only when severe.
            Hurt("cold", Math.Max(0, cold - 0.3) * 0.05);

            Infection(p, age, cold);
            if (p.Illness != null)
            {
                // The body's response builds over the days of a sickness, so most illnesses peak and pass.
                var d = p.Illness;
                p.SickDays++;
                // Care and rest help the body fight; they can't fight for it.
                double fight = Physiology.ImmuneStrength(p, age) * 0.08 * (1 + p.SickDays * 0.15)
                               * (1 + 0.5 * Math.Min(4, p.CareToday) / 4 * (_healers ? 1.6 : 1) + (p.Current is Rest || p.Current is Sleep ? 0.25 : 0));
                p.Sickness += d.Growth * (age < 5 ? d.ChildRisk : 1) - fight;
                if (p.Sickness <= 0)
                {
                    p.Sickness = 0; p.Illness = null; p.SickDays = 0;
                    if (d.ImmunityGain > 0) p.Immunities[d] = Math.Max(p.ImmunityTo(d), d.ImmunityGain);
                }
                else Hurt(d.DiedOf, p.Sickness * d.Harm * (age < 5 ? d.ChildRisk : 1));
            }
            foreach (var d in p.Immunities.Keys.ToList()) p.Immunities[d] = Math.Max(0, p.Immunities[d] - d.ImmunityFade);

            p.Injury = Math.Max(0, p.Injury - 0.01 * (0.5 + p.B(BodyTrait.Endurance)));
            if (p.Satiety >= 0.35 && p.Sickness == 0) p.Health += 0.01 * (age > 50 ? 0.6 : 1);
            p.Health = Math.Min(p.Health, Physiology.MaxHealth(age) - p.Injury * 0.5);

            // People remember how lean each season got (the worst of it, not the average; getting thin
            // counts, not only going hungry). The memory spreads like any belief, and as a season they
            // know to be lean draws near it moves them to put food by (see Decider.Foresight).
            p.WinterLow = Math.Min(p.WinterLow, Math.Min(p.Satiety, Physiology.BodyFat(p, age) / 0.6));
            if (Clock.DayOfSeason == SimClock.DaysPerSeason - 1 && age >= 10)
            {
                // A hard season is unforgettable: the lesson lands at once, hardest on the less resilient.
                p.Expect.Absorb(LeanTopic(Clock.Season), 1 - p.WinterLow, 0.3 + 0.3 * (1 - p.M(MindTrait.Resilience)), 0.2);
                LearnRationing(p);
                if (p.WinterLow < 0.6) AfterHungrySeason(p);
                p.WinterLow = 1;
            }

            FadeBelief(p);

            p.CareToday = 0;
            p.Fatigue *= 0.9;
            // Whatever makes life here wearing (a crowd of near-strangers, but also feuds, hunger, long days)
            // sets people thinking about moving on.
            if (!p.Restless && age >= 20 && Rng.Chance((Off("restless") ? Crowding(p) : Math.Min(1, p.Discontent)) * 0.02)) p.Restless = true;
            if (age < 12) DriftTowardParents(p);

            if (p.Health > 0) return;
            string cause = harm.Count == 0 ? (p.Injury > 0.5 ? "wounds" : "old age") : harm.OrderByDescending(kv => kv.Value).First().Key;
            var weakened = new List<string>();
            if (Environment.GetEnvironmentVariable("ANTHRO_DEBUG_DEATHS") != null)
                Console.Error.WriteLine($"DEATH {p.Name} age {age:F1} {Clock.Season} cause {cause} | ledger {string.Join(", ", harm.Select(kv => $"{kv.Key} {kv.Value:F2}"))} | reserve {p.Reserve:F2} stomach {p.Stomach:F2} home food {p.Home?.Food.Total ?? 0:F1} camp food {Band.FoodTotal:F0} for {Band.Members.Count} | sick {p.Illness?.Name ?? "-"} | doing {p.Current} | expect forage {p.Expect.Get(Verb.Forage, 0):F2} hunt {p.Expect.Get(Verb.Hunt, 0):F2} | home size {p.Home?.Members.Count ?? 0} fuel {p.Home?.Fuel ?? 0:F1}");
            if (cause != "starvation" && p.Satiety < 0.5) weakened.Add("hunger");
            if (cause != "cold" && p.ColdStress > 0.15) weakened.Add("cold");
            if (Physiology.MaxHealth(age) < 0.3 && cause != "old age") weakened.Add("age");
            Kill(p, cause, weakened.Count == 0 ? null : "weakened by " + string.Join(" and ", weakened));
        }

        /// <summary>
        /// Sickness always comes from somewhere that isn't another person: a fouled camp, rotten
        /// meat, an open wound, or (for someone already sick) a winter chill settling in the lungs.
        /// Catching diseases from people happens through <see cref="Contact"/>.
        /// </summary>
        /// <summary>
        /// Calibration (fitted to forager child mortality, ~20% before 1 and ~45% before 15): the
        /// daily chance a sickness settles in the chest before cold is counted, and how much likelier
        /// gut infections are in the weaning years. (Set back from 0.005/6 to 0.003/4 once hunger itself
        /// killed children in lean seasons: the higher values were fitted to always-fed children.)
        /// </summary>
        public static double ChestRisk = 0.003, WeaningRisk = 4;

        /// <summary>
        /// Calibration: chance per hour out on the land of a serious accident. Fitted to forager
        /// accident deaths of ~1.5-2.5 per 1,000 a year (Hadza, Ache): an adult spends ~2,000 hours a
        /// year out, about a quarter of serious accidents kill, hunting doubles the risk.
        /// </summary>
        public static double AccidentRate = 2.5e-6;

        /// <summary>
        /// Out on the land, things go wrong now and then: a fall, a bad cut, a slip into cold water.
        /// Most are wounds that heal (or fester); some kill.
        /// </summary>
        void MaybeAccident(Person p)
        {
            var a = p.Current;
            bool water = a is Fish;
            if (!(a is Forage || a is Hunt || a is Fish || a is Trap || a is GatherWood || a is GatherFlint || a is Visit)) return;
            if (!Rng.Chance(AccidentRate * (a is Hunt ? 2 : 1))) return;
            p.Injury = Inheritance.Clamp01(p.Injury + Rng.Range(0.3, 1.2));
            if (p.Injury >= 0.95) Kill(p, water ? "drowning" : "a fall");
            else Chronicle.Write(Clock, Importance.Detail, $"{p.Name} was badly hurt in {(water ? "the river" : "a fall")}.");
        }

        /// <summary>Calibration: daily chance of catching a fever from insects or water (warm-season peak).</summary>
        public static double FeverRate = 0.0003;

        void Infection(Person p, double age, double coldStress)
        {
            double immune = Physiology.ImmuneStrength(p, age);
            // A breastfed baby in its first months carries its mother's protection, from her milk.
            var mother = p.Mother;
            bool nursed = age < 0.5 && mother != null && mother.Alive && Band.Members.Contains(mother);
            double Protected(Disease d) => Math.Max(p.ImmunityTo(d), nursed ? mother!.ImmunityTo(d) * 0.8 : 0);
            if (p.Illness != null)
            {
                // Another sickness can settle in the lungs: most often in babies and the old, and far
                // more when they're chilled. What winter kills the old with, and small children all year.
                double frail = age < 2 ? 3 : age < 5 ? 1.5 : age > 60 ? 2 : 1;
                if (p.Illness != Disease.LungSickness && Rng.Chance((ChestRisk + coldStress * 0.05) * frail * (1.3 - immune)))
                {
                    p.Illness = Disease.LungSickness;
                    p.Sickness = Math.Max(p.Sickness, 0.3);
                    p.SickDays = 0;
                }
                return;
            }
            double weak = 1.3 - immune;
            // Colds are always around in any human group, and spread more when people huddle in winter.
            double t = CampCell.Temperature;
            double coldSeason = 1 + 2 * Math.Max(0, Math.Min(1, (10 - t) / 14));
            // Small children catch everything going: their bodies haven't met these germs yet.
            double young = age < 5 ? 2.5 : age < 14 ? 1.5 : 1;
            if (Rng.Chance(0.004 * coldSeason * young * weak * (1 - Protected(Disease.Cold)))) { p.Catch(Disease.Cold, Rng.Range(0.1, 0.3)); return; }
            // Gut infections are always around for small children, from dirt and, worst of all, from
            // the first foods other than milk: the weaning years are the most dangerous of childhood.
            double weaning = age >= 0.5 && age < 3 ? WeaningRisk : 1;
            if (age < 5 && Rng.Chance(0.0015 * weaning * weak * (1 - Protected(Disease.Flux)))) { p.Catch(Disease.Flux, Rng.Range(0.1, 0.3)); return; }
            // Fevers carried by insects and water turn up now and then, more in the warm months.
            double warm = Math.Max(0.2, Math.Min(2, t / 10));
            if (Rng.Chance(FeverRate * warm * young * weak * (1 - Protected(Disease.Fever)))) { p.Catch(Disease.Fever, Rng.Range(0.15, 0.35)); return; }
            if (p.AteRotten && Rng.Chance(0.3 * weak)) { p.Catch(Disease.BadMeat, Rng.Range(0.15, 0.35)); return; }
            if (p.Injury > 0.2 && Rng.Chance(p.Injury * 0.03 * weak)) { p.Catch(Disease.WoundRot, Rng.Range(0.2, 0.4)); return; }
            double filth = Band.Filth * 0.0004 * (age < 5 ? 2 : 1);
            if (Rng.Chance(filth * weak * (1 - Protected(Disease.Flux)))) p.Catch(Disease.Flux, Rng.Range(0.15, 0.4));
        }

        /// <summary>
        /// Close contact between two people for some hours: a contagious illness may pass either way,
        /// depending on how sick the carrier is and how well the other resists.
        /// </summary>
        public void Contact(Person a, Person b, double hours)
        {
            Pass(a, b, hours);
            Pass(b, a, hours);
        }

        void Pass(Person from, Person to, double hours)
        {
            var d = from.Illness;
            if (d == null || d.Contagion <= 0 || to.Illness != null || !to.Alive) return;
            double resist = Physiology.ImmuneStrength(to, to.AgeYears(Clock));
            double chance = d.Contagion * from.Sickness * hours * (1.3 - resist) * (1 - to.ImmunityTo(d));
            if (Rng.Chance(chance)) to.Catch(d, Rng.Range(0.1, 0.3));
        }

        /// <summary>Whether someone is in camp tonight (not off visiting another band).</summary>
        bool Home(Person p) => !(p.Current is Visit) && Band.Members.Contains(p);

        bool HasSleepingFamily(Person p) =>
            !(p.Current is Visit) &&
            (p.Spouses.Any(Home)
            || p.Children.Any(c => c.Alive && c.AgeYears(Clock) < 14 && Home(c))
            || (p.AgeYears(Clock) < 14 && ((p.Mother != null && Home(p.Mother)) || (p.Father != null && Home(p.Father)))));

        /// <summary>Families sleep together: a night is many hours of close contact.</summary>
        /// <summary>
        /// Living at close quarters: everyone about camp breathes the same smoke and handles the same
        /// things as everyone sick with something catching, a little each day. The more people in camp,
        /// the more sick people each one meets, so crowd sicknesses spread faster in big camps.
        /// </summary>
        void CampAir()
        {
            var sick = Band.Members.Where(p => p.Illness != null && p.Illness.Contagion > 0 && Home(p)).ToList();
            if (sick.Count == 0) return;
            foreach (var p in Band.Members)
            {
                if (p.Illness != null || !Home(p)) continue;
                foreach (var s in sick) { Pass(s, p, 0.15); if (p.Illness != null) break; }
            }
        }

        void SleepingTogether()
        {
            foreach (var p in Band.Members)
            {
                if (p.Current is Visit) continue;
                foreach (var s in p.Spouses)
                    if (s.Id > p.Id && Home(s))
                    {
                        Contact(p, s, 3);
                        // Sharing a hearth and a bed keeps a couple's bond alive.
                        p.AddAffinity(s, 0.004); s.AddAffinity(p, 0.004);
                    }
                foreach (var c in p.Children)
                    if (c.Alive && c.AgeYears(Clock) < 14 && Home(c)) Contact(p, c, 2);
            }
        }

        /// <summary>Children's interests lean toward whoever raises them.</summary>
        static void DriftTowardParents(Person child)
        {
            foreach (var parent in new[] { child.Mother, child.Father })
            {
                if (parent == null || !parent.Alive) continue;
                for (int i = 0; i < child.Interests.Length; i++)
                    child.Interests[i] += (parent.Interests[i] - child.Interests[i]) * 0.002;
            }
        }

        static readonly bool _debugKills = Environment.GetEnvironmentVariable("ANTHRO_DEBUG_KILLS") != null;

        public void Kill(Person p, string cause, string? weakenedBy = null)
        {
            if (!p.Alive) return;
            double age = p.AgeYears(Clock);
            p.Alive = false;
            p.DeathCause = cause;
            p.DeathDay = Clock.Day;
            Band.LastDeathDay = Clock.Day;
            // Losing family makes people hold the rest closer.
            foreach (var kin in Band.Members) if (kin.IsKin(p)) kin.Shift(Drive.Family, 0.01);
            // Laying the dead to rest eases the grief of those left (less of it turns to discontent).
            if (Band.AnyoneKnows("burial.cover"))
                foreach (var m in Band.Members) m.Discontent *= Band.AnyoneKnows("burial.goods") ? 0.9 : 0.95;
            LeaveHousehold(p);
            Band.Members.Remove(p);
            Band.Dead.Add(p);
            foreach (var s in p.Spouses.ToList()) Person.Unwed(p, s);
            ThisYear.Deaths[cause] = ThisYear.Deaths.TryGetValue(cause, out var n) ? n + 1 : 1;
            if (_debugKills && cause.StartsWith("killed by")) Console.Error.WriteLine($"KILL Y{Clock.Year} {Clock.Season} d{Clock.DayOfSeason} sim#{Id} '{Band.Name}' inBands={Region.Bands.Contains(this)} {p.Name} {cause} doing {p.Current} | {string.Join(" <- ", Environment.StackTrace.Split(new[] { Environment.NewLine }, StringSplitOptions.None).Skip(2).Take(4).Select(l => l.Trim()))}");

            string what = cause.StartsWith("killed") || cause.StartsWith("trampled") || cause.StartsWith("gored") ? $"was {cause}" : $"died of {cause}";
            WitnessDeath(p, cause);
            if (cause.StartsWith("starvation"))
            {
            }
            string who = p.BigKills >= 5 ? $"{p.Name}, one of the band's great hunters," : p.Name;
            Chronicle.Write(Clock, age < 5 ? Importance.Detail : Importance.Notable, $"{who} {what} at {(int)age}{(weakenedBy == null ? "" : ", " + weakenedBy)}.");

            foreach (var stage in p.Knows)
                if (!Band.AnyoneKnows(stage) && Web != null && Web.Stages.TryGetValue(stage, out var s) && !s.Known)
                    Chronicle.Write(Clock, Importance.Major, $"When {p.Name} died, the band lost {s.Name.ToLower()}.");

            if (Band.Members.Count == 0)
                Chronicle.Write(Clock, Importance.Major, $"With {p.Name}'s death, the {Band.Name} band is gone.");
        }

        /// <summary>
        /// Everyone hears how someone died. A death by an animal teaches the whole band to fear it,
        /// hardest for those who loved the dead.
        /// </summary>
        void WitnessDeath(Person dead, string cause)
        {
            var sp = Species.All.FirstOrDefault(s => cause.EndsWith(" " + s.Name));
            if (sp == null) return;
            foreach (var p in Band.Members)
                p.Expect.LearnDanger(p, sp.Name, 1, HuntParty.InstinctiveDanger(sp), 0.3 + p.GetAffinity(dead));
        }

        // ---- Families ------------------------------------------------------------------------

        /// <summary>
        /// Whether a couple conceives today. Ovulation follows the mother's energy balance: well-fed
        /// women conceive readily, thin ones rarely (this is how lean times lower births among real
        /// foragers). Nursing suppresses it, strongly at first and fading as the child weans over its
        /// first four years, which spaces births about four years apart.
        /// </summary>
        void MaybeConceive(Person p)
        {
            if (p.Sex != Sex.Female || p.DueDay >= 0 || !p.Married) return;
            double age = p.AgeYears(Clock);
            if (age < 16 || age > 49 || p.Sterile) return;
            double byAge = FecundityByAge(age);
            double condition = Physiology.Fecundity(p, age);
            var youngest = p.Children.Where(c => c.Alive).Select(c => c.AgeYears(Clock)).DefaultIfEmpty(99).Min();
            // On-demand nursing for years keeps ovulation suppressed, longer in a thin mother: her body
            // won't start another child while it's still struggling to feed this one (lactational
            // amenorrhea lasts about three years in well-fed foragers, nearer five in hungry ones).
            double weaning = 3 + 2 * (1 - Math.Min(1, Physiology.BodyFat(p, age) / 0.8));
            double nursing = youngest < weaning ? Math.Pow(youngest / weaning, 2) : 1;
            if (Rng.Chance(0.12 / 30 * byAge * condition * nursing * p.Health)) { p.DueDay = Clock.Day + 270; p.ExpectingWith = FatherOfNext(p); }
        }

        /// <summary>A woman with more than one husband conceives by one of them: more likely the one she's closest to.</summary>
        Person FatherOfNext(Person woman)
        {
            if (woman.Spouses.Count == 1) return woman.Spouses[0];
            double total = woman.Spouses.Sum(s => 0.1 + Math.Max(0, woman.GetAffinity(s)));
            double r = Rng.Range(0, total);
            foreach (var s in woman.Spouses) { r -= 0.1 + Math.Max(0, woman.GetAffinity(s)); if (r <= 0) return s; }
            return woman.Spouses[^1];
        }

        /// <summary>
        /// How fertile a woman is at an age, relative to her early twenties: low in the late teens
        /// (forager women first give birth around 19), then the natural-fertility schedule of
        /// populations without contraception (Coale and Trussell): 0.94 at 25-29, 0.86 at 30-34, 0.70
        /// at 35-39, 0.36 at 40-44, 0.05 at 45-49.
        /// </summary>
        static double FecundityByAge(double age)
        {
            if (age < 22) return 0.2 + 0.8 * (age - 16) / 6;
            double[] mid = { 22.5, 27.5, 32.5, 37.5, 42.5, 47.5, 50 };
            double[] n = { 1.0, 0.937, 0.861, 0.698, 0.363, 0.052, 0 };
            for (int i = 0; i < mid.Length - 1; i++)
                if (age < mid[i + 1]) return n[i] + (n[i + 1] - n[i]) * Math.Max(0, age - mid[i]) / (mid[i + 1] - mid[i]);
            return 0;
        }

        void GiveBirth(Person mother)
        {
            mother.DueDay = -1;
            var father = mother.ExpectingWith ?? mother.Partner!;
            mother.ExpectingWith = null;
            bool blessed = mother.BlessedNextBirth && !Off("god") && !Off("bless");
            mother.BlessedNextBirth = false;
            var child = NewChild(mother, father, 0);
            if (blessed)
            {
                child.Blessed = true;
                child.Health = 1.0;
                child.Body[(int)BodyTrait.Strength] = Math.Min(1.0, child.Body[(int)BodyTrait.Strength] + 0.3);
                child.Body[(int)BodyTrait.Endurance] = Math.Min(1.0, child.Body[(int)BodyTrait.Endurance] + 0.3);
                child.Body[(int)BodyTrait.Immunity] = Math.Min(1.0, child.Body[(int)BodyTrait.Immunity] + 0.3);
                child.Mind[(int)MindTrait.Boldness] = Math.Min(1.0, child.Mind[(int)MindTrait.Boldness] + 0.3);
                child.Mind[(int)MindTrait.Resilience] = Math.Min(1.0, child.Mind[(int)MindTrait.Resilience] + 0.3);
                child.Mind[(int)MindTrait.Aptitude] = Math.Min(1.0, child.Mind[(int)MindTrait.Aptitude] + 0.3);
            }
            else
            {
                child.Health = 0.6 + 0.4 * mother.Satiety;
            }
            ThisYear.Births++;
            Chronicle.Write(Clock, blessed ? Importance.Major : Importance.Detail,
                blessed ? $"{child.Name} was born blessed by the god to {mother.Name} and {father.Name}." : $"{child.Name} was born to {mother.Name} and {father.Name}.");
            AfterBirth(mother, father);

            // Births go wrong more often for older, weaker or hungrier mothers.
            double age = mother.AgeYears(Clock);
            double complication = blessed ? 0.0 : 0.01 + Math.Max(0, age - 35) * 0.004 + (1 - mother.Health) * 0.05 + (1 - mother.Satiety) * 0.03;
            // An infection after the birth can leave a woman unable to conceive again: in populations
            // without medicine about one woman in ten is sterile by her mid-thirties.
            if (!blessed && Rng.Chance(0.025 + complication)) mother.Sterile = true;
            if (!blessed && Rng.Chance(complication))
            {
                // Bleeding and infection after birth: about half of serious complications kill, which
                // comes to roughly one death per hundred births, as among real foragers.
                mother.Health -= Rng.Range(0.5, 1.5);
                if (mother.Health <= 0) Kill(mother, "childbirth");
                else Chronicle.Write(Clock, Importance.Notable, $"{mother.Name} barely survived giving birth to {child.Name}.");
            }
        }

        /// <summary>Unpaired adults of opposite sex who aren't close kin.</summary>
        public bool CouldPair(Person a, Person b) =>
            a.Sex != b.Sex && !a.Married && !b.Married && a.Alive && b.Alive && !a.IsKin(b)
            && Grown(a) && Grown(b) && a.AgeYears(Clock) <= (a.Sex == Sex.Female ? 45 : 60) && b.AgeYears(Clock) <= (b.Sex == Sex.Female ? 45 : 60);

        /// <summary>
        /// Two people pair. If they come from different bands, each weighs what moving would cost them
        /// (the people they'd leave, how each band is faring, and what both bands' customs say about
        /// who moves), and whoever would lose less moves.
        /// </summary>
        public void Pair(Person a, Person b)
        {
            Person.Wed(a, b, Clock.Day);
            a.AddAffinity(b, 0.3); b.AddAffinity(a, 0.3);
            var bandA = Region.BandOf(a) ?? this;
            var bandB = Region.BandOf(b) ?? this;
            if (bandA == bandB)
            {
                bandA.NewHousehold(a, b);
                bandA.Chronicle.Write(Clock, Importance.Detail, $"{a.Name} and {b.Name} paired.");
                return;
            }
            double norm = (bandA.Band.ResidenceNorm + bandB.Band.ResidenceNorm) / 2;
            double Cost(Person p, Simulation own, Simulation other) =>
                Ties(p, own.Band.Members) - Ties(p, other.Band.Members) * 0.5 - (other.Prospects - own.Prospects) * 4
                - norm * (p.Sex == Sex.Female ? 1 : -1);
            bool aMoves = Cost(a, bandA, bandB) <= Cost(b, bandB, bandA);
            var mover = aMoves ? a : b;
            var partner = aMoves ? b : a;
            var from = aMoves ? bandA : bandB;
            var to = aMoves ? bandB : bandA;
            // A custom about who moves forms from what people actually do, on both sides.
            foreach (var band in new[] { from, to })
                band.Band.ResidenceNorm += ((mover.Sex == Sex.Female ? 1 : -1) - band.Band.ResidenceNorm) * 0.1;
            // Young children with no other parent there go with them.
            var kids = mover.Children.Where(c => c.Alive && c.AgeYears(Clock) < 14 && from.Band.Members.Contains(c)
                                                 && !(c.Mother != mover && c.Mother != null && from.Band.Members.Contains(c.Mother))
                                                 && !(c.Father != mover && c.Father != null && from.Band.Members.Contains(c.Father))).ToList();
            Region.Move(new List<Person> { mover }.Concat(kids).ToList(), from, to, partner);
            from.Chronicle.Write(Clock, Importance.Notable, $"{mover.Name} paired with {partner.Name} and left to live with the {to.Band.Name} band.");
            to.Chronicle.Write(Clock, Importance.Notable, $"{mover.Name} of the {from.Band.Name} band paired with {partner.Name} and joined the band.");
        }

        /// <summary>
        /// Every band starts with its own shade of values, shared by its people with personal variation.
        /// After that, norms move only through what happens to people and what they tell each other.
        /// </summary>
        void SeedCulture(List<Person> group)
        {
            // Each band's own mix of what people care about, held with personal variation.
            var mix = new double[TraitCounts.Drives];
            for (int i = 0; i < mix.Length; i++) mix[i] = Inheritance.Clamp01(Rng.Normal(0.5, 0.08));
            foreach (var p in group)
                for (int i = 0; i < mix.Length; i++) p.Drives[i] = p.BaseDrives[i] = Inheritance.Clamp01(mix[i] + Rng.Normal(0, 0.08));
        }


        /// <summary>People who grew up in the same band know each other: some fondness for everyone, more for kin.</summary>
        void KnitTogether(List<Person> group)
        {
            foreach (var a in group)
                foreach (var b in group)
                    if (a != b && a.GetAffinity(b) < 0.1) a.AddAffinity(b, a.IsKin(b) ? 0.5 : Rng.Range(0.05, 0.35));
        }

        /// <summary>
        /// What someone would lose by leaving a group: their closest bonds there (a person's circle
        /// is a handful of people, not the whole band), with their own children counting most,
        /// then other close kin, then friends.
        /// </summary>
        internal static double Ties(Person p, IEnumerable<Person> group)
        {
            var bonds = new List<double>();
            foreach (var o in group)
            {
                if (o == p) continue;
                double weight = p.Children.Contains(o) && o.Alive ? 3 : p.IsKin(o) ? 2 : 1;
                // A bond with someone you distrust holds you far less.
                double b = p.GetAffinity(o) * weight * (0.5 + p.GetTrust(o));
                if (b > 0) bonds.Add(b);
            }
            bonds.Sort();
            double t = 0;
            for (int i = bonds.Count - 1, k = 0; i >= 0 && k < 8; i--, k++) t += bonds[i];
            return t;
        }

        /// <summary>
        /// How much of the camp is near-strangers to this person. A person's circle is a few dozen
        /// people at most; past that, a big camp brings friction.
        /// </summary>
        double Crowding(Person p)
        {
            int n = Band.Members.Count;
            if (n <= 1) return 0;
            int bonded = 0;
            foreach (var o in Band.Members) if (o != p && p.GetAffinity(o) > 0.1) bonded++;
            return Math.Max(0, 1 - bonded / (double)(n - 1)) * Math.Min(1, n / 60.0);
        }

        /// <summary>
        /// How worn down someone is by the work of feeding their family: real foragers spend four to six
        /// hours a day getting food and fuel (more with carrying and processing); past about seven, as the
        /// land around a big camp is stripped and every trip gets longer, the days grind (full strain at
        /// eleven). Better ways of getting food shorten the day, and so let more people live together.
        /// </summary>
        internal static double Overwork(Person p) => Math.Max(0, Math.Min(1, (p.Workload - 7) / 4));

        /// <summary>How well-off the band looks: people notice hunger.</summary>
        internal double Prospects => Band.Members.Count == 0 ? 0 : Band.Members.Average(m => m.Satiety);

        // ---- Visiting ------------------------------------------------------------------------

        /// <summary>
        /// How much each adult wants to go and visit another band, and which one: missing kin and
        /// friends who live there, and, for the unpaired with no one suitable at home, the hope of
        /// finding a partner. Longing grows with the time since they last went. People know roughly
        /// where the bands within a few days' walk are camped.
        /// </summary>
        void UpdateVisitPulls()
        {
            VisitPulls.Clear();
            VisitForFood.Clear();
            var others = Region.Bands.Where(b => b != this && b.Band.Members.Count > 0 && !Region.Together(this, b)).ToList();
            if (others.Count == 0) return;
            foreach (var p in Band.Members)
            {
                if (p.Map.Camps.Count == 0) continue;
                double age = p.AgeYears(Clock);
                if (age < 16 || p.DueDay >= 0 || p.Sickness > 0.2 || p.Current is Visit) continue;
                // Worry about food: the hearth running short with the land not keeping up. Someone can go
                // asking while another adult minds the little ones.
                var home = HomeOf(p);
                double worry = home.Income >= home.Members.Count * 0.9 || Clock.Day - p.LastVisitDay < 7 ? 0
                    : Inheritance.Clamp01(1 - home.DaysOfFood / Math.Max(6, Math.Min(30, Activities.Decider.LeanDays(this, p))));
                bool tied = p.Children.Any(c => c.Alive && c.AgeYears(Clock) < 4) && !home.Adults(Clock).Any(a => a != p && !(a.Current is Visit));
                if (worry > 0.3 && !tied)
                {
                    Simulation? where = null; double most = 0;
                    foreach (var b in others)
                    {
                        if (!p.Map.Camps.TryGetValue(b.Id, out var at) || Clock.Day - at.day > SimClock.DaysPerYear) continue;
                        double km = WorldMap.Distance(at.x, at.y, Band.CampX, Band.CampY) * WorldMap.CellMeters / 1000;
                        if (km > 25) continue;
                        // Whom they'd be asking: their own people's camps by how they feel about them; another
                        // people's as outsiders. What asking such people has brought is learned, and differs by culture.
                        var rel = b.Tribe == Tribe ? CampRelation(p, b) : Relation.Outsider;
                        double hope = Ties(p, b.Band.Members) * 0.15 + Felt(Expects(p, Act.Ask, rel)) * 0.4 - p.GetBandGrievance(b.Id) * 0.1;
                        double pull = worry * (0.3 + hope) - km * 0.02;
                        if (pull > most) { most = pull; where = b; }
                    }
                    if (where != null) { VisitPulls[p] = (where, most); VisitForFood.Add(p); continue; }
                }
                if (p.Children.Any(c => c.Alive && c.AgeYears(Clock) < 4)) continue;
                double longing = Math.Min(1, (Clock.Day - p.LastVisitDay) / 90.0);
                if (longing <= 0) continue;
                bool seeking = p.Partner == null && age <= (p.Sex == Sex.Female ? 40 : 50) && !Band.Members.Any(o => CouldPair(p, o));
                Simulation? best = null;
                double bestPull = 0;
                foreach (var b in others)
                {
                    // Only bands they know where to find (as far as they know).
                    if (!p.Map.Camps.TryGetValue(b.Id, out var where) || Clock.Day - where.day > SimClock.DaysPerYear
                        || WorldMap.Distance(where.x, where.y, Band.CampX, Band.CampY) > Region.KnownRange * (p.Knows.Contains("wayfind.stars") ? 1.5 : 1)) continue;
                    double missing = Ties(p, b.Band.Members) * 0.15;
                    double courting = seeking && b.Band.Members.Any(o => CouldPair(p, o)) ? 0.4 + 0.4 * p.M(MindTrait.Boldness) : 0;
                    double walk = TravelHours(WorldMap.Distance(b.Band.CampX, b.Band.CampY, Band.CampX, Band.CampY), p) * 0.03;
                    double pull = (missing + courting) * longing * (0.5 + p.M(MindTrait.Sociability) * 0.5) - walk;
                    if (pull > bestPull) { bestPull = pull; best = b; }
                }
                if (best != null) VisitPulls[p] = (best, bestPull);
            }
        }

        /// <summary>Bookkeeping check for tests: households list exactly the band's members whose home they are.</summary>
        internal void CheckHouseholds()
        {
            var inHousehold = new HashSet<Person>();
            foreach (var h in Band.Households)
                foreach (var m in h.Members)
                {
                    if (!inHousehold.Add(m)) throw new InvalidOperationException($"{m.Name} is in two households of the {Band.Name} band");
                    if (!Band.Members.Contains(m) || m.Home != h)
                        throw new InvalidOperationException($"{m.Name}#{m.Id} is listed in a {Band.Name} household but inBand={Band.Members.Contains(m)} homeMatches={m.Home == h}");
                }
            foreach (var m in Band.Members)
                if (m.Home != null && !m.Home.Members.Contains(m)) throw new InvalidOperationException($"{m.Name}'s home doesn't list them");
        }

        /// <summary>
        /// Once people know how, they tend the wild grain around a camp they stay at: clearing it,
        /// later scattering seed, later keeping the best seed. Stands within a couple of km grow
        /// richer each week the band stays a month or more; abandoned ones drift back to wild.
        /// </summary>
        void TendGrain()
        {
            if (!Band.AnyoneKnows("grain.clear") || Clock.Day - _campSince < 30) return;
            double ceiling = Band.AnyoneKnows("grain.select") ? 2.5 : Band.AnyoneKnows("grain.scatter") ? 2 : 1.5;
            foreach (var (c, d) in _nearCamp)
            {
                if (c.Plant != PlantFood.WildGrain || d > WorldMap.FromMeters(2000) || c.WildCapacity <= 0) continue;
                c.PlantCapacity = Math.Min(c.WildCapacity * ceiling, c.PlantCapacity + c.WildCapacity * 0.02);
            }
        }

        // ---- Moving camp -----------------------------------------------------------------------

        /// <summary>
        /// The pull of staying: attachment grows with time at a camp, weariness from the last move
        /// fades over weeks, and carrying small children and the old makes every move harder.
        /// </summary>
        double StayPull
        {
            get
            {
                double days = Clock.Day - _campSince;
                double attachment = Math.Min(1, days / 180.0) * 0.25;
                // Returning to known seasonal camps, staying long, staying all year: each makes moving cost more.
                if (Band.AnyoneKnows("camp.return")) attachment *= 1.3;
                if (Band.AnyoneKnows("camp.long")) attachment *= 1.5;
                if (Band.AnyoneKnows("camp.year")) attachment *= 2;
                double weariness = Math.Exp(-days / 30.0) * 0.6;
                int n = Math.Max(1, Band.Members.Count);
                double burden = Band.Members.Count(m => { var a = m.AgeYears(Clock); return a < 5 || a > 60; }) / (double)n * 0.5;
                return attachment + weariness + burden + (Off("investment") ? 0 : Investment);
            }
        }

        /// <summary>
        /// What a camp has put into its place, and would lose by leaving: stored food beyond what its
        /// people can carry, houses that took weeks to build, wild grain they've been tending. This is
        /// how storage and building tie people down: nobody decides to settle; leaving just costs more.
        /// </summary>
        double Investment
        {
            get
            {
                int n = Math.Max(1, Band.Members.Count);
                double carry = Band.Members.Sum(m => Physiology.CarryCapacity(m, m.AgeYears(Clock)));
                double heavy = Math.Max(0, Band.FoodTotal - carry) / (n * 0.9);          // days of food that would be left behind
                double days = Clock.Day - _campSince;
                double houses = days < 30 ? 0 : Band.AnyoneKnows("shelter.pit") ? 0.3 : Band.AnyoneKnows("shelter.hut") ? 0.2 : 0;
                double fields = _nearCamp.Where(cd => cd.cell.Plant == PlantFood.WildGrain && cd.cell.WildCapacity > 0)
                                         .Sum(cd => Math.Max(0, cd.cell.PlantCapacity / cd.cell.WildCapacity - 1)) * 0.05;
                // Weirs built on the rivers nearby: work that stays where it was done.
                double weirs = Math.Min(0.3, _nearCamp.Where(cd => cd.cell.Weir >= 0.5).Sum(cd => cd.cell.Weir) * 0.15);
                // Hands stencilled on the rock: a place claimed and remembered as theirs.
                double marked = days >= 60 && Band.AnyoneKnows("ochre.hand") ? 0.1 : 0;
                return Math.Min(0.6, heavy / 60) + houses + Math.Min(0.4, fields) + marked + weirs;
            }
        }

        /// <summary>How long the camp has stood where it is, in days.</summary>
        public double DaysHere => Clock.Day - _campSince;

        /// <summary>The last day this camp was raided or lost someone to another people's violence.</summary>
        internal long LastThreatDay = -10000;
        /// <summary>How present the danger of attack feels: fresh after a raid, fading over a couple of years.</summary>
        internal double Threat => Off("threat") ? 0 : Math.Exp(-(Clock.Day - LastThreatDay) / 365.0);

        /// <summary>
        /// How good someone thinks foraging is around the current camp this season: the best few
        /// nearby places they remember from this time of year.
        /// </summary>
        double LocalValue(Person p)
        {
            double a = 0, b = 0, c = 0;
            foreach (var kv in p.Places[(int)Clock.Season])
            {
                var (x, y) = Person.FromPlaceKey(kv.Key);
                if (WorldMap.Distance(x, y, Band.CampX, Band.CampY) > WorldMap.FromMeters(4000)) continue;
                double v = kv.Value;
                if (v > a) { c = b; b = a; a = v; } else if (v > b) { c = b; b = v; } else if (v > c) c = v;
            }
            int n = (a > 0 ? 1 : 0) + (b > 0 ? 1 : 0) + (c > 0 ? 1 : 0);
            double here = n > 0 ? (a + b + c) / n : p.Expect.Get(Verb.Forage, Forage.FallbackPerHour);
            // A fouled camp makes staying feel worse.
            // A camp near a band they're angry at feels unsafe.
            double unsafeNear = 0;
            foreach (var other in Region.Bands)
            {
                if (other == this || other.Extinct) continue;
                double g = p.GetBandGrievance(other.Id);
                if (g > 0 && WorldMap.Distance(other.Band.CampX, other.Band.CampY, Band.CampX, Band.CampY) < WorldMap.FromMeters(8000))
                    unsafeNear += g / (g + 3);
            }
            return here - Band.Filth * 0.02 - unsafeNear * 0.2;
        }

        /// <summary>
        /// Someone who remembers a better place than what they've been finding lately may propose
        /// moving there. Each adult weighs it: their own memory of the place if they have one,
        /// otherwise how much they trust whoever proposed it. Influence decides.
        /// </summary>
        public void ConsiderMoving(Person proposer)
        {
            if (proposer.LastProposalDay == Clock.Day || !Band.Members.Contains(proposer) || proposer.AgeYears(Clock) < 18) return;
            proposer.LastProposalDay = Clock.Day;
            proposer.Restless = false;
            var memory = proposer.Places[(int)Clock.Season];
            if (memory.Count == 0) { ConsiderSplitting(proposer); return; }
            double local = LocalValue(proposer);
            int bestKey = -1; double bestValue = 0;
            foreach (var kv in memory)
            {
                var (x, y) = Person.FromPlaceKey(kv.Key);
                double d = WorldMap.Distance(x, y, Band.CampX, Band.CampY);
                if (d < WorldMap.FromMeters(1500)) continue;
                double v = kv.Value - d * WorldMap.CellMeters / 1000 * 0.015;
                if (v > bestValue) { bestValue = v; bestKey = kv.Key; }
            }
            // The gain has to outweigh the effort of moving.
            if (bestKey < 0) { ConsiderSplitting(proposer); return; }
            var (bx, by) = Person.FromPlaceKey(bestKey);
            double hoursAway = TravelHours(WorldMap.Distance(bx, by, Band.CampX, Band.CampY), proposer);
            if (bestValue - local <= 0.1 + 0.3 * StayPull + LostIfMoving(HomeOf(proposer), hoursAway) * 0.02) { ConsiderSplitting(proposer); return; }

            double yes = 0, no = 0;
            // Whether the old were more for it than the young, for judging later whether heeding them paid.
            double ageYes = 0, ageAll = 0; int nYes = 0, nAll = 0;
            foreach (var a in Band.Members.Where(Grown))
            {
                double mine = LocalValue(a);
                double trust = a == proposer ? 1 : Inheritance.Clamp01(a.GetAffinity(proposer) * (0.5 + proposer.M(MindTrait.Charisma)) + Following(a, proposer));
                double theirs = a.Places[(int)Clock.Season].TryGetValue(bestKey, out var known) ? known : mine + (bestValue - mine) * trust;
                // How much each voice carries: charisma, age, and how much the band has come to follow them.
                double weight = 0.5 + a.M(MindTrait.Charisma) + a.AgeYears(Clock) / 60 * Elders(a)
                                + Band.Members.Where(o => o != a && Grown(o)).Select(o => Following(o, a)).DefaultIfEmpty(0).Average() * 0.5;
                // What their own family would have to leave behind weighs on them most.
                bool votesYes = theirs - mine > 0.05 + 0.3 * StayPull + LostIfMoving(HomeOf(a), hoursAway) * 0.02;
                if (votesYes) { yes += weight; ageYes += a.AgeYears(Clock); nYes++; } else no += weight;
                ageAll += a.AgeYears(Clock); nAll++;
            }
            if (yes <= no) { ConsiderSplitting(proposer); return; }

            var (tx, ty) = Person.FromPlaceKey(bestKey);
            double ddx = tx - Band.CampX, ddy = ty - Band.CampY;
            string dir = Math.Abs(ddx) > Math.Abs(ddy) ? (ddx > 0 ? "east" : "west") : (ddy > 0 ? "south" : "north");
            foreach (var m in Band.Members.Where(m => Grown(m) && m != proposer))
            {
                m.MovedBy = proposer; m.MovedDay = Clock.Day; m.YieldBeforeMove = LocalValue(m);
                m.EldersTiltOnMove = nYes == 0 || nAll == 0 ? 0 : (ageYes / nYes - ageAll / nAll) / 20;
            }
            if (!Off("leavebehind")) LeaveBehindWhatCantBeCarried(hoursAway);
            Band.CampX = tx; Band.CampY = ty;
            _campSince = Clock.Day;
            Band.Filth = 0;
            RefreshForagePatches();
            Chronicle.Write(Clock, Importance.Notable, $"On {proposer.Name}'s urging, the band moved camp {dir}, to the {World[tx, ty].Terrain.ToString().ToLower()}.");
        }

        /// <summary>
        /// On a move each family takes what its people can carry; the rest is left (people who know how to
        /// cache food come back for about half of it).
        /// </summary>
        /// <summary>Days of food a household would have to abandon on a move this many hours' walk away.</summary>
        double LostIfMoving(Household h, double hours)
        {
            if (Off("lostweigh")) return 0;
            double extra = h.Food.Total - CarryOnMove(h, hours);
            if (extra <= 0) return 0;
            double keep = h.Knows("store.cache", Clock) ? 0.5 : 0;
            return extra * (1 - keep) / Math.Max(0.9, h.Members.Count * 0.9);
        }

        /// <summary>What a household can move: each trip its people carry what they can, and a short move allows several trips (up to four).</summary>
        double CarryOnMove(Household h, double hours) =>
            h.Members.Sum(m => Physiology.CarryCapacity(m, m.AgeYears(Clock))) * Math.Max(1, Math.Min(4, 10 / Math.Max(1, hours)));

        void LeaveBehindWhatCantBeCarried(double hours)
        {
            foreach (var h in Band.Households)
            {
                double extra = h.Food.Total - CarryOnMove(h, hours);
                if (extra <= 0) continue;
                double keep = h.Knows("store.cache", Clock) ? 0.5 : 0;
                double lose = extra * (1 - keep);
                double frac = lose / h.Food.Total;
                h.Food.Meat *= 1 - frac; h.Food.Plants *= 1 - frac; h.Food.Stored *= 1 - frac; h.Food.Cooked *= 1 - frac; h.Food.Dried *= 1 - frac;
                ThisYear.Abandoned += lose;
            }
        }

        /// <summary>Grown, as this band reckons it.</summary>
        public bool Grown(Person p) => p.AgeYears(Clock) >= Band.ComingOfAge;

        bool Between(Person p, double min, double max) { var age = p.AgeYears(Clock); return age >= min && age <= max; }

        /// <summary>How attractive the band's culture finds this person.</summary>
        public double Attraction(Person p)
        {
            double s = 0;
            for (int i = 0; i < p.Mind.Length; i++) s += Band.AttractionMind[i] * (p.Mind[i] - 0.5);
            for (int i = 0; i < p.Body.Length; i++) s += Band.AttractionBody[i] * (p.Body[i] - 0.5);
            return s;
        }

        // ---- Hunting results ------------------------------------------------------------------

        public void RecordKill(Species sp, Person killer, int partySize)
        {
            ThisYear.Kills[sp.Name] = ThisYear.Kills.TryGetValue(sp.Name, out var n) ? n + 1 : 1;

            // A band admires its successful hunters a little more each time: culture drifting.
            Band.AttractionMind[(int)MindTrait.Boldness] = Math.Min(1, Band.AttractionMind[(int)MindTrait.Boldness] + 0.002);
            Band.AttractionBody[(int)BodyTrait.Strength] = Math.Min(1, Band.AttractionBody[(int)BodyTrait.Strength] + 0.002);

            // The band hears about a big kill: a little of the daring rubs off on everyone.
            if (sp == Species.Mammoth)
                Chronicle.Write(Clock, Importance.Major, $"{killer.Name}'s party of {partySize} brought down a mammoth.");
            else if (_killedBefore.Add(sp) && sp != Species.Rabbit)
                Chronicle.Write(Clock, Importance.Notable, $"{killer.Name} made the band's first {sp.Name} kill in this valley.");
        }

        // ---- Threats and movement --------------------------------------------------------------

        void WolvesNearCamp()
        {
            if (Band.Members.Count == 0) return;
            // Torches by night and dogs about camp keep wolves off.
            double strength = Band.Strength(Clock) * (Band.AnyoneKnows("fire.torch") ? 1.3 : 1) * DogGuard;
            foreach (var pack in Ecology.Living(Species.Wolf).ToList())
            {
                if (WorldMap.Distance(pack.X, pack.Y, Band.CampX, Band.CampY) > WorldMap.FromMeters(3000) || pack.Hunger < 0.75) continue;
                double power = pack.Count * pack.Hunger * 0.8;
                // Wolves fear people. Only a starving pack risks it, and only against someone out on
                // their own near it, or a small child at a hearth whose fire has gone out.
                var exposed = Band.Members.Where(p =>
                        (p.Current is Forage || p.Current is GatherFlint || p.Current is GatherWood || p.Current is Fish)
                            && WorldMap.Distance(p.X, p.Y, pack.X, pack.Y) < WorldMap.FromMeters(1500)
                        || p.AgeYears(Clock) < 8 && p.Home != null && !p.Home.FireLit).ToList();
                // The pack's own reckoning: the hungrier it is and the weaker the people look, the more
                // likely it is to risk them (a week without food makes a pack bold; it never goes for a
                // camp it can't match).
                double boldness = Math.Max(0, Math.Min(1, (pack.Hunger - 0.9) * 2)) * Math.Max(0, power / (power + strength) - 0.25) * 2;
                if (exposed.Count > 0 && Rng.Chance(boldness))
                {
                    var victim = Rng.Pick(exposed);
                    victim.Injury = Inheritance.Clamp01(victim.Injury + Rng.Range(0.3, 1.0));
                    pack.Hunger = 0;
                    if (victim.Injury >= 0.9) Kill(victim, "killed by wolves");
                    else Chronicle.Write(Clock, Importance.Major, $"Starving wolves attacked {victim.Name}.");
                }
                else if (!_wolvesSeenThisSeason && Rng.Chance(0.1))
                {
                    _wolvesSeenThisSeason = true;
                    Chronicle.Write(Clock, Importance.Detail, "Wolves circled the camp at night, picking at scraps.");
                }
            }
        }

        /// <summary>Stretches of river and marsh near camp with the most fish for the walk, best first (refreshed daily).</summary>
        public readonly List<Cell> FishSpots = new List<Cell>();

        /// <summary>Places with dead wood lying about near camp, nearest and richest first (refreshed daily).</summary>
        public readonly List<Cell> WoodSources = new List<Cell>();

        // The cells within a day's foraging of camp and their distances, worked out once per camp.
        readonly List<(Cell cell, double dist)> _nearCamp = new List<(Cell, double)>();
        double _nearX = double.NaN, _nearY = double.NaN;
        double[] _keys = new double[0];
        Cell[] _cells = new Cell[0];

        internal void RefreshForagePatches()
        {
            if (_nearX != Band.CampX || _nearY != Band.CampY)
            {
                _nearX = Band.CampX; _nearY = Band.CampY;
                _nearCamp.Clear();
                foreach (var c in World.Around(Band.CampX, Band.CampY, WorldMap.FromMeters(4000)))
                    _nearCamp.Add((c, WorldMap.Distance(c.X, c.Y, Band.CampX, Band.CampY)));
                _keys = new double[_nearCamp.Count];
                _cells = new Cell[_nearCamp.Count];
            }
            double woodReach = WorldMap.FromMeters(3000);
            int n = 0;
            foreach (var (c, d) in _nearCamp)
                if (d <= woodReach && c.Wood > 1) { _keys[n] = d - c.Wood * 0.05; _cells[n++] = c; }
            TakeBest(WoodSources, n);
            n = 0;
            foreach (var (c, d) in _nearCamp)
                if (c.PlantStock > 0.3) { _keys[n] = -c.PlantStock / (1 + d * WorldMap.CellMeters / 1000 * 0.3); _cells[n++] = c; }
            TakeBest(ForagePatches, n);
            n = 0;
            foreach (var (c, d) in _nearCamp)
                if (c.FishStock > 1) { _keys[n] = -c.FishStock / (1 + d * WorldMap.CellMeters / 1000 * 0.3); _cells[n++] = c; }
            TakeBest(FishSpots, n);
            if (Clock.Day % 7 == 0 || _here.Count == 0) RefreshMaterials();
        }

        /// <summary>The 25 candidates with the lowest keys, in order.</summary>
        void TakeBest(List<Cell> into, int n)
        {
            Array.Sort(_keys, _cells, 0, n);
            into.Clear();
            for (int i = 0; i < Math.Min(25, n); i++) into.Add(_cells[i]);
        }

        void ReportHunger()
        {
            if (_hungerReportedThisSeason || Band.Members.Count == 0) return;
            if (Band.Members.Average(p => p.Satiety) < 0.4)
            {
                _hungerReportedThisSeason = true;
                Chronicle.Write(Clock, Importance.Major, "Hunger gripped the band.");
            }
        }

        internal YearStats EndYear()
        {
            ThisYear.Population = Band.Members.Count;
            ThisYear.Couples = Band.Members.Count(m => m.Sex == Sex.Female && m.Partner != null);
            var adults = Band.Members.Where(m => m.AgeYears(Clock) >= 16).ToList();
            if (adults.Count > 0)
            {
                // What the band has come to expect: standing (the old "valor") and what giving brings.
                ThisYear.Valor = adults.Average(m => m.Drives[(int)Drive.Standing]);
                ThisYear.Sharing = adults.Average(m => Expects(m, Act.Give, Relation.Neutral));
            }
            if (ThisYear.Samples > 0) { ThisYear.AvgSatiety /= ThisYear.Samples; ThisYear.AvgHealth /= ThisYear.Samples; }
            if (Environment.GetEnvironmentVariable("ANTHRO_DEBUG_HOMES") != null)
                foreach (var h in Band.Households.Where(h => !h.Adults(Clock).Any()))
                    Console.Error.WriteLine($"NOADULT Y{Clock.Year} household {h.Id}: " + string.Join(", ", h.Members.Select(m => $"{m.Name} {m.AgeYears(Clock):F1} mother {(m.Mother == null ? "-" : m.Mother.Alive ? (Band.Members.Contains(m.Mother) ? "here" : "away") : "dead")} father {(m.Father == null ? "-" : m.Father.Alive ? (Band.Members.Contains(m.Father) ? "here" : "away") : "dead")}")));
            if (Environment.GetEnvironmentVariable("ANTHRO_DEBUG_MEMBERS") != null)
                foreach (var m in Band.Members)
                    Console.Error.WriteLine($"MEMBER Y{Clock.Year} {m.Name} age {m.AgeYears(Clock):F1} health {m.Health:F2} reserve {m.Reserve:F2} sat {m.Satiety:F2} stomach {m.Stomach:F2} home {(m.Home == null ? "-" : m.Home.Members.Count.ToString())} food {m.Home?.Food.Total ?? 0:F1} doing {m.Current} discontent {m.Discontent:F2}");
            var year = ThisYear;
            YearEnded?.Invoke(year);
            ThisYear = new YearStats { Year = Clock.Year };
            return year;
        }
    }
}
