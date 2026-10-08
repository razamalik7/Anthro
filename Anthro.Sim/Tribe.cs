using System;
using System.Collections.Generic;
using System.Linq;
using Anthro.Sim.Core;
using Anthro.Sim.People;
using Anthro.Sim.World;

namespace Anthro.Sim
{
    /// <summary>
    /// A people: the named group a person belongs to, sharing a language, customs and a story, made of
    /// one or more camps (each camp a <see cref="Simulation"/>). Camps split and rejoin within a people as
    /// the land and their numbers demand (real foragers' fission and fusion); families move between
    /// its camps to get away from a feud. Only rarely does a camp break away to become a people of its
    /// own: after long bad blood with the rest, years apart, and customs grown different.
    /// </summary>
    public sealed class Tribe
    {
        public readonly int Id;
        public string Name;
        public readonly int FoundedYear;
        /// <summary>The people this one broke away from, if any.</summary>
        public readonly Tribe? Parent;

        public Tribe(int id, string name, int year, Tribe? parent) { Id = id; Name = name; FoundedYear = year; Parent = parent; }
    }

    public sealed partial class Region
    {
        /// <summary>
        /// Design rule (not a fact about the world): at most this many peoples on the map at once, so the
        /// game stays playable and fast. Past it, a camp that would break away stays with its people.
        /// </summary>
        public const int MaxTribes = 5;

        public readonly List<Tribe> Tribes = new List<Tribe>();
        int _nextTribeId;

        internal Tribe NewTribe(string name, Tribe? parent)
        {
            var t = new Tribe(_nextTribeId++, name, Clock.Year, parent);
            Tribes.Add(t);
            return t;
        }

        /// <summary>The peoples with at least one camp still living.</summary>
        public IEnumerable<Tribe> LivingTribes => Tribes.Where(t => Bands.Any(b => b.Tribe == t && !b.Extinct));

        public IEnumerable<Simulation> CampsOf(Tribe t) => Bands.Where(b => b.Tribe == t && !b.Extinct);

        /// <summary>
        /// Once a year each camp's ties to the rest of its people are tested. A camp breaks away when its
        /// people have come to hold grudges against the others' camps, when it has gone years without
        /// meeting them, and when its customs have drifted from theirs. Rare: most camps never do.
        /// </summary>
        void ConsiderSchisms()
        {
            if (LivingTribes.Count() >= MaxTribes) return;
            foreach (var camp in Bands.ToList())
            {
                if (camp.Extinct || camp.Tribe == null) continue;
                var kin = CampsOf(camp.Tribe).Where(c => c != camp).ToList();
                if (kin.Count == 0) continue;
                double rift = camp.RiftWith(kin);
                // Authored: a deep rift (bad blood, years apart, different ways) breaks a camp away about
                // one year in ten; a shallow one never does.
                if (rift < 1 || !Rng.Chance(0.1 * Math.Min(2, rift - 0.5))) continue;
                var old = camp.Tribe;
                string was = camp.Band.Name;
                camp.Tribe = NewTribe(Names.Next(Rng), old);
                camp.Band.Name = camp.Tribe.Name;
                Chronicle.Write(Clock, Importance.Major, $"The {was} camp broke with the {old.Name}: they are the {camp.Tribe.Name} now, a people of their own.");
                camp.Chronicle.Write(Clock, Importance.Major, $"We broke with the {old.Name}. We are the {camp.Tribe.Name} now.");
                foreach (var k in kin) k.Chronicle.Write(Clock, Importance.Major, $"A camp of the {old.Name} broke away and call themselves the {camp.Tribe.Name}.");
                if (LivingTribes.Count() >= MaxTribes) return;
            }
        }
    }

    public sealed partial class Simulation
    {
        /// <summary>The people this camp belongs to.</summary>
        public Tribe? Tribe;

        /// <summary>When people of this camp last met people of another camp of their people (camps nearby, visits).</summary>
        internal long LastKinContact;

        /// <summary>
        /// How far this camp has come apart from the rest of its people: grudges its adults hold against
        /// their camps (scaled so a few bad feuds count fully), years without meeting them (a decade
        /// counts fully), and how different its customs have grown (who moves at marriage, who keeps the
        /// children). Each part is 0-1 and they add.
        /// </summary>
        internal double RiftWith(List<Simulation> kin)
        {
            var adults = Band.Members.Where(Grown).ToList();
            if (adults.Count == 0) return 0;
            double grudge = adults.Average(a => kin.Max(k => a.GetBandGrievance(k.Id)));
            double badBlood = grudge / (grudge + 3);
            double apart = Math.Min(1, (Clock.Day - LastKinContact) / (double)(SimClock.DaysPerYear * 10));
            // How different their ways have grown: the household customs, and everything people learn and
            // pass on about how to live (how far violence goes, honour, peacemaking, taking sides, duels,
            // rationing, asking, elders, hunting, marriage), camp against camp. A difference of about a
            // third on average counts fully.
            double household = kin.Average(k => Math.Abs(k.Band.ResidenceNorm - Band.ResidenceNorm) + Math.Abs(k.Band.ChildrenFollowMother - Band.ChildrenFollowMother)) / 4;
            var theirs = kin.SelectMany(k => k.Band.Members.Where(k.Grown)).ToList();
            double ways = 0;
            if (theirs.Count > 0)
            {
                double diff = 0;
                foreach (var t in _cultureTopics)
                    diff += Math.Abs(adults.Average(a => a.Expect.Get(t, 0)) - theirs.Average(o => o.Expect.Get(t, 0)));
                ways = Math.Min(1, diff / _cultureTopics.Length / 0.33);
            }
            return badBlood + apart + Math.Max(household, ways);
        }

        static readonly string[] _cultureTopics =
        {
            "bound:kin", "bound:friend", "bound:neutral", "bound:outsider", "norm:honour", "norm:peacemaking", "norm:sides", "norm:duel",
            "norm:rationing", "norm:demand", "norm:elders", "norm:hunting", "wed:more",
        };

        /// <summary>A camp's name: the people's, and whose fire it gathers around.</summary>
        internal string CampName(Person? leader) => leader == null ? Tribe?.Name ?? "?" : $"{Tribe?.Name} of {leader.Name}";
    }
}
