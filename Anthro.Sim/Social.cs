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
    /// The social fabric: households that own their food, asking for food and being refused, trust
    /// and fondness that grow and fade, theft and fights when times are desperate, and bands that
    /// split along the lines of who likes and trusts whom.
    /// </summary>
    public sealed partial class Simulation
    {
        int _nextHouseholdId;

        // ---- Households ------------------------------------------------------------------------

        public Household NewHousehold(params Person[] people)
        {
            var h = new Household(_nextHouseholdId++);
            Band.Households.Add(h);
            foreach (var p in people) MoveInto(p, h);
            return h;
        }

        /// <summary>
        /// Moves someone into a household. Their own young children who lived with them come too;
        /// if they leave a hearth empty, its food comes with them.
        /// </summary>
        public void MoveInto(Person p, Household h, bool withDependents = true)
        {
            var old = p.Home;
            if (old == h) return;
            if (old != null) old.Members.Remove(p);
            h.Members.Add(p);
            p.Home = h;
            if (withDependents && old != null)
                foreach (var c in p.Children.Where(c => c.Alive && c.Home == old && c.AgeYears(Clock) < 14).ToList())
                    MoveInto(c, h, false);
            if (old != null && old.Members.Count == 0)
            {
                MoveFood(old.Food, h.Food);
                Band.Households.Remove(old);
            }
        }

        /// <summary>Takes someone out of their household (death or departure). Children left with no adult are taken in by others.</summary>
        void LeaveHousehold(Person p)
        {
            var h = p.Home;
            if (h == null) return;
            h.Members.Remove(p);
            p.Home = null;
            if (h.Members.Count > 0 && !h.Adults(Clock).Any())
                foreach (var orphan in h.Members.ToList()) Adopt(orphan);
            if (h.Members.Count == 0)
            {
                // The food of an emptied hearth goes to whoever was closest to its last member.
                var heir = Band.Households.Where(o => o != h)
                    .OrderByDescending(o => o.Members.Sum(m => p.GetAffinity(m))).FirstOrDefault();
                if (heir != null) MoveFood(h.Food, heir.Food);
                Band.Households.Remove(h);
            }
        }

        /// <summary>
        /// An orphan is taken in: by grandparents, grown siblings, aunts and uncles first, otherwise by
        /// the warm-hearted adult who's fondest of them.
        /// </summary>
        void Adopt(Person child)
        {
            double Kinship(Person a)
            {
                if (child.Mother?.Mother == a || child.Mother?.Father == a || child.Father?.Mother == a || child.Father?.Father == a) return 3;
                if (child.Mother != null && child.Mother.Children.Contains(a)) return 2.5;
                if (child.Father != null && child.Father.Children.Contains(a)) return 2.5;
                var auntsUncles = new[] { child.Mother?.Mother, child.Mother?.Father, child.Father?.Mother, child.Father?.Father }
                    .Where(g => g != null).SelectMany(g => g!.Children);
                return auntsUncles.Contains(a) ? 2 : 0;
            }
            var carer = Band.Members.Where(a => a != child && a.Home != null && Grown(a))
                .OrderByDescending(a => Kinship(a) + a.GetAffinity(child) + a.M(MindTrait.Warmth) * 0.5).FirstOrDefault();
            if (carer == null) return;
            MoveInto(child, carer.Home!, false);
            carer.AddAffinity(child, 0.2);
            child.AddAffinity(carer, 0.2);
            Chronicle.Write(Clock, Importance.Detail, $"{carer.Name} took in {child.Name}.");
        }

        /// <summary>Food carried home goes into the family's store.</summary>
        internal static void Unpack(FoodStore pack, FoodStore home) => MoveFood(pack, home);

        static void MoveFood(FoodStore from, FoodStore to)
        {
            to.MeatAge = to.Meat + from.Meat <= 0 ? 0 : (to.Meat * to.MeatAge + from.Meat * from.MeatAge) / (to.Meat + from.Meat);
            to.Meat += from.Meat;
            to.AnimalIn += from.AnimalIn; to.PlantIn += from.PlantIn;
            to.Plants += from.Plants;
            to.Stored += from.Stored;
            to.Cooked += from.Cooked;
            to.Dried += from.Dried;
            from.Meat = from.Plants = from.Stored = from.Cooked = from.Dried = 0;
        }

        static void MoveFood(Household from, Household to, double amount, out double meat)
        {
            double taken = from.Food.Take(amount, out meat);
            to.Food.AddMeat(meat);
            to.Food.AddPlants(taken - meat, keeps: false);
        }

        /// <summary>A person's household, making one if somehow they have none.</summary>
        public Household HomeOf(Person p) => p.Home ?? NewHousehold(p);

        // ---- Bonds over time -------------------------------------------------------------------

        /// <summary>
        /// Fondness fades without time together (toward a floor that's higher for family), and trust
        /// drifts back toward no-opinion. Daily time together is what keeps bonds alive.
        /// </summary>
        void DailyBonds()
        {
            // No child is ever left alone at a hearth: someone always takes them in.
            foreach (var h in Band.Households.Where(h => h.Members.Count > 0 && !h.Adults(Clock).Any()).ToList())
                foreach (var orphan in h.Members.ToList()) Adopt(orphan);
            UpdateDiscontent();
            DailySociety();
            var known = new Dictionary<int, Person>();
            foreach (var m in Band.Members) known[m.Id] = m;
            foreach (var p in Band.Members)
            {
                foreach (var id in p.Affinity.Keys.ToList())
                {
                    double floor = known.TryGetValue(id, out var o) && p.IsKin(o) ? 0.4 : 0.05;
                    double v = p.Affinity[id];
                    if (v > floor) p.Affinity[id] = v - (v - floor) * 0.003;
                }
                foreach (var id in p.Trust.Keys.ToList())
                    p.Trust[id] += (0.5 - p.Trust[id]) * 0.002;
            }
        }

        /// <summary>Discontent drifts toward today's hardship, crowding and distrust, over roughly a season.</summary>
        void UpdateDiscontent()
        {
            foreach (var p in Band.Members)
                if (Grown(p))
                {
                    // Shared rites hold a band together.
                    double cohesion = (Band.AnyoneKnows("lang.ritual") ? 0.8 : 1) * (Band.AnyoneKnows("burial.rites") ? 0.9 : 1);
                    p.Discontent += ((_hardship + Crowding(p) + Overwork(p) + Strife(p)) * cohesion - p.Discontent) * 0.012;
                }
        }

        /// <summary>How much of the camp this person distrusts (0 when everyone is trusted or unknown).</summary>
        double Strife(Person p)
        {
            double sum = 0; int n = 0;
            foreach (var o in Band.Members)
            {
                if (o == p || o.AgeYears(Clock) < 14) continue;
                sum += Math.Max(0, 0.5 - p.GetTrust(o)) * 2;
                n++;
            }
            // Feuds with people here make a person want to be elsewhere: how foragers mostly end them.
            double grudges = 0;
            foreach (var kv in p.Grievance) grudges += kv.Value;
            return (n == 0 ? 0 : sum / n) + grudges / (grudges + 6);
        }

        /// <summary>Not off in the field or away visiting: about camp, where people see what goes on.</summary>
        internal static bool AtCamp(Person p) => !(p.Current is Forage || p.Current is Hunt || p.Current is GatherFlint || p.Current is GatherWood || p.Current is Visit || p.Current is Fish || p.Current is Trap);

        static double Norm(Person p, string name, double fallback) => p.Expect.Get(Expectations.Norm(name), fallback);

        // ---- Splitting and leaving -----------------------------------------------------------------

        /// <summary>
        /// A band too small to make it on its own looks for a band its people know of nearby to join
        /// (checked weekly; more readily in hard times). This is how families that scattered in a
        /// lean season come back together.
        /// </summary>
        internal void SeekCompany()
        {
            var adults = Band.Members.Where(Grown).ToList();
            if (adults.Count == 0 || adults.Count >= adults.Max(Needed)) return;
            // Camps they've seen or heard of, and their own people's camps (one always knows roughly
            // where one's own people are ranging).
            var known = adults.SelectMany(a => a.Map.Camps).Select(kv => kv.Key)
                .Concat(Tribe == null ? Enumerable.Empty<int>() : Region.CampsOf(Tribe).Select(c => c.Id)).Distinct()
                .Select(id => Region.BandById(id)).Where(b => b != null && b != this && !b.Extinct
                    && WorldMap.Distance(b.Band.CampX, b.Band.CampY, Band.CampX, Band.CampY) < Region.KnownRange)
                .OrderBy(b => WorldMap.Distance(b!.Band.CampX, b.Band.CampY, Band.CampX, Band.CampY)).Take(2).ToList();
            foreach (var b in known) { if (Extinct) return; ConsiderJoining(b!); }
        }

        /// <summary>
        /// When life in the band has become hard (a long hungry stretch, a camp full of near-strangers,
        /// or a camp full of people they distrust), someone may propose leaving. Households decide
        /// whole: each weighs relief against the bonds they'd leave behind. Each adult also judges
        /// whether the leaving group is big enough for them to risk it (the bold accept smaller
        /// groups). A bold person may strike out with just their own family to join a neighboring
        /// band. Those who leave become a band of their own.
        /// </summary>
        /// <summary>Each adult's sense of how many grown people a group needs to make it on its own (the bold accept fewer).</summary>
        static double Needed(Person a) => 2 + (1 - a.M(MindTrait.Boldness)) * 6;

        void ConsiderSplitting(Person proposer)
        {
            double hardship = _hardship;
            // Only discontent that has built up over a season or more leads anyone to leave.
            if (proposer.Discontent < 0.3 || proposer.AgeYears(Clock) < 20 || proposer.Home == null) return;
            if (Clock.Day - proposer.LastSplitProposalDay < SimClock.DaysPerSeason) return;
            // Someone who has only just come to this band gives it a year before leaving again.
            if (Clock.Day - proposer.LeftDay < SimClock.DaysPerYear) return;
            proposer.LastSplitProposalDay = Clock.Day;

            var going = new List<Household> { proposer.Home };
            IEnumerable<Person> Going() => going.SelectMany(h => h.Members);
            IEnumerable<Person> Staying() => Band.Members.Where(m => !going.Contains(m.Home!));
            // A feud of one's own is settled the forager way: one family moves to another band. Only
            // what weighs on everyone (hunger, long days, a camp of near-strangers) splits a band.
            bool personal = Strife(proposer) > hardship + Crowding(proposer) + Overwork(proposer);

            foreach (var h in Band.Households.Where(h => h != proposer.Home && !personal).ToList())
            {
                var head = h.Adults(Clock).OrderByDescending(a => a.M(MindTrait.Charisma) + a.AgeYears(Clock) / 60).FirstOrDefault();
                if (head == null) continue;
                double relief = head.Discontent * 8 * (0.5 + head.M(MindTrait.Boldness));
                if (relief + Ties(head, Going()) > Ties(head, Staying())) going.Add(h);
            }

            bool changed = true;
            while (changed)
            {
                changed = false;
                int adults = Going().Count(m => Grown(m));
                foreach (var h in going.Where(h => h != proposer.Home).ToList())
                    if (h.Adults(Clock).All(a => adults < Needed(a))) { going.Remove(h); changed = true; }
            }

            int goingAdults = Going().Count(m => Grown(m));
            int stayingAdults = Staying().Count(m => Grown(m));
            if (stayingAdults < 2) return;
            // With raiders about, a small group on its own is easy prey: it takes more people to dare it.
            double needed = Needed(proposer) * (1 + Threat);

            if (goingAdults < needed)
            {
                // Too few to make a band of their own. Someone may instead take their household away:
                // to a band they know where they'd be better off (people they're tied to, or a band
                // faring better). In hard times anyone might; otherwise only the bold, who may even
                // strike out on their own. (Hard times scatter families to their kin elsewhere; they
                // don't found new bands of three.)
                bool desperate = hardship >= 0.6;
                if (going.Count == 1 && (proposer.M(MindTrait.Boldness) > 0.6 || desperate || personal))
                {
                    var family = Going().ToList();
                    // Weighed like joining a split: the relief of getting away, plus what's waiting
                    // there, against the bonds they'd leave behind.
                    double relief = proposer.Discontent * 8 * (0.5 + proposer.M(MindTrait.Boldness));
                    double leaving = Ties(proposer, Staying());
                    var join = Region.Bands.Where(b => b != this && b.Band.Members.Count > 0
                                                       && WorldMap.Distance(b.Band.CampX, b.Band.CampY, Band.CampX, Band.CampY) < Region.KnownRange)
                        .Select(b => (band: b, value: Ties(proposer, b.Band.Members) + (b.Prospects - Prospects) * 4))
                        .OrderByDescending(x => x.value).FirstOrDefault();
                    if (relief + Math.Max(0, join.value) <= leaving) return;
                    if (join.band != null && join.value > 0)
                    {
                        Region.MoveHouseholds(going, this, join.band);
                        Chronicle.Write(Clock, Importance.Notable, family.Count == 1
                            ? $"{proposer.Name} left to join the {join.band.Band.Name} band."
                            : $"{proposer.Name} took their family of {family.Count} to join the {join.band.Band.Name} band.");
                        join.band.Chronicle.Write(Clock, Importance.Notable, $"{proposer.Name} of the {Band.Name} band came to live with the band{(family.Count > 1 ? $", bringing {family.Count - 1} more" : "")}.");
                    }
                    // The bold may strike out alone or with their family (rare, and seldom for long: a lone
                    // wanderer soon looks for a camp to join, see SeekCompany).
                    else if (proposer.M(MindTrait.Boldness) > 0.6 && !desperate)
                    {
                        var site = ChooseCampSite(proposer, WorldMap.FromMeters(3000), WorldMap.FromMeters(12000));
                        var alone = Region.NewBand();
                        alone.SettleNew(this, site.X, site.Y, proposer);
                        Region.MoveHouseholds(going, this, alone);
                        Chronicle.Write(Clock, Importance.Notable, family.Count == 1
                            ? $"{proposer.Name} struck out alone."
                            : $"{proposer.Name} struck out on their own with their family of {family.Count}.");
                        alone.Chronicle.Write(Clock, Importance.Major, $"{proposer.Name} left the {Band.Name} band and made camp alone{(family.Count > 1 ? $" with {family.Count - 1} more" : "")}.");
                    }
                }
                return;
            }

            var newSite = ChooseCampSite(proposer, WorldMap.FromMeters(4000), WorldMap.FromMeters(15000));
            var band = Region.NewBand();
            band.SettleNew(this, newSite.X, newSite.Y, proposer);
            var people = Going().ToList();
            // The dogs go with their people, about as many as belong to the leaving households.
            int dogs = Band.Members.Count == 0 ? 0 : (int)Math.Round(Band.Dogs * people.Count / (double)Band.Members.Count);
            Band.Dogs -= dogs; band.Band.Dogs = dogs; band.Band.DogsFed = Band.DogsFed;
            Region.MoveHouseholds(going, this, band);
            string why = Overwork(proposer) > hardship && Overwork(proposer) > Crowding(proposer) ? "Long days of work" : hardship >= Crowding(proposer) ? "Hard times" : "A crowded camp";
            Chronicle.Write(Clock, Importance.Major,
                $"{why} split the camp: {proposer.Name} led {people.Count} people off to make a camp of their own.");
            band.Chronicle.Write(Clock, Importance.Major,
                $"{why} split the {Band.Name} camp: {proposer.Name} led {people.Count} people off to make a camp of their own.");
        }

        /// <summary>
        /// A band starting out: people who split off from <paramref name="parent"/>. They carry its
        /// customs and tastes with them, and their share of its tools.
        /// </summary>
        internal void SettleNew(Simulation parent, double campX, double campY, Person? leader = null)
        {
            // A new camp of the same people, named for whose fire it gathers around.
            Tribe = parent.Tribe;
            LastKinContact = parent.LastKinContact = Clock.Day;
            Band.Name = CampName(leader);
            Array.Copy(parent.Band.AttractionMind, Band.AttractionMind, Band.AttractionMind.Length);
            Array.Copy(parent.Band.AttractionBody, Band.AttractionBody, Band.AttractionBody.Length);
            Band.ResidenceNorm = parent.Band.ResidenceNorm;
            Band.ComingOfAge = parent.Band.ComingOfAge;
            Band.ChildrenFollowMother = parent.Band.ChildrenFollowMother;
            Band.CampX = campX; Band.CampY = campY;
            _campSince = Clock.Day;
        }

        /// <summary>
        /// People leaving this band. They're taken out of their hearths first, so no child is briefly
        /// "orphaned"; a hearth they empty goes with them, food and all. Anyone left at a hearth with
        /// no adult is taken in.
        /// </summary>
        internal FoodStore Leave(List<Person> people)
        {
            var carried = new FoodStore();
            foreach (var p in people)
            {
                if (p.Home != null) p.Home.Members.Remove(p);
                p.Home = null;
            }
            foreach (var h in Band.Households.Where(h => h.Members.Count == 0).ToList())
            {
                MoveFood(h.Food, carried);
                Band.Households.Remove(h);
            }
            RemoveMembers(people);
            foreach (var h in Band.Households.Where(h => h.Members.Count > 0 && !h.Adults(Clock).Any()).ToList())
                foreach (var orphan in h.Members.ToList()) Adopt(orphan);
            return carried;
        }

        void RemoveMembers(List<Person> people)
        {
            foreach (var p in people)
            {
                p.LeftDay = Clock.Day;
                Band.Members.Remove(p);
                p.Current = null;
            }
        }

        /// <summary>People arriving from another band: they come to camp and make a hearth (with their partner, if they're joining one).</summary>
        internal void Welcome(List<Person> people, FoodStore carried, Person? partner)
        {
            foreach (var p in people)
            {
                p.X = Band.CampX; p.Y = Band.CampY;
                p.LeftDay = Clock.Day;
                p.Current = null;
                // Discontent is about life in a band; the grievances were with the people they left.
                p.Discontent = 0;
                Band.Members.Add(p);
            }
            var hearth = partner != null ? NewHousehold(new[] { partner }.Concat(people).ToArray()) : NewHousehold(people.ToArray());
            MoveFood(carried, hearth.Food);
        }

        /// <summary>Whole households leave, hearths and all.</summary>
        internal void LetHouseholdsGo(List<Household> homes, List<Person> people)
        {
            foreach (var h in homes) Band.Households.Remove(h);
            RemoveMembers(people);
        }

        /// <summary>Whole households arrive, hearths and all.</summary>
        internal void TakeInHouseholds(List<Household> homes, List<Person> people)
        {
            foreach (var h in homes) Band.Households.Add(h);
            foreach (var p in people)
            {
                p.X = Band.CampX; p.Y = Band.CampY;
                p.LeftDay = Clock.Day;
                p.Current = null;
                // Discontent is about life in a band; the grievances were with the people they left.
                p.Discontent = 0;
                Band.Members.Add(p);
            }
            if (Band.Name.Length > 0 && Band.Members.Count == people.Count)
                Chronicle.Write(Clock, Importance.Major,
                    $"The {Band.Name} band, {Band.Members.Count} {(Band.Members.Count == 1 ? "person" : "people")}, made camp in the {World[(int)Band.CampX, (int)Band.CampY].Terrain.ToString().ToLower()}.");
            RefreshForagePatches();
        }

        /// <summary>
        /// Two bands camped together. Anyone who has lived here at least a year may decide their
        /// family would be better off with the other band, the same way anyone weighs where to live:
        /// the people they'd be with, and how well each band is doing.
        /// </summary>
        internal void ConsiderJoining(Simulation other)
        {
            foreach (var p in Band.Members.Where(p => p.Alive && Grown(p)).ToList())
            {
                if (!Band.Members.Contains(p) || Clock.Day - p.LeftDay < SimClock.DaysPerYear || other.Extinct) continue;
                var family = Band.Members.Where(o => o == p || p.Spouses.Contains(o) || (p.Children.Contains(o) && o.AgeYears(Clock) < 14)).ToList();
                double here = Ties(p, Band.Members.Except(family));
                // A group too small to hunt together and carry each other through bad times is a hard
                // place to be when times are bad (in good times a small camp does fine).
                int adultsHere = Band.Members.Count(Grown), adultsThere = other.Band.Members.Count(Grown);
                double shortHere = Math.Max(0, Needed(p) - adultsHere) / Needed(p), shortThere = Math.Max(0, Needed(p) - adultsThere - 1) / Needed(p);
                // Living alone is hard in any season: no one to share a kill with, keep watch or marry
                // (authored weight: it outweighs a camp being somewhat hungrier than one's own fire).
                double alone = adultsHere <= 1 ? 2 : 0;
                // With raiders about, there's safety in numbers.
                double safety = Math.Max(Threat, other.Threat) * (adultsThere - adultsHere) / 10.0;
                double there = Ties(p, other.Band.Members) + (other.Prospects - Prospects) * 4 + (shortHere - shortThere) * _hardship * 3 + alone + safety
                               - (other.Tribe != Tribe ? 1 : 0);   // strangers' ways and speech
                if (there <= here * 1.3 + 0.5) continue;
                Region.Move(family, this, other);
                Chronicle.Write(Clock, Importance.Notable, family.Count == 1
                    ? $"{p.Name} left to live with the {other.Band.Name} band."
                    : $"{p.Name} took their family of {family.Count} to live with the {other.Band.Name} band.");
                other.Chronicle.Write(Clock, Importance.Notable, family.Count == 1
                    ? $"{p.Name} of the {Band.Name} band joined the band."
                    : $"{p.Name} of the {Band.Name} band joined the band, bringing {family.Count - 1} more.");
            }
        }
    }
}
