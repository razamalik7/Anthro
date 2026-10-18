using System;
using System.Collections.Generic;
using System.Linq;
using Anthro.Sim.Activities;
using Anthro.Sim.Core;
using Anthro.Sim.Discovery;
using Anthro.Sim.People;
using Anthro.Sim.World;

namespace Anthro.Sim.Api
{
    public enum GodCommandKind : byte
    {
        Omen = 1,
        Bounty = 2,
        Weather = 3,
        BlessedBirth = 4,
        ForkNudge = 5,
        Heal = 6,
        Lightning = 7
    }

    public sealed class GodCommandRecord
    {
        public long Tick { get; set; }
        public GodCommandKind Kind { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int TargetPersonId { get; set; } = -1;
        public bool BoolArg { get; set; }
        public int IntArg { get; set; }
        public double DoubleArg { get; set; }
        public string TextArg { get; set; } = "";
    }

    /// <summary>
    /// Phase 2: Canonical Milestone 1 scenario factory.
    /// Starts with 1 founding band (~30 people) + 2 kin camps on the 20km x 20km valley map,
    /// player-controlled god (Policy = GodPolicy.None), and 50 starting favor.
    /// </summary>
    public static class MilestoneScenario
    {
        public static GameSession StartNew(ulong seed = 42, DiscoveryWeb? web = null, int bandSize = 30, int kinCamps = 2, int otherBands = 0)
        {
            return new GameSession(seed, web, bandSize, kinCamps, otherBands, GodPolicy.None);
        }
    }

    /// <summary>
    /// Phase 3: Main Simulation <-> Unity session controller.
    /// Wraps <see cref="Region"/> with deterministic command recording, read-only view snapshots,
    /// unified notification streaming, and time-control state.
    /// </summary>
    public sealed class GameSession
    {
        public ulong Seed { get; }
        public int InitialBandSize { get; }
        public int InitialKinCamps { get; }
        public int InitialOtherBands { get; }
        public Region Region { get; }

        public bool IsPaused { get; set; }
        public int HoursPerTick { get; set; } = 1;
        public bool AutoPauseOnMajorEvent { get; set; }

        readonly List<GodCommandRecord> _commandLog = new List<GodCommandRecord>();
        readonly List<SimNotification> _notifications = new List<SimNotification>();
        readonly HashSet<Simulation> _watchedBands = new HashSet<Simulation>();
        int _lastGodEventIndex;

        public IReadOnlyList<GodCommandRecord> CommandLog => _commandLog;

        public GameSession(ulong seed, DiscoveryWeb? web = null, int bandSize = 30, int kinCamps = 2, int otherBands = 0, GodPolicy policy = GodPolicy.None)
        {
            Seed = seed;
            InitialBandSize = bandSize;
            InitialKinCamps = kinCamps;
            InitialOtherBands = otherBands;

            Region = Region.Create(seed, web, bandSize: bandSize, otherBands: otherBands, kinCamps: kinCamps);
            Region.Policy = policy;

            Region.Chronicle.Written += e => OnChronicleEntry(e, null);
            SyncBandWatchers();
            _lastGodEventIndex = Region.GodEvents.Count;
        }

        void SyncBandWatchers()
        {
            foreach (var band in Region.Bands)
            {
                if (_watchedBands.Add(band))
                {
                    var captured = band;
                    captured.Chronicle.Written += e => OnChronicleEntry(e, captured);
                }
            }
        }

        void OnChronicleEntry(ChronicleEntry entry, Simulation? band)
        {
            if (entry.Importance == Importance.Detail) return;

            var kind = entry.Importance == Importance.Major
                ? NotificationKind.ChronicleMajor
                : NotificationKind.ChronicleNotable;

            int bx = band != null ? (int)band.Band.CampX : 0;
            int by = band != null ? (int)band.Band.CampY : 0;
            int bandId = band != null ? band.Id : -1;

            _notifications.Add(new SimNotification
            {
                Tick = Region.Clock.Tick,
                Year = entry.Year,
                Season = entry.Season,
                DayOfSeason = entry.DayOfSeason,
                Kind = kind,
                Text = entry.Text,
                X = bx,
                Y = by,
                FocalBandId = bandId
            });

            if (AutoPauseOnMajorEvent && entry.Importance == Importance.Major)
            {
                IsPaused = true;
            }
        }

        void HarvestGodEvents()
        {
            while (_lastGodEventIndex < Region.GodEvents.Count)
            {
                var ev = Region.GodEvents[_lastGodEventIndex++];
                NotificationKind? kind = ev.Kind switch
                {
                    "Prayer" => NotificationKind.PrayerAsked,
                    "PrayerAnswered" or "EmberAnswered" => NotificationKind.PrayerAnswered,
                    "PrayerFailed" or "PrayerFaded" => NotificationKind.PrayerFailed,
                    "LastEmber" or "EmberIgnored" => NotificationKind.PrayerEmber,
                    "ForkPair" or "ForkLeave" or "ForkDuel" or "ForkRaid" => NotificationKind.ForkBranch,
                    "Omen" or "BountyPlant" or "BountyAnimal" or "Weather" or "BlessedBirth" or "ForkNudge" or "Heal" or "Lightning" => NotificationKind.PowerCast,
                    "GodFaded" => NotificationKind.GodFaded,
                    _ => null
                };

                if (kind.HasValue)
                {
                    _notifications.Add(new SimNotification
                    {
                        Tick = ev.Tick,
                        Year = Region.Clock.Year,
                        Season = Region.Clock.Season,
                        DayOfSeason = Region.Clock.DayOfSeason,
                        Kind = kind.Value,
                        Text = ev.Summary,
                        X = ev.X,
                        Y = ev.Y
                    });

                    if (AutoPauseOnMajorEvent && (kind.Value == NotificationKind.PrayerEmber || kind.Value == NotificationKind.ForkBranch || kind.Value == NotificationKind.GodFaded))
                    {
                        IsPaused = true;
                    }
                }
            }
        }

        /// <summary>
        /// Advance the simulation by <paramref name="hours"/> (or <see cref="HoursPerTick"/> via <see cref="Tick"/>).
        /// </summary>
        public int StepHours(int hours = 1, bool ignorePause = true)
        {
            int stepped = 0;
            for (int i = 0; i < hours; i++)
            {
                if (!ignorePause && IsPaused) break;
                Region.Step();
                SyncBandWatchers();
                HarvestGodEvents();
                stepped++;
            }
            return stepped;
        }

        /// <summary>
        /// Advance one UI tick according to <see cref="HoursPerTick"/> unless <see cref="IsPaused"/> is true.
        /// </summary>
        public int StepTick() => StepHours(HoursPerTick, ignorePause: false);

        /// <summary>
        /// Drain all pending notifications since the last drain call.
        /// </summary>
        public IReadOnlyList<SimNotification> DrainNotifications()
        {
            HarvestGodEvents();
            if (_notifications.Count == 0) return Array.Empty<SimNotification>();
            var copy = _notifications.ToArray();
            _notifications.Clear();
            return copy;
        }

        public IReadOnlyList<SimNotification> PeekNotifications()
        {
            HarvestGodEvents();
            return _notifications;
        }

        // =====================================================================
        // Player Commands (recorded deterministically by Tick + PersonId/Coords)
        // =====================================================================

        public void CastOmen(int x, int y, string description = "A sign in the sky", bool isGood = false)
        {
            RecordAndApply(new GodCommandRecord
            {
                Tick = Region.Clock.Tick,
                Kind = GodCommandKind.Omen,
                X = x,
                Y = y,
                BoolArg = isGood,
                TextArg = description ?? "A sign in the sky"
            });
        }

        public void CastBounty(int x, int y, bool animals)
        {
            RecordAndApply(new GodCommandRecord
            {
                Tick = Region.Clock.Tick,
                Kind = GodCommandKind.Bounty,
                X = x,
                Y = y,
                BoolArg = animals
            });
        }

        public void CastWeather(Season season, double delta)
        {
            RecordAndApply(new GodCommandRecord
            {
                Tick = Region.Clock.Tick,
                Kind = GodCommandKind.Weather,
                IntArg = (int)season,
                DoubleArg = delta
            });
        }

        public void CastBlessedBirth(int motherPersonId)
        {
            RecordAndApply(new GodCommandRecord
            {
                Tick = Region.Clock.Tick,
                Kind = GodCommandKind.BlessedBirth,
                TargetPersonId = motherPersonId
            });
        }

        public void CastForkNudge(int personId, string choice)
        {
            RecordAndApply(new GodCommandRecord
            {
                Tick = Region.Clock.Tick,
                Kind = GodCommandKind.ForkNudge,
                TargetPersonId = personId,
                TextArg = choice ?? ""
            });
        }

        public void CastHeal(int personId)
        {
            RecordAndApply(new GodCommandRecord
            {
                Tick = Region.Clock.Tick,
                Kind = GodCommandKind.Heal,
                TargetPersonId = personId
            });
        }

        public void CastLightning(int personId)
        {
            RecordAndApply(new GodCommandRecord
            {
                Tick = Region.Clock.Tick,
                Kind = GodCommandKind.Lightning,
                TargetPersonId = personId
            });
        }

        internal void RecordAndApply(GodCommandRecord rec)
        {
            _commandLog.Add(rec);
            ApplyCommandRecordToRegion(rec);
        }

        internal void ApplyCommandRecordToRegion(GodCommandRecord rec)
        {
            switch (rec.Kind)
            {
                case GodCommandKind.Omen:
                    Region.CastOmen(rec.X, rec.Y, rec.TextArg, rec.BoolArg);
                    break;
                case GodCommandKind.Bounty:
                    Region.CastBounty(rec.X, rec.Y, rec.BoolArg);
                    break;
                case GodCommandKind.Weather:
                    Region.CastWeather((Season)rec.IntArg, rec.DoubleArg);
                    break;
                case GodCommandKind.BlessedBirth:
                {
                    var mother = FindPersonById(rec.TargetPersonId);
                    if (mother != null) Region.CastBlessedBirth(mother);
                    break;
                }
                case GodCommandKind.ForkNudge:
                {
                    var person = FindPersonById(rec.TargetPersonId);
                    if (person != null) Region.CastForkNudge(person, rec.TextArg);
                    break;
                }
                case GodCommandKind.Heal:
                {
                    var target = FindPersonById(rec.TargetPersonId);
                    if (target != null) Region.CastHeal(target);
                    break;
                }
                case GodCommandKind.Lightning:
                {
                    var target = FindPersonById(rec.TargetPersonId);
                    if (target != null) Region.CastLightning(target);
                    break;
                }
            }
        }

        Person? FindPersonById(int personId) =>
            Region.People.FirstOrDefault(p => p.Id == personId);

        // =====================================================================
        // Read-Only Snapshot Builders for Unity Presentation
        // =====================================================================

        public static BeliefBand ClassifyBeliefBand(Person p, long currentDay)
        {
            double real = Simulation.GodReal(p);
            if (real < 0.30) return BeliefBand.Skeptic;
            if (real < 0.60) return BeliefBand.Open;
            bool practising = (currentDay - p.LastPrayDay <= SimClock.DaysPerYear)
                           || (currentDay - p.LastThanksDay <= SimClock.DaysPerYear)
                           || p.OwnDeeds.Any(d => d.Deed.Act == Act.Offer && currentDay - d.Deed.Day <= SimClock.DaysPerYear);
            if (real > 0.85 && practising) return BeliefBand.Devout;
            return BeliefBand.Believer;
        }

        public GodStateView GetGodState()
        {
            double blessingMult = Region.GodHelp > 0
                ? Math.Max(0.7, 1.0 - Math.Min(10.0, Region.GodHelp) * 0.03)
                : 1.0;
            double wrathMult = Region.GodHarm > 0
                ? Math.Max(0.7, 1.0 - Math.Min(10.0, Region.GodHarm) * 0.03)
                : 1.0;

            int believers = Region.People.Count(Simulation.IsBeliever);
            bool healUnlocked = Region.GodHelp >= 4.0 && believers >= 5 && (Region.Clock.Day - Region.LastHealDay >= 3600);
            bool lightningUnlocked = Region.GodHarm >= 4.0 && believers >= 5 && (Region.Clock.Day - Region.LastLightningDay >= 3600);

            return new GodStateView
            {
                Tick = Region.Clock.Tick,
                Year = Region.Clock.Year,
                Season = Region.Clock.Season,
                DayOfSeason = Region.Clock.DayOfSeason,
                Hour = Region.Clock.Hour,
                Favor = Region.Favor,
                SustainableFavor = Region.SustainableFavor,
                GodHelp = Region.GodHelp,
                GodHarm = Region.GodHarm,
                GodFaded = Region.GodFaded,
                IgnoredEmbers = Region.IgnoredEmbers,
                BlessingCostMultiplier = blessingMult,
                WrathCostMultiplier = wrathMult,
                OmenCost = 5.0 * wrathMult,
                BountyPlantCost = 20.0 * blessingMult,
                BountyAnimalCost = 30.0 * blessingMult,
                WeatherCost = 60.0,
                BlessedBirthCost = 40.0 * blessingMult,
                ForkNudgeCost = 10.0,
                HealCost = 50.0 * blessingMult,
                LightningCost = 50.0 * wrathMult,
                HealUnlocked = healUnlocked,
                LightningUnlocked = lightningUnlocked
            };
        }

        public WorldCellView GetCell(int x, int y)
        {
            var c = Region.World[x, y];
            return new WorldCellView
            {
                X = c.X,
                Y = c.Y,
                Terrain = c.Terrain,
                Elevation = c.Elevation,
                Features = c.Features,
                Plant = c.Plant,
                PlantStock = c.PlantStock,
                PlantCapacity = c.PlantCapacity,
                FishStock = c.FishStock,
                FishCapacity = c.FishCapacity,
                Wood = c.Wood,
                WoodCapacity = c.WoodCapacity,
                Weir = c.Weir,
                Snow = c.Snow,
                Temperature = c.Temperature
            };
        }

        public IReadOnlyList<HerdView> GetHerds()
        {
            var list = new List<HerdView>();
            foreach (var g in Region.Ecology.Groups)
            {
                if (g.Gone || g.Count <= 0) continue;
                list.Add(new HerdView
                {
                    Id = g.Id,
                    SpeciesName = g.Species.Name,
                    Count = g.Count,
                    X = g.X,
                    Y = g.Y,
                    Condition = g.Condition,
                    Huntable = g.Species.Huntable
                });
            }
            return list;
        }

        public IReadOnlyList<CampView> GetCamps()
        {
            var list = new List<CampView>(Region.Bands.Count);
            foreach (var sim in Region.Bands)
            {
                if (sim.Band.Members.Count == 0) continue;
                list.Add(BuildCampView(sim));
            }
            return list;
        }

        public CampView? GetCamp(int bandId)
        {
            var sim = Region.Bands.FirstOrDefault(b => b.Id == bandId);
            return sim != null ? BuildCampView(sim) : null;
        }

        CampView BuildCampView(Simulation sim)
        {
            var members = sim.Band.Members.Where(m => m.Alive).ToList();
            var adults = members.Where(sim.Grown).ToList();
            var sample = adults.Count > 0 ? adults : members;

            int skeptic = 0, open = 0, believer = 0, devout = 0;
            foreach (var p in sample)
            {
                switch (ClassifyBeliefBand(p, Region.Clock.Day))
                {
                    case BeliefBand.Skeptic: skeptic++; break;
                    case BeliefBand.Open: open++; break;
                    case BeliefBand.Believer: believer++; break;
                    case BeliefBand.Devout: devout++; break;
                }
            }

            double Avg(Func<Person, double> fn) => sample.Count > 0 ? sample.Average(fn) : 0.0;
            double foodTotal = sim.Band.FoodTotal;
            double dailyNeed = Math.Max(0.5, members.Count * 0.9);
            var techs = members.SelectMany(m => m.Knows).Distinct().OrderBy(s => s).ToArray();

            return new CampView
            {
                BandId = sim.Id,
                Name = sim.Band.Name,
                TribeName = sim.Tribe?.Name ?? "",
                CampX = sim.Band.CampX,
                CampY = sim.Band.CampY,
                Population = members.Count,
                Adults = adults.Count,
                FoodTotal = foodTotal,
                DaysOfFood = foodTotal / dailyNeed,
                Tools = sim.Band.Materials.Tools,
                Firewood = sim.Band.Households.Sum(h => h.Fuel),
                SkepticCount = skeptic,
                OpenCount = open,
                BelieverCount = believer,
                DevoutCount = devout,
                MeanGodReal = Avg(Simulation.GodReal),
                MeanGodAnswers = Avg(Simulation.GodAnswers),
                MeanGodKind = Avg(Simulation.GodKind),
                MeanGodGive = Avg(p => Simulation.GodAct(p, Act.Give)),
                MeanGodTake = Avg(p => Simulation.GodAct(p, Act.Take)),
                MeanGodAttack = Avg(p => Simulation.GodAct(p, Act.Attack)),
                MeanGodOffer = Avg(Simulation.GodOffer),
                MeanGodThank = Avg(Simulation.GodThank),
                KnownTechs = techs
            };
        }

        public IReadOnlyList<PersonView> GetPeople()
        {
            var list = new List<PersonView>();
            foreach (var sim in Region.Bands)
            {
                foreach (var p in sim.Band.Members)
                {
                    if (!p.Alive) continue;
                    list.Add(BuildPersonView(p, sim.Id));
                }
            }
            return list;
        }

        public PersonView? GetPerson(int personId)
        {
            foreach (var sim in Region.Bands)
            {
                var p = sim.Band.Members.FirstOrDefault(m => m.Id == personId);
                if (p != null) return BuildPersonView(p, sim.Id);
            }
            return null;
        }

        PersonView BuildPersonView(Person p, int bandId)
        {
            return new PersonView
            {
                Id = p.Id,
                Name = p.Name,
                Sex = p.Sex,
                AgeYears = p.AgeYears(Region.Clock),
                IsAdult = p.IsAdult(Region.Clock),
                Alive = p.Alive,
                BandId = bandId,
                X = p.X,
                Y = p.Y,
                CurrentVerb = p.Current?.Verb ?? Verb.Rest,
                CurrentStep = p.Current?.Step ?? "",
                Health = p.Health,
                Fatigue = p.Fatigue,
                Injury = p.Injury,
                Sickness = p.Sickness,
                IllnessName = p.Illness?.Name,
                Satiety = p.Satiety,
                Stomach = p.Stomach,
                Reserve = p.Reserve,
                ColdStress = p.ColdStress,
                Discontent = p.Discontent,
                Dread = p.Dread,
                Blessed = p.Blessed,
                BlessedNextBirth = p.BlessedNextBirth,
                BeliefBand = ClassifyBeliefBand(p, Region.Clock.Day),
                GodReal = Simulation.GodReal(p),
                GodAnswers = Simulation.GodAnswers(p),
                GodKind = Simulation.GodKind(p),
                GodGive = Simulation.GodAct(p, Act.Give),
                GodTake = Simulation.GodAct(p, Act.Take),
                GodAttack = Simulation.GodAct(p, Act.Attack),
                GodOffer = Simulation.GodOffer(p),
                GodThank = Simulation.GodThank(p),
                Credulity = p.M(MindTrait.Credulity),
                Warmth = p.M(MindTrait.Warmth),
                Boldness = p.M(MindTrait.Boldness),
                MotherId = p.Mother?.Id ?? -1,
                FatherId = p.Father?.Id ?? -1,
                SpouseIds = p.Spouses.Select(s => s.Id).ToArray(),
                ChildIds = p.Children.Where(c => c.Alive).Select(c => c.Id).ToArray(),
                ActiveNudges = new Dictionary<string, string>(p.Nudges)
            };
        }

        public IReadOnlyList<PrayerView> GetActivePrayers()
        {
            var list = new List<PrayerView>(Region.ActivePrayers.Count);
            foreach (var pr in Region.ActivePrayers)
            {
                if (pr.Resolved) continue;
                var band = Region.BandOf(pr.Asker);
                list.Add(new PrayerView
                {
                    Id = pr.Id,
                    AskerId = pr.Asker.Id,
                    AskerName = pr.Asker.Name,
                    BandId = band?.Id ?? -1,
                    CampX = pr.CampX,
                    CampY = pr.CampY,
                    Need = pr.Need,
                    SickTargetId = pr.SickTarget?.Id ?? -1,
                    StartDay = pr.StartDay,
                    IsEmber = pr.IsEmber,
                    HasOffering = pr.HasOffering,
                    PowerCasted = pr.PowerCasted,
                    InitialSeverity = pr.InitialSeverity
                });
            }
            return list;
        }
    }
}
