using System;
using System.Linq;
using Anthro.Sim.Ecology;
using Anthro.Sim.People;

namespace Anthro.Sim.Activities
{
    /// <summary>
    /// What a hunting party carries. Range decides how close hunters must get (and how exposed they
    /// are when an animal turns); power decides how many hits a kill takes. Better weapons come from
    /// the discovery web, so discoveries change hunting through these two numbers only.
    /// </summary>
    public sealed class Weapon
    {
        public readonly string Name;
        public readonly string StageId;
        /// <summary>Meters from which a hunter can strike.</summary>
        public readonly double Range;
        /// <summary>Damage of a good hit, in units of one wooden-spear hit.</summary>
        public readonly double Power;

        Weapon(string name, string stageId, double range, double power)
        {
            Name = name; StageId = stageId; Range = range; Power = power;
        }

        static readonly Weapon[] Catalog =
        {
            // Thrusting: a ~2 m spear held partway along, plus a lunge. Throwing is its own discovery.
            new Weapon("wooden spears", "spear.wood", 2.5, 1.0),
            new Weapon("stone-tipped spears", "spear.stone", 2.5, 1.6),
            new Weapon("light throwing spears", "spear.light", 20, 0.9),
            new Weapon("spear-throwers", "atl.hook", 35, 1.3),
            new Weapon("spear-thrower darts", "atl.dart", 40, 1.4),
            new Weapon("bows", "bow.simple", 45, 1.1),
            new Weapon("stone-tipped arrows", "bow.stone", 45, 1.6),
            new Weapon("fletched arrows", "bow.fletch", 55, 1.3),
            new Weapon("backed bows", "bow.backed", 60, 1.8),
        };

        /// <summary>The known weapon best suited to this target: reach it, then hurt it.</summary>
        public static Weapon Best(Band band, AnimalGroup target) =>
            Catalog.Where(w => band.AnyoneKnows(w.StageId))
                .OrderByDescending(w => w.Power * Math.Min(1, w.Range / target.FlightDistance))
                .FirstOrDefault() ?? Catalog[0];

        /// <summary>Within this range, a charging animal reaches the hunters.</summary>
        public bool CloseRange => Range < 15;
    }
}
