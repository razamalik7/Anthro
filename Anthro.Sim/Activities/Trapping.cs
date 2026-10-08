using System;
using System.Linq;
using Anthro.Sim.Ecology;
using Anthro.Sim.People;
using Anthro.Sim.World;

namespace Anthro.Sim.Activities
{
    /// <summary>What known ways of trapping catch, per hour of setting and checking traps where small game is about.</summary>
    public static class TrappingMethods
    {
        public static double Efficiency(Person p)
        {
            var k = p.Knows;
            double e = 0;
            if (k.Contains("trap.snare")) e = 0.4;
            if (k.Contains("trap.dead")) e = Math.Max(e, 0.5);
            if (k.Contains("trap.spring")) e = Math.Max(e, 0.7);
            return e;
        }
    }

    /// <summary>
    /// Setting snares and deadfalls on the runs small game uses, and checking yesterday's: a steady,
    /// unglamorous trickle of meat. What's caught comes out of the animals actually living there.
    /// </summary>
    public sealed class Trap : TimedActivity
    {
        public const double FallbackPerHour = 0.3;
        AnimalGroup? _warren;
        double _caught;

        public Trap(Person p) : base(p, 3) { Step = "setting and checking traps"; }
        public override Verb Verb => Verb.Trap;

        public override void Begin(Simulation sim)
        {
            // Where they've seen small game lately, near camp.
            var seen = P.Map.Game.Values.Where(s => s.Species.Size < 1 && sim.Clock.Day - s.Day < 14)
                .OrderBy(s => WorldMap.Distance(s.X, s.Y, sim.Band.CampX, sim.Band.CampY)).FirstOrDefault();
            _warren = seen.Species == null ? null : sim.Ecology.ById(seen.GroupId);
        }

        protected override void OnHour(Simulation sim)
        {
            if (_warren == null || _warren.Gone) return;
            double plenty = Math.Min(1, _warren.Count / 30.0);
            double rate = TrappingMethods.Efficiency(P) * plenty * (0.6 + 0.4 * P.M(MindTrait.Patience));
            int animals = (int)Math.Floor(rate / _warren.Species.Meat + sim.Rng.NextDouble());
            animals = Math.Min(animals, _warren.Count);
            if (animals <= 0) return;
            _warren.Count -= animals; sim.Ecology.Count(_warren.Species, "hunted", animals);
            _caught += animals * _warren.Species.Meat;
        }

        protected override void OnEnd(Simulation sim)
        {
            sim.HomeOf(P).Food.AddMeat(_caught);
            sim.ThisYear.Hunted += _caught;
            P.ProvidedToday += _caught;
            P.Expect.Learn(P, Verb.Trap, _caught / 3, FallbackPerHour);
            if (_caught > 0) P.Nudge(Interest.Animals, 0.003);
        }
    }
}
