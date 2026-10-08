using System;
using System.Collections.Generic;
using System.Linq;
using Anthro.Sim.Core;
using Anthro.Sim.People;

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
        void Execute(Region region);
    }

    public sealed partial class Region
    {
        /// <summary>Starting favor pool (from PLANNING and GOD-LAYER).</summary>
        public const double StartFavorPool = 50.0;

        public double Favor { get; set; } = StartFavorPool;
        public double Reputation { get; set; } = 0.0;
        public double FavorCapFactor { get; set; } = 10.0;

        /// <summary>
        /// Favor cap = Σ believers' god:real * k.
        /// Retains at least StartFavorPool while belief is budding.
        /// </summary>
        public double FavorCap => Math.Max(StartFavorPool, People.Where(Simulation.IsBeliever).Sum(Simulation.GodReal) * FavorCapFactor);

        public readonly List<GodEvent> GodEvents = new List<GodEvent>();
        readonly Queue<IGodCommand> _pendingGodCommands = new Queue<IGodCommand>();

        /// <summary>Enqueue a god command to be executed deterministically at the start of the next hour step.</summary>
        public void EnqueueGodCommand(IGodCommand cmd)
        {
            if (cmd == null) return;
            _pendingGodCommands.Enqueue(cmd);
        }

        // Power command stubs (T2: API and deterministic queue; implementation in T4/T8)

        public void CastOmen(int x, int y, string description = "A sign in the sky") =>
            EnqueueGodCommand(new OmenCommand(x, y, description));

        public void CastBounty(int x, int y, bool animals) =>
            EnqueueGodCommand(new BountyCommand(x, y, animals));

        public void CastWeather(Season season, double delta) =>
            EnqueueGodCommand(new WeatherCommand(season, delta));

        public void CastBlessedBirth(Person mother) =>
            EnqueueGodCommand(new BlessedBirthCommand(mother));

        public void CastForkNudge(Person person, string choice) =>
            EnqueueGodCommand(new ForkNudgeCommand(person, choice));

        /// <summary>Applied at the start of an hour step before any band actions.</summary>
        internal void ApplyGodCommands()
        {
            if (Simulation.Off("god"))
            {
                _pendingGodCommands.Clear();
                return;
            }

            while (_pendingGodCommands.Count > 0)
            {
                var cmd = _pendingGodCommands.Dequeue();
                if (Favor >= cmd.Cost)
                {
                    Favor -= cmd.Cost;
                    cmd.Execute(this);
                    GodEvents.Add(new GodEvent(Clock.Tick, cmd.PowerName, $"{cmd.PowerName} cast for cost {cmd.Cost:F1}"));
                }
            }
        }

        // Command stub implementations
        sealed class OmenCommand : IGodCommand
        {
            readonly int _x, _y;
            readonly string _desc;
            public OmenCommand(int x, int y, string desc) { _x = x; _y = y; _desc = desc; }
            public string PowerName => "Omen";
            public double Cost => 5.0;
            public void Execute(Region region)
            {
                // Stub in T2; hooked to witnesses in T4
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
                // Stub in T2; hooked to cell ecology in T4
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
                // Stub in T2; hooked to Climate in T4
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
                // Stub in T2; hooked to NewChild in T4
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
                // Stub in T2; hooked to Decider/Family in T8
            }
        }
    }
}
