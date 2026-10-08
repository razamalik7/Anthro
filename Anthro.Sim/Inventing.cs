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
    /// <summary>
    /// Discovery and the passing on of know-how. Nobody researches anything: while doing ordinary
    /// work, a person who knows what a stage builds on, has what it needs at hand, and is pushed by
    /// the situation it answers may hit on it. The curious, the apt, the interested and the
    /// practiced notice more. Stages with an accident (a stone left in the fire) can also just
    /// happen to someone, if they notice. Know-how then spreads only by people showing and telling
    /// each other, within a band and across bands when people meet, and is lost when the last
    /// person who knows it dies.
    /// </summary>
    public sealed partial class Simulation
    {
        /// <summary>
        /// Calibration: chance per hour of relevant work that an average person, with everything at
        /// hand and the pressure fully on, hits on a stage. Set so that a band in the right conditions
        /// tends to find a nearby stage within decades (without help, a people finds one to three new
        /// things in a lifetime; a god can speed that a great deal).
        /// </summary>
        public static double DiscoveryRate = 1e-7;

        /// <summary>What's at hand around camp (terrain, stone, animals); refreshed with the forage patches.</summary>
        readonly HashSet<string> _here = new HashSet<string>();

        void RefreshMaterials()
        {
            _here.Clear();
            foreach (var (c, d) in _nearCamp)
            {
                if (d > WorldMap.FromMeters(3000)) continue;
                _here.Add(c.Terrain.ToString().ToLowerInvariant());
                if (c.Has(Feature.Clay)) _here.Add("clay");
                if (c.Has(Feature.Ochre)) _here.Add("ochre");
                if (c.Has(Feature.Cave)) _here.Add("cave");
                if (c.Has(Feature.Reeds)) _here.Add("reeds");
                if (c.Plant == PlantFood.Nuts) _here.Add("nuts");
                if (c.Plant == PlantFood.WildGrain) _here.Add("grain");
                if (c.FishStock > c.FishCapacity * 1.5 && c.FishCapacity > 0) _here.Add("fishrun");
            }
            if (World.FlintCells.Any(c => WorldMap.Distance(c.X, c.Y, Band.CampX, Band.CampY) < WorldMap.FromMeters(8000))) _here.Add("flint");
            foreach (var m in Band.Members)
                foreach (var s in m.Map.Game.Values)
                {
                    if (Clock.Day - s.Day > 14 || WorldMap.Distance(s.X, s.Y, Band.CampX, Band.CampY) > WorldMap.FromMeters(5000)) continue;
                    _here.Add(s.Species.Predator ? "wolves" : "game");
                }
            if (CampCell.Snow > 10) _here.Add("snow");
        }

        bool AtHand(string material, Person p) => material switch
        {
            "bone" => Band.Materials.Bone > 0,
            "hide" => Band.Materials.Hides > 0,
            "sinew" => Band.Materials.Sinew > 0,
            "fat" => Band.Materials.Bone > 0 || Band.MeatTotal > 1,
            "glut" => p.Home != null && p.Home.Food.Perishable > p.Home.Members.Count * 0.9 * 3,
            _ => _here.Contains(material),
        };

        /// <summary>How strongly a situation is pressing on this person (0-1).</summary>
        double Pressure(string pressure, Person p) => pressure switch
        {
            "hunger" => 1 - p.Satiety + (p.Home != null && p.Home.DaysOfFood < 2 ? 0.3 : 0),
            "cold" => Math.Min(1, p.ColdStress * 3),
            "winter" => Math.Max(0, Math.Min(1, (5 - CampCell.Temperature) / 10)),
            "dark" => Math.Max(0, Math.Min(1, (16 - Clock.DayLength) / 8)),
            "spoilage" => p.Home == null ? 0 : Math.Min(1, p.Home.SpoiledRecent / Math.Max(1, p.Home.Members.Count * 2)),
            "flight" => Math.Min(1, Band.StalkFailures / 5),
            "danger" => _wolvesSeenThisSeason ? 1 : 0,
            "death" => Math.Max(0, 1 - (Clock.Day - Band.LastDeathDay) / 60.0),
            "carry" => p.Home == null ? 0 : Math.Min(1, p.Home.Food.Plants / Math.Max(1, p.Home.Members.Count * 0.9 * 4)),
            _ => 0,
        };

        bool WayOfLife(string? way) => way switch
        {
            null => true,
            "settled" => Clock.Day - _campSince >= 14,
            "settled-long" => Clock.Day - _campSince >= SimClock.DaysPerSeason,
            _ => true,
        };

        static double Lean(double v) => 0.5 + v;

        /// <summary>An hour of someone's work: might they hit on something new?</summary>
        void TryDiscover(Person p)
        {
            if (Web == null || p.Current == null || p.AgeYears(Clock) < 12) return;
            var candidates = Web.DiscoverableDuring(Expectations.Topic(p.Current.Verb));
            if (candidates.Count == 0) return;
            // Discoveries are rare: roll first against the best case for every candidate together,
            // and only look closely if the roll could succeed. (Same odds; far less work.)
            const double BestCase = 2 * 1.5 * 1.5 * 1.5 * 1.5 * 1.5 * 1.5 * 3;
            double u = Rng.NextDouble();
            if (u >= DiscoveryRate * BestCase * candidates.Count) return;
            int index = (int)(u / (DiscoveryRate * BestCase));
            if (index >= candidates.Count) return;
            {
                var s = candidates[index];
                double within = u / (DiscoveryRate * BestCase) - index;   // where the roll fell inside this stage's best case
                if (p.Knows.Contains(s.Id)) return;
                var r = s.Sim!;
                if (!DiscoveryWeb.CanLearn(s, p.Knows) || !WayOfLife(r.Way)) return;
                foreach (var m in r.Materials) if (!AtHand(m, p)) return;

                double strongest = 0;
                foreach (var x in r.Pressures) strongest = Math.Max(strongest, Pressure(x, p));
                double push = r.Pressures.Count == 0 ? 1 : 0.2 + 1.8 * Math.Min(1, strongest);
                double who = Lean(p.M(MindTrait.Curiosity)) * Lean(p.M(MindTrait.Aptitude))
                             * (r.InterestIndex >= 0 ? Lean(p.Interests[r.InterestIndex]) : 1)
                             * (r.SkillIndex >= 0 ? Lean(p.Skills[r.SkillIndex]) : 1)
                             * (r.TraitIndex >= 0 ? Lean(p.Mind[r.TraitIndex]) : 1);
                // Stages that can come about by accident turn up more often; the person still has to notice.
                double accident = s.Accident != null ? 1.5 : 1;
                // Knowing something can be done, and roughly how, makes working it out far likelier.
                double heard = 1 + 5 * (p.Learning.TryGetValue(s.Id, out var part) ? part : 0);
                if (within < r.Rate * push * who * accident * heard / BestCase) Discover(p, s);
            }
        }

        void Discover(Person p, Stage s)
        {
            p.Knows.Add(s.Id);
            p.Learning.Remove(s.Id);
            bool firstAnywhere = !Region.People.Any(o => o != p && o.Knows.Contains(s.Id));
            string how = s.Accident != null && Rng.Chance(0.5) ? $" ({s.Accident.TrimEnd('.')}.)" : "";
            Chronicle.Write(Clock, Importance.Major, $"{p.Name} worked out {s.Name.ToLowerInvariant()}.{how}");
            if (firstAnywhere) Region.Chronicle.Write(Clock, Importance.Notable, $"The {Band.Name} band were the first anyone knows of to have {s.Name.ToLowerInvariant()}.");
            Region.DiscoveryLog.Add((Clock.Year, Band.Name, s.Id));
        }

        /// <summary>
        /// Showing and telling. In talk, or while someone watches another work, a person may pass on a
        /// little of one thing they know that the other is ready to learn. Learning builds up over many
        /// such times. Ideas that live in talk (stories, names, customs) can be learned by talk alone;
        /// a craft can only be got so far by hearing about it (about 40%: knowing it can be done and
        /// roughly how), and the rest comes from watching it done. Good teachers and apt learners pass
        /// on more; storytelling and counting help in talk.
        /// </summary>
        public void PassOn(Person teacher, Person learner, double strength = 1, bool shown = false)
        {
            if (Web == null || learner.AgeYears(Clock) < 4) return;
            Stage? pick = null;
            int seen = 0;
            foreach (var id in teacher.Knows)
            {
                if (learner.Knows.Contains(id) || !Web.Stages.TryGetValue(id, out var s) || s.Known) continue;
                if (!DiscoveryWeb.CanLearn(s, learner.Knows)) continue;
                bool talkable = s.Sim != null && s.Sim.During.Contains("Socialize");
                // Nothing more to get from being told about a craft one has already heard all about.
                if (!shown && !talkable && learner.Learning.TryGetValue(id, out var had) && had >= TalkCeiling) continue;
                if (Rng.Range(0, ++seen) == 0) pick = s;
            }
            if (pick == null) return;
            double chance = 0.25 * strength * (0.3 + teacher.S(Skill.Teaching)) * Lean(learner.M(MindTrait.Aptitude))
                            * (teacher.Knows.Contains("lang.story") ? 1.3 : 1) * (teacher.Knows.Contains("lang.count") ? 1.1 : 1);
            if (!Rng.Chance(chance)) return;
            teacher.Practice(Skill.Teaching, 1);
            bool canTalk = pick.Sim != null && pick.Sim.During.Contains("Socialize");
            double before = learner.Learning.TryGetValue(pick.Id, out var b) ? b : 0;
            double after = before + (shown ? 0.35 : 0.15);
            if (!shown && !canTalk) after = Math.Min(after, TalkCeiling);
            if (after < 1) { learner.Learning[pick.Id] = after; return; }
            learner.Learning.Remove(pick.Id);
            var home = Region.BandOf(learner) ?? this;
            bool newToBand = !home.Band.AnyoneKnows(pick.Id);
            learner.Knows.Add(pick.Id);
            var teacherBand = Region.BandOf(teacher);
            if (newToBand && teacherBand != null && teacherBand != home)
                home.Chronicle.Write(Clock, Importance.Major, $"{learner.Name} learned {pick.Name.ToLowerInvariant()} from {teacher.Name} of the {teacherBand.Band.Name} band.");
            else
                home.Chronicle.Write(Clock, Importance.Detail, $"{teacher.Name} showed {learner.Name} {pick.Name.ToLowerInvariant()}.");
        }

        /// <summary>How far a craft can be learned by hearing about it alone.</summary>
        const double TalkCeiling = 0.4;
    }
}
