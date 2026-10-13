using System;
using System.Collections.Generic;
using System.Linq;
using Anthro.Sim.Core;
using Anthro.Sim.Ecology;
using Anthro.Sim.People;
using Anthro.Sim.World;

namespace Anthro.Sim
{
    public sealed class GodEvent
    {
        public long Tick { get; }
        public string Kind { get; }
        public string Summary { get; }
        public int X { get; }
        public int Y { get; }

        public GodEvent(long tick, string kind, string summary, int x = 0, int y = 0)
        {
            Tick = tick;
            Kind = kind;
            Summary = summary;
            X = x;
            Y = y;
        }
    }

    public interface IGodCommand
    {
        string PowerName { get; }
        double Cost { get; }
        bool CanExecute(Region region) => true;
        void Execute(Region region);
    }

    public sealed partial class Region
    {
        /// <summary>Starting favor pool (from PLANNING and GOD-LAYER).</summary>
        public const double StartFavorPool = 50.0;

        public double Favor { get; set; } = StartFavorPool;
        public double Reputation { get; set; } = 0.0;
        public double FavorCapFactor { get; set; } = 10.0;
        public bool GodFaded { get; set; }
        public int IgnoredEmbers { get; set; }
        public GodPolicy Policy { get; set; } = BotGod.PolicyFromEnv();

        public long LastHealDay = -99999;
        public long LastLightningDay = -99999;
        public long LastWeatherDay = -99999;
        public long LastBlessedBirthDay = -99999;

        // Favor audit tracking (T10 A1)
        public double FavorEarnedPrayer { get; set; }
        public double FavorEarnedOffer { get; set; }
        public double FavorEarnedRite { get; set; }
        public double FavorEarnedOther { get; set; } = StartFavorPool;
        public readonly Dictionary<string, double> FavorSpentByPower = new Dictionary<string, double>();
        public long DaysBroke { get; set; }
        public long FirstBrokeDay { get; set; } = -1;
        public long DaysTotal { get; set; }

        // Offering origin tracking (T10 Batch B)
        public int OfferingsFromSeedCount { get; set; }
        public int OfferingsFromPleaCount { get; set; }
        public int OfferingsFromGraveGoodsCount { get; set; }

        public bool SpendFavor(double amount)
        {
            if (GodFaded || amount <= 0) return false;
            if (Favor < amount) return false;
            Favor -= amount;
            return true;
        }

        /// <summary>
        /// Favor cap = Σ believers' god:real * k.
        /// Retains at least StartFavorPool while belief is budding.
        /// </summary>
        public double FavorCap => GodFaded ? 0.0 : Math.Max(StartFavorPool, People.Where(Simulation.IsBeliever).Sum(Simulation.GodReal) * FavorCapFactor);

        public readonly List<GodEvent> GodEvents = new List<GodEvent>();
        readonly Queue<IGodCommand> _pendingGodCommands = new Queue<IGodCommand>();

        /// <summary>Enqueue a god command to be executed deterministically at the start of the next hour step.</summary>
        public void EnqueueGodCommand(IGodCommand cmd)
        {
            if (cmd == null || GodFaded) return;
            _pendingGodCommands.Enqueue(cmd);
        }

        // Power command stubs (T2: API and deterministic queue; implementation in T4/T8/T9C)

        public void CastOmen(int x, int y, string description = "A sign in the sky", bool isGood = false) =>
            EnqueueGodCommand(new OmenCommand(x, y, description, isGood));

        public void CastBounty(int x, int y, bool animals) =>
            EnqueueGodCommand(new BountyCommand(x, y, animals));

        public void CastWeather(Season season, double delta) =>
            EnqueueGodCommand(new WeatherCommand(season, delta));

        public void CastBlessedBirth(Person mother) =>
            EnqueueGodCommand(new BlessedBirthCommand(mother));

        public bool CanCastBlessedBirth(Person mother) =>
            mother != null && mother.Alive && mother.Sex == Sex.Female &&
            !mother.BlessedNextBirth &&
            !Simulation.Off("god") && !Simulation.Off("power") && !Simulation.Off("bless") &&
            (Clock.Day - LastBlessedBirthDay >= 3600);

        public void CastForkNudge(Person person, string choice) =>
            EnqueueGodCommand(new ForkNudgeCommand(person, choice));

        public bool CanCastHeal(Person person) =>
            person != null && person.Alive &&
            !Simulation.Off("god") && !Simulation.Off("power") && !Simulation.Off("heal") &&
            Reputation >= 4.0 &&
            People.Count(Simulation.IsBeliever) >= 5 &&
            (Clock.Day - LastHealDay >= 3600);

        public bool CanCastLightning(Person person) =>
            person != null && person.Alive &&
            !Simulation.Off("god") && !Simulation.Off("power") && !Simulation.Off("lightning") &&
            Reputation <= -4.0 &&
            People.Count(Simulation.IsBeliever) >= 5 &&
            (Clock.Day - LastLightningDay >= 3600);

        public void CastHeal(Person person) =>
            EnqueueGodCommand(new HealCommand(person));

        public void CastLightning(Person person) =>
            EnqueueGodCommand(new LightningCommand(person));

        /// <summary>Applied at the start of an hour step before any band actions.</summary>
        public void ApplyGodCommands()
        {
            if (Simulation.Off("god") || GodFaded)
            {
                _pendingGodCommands.Clear();
                return;
            }

            while (_pendingGodCommands.Count > 0)
            {
                var cmd = _pendingGodCommands.Dequeue();
                if (!cmd.CanExecute(this)) continue;

                double cost = cmd.Cost;
                bool isBlessing = cmd.PowerName == "BountyPlant" || cmd.PowerName == "BountyAnimal" || cmd.PowerName == "BlessedBirth" || cmd.PowerName == "Heal";
                bool isWrath = cmd.PowerName == "Omen" || cmd.PowerName == "Lightning";
                if (isBlessing && Reputation > 0)
                    cost *= Math.Max(0.7, 1.0 - Reputation * 0.03);
                else if (isWrath && Reputation < 0)
                    cost *= Math.Max(0.7, 1.0 - Math.Abs(Reputation) * 0.03);

                if (Favor >= cost)
                {
                    Favor -= cost;
                    FavorSpentByPower.TryGetValue(cmd.PowerName, out double curSpent);
                    FavorSpentByPower[cmd.PowerName] = curSpent + cost;
                    cmd.Execute(this);
                }
            }
        }

        // Power command implementations
        sealed class OmenCommand : IGodCommand
        {
            readonly int _x, _y;
            readonly string _desc;
            readonly bool _isGood;
            public OmenCommand(int x, int y, string desc, bool isGood = false) { _x = x; _y = y; _desc = desc; _isGood = isGood; }
            public string PowerName => "Omen";
            public double Cost => 5.0;
            public void Execute(Region region)
            {
                if (Simulation.Off("god") || Simulation.Off("power") || Simulation.Off("omen")) return;
                double radius = WorldMap.FromMeters(5000);
                var witnesses = region.Bands
                    .Where(b => WorldMap.Distance(b.Band.CampX, b.Band.CampY, _x, _y) <= radius)
                    .SelectMany(b => b.Band.Members.Where(b.Grown))
                    .ToList();

                foreach (var w in witnesses)
                {
                    w.Expect.Absorb(Simulation.GodRealTopic, 1.0, 0.08, Simulation.GodReal(w));
                }

                region.Reputation = Math.Max(-10.0, Math.Min(10.0, region.Reputation + (_isGood ? 0.5 : -0.5)));
                region.Chronicle.Write(region.Clock, Importance.Notable, $"A striking omen appeared near ({_x}, {_y}): {_desc}.");
                region.GodEvents.Add(new GodEvent(region.Clock.Tick, "Omen", _desc, _x, _y));
                region.InterpretStrikingEvent(_x, _y, isGood: _isGood, "Omen");
            }
        }

        sealed class BountyCommand : IGodCommand
        {
            readonly int _x, _y;
            readonly bool _animals;
            public BountyCommand(int x, int y, bool animals) { _x = x; _y = y; _animals = animals; }
            public string PowerName => _animals ? "BountyAnimal" : "BountyPlant";
            public double Cost => _animals ? 30.0 : 20.0;
            public void Execute(Region region)
            {
                if (Simulation.Off("god") || Simulation.Off("power") || Simulation.Off("bounty")) return;
                region.Reputation += 1.0;
                if (!_animals)
                {
                    double r = WorldMap.FromMeters(3000);
                    foreach (var c in region.World.Around(_x, _y, r))
                    {
                        c.PlantStock = Math.Max(c.PlantStock, c.PlantCapacity * 1.5);
                        if (c.Terrain == Terrain.River)
                            c.FishStock = Math.Max(c.FishStock, c.FishCapacity * 1.2);
                        c.Wood = Math.Max(c.Wood, c.WoodCapacity);
                    }
                    region.Chronicle.Write(region.Clock, Importance.Notable, $"The earth brought forth an abundance of food near ({_x}, {_y}).");
                    region.GodEvents.Add(new GodEvent(region.Clock.Tick, "BountyPlant", $"Plant bounty near ({_x}, {_y})", _x, _y));
                    region.InterpretStrikingEvent(_x, _y, isGood: true, "BountyPlant");
                }
                else
                {
                    double r = WorldMap.FromMeters(5000);
                    var nearbyGroups = region.Ecology.Groups
                        .Where(g => !g.Gone && g.Species.Huntable && WorldMap.Distance(g.X, g.Y, _x, _y) <= r)
                        .ToList();

                    if (nearbyGroups.Count > 0)
                    {
                        foreach (var g in nearbyGroups)
                        {
                            g.Count = Math.Min(g.Species.GroupMax * 2, g.Count + Math.Max(3, g.Species.GroupMin));
                            g.Condition = 1.0;
                            g.Fear = 0;
                            g.Shy = 0;
                            g.TargetX = _x;
                            g.TargetY = _y;
                        }
                    }
                    else
                    {
                        region.Ecology.SpawnGroup(Species.Deer, _x, _y, Species.Deer.GroupMin * 2, region);
                    }
                    region.Chronicle.Write(region.Clock, Importance.Notable, $"Herds of game gathered near ({_x}, {_y}).");
                    region.GodEvents.Add(new GodEvent(region.Clock.Tick, "BountyAnimal", $"Animal bounty near ({_x}, {_y})", _x, _y));
                    region.InterpretStrikingEvent(_x, _y, isGood: true, "BountyAnimal");
                }
            }
        }

        sealed class WeatherCommand : IGodCommand
        {
            readonly Season _season;
            readonly double _delta;
            public WeatherCommand(Season season, double delta) { _season = season; _delta = delta; }
            public string PowerName => "Weather";
            public double Cost => 60.0;
            public void Execute(Region region)
            {
                if (Simulation.Off("god") || Simulation.Off("power") || Simulation.Off("weather")) return;
                var climate = region.World.Climate;
                climate.SeasonOverride += _delta * 3.0;

                if (_season == Season.Winter)
                {
                    climate.WinterSeverity = Math.Max(-2.5, Math.Min(3.0, climate.WinterSeverity - _delta * 0.8));
                }

                if (_delta > 0)
                {
                    climate.Mast = Math.Max(climate.Mast, climate.Mast * (1.0 + 0.3 * _delta));
                    climate.Berries = Math.Max(climate.Berries, climate.Berries * (1.0 + 0.3 * _delta));
                    climate.Grain = Math.Max(climate.Grain, climate.Grain * (1.0 + 0.3 * _delta));
                    climate.FishRun = Math.Max(climate.FishRun, climate.FishRun * (1.0 + 0.3 * _delta));
                    region.Reputation += 1.5;
                    region.Chronicle.Write(region.Clock, Importance.Notable, $"The heavens softened the season ({_season}).");
                }
                else
                {
                    climate.Mast = Math.Max(0.2, climate.Mast * (1.0 + 0.3 * _delta));
                    climate.Berries = Math.Max(0.2, climate.Berries * (1.0 + 0.3 * _delta));
                    climate.Grain = Math.Max(0.2, climate.Grain * (1.0 + 0.3 * _delta));
                    climate.FishRun = Math.Max(0.2, climate.FishRun * (1.0 + 0.3 * _delta));
                    region.Reputation -= 1.5;
                    region.Chronicle.Write(region.Clock, Importance.Notable, $"A bitter harshness gripped the land ({_season}).");
                }
                region.GodEvents.Add(new GodEvent(region.Clock.Tick, "Weather", $"Weather changed for {_season} (delta {_delta:F1})"));
                region.InterpretStrikingEvent(0, 0, isGood: _delta > 0, "Weather");
            }
        }

        sealed class BlessedBirthCommand : IGodCommand
        {
            readonly Person _mother;
            public BlessedBirthCommand(Person mother) { _mother = mother; }
            public string PowerName => "BlessedBirth";
            public double Cost => 40.0;
            public void Execute(Region region)
            {
                if (Simulation.Off("god") || Simulation.Off("power") || Simulation.Off("bless")) return;
                region.LastBlessedBirthDay = region.Clock.Day;
                _mother.BlessedNextBirth = true;
                region.Reputation += 1.5;
                region.Chronicle.Write(region.Clock, Importance.Notable, $"A divine blessing was laid upon {_mother.Name}'s unborn child.");
                var band = region.BandOf(_mother);
                int bx = band != null ? (int)band.Band.CampX : 0;
                int by = band != null ? (int)band.Band.CampY : 0;
                region.GodEvents.Add(new GodEvent(region.Clock.Tick, "BlessedBirth", $"Blessing on {_mother.Name}", bx, by));
            }
        }

        sealed class ForkNudgeCommand : IGodCommand
        {
            readonly Person _person;
            readonly string _choice;
            public ForkNudgeCommand(Person person, string choice) { _person = person; _choice = choice; }
            public string PowerName => "ForkNudge";
            public double Cost => 10.0;
            public void Execute(Region region)
            {
                if (Simulation.Off("god") || Simulation.Off("power") || Simulation.Off("nudge")) return;
                string fork = "general";
                string val = _choice;
                if (_choice.Contains(":"))
                {
                    var parts = _choice.Split(':');
                    fork = parts[0].Trim().ToLowerInvariant();
                    val = parts[1].Trim().ToLowerInvariant();
                }
                else
                {
                    val = _choice.Trim().ToLowerInvariant();
                }

                _person.Nudges[fork] = val;
                if (fork == "general")
                {
                    _person.Nudges["marry"] = val;
                    _person.Nudges["leave"] = val;
                    _person.Nudges["duel"] = val;
                    _person.Nudges["raid"] = val;
                }

                region.Chronicle.Write(region.Clock, Importance.Detail, $"An unseen whisper guided {_person.Name}'s choice ({_choice}).");
                var band = region.BandOf(_person);
                int bx = band != null ? (int)band.Band.CampX : 0;
                int by = band != null ? (int)band.Band.CampY : 0;
                region.GodEvents.Add(new GodEvent(region.Clock.Tick, "ForkNudge", $"{_person.Name} nudged: {_choice}", bx, by));
            }
        }

        sealed class HealCommand : IGodCommand
        {
            readonly Person _target;
            public HealCommand(Person target) { _target = target; }
            public string PowerName => "Heal";
            public double Cost => 50.0;
            public bool CanExecute(Region region) => region.CanCastHeal(_target);
            public void Execute(Region region)
            {
                if (!region.CanCastHeal(_target)) return;
                region.LastHealDay = region.Clock.Day;
                region.Reputation = Math.Min(10.0, region.Reputation + 1.0);
                _target.Illness = null;
                _target.Injury = 0.0;
                _target.Health = Math.Max(_target.Health, 0.85);

                var band = region.BandOf(_target);
                int bx = band != null ? (int)band.Band.CampX : (int)_target.X;
                int by = band != null ? (int)band.Band.CampY : (int)_target.Y;

                region.Chronicle.Write(region.Clock, Importance.Major, $"{_target.Name}'s fever broke and their wounds closed.");
                region.GodEvents.Add(new GodEvent(region.Clock.Tick, "Heal", $"Miraculous healing of {_target.Name}", bx, by));
                region.InterpretStrikingEvent(bx, by, isGood: true, "Heal", _target);
            }
        }

        sealed class LightningCommand : IGodCommand
        {
            readonly Person _target;
            public LightningCommand(Person target) { _target = target; }
            public string PowerName => "Lightning";
            public double Cost => 50.0;
            public bool CanExecute(Region region) => region.CanCastLightning(_target);
            public void Execute(Region region)
            {
                if (!region.CanCastLightning(_target)) return;
                region.LastLightningDay = region.Clock.Day;
                region.Reputation = Math.Max(-10.0, region.Reputation - 1.0);

                var band = region.BandOf(_target);
                int bx = band != null ? (int)band.Band.CampX : (int)_target.X;
                int by = band != null ? (int)band.Band.CampY : (int)_target.Y;

                region.Chronicle.Write(region.Clock, Importance.Major, $"A bolt of lightning struck {_target.Name} dead on the open ground.");
                region.GodEvents.Add(new GodEvent(region.Clock.Tick, "Lightning", $"Lightning struck {_target.Name}", bx, by));
                region.InterpretStrikingEvent(bx, by, isGood: false, "Lightning", _target);
                if (band != null && _target.Alive)
                {
                    band.Kill(_target, "killed by lightning");
                }
            }
        }
    }
}
