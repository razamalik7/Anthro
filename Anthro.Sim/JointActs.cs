using System;
using System.Collections.Generic;
using System.Linq;
using Anthro.Sim.Activities;
using Anthro.Sim.Core;
using Anthro.Sim.People;
using Anthro.Sim.World;

namespace Anthro.Sim
{
    /// <summary>
    /// Acts no one can do alone. Someone invites others to join; each person invited weighs it
    /// for themselves (what they'd gain, their anger, their bond with whoever is asking, what such
    /// things have brought before, guilt and fear). Those on the other side decide whether to stand
    /// together too. What happens is physical, and every blow and every take is a deed like any
    /// other: witnessed, remembered, gossiped and learned from.
    /// </summary>
    public sealed partial class Simulation
    {
        // Per-hour caches for decisions (people decide many times an hour; who's grown at a hearth and how
        // strong they are doesn't change within it).
        long _cacheTick = -1;
        readonly Dictionary<Household, (List<Person> adults, double might)> _adultsNow = new Dictionary<Household, (List<Person>, double)>();
        readonly Dictionary<int, Person> _byId = new Dictionary<int, Person>();

        void FreshCaches()
        {
            if (_cacheTick == Clock.Tick) return;
            _cacheTick = Clock.Tick;
            _adultsNow.Clear();
            _byId.Clear();
            foreach (var m in Band.Members) _byId[m.Id] = m;
        }

        /// <summary>A hearth's grown members this hour, and how hard they'd fight together.</summary>
        internal (List<Person> adults, double might) AdultsNow(Household h)
        {
            FreshCaches();
            if (!_adultsNow.TryGetValue(h, out var v))
            {
                var a = h.Adults(Clock).ToList();
                v = (a, a.Sum(Might));
                _adultsNow[h] = v;
            }
            return v;
        }

        /// <summary>A member of this camp by id (this hour), or null.</summary>
        internal Person? MemberById(int id) { FreshCaches(); return _byId.TryGetValue(id, out var p) && Band.Members.Contains(p) ? p : null; }

        /// <summary>How hard someone would fight: strength, health, wounds, temper.</summary>
        double Might(Person p) => Strength(p) * (0.6 + 0.4 * p.M(MindTrait.Temper));

        /// <summary>How a person feels about a household: their anger at its adults, and their fondness.</summary>
        double AngerAt(Person p, Household h) => h.Adults(Clock).Select(a => p.GetGrievance(a)).DefaultIfEmpty(0).Max();

        /// <summary>
        /// What going with others to take a household's food by force is worth to this person:
        /// their family's hunger against that hearth's plenty, their anger at it, and what seen
        /// takes have brought them, against guilt and fear. Same scale as their other options.
        /// </summary>
        public double RallyValue(Person p, Household hearth) => RallyValue(p, hearth, FamilyHunger(p), joining: true);

        /// <summary>How hungry the hungriest of someone's family is.</summary>
        internal static double FamilyHunger(Person p) => 1 - (p.Home?.Members.Select(m => m.Satiety).DefaultIfEmpty(1).Min() ?? p.Satiety);

        internal double RallyValue(Person p, Household hearth, double hunger, bool joining = false)
        {
            if (p.Home == hearth) return double.NegativeInfinity;
            var rel = RelationTo(p, hearth);
            var (adults, defenders) = AdultsNow(hearth);
            Person? owner = null;
            double fondest = double.NegativeInfinity, anger = 0;
            foreach (var a in adults)
            {
                double f = p.GetAffinity(a);
                if (f > fondest) { fondest = f; owner = a; }
                double g = p.GetGrievance(a);
                if (g > anger) anger = g;
            }
            double plenty = Math.Min(1, Spare(hearth) / Math.Max(1, (p.Home?.Members.Count ?? 1) * 2));
            // It takes a reason: anger at them, or hunger at home with food over there. What taking has
            // brought before only counts for as much as there's a reason to want it again (someone full
            // and at peace with the family has no cause to rob them, however well it went last time).
            double motive = anger / (anger + 3) + hunger * plenty;
            bool off = Off("rallymotive");
            double learned = (Felt(Expects(p, Act.Take, rel, true)) + GodActValue(p, Act.Take)) * (off ? 1 : Math.Min(1, motive * 2));
            // Joining a gang is taking sides: some peoples close ranks readily, others keep out of it.
            double sides = off || joining ? (Sides(p) - 0.4) * 0.5 : 0;
            return motive * (0.3 + p.M(MindTrait.Temper)) + learned + sides
                   - (owner == null ? 0 : Felt(Guilt(p, owner)) * 0.5) - RallyFear(p, defenders);
        }

        /// <summary>
        /// Fear of taking on a family: how strong they are next to oneself (as a defender weighs raiders),
        /// and one's own wounds, so a beating keeps people from trying again for a while.
        /// </summary>
        double RallyFear(Person p, double defenders)
        {
            if (Off("rallyfear")) return defenders * 0.1 * p.W(Drive.Safety);
            return defenders / Math.Max(0.1, Might(p)) * 0.25 * p.W(Drive.Safety) + p.Injury;
        }

        /// <summary>How much being asked by this person pulls someone along: fondness, trust, and following them before.</summary>
        static double Pull(Person p, Person leader) =>
            p.GetAffinity(leader) * 0.5 + (p.GetTrust(leader) - 0.5) + Following(p, leader) * 0.3;

        /// <summary>Joins a group act if it, plus being asked, beats the best other thing they'd do now.</summary>
        bool Joins(Person p, Person leader, double value, Verb verb)
        {
            if (!Grown(p) || p.Health < 0.4 || !AwakeInCamp(p) || double.IsNegativeInfinity(value)) return false;

            double nudgeJoin = (!Off("god") && !Off("nudge")) ? Math.Max(p.ConsumeNudgeWeight(verb == Verb.Rally ? "rally" : "raid", 0.5, "join", "yes"), p.ConsumeNudgeWeight("raid", 0.5, "join", "yes")) : 0.0;
            bool willJoin = value + Pull(p, leader) + Rng.Normal(0, 0.1) + nudgeJoin > Decider.BestAlternative(this, p, verb);
            if (willJoin && !Off("god"))
            {
                Region.GodEvents.Add(new GodEvent(Clock.Tick, "ForkRaid", $"{p.Name} decided to join {leader.Name}'s {verb}", (int)Band.CampX, (int)Band.CampY));
            }
            return willJoin;
        }

        /// <summary>
        /// Does this person stand with a household that's being raided, or stay out of it? Their bond
        /// with the household (their own hearth most of all) and anger at the raiders, against fondness
        /// for the raiders and fear of them.
        /// </summary>
        bool Defends(Person p, Household hearth, List<Person> raiders)
        {
            if (raiders.Contains(p) || !Grown(p) || p.Health < 0.4 || !AwakeInCamp(p)) return false;
            double bond = p.Home == hearth ? 1.5 * p.W(Drive.Family)
                : hearth.Adults(Clock).Select(a => (p.IsKin(a) ? 0.8 : 0) + p.GetAffinity(a)).DefaultIfEmpty(0).Max();
            double angerAtRaiders = raiders.Select(r => p.GetGrievance(r)).DefaultIfEmpty(0).Max();
            double fondOfRaiders = raiders.Select(r => p.GetAffinity(r) + (p.IsKin(r) ? 0.8 : 0)).DefaultIfEmpty(0).Max();
            // Facing a crowd of camp-mates come for the food, most people hand it over and nurse the grudge.
            double fear = raiders.Sum(Might) / Math.Max(0.1, Might(p)) * 0.25 * p.W(Drive.Safety);
            return bond + angerAtRaiders / (angerAtRaiders + 3) - fondOfRaiders * 0.7 + p.M(MindTrait.Boldness) * 0.3 - fear + Rng.Normal(0, 0.1) > 0;
        }

        /// <summary>
        /// Someone rallies others to take a household's food by force. Those who join go together;
        /// the household and whoever stands with it resist. The stronger side carries it. Every
        /// blow is a deed; if the raiders prevail, each takes a share of what they can carry.
        /// </summary>
        public void DoRally(Person leader, Household hearth)
        {
            leader.LastRallyDay = Clock.Day;
            var raiders = new List<Person> { leader };
            foreach (var p in Band.Members.ToList())
                if (p != leader && Joins(p, leader, RallyValue(p, hearth), Verb.Rally)) raiders.Add(p);
            if (raiders.Count < 2)
            {
                // Nobody would come: the leader learns that rallying doesn't work here.
                Learn(leader, Lesson(Act.Take, RelationTo(leader, hearth), true), 0, CulturalDefault(Act.Take, RelationTo(leader, hearth), true), 0.3);
                Chronicle.Write(Clock, Importance.Detail, $"{leader.Name} tried to rally others against {hearth.Adults(Clock).FirstOrDefault()?.Name ?? "a family"}'s hearth; no one would go.");
                return;
            }
            var defenders = Band.Members.Where(p => Defends(p, hearth, raiders)).ToList();
            ThisYear.Raids++;

            // Nobody stands in their way: they simply take it. Otherwise it comes to a fight.
            bool prevailed = defenders.Count == 0 || Fight(raiders, defenders, null, overFood: true, inCamp: true, kind: "gang").Winner == 1;

            string owner = hearth.Adults(Clock).FirstOrDefault()?.Name ?? "a family";
            if (prevailed)
            {
                // Each raider carries off a share of what's there.
                foreach (var r in raiders.Where(r => r.Alive && Band.Members.Contains(r)))
                {
                    DoTake(r, hearth, Physiology.CarryCapacity(r, r.AgeYears(Clock)));
                    // Having taken what they came for, the anger that drove them cools.
                    foreach (var a in hearth.Adults(Clock)) r.AddGrievance(a, -r.GetGrievance(a) * 0.6);
                }
                Chronicle.Write(Clock, Importance.Major,
                    $"{leader.Name} led {raiders.Count - 1} others to take {owner}'s food by force{(defenders.Count > 0 ? $", over {defenders.Count} who stood against them" : "")}.");
            }
            else
            {
                foreach (var r in raiders.Where(r => r.Alive))
                    Learn(r, Lesson(Act.Take, RelationTo(r, hearth), true), -2, CulturalDefault(Act.Take, RelationTo(r, hearth), true), 0.5);
                Chronicle.Write(Clock, Importance.Major, $"{leader.Name} and {raiders.Count - 1} others tried to take {owner}'s food and were driven off.");
            }
            // The raided remember everyone who came.
            foreach (var a in hearth.Members)
                foreach (var r in raiders) a.AddGrievance(r, 2);
        }

        // ---- Between bands ----------------------------------------------------------------------

        /// <summary>
        /// What raiding another band is worth to this person: their anger at it and what fighting
        /// outsiders has brought, how much they care about standing; against how strong that band
        /// looks next to their own, and children at home. Same scale as their other options.
        /// </summary>
        /// <summary>How someone feels about another camp as a whole: by the relation they have to most of its grown people.</summary>
        Relation CampRelation(Person p, Simulation camp)
        {
            var adults = camp.Band.Members.Where(camp.Grown).ToList();
            if (adults.Count == 0) return Relation.Neutral;
            return adults.GroupBy(o => p.IsKin(o) ? Relation.Kin : p.GetTrust(o) < 0.35 ? Relation.Distrusted
                                          : p.GetAffinity(o) > 0.3 ? Relation.Friend : Relation.Neutral)
                         .OrderByDescending(g => g.Count()).First().Key;
        }

        public double RaidValue(Person p, Simulation foe)
        {
            double anger = p.GetBandGrievance(foe.Id);
            int kids = p.Children.Count(c => c.Alive && c.AgeYears(Clock) < 10);
            double odds = foe.Band.Members.Count(foe.Grown) / (double)Math.Max(1, Band.Members.Count(m => Grown(m)));
            // Going against a camp of one's own people weighs like fighting people one knows, by how one
            // has come to feel about them: easier against a camp one has learned to distrust, hard against
            // one full of friends and kin. Against another people, it's fighting outsiders.
            var rel = foe.Tribe != Tribe ? Relation.Outsider : CampRelation(p, foe);
            return anger / (anger + 4) * (0.3 + p.M(MindTrait.Temper)) * 1.5 + Felt(Expects(p, Act.Attack, rel))
                   + GodActValue(p, Act.Attack) + p.Drives[(int)Drive.Standing] * 0.2
                   - Math.Min(1.5, odds) * (0.3 + kids * 0.1) * (1.2 - p.M(MindTrait.Boldness) * 0.6) * p.W(Drive.Safety);
        }

        /// <summary>
        /// Someone calls for a raid on another band. Each person weighs going: their own anger at
        /// that band, their bond with whoever is calling, what fighting outsiders has brought, the
        /// band's valor and peace, young children at home. If enough go, they fall on the other camp
        /// at first light; whoever wakes in time defends their hearths. Every blow is a deed, felt
        /// and remembered by the people it lands on, and the raided band's anger turns on this band.
        /// </summary>
        public void DoWarRaid(Person leader, Simulation target)
        {
            var party = Band.Members.Where(p => p == leader || p.Health >= 0.5 && Joins(p, leader, RaidValue(p, target), Verb.Raid)).ToList();
            if (party.Count < 3)
            {
                Chronicle.Write(Clock, Importance.Detail, $"{leader.Name} called for a raid on the {target.Band.Name}; too few would go.");
                return;
            }
            // They go where the leader thinks the camp is; if it has moved on, they find it empty.
            if (leader.Map.Camps.TryGetValue(target.Id, out var thought)
                && WorldMap.Distance(thought.x, thought.y, target.Band.CampX, target.Band.CampY) > WorldMap.FromMeters(3000))
            {
                foreach (var r in party) { r.Map.Camps.Remove(target.Id); r.Fatigue = Inheritance.Clamp01(r.Fatigue + 0.3); }
                Chronicle.Write(Clock, Importance.Notable, $"{leader.Name} led a raid on the {target.Band.Name}, but their camp had moved on.");
                return;
            }
            ThisYear.Raids++;
            // First light: only some of the other camp wake in time to stand.
            // Who wakes in time: anyone already up, and the rest by chance, likelier with dogs in camp
            // and a bigger, noisier party.
            double wake = 0.35 + Math.Min(0.4, target.Band.Dogs * 0.05) + Math.Min(0.25, party.Count * 0.03);
            var defenders = target.Band.Members.Where(d => target.Grown(d) && d.Health > 0.3 && !(d.Current is Visit || d.Current is Hunt)
                                                           && (AwakeInCamp(d) || Rng.Chance(wake))).ToList();
            // With no one standing, the raiders fall on whoever is there, who can only run.
            var pool = defenders.Count > 0 ? defenders : target.Band.Members.Where(m => m.AgeYears(Clock) >= 10).ToList();
            bool won = pool.Count == 0 || target.Fight(party, pool, null, overFood: false, inCamp: false, kind: "raid").Winner == 1;
            if (won)
                foreach (var r in party.Where(r => r.Alive))
                {
                    var hearth = target.Band.Households.Where(h => h.Food.Total > 1).OrderByDescending(h => h.Food.Total).FirstOrDefault();
                    if (hearth == null) break;
                    target.DoTake(r, hearth, Physiology.CarryCapacity(r, r.AgeYears(Clock)));
                }
            foreach (var r in party)
            {
                r.Fatigue = Inheritance.Clamp01(r.Fatigue + 0.4);
                // Revenge taken cools the anger (more if it went well).
                r.AddBandGrievance(target.Id, -r.GetBandGrievance(target.Id) * (won ? 0.6 : 0.3));
            }
            Chronicle.Write(Clock, Importance.Major, $"{leader.Name} led {party.Count - 1} others in a raid on the {target.Band.Name}{(won ? "" : "; they were driven off")}.");
            target.LastThreatDay = Clock.Day;
            target.Chronicle.Write(Clock, Importance.Major, $"The {Band.Name} fell on the camp at first light{(won ? " and carried off food" : " and were driven off")}.");
            Region.Chronicle.Write(Clock, Importance.Notable, won ? $"The {Band.Name} raided the {target.Band.Name}." : $"The {target.Band.Name} drove off a raid by the {Band.Name}.");
        }

        /// <summary>
        /// Two people of different bands run into each other on ground both bands forage. Each
        /// weighs it: anger at the other's band or at them, how hungry they are and how much they
        /// depend on this ground, temper, against fondness, guilt and a culture of peace. Usually
        /// they talk (news, know-how and sickness pass between them); sometimes it comes to blows.
        /// </summary>
        internal void MeetOnSharedGround(Person mine, Person other, Simulation theirs, int shared)
        {
            double Hostility(Person p, Person o, int oBand, Simulation pCamp)
            {
                double g = p.GetBandGrievance(oBand) + p.GetGrievance(o);
                double need = 1 - p.Satiety + (p.Home != null && p.Home.DaysOfFood < 2 ? 0.3 : 0);
                double reliance = Math.Min(1, shared / 10.0);
                return g / (g + 3) + need * reliance * 0.6 + p.M(MindTrait.Temper) * 0.15 - p.GetAffinity(o) - Felt(Guilt(p, o)) * 0.5
                       + Felt(Expects(p, Act.Attack, pCamp.RelationTo(p, o))) + Rng.Normal(0, 0.1);
            }
            // Against what talking would bring: fondness, curiosity and sociability, and fear of a fight.
            double Friendly(Person p, Person o) => p.GetAffinity(o) + p.M(MindTrait.Sociability) * 0.3 + p.M(MindTrait.Curiosity) * 0.2
                                                   + Math.Max(0, Strength(o) - Strength(p)) * 0.5 * p.W(Drive.Safety);
            double hMine = Hostility(mine, other, theirs.Id, this) - Friendly(mine, other), hTheirs = Hostility(other, mine, Id, theirs) - Friendly(other, mine);
            if (Math.Max(hMine, hTheirs) > 0)
            {
                if (hMine >= hTheirs) Fight(new[] { mine }, new[] { other }, null, overFood: true, inCamp: false, kind: "encounter");
                else theirs.Fight(new[] { other }, new[] { mine }, null, overFood: true, inCamp: false, kind: "encounter");
                return;
            }
            mine.AddAffinity(other, 0.03); other.AddAffinity(mine, 0.03);
            // They learn where each other's people are camped, and talk of the land.
            mine.Map.Camps[theirs.Id] = (theirs.Band.CampX, theirs.Band.CampY, Clock.Day);
            other.Map.Camps[Id] = (Band.CampX, Band.CampY, Clock.Day);
            TellPlaces(mine, other);
            TellPlaces(other, mine);
            Contact(mine, other, 1);
            SocialLearning.Exchange(mine, other, Rng);
            Gossip(mine, other);
            PassOn(mine, other);
            PassOn(other, mine);
        }
    }
}
