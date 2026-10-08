using System.Collections.Generic;
using Anthro.Sim.Core;

namespace Anthro.Sim.Ecology
{
    /// <summary>A herd, pack or warren that moves and lives as one unit.</summary>
    public sealed class AnimalGroup
    {
        public readonly int Id;
        public readonly Species Species;
        public int Count;
        public double X, Y;
        public double TargetX, TargetY;

        /// <summary>Predators only. Above 0.75 a pack is hungry enough to get bold.</summary>
        public double Hunger;

        /// <summary>
        /// Rises when hunted or crowded by people, fades with quiet days. Frightened animals spot
        /// hunters sooner and keep away from camp.
        /// </summary>
        public double Fear;

        /// <summary>
        /// How wary of people this herd has become (0-1) from being hunted: hunted game keeps farther
        /// off, feeds at night and breaks through drive lines (flight distances in hunted populations
        /// run two to three times those in unhunted ones). Builds with every hunt that touches it and
        /// fades over a year or two of being left alone.
        /// </summary>
        public double Shy;

        /// <summary>
        /// How well fed the animals are (0-1): rises on good grazing, falls on poor ground and in
        /// snow. Thin animals die in hard winters and bear fewer young; it's also how fat their meat is.
        /// </summary>
        public double Condition = 0.9;

        /// <summary>
        /// Old, sick or weak animals wolves have already taken: deaths from age and sickness the herd
        /// won't have now (wolves mostly kill what would soon have died; their kills largely replace
        /// natural deaths rather than add to them).
        /// </summary>
        public int Culled;

        /// <summary>Share of the females carrying young this year, set by their condition at the autumn rut.</summary>
        public double Pregnant = 0.8;

        /// <summary>A herd that leaves the map when the land can't feed it, and comes back when it can.</summary>
        public bool Migratory;
        /// <summary>Heading for the map's edge to leave.</summary>
        public bool Leaving;
        /// <summary>Off the map (the same animals, away for the season): not seen, not hunted.</summary>
        public bool Away;
        public long LeftDay;

        /// <summary>This group's own chance stream, so groups can be updated in parallel and still replay exactly.</summary>
        public Rng? Rng;

        /// <summary>Where the group has been each day this past week: the trail it leaves on the land.</summary>
        public readonly List<(double X, double Y, long Day)> Trail = new List<(double, double, long)>();

        public AnimalGroup(int id, Species species, int count, double x, double y)
        {
            Id = id; Species = species; Count = count; X = x; Y = y; TargetX = x; TargetY = y;
        }

        public bool Gone => Count <= 0;

        /// <summary>How close hunters must get before the animals bolt, in meters.</summary>
        public double FlightDistance => Species.Wariness * (1 + Fear) * 60;
    }
}
