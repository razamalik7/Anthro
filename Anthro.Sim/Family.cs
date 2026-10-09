using System;
using System.Collections.Generic;
using System.Linq;
using Anthro.Sim.Activities;
using Anthro.Sim.Core;
using Anthro.Sim.People;

namespace Anthro.Sim
{
    /// <summary>
    /// Couples and leaders, from people. Jealousy is innate; whether a couple stays together, and
    /// whom people follow, comes from what each person feels and has learned.
    /// </summary>
    public sealed partial class Simulation
    {
        // ---- Jealousy and separation ----------------------------------------------------------------

        /// <summary>
        /// Someone with a partner warms to another person. If the partner is about and notices, they
        /// feel it: anger at the rival, and a little hurt toward their partner. Innate (mate guarding);
        /// what they do about it is their own choice, through the same confronting and fighting as anything else.
        /// </summary>
        public void Jealousy(Person p, Person other)
        {
            if (!p.Married || other.Sex == p.Sex || p.IsKin(other) || p.Spouses.Contains(other) || p.GetAffinity(other) < 0.3) return;
            foreach (var partner in p.Spouses)
            {
                if (!partner.Alive || !AwakeInCamp(partner) || !Rng.Chance(0.15)) continue;
                double sting = (p.GetAffinity(other) - 0.3) * (0.5 + partner.M(MindTrait.Temper)) * 2 * AtStake(partner, p);
                partner.AddGrievance(other, sting);
                partner.AddAffinity(p, -0.01 * sting);
            }
        }


        /// <summary>A season that left someone thin: they judge what their marriage brings, and the camp judges any plural marriage they're in.</summary>
        internal void AfterHungrySeason(Person p)
        {
            if (!p.Married || !Grown(p)) return;
            TakeStock(p, Band.Members.Where(Grown).ToList());
            if (p.Spouses.Count > 1 && p.Home != null) LearnFromPluralMarriage(p);
        }

        /// <summary>
        /// A child born in camp: a plural household it's born into is seen to prosper, and couples who've
        /// had none together in years are reminded of it.
        /// </summary>
        internal void AfterBirth(Person mother, Person father)
        {
            var hub = mother.Spouses.Count > 1 ? mother : father.Spouses.Count > 1 ? father : null;
            if (hub != null && hub.Home != null && Band.Members.Contains(hub)) LearnFromPluralMarriage(hub);
            List<Person>? adults = null;
            foreach (var p in Band.Members.Where(m => m.Married && m != mother && m != father).ToList())
            {
                if (!p.Spouses.Any(s => (Clock.Day - p.WedOn[s]) / (double)SimClock.DaysPerYear >= 4 && (p.Sex == Sex.Female ? p : s).AgeYears(Clock) < 40
                                        && !p.Children.Any(c => c.Alive && (c.Mother == s || c.Father == s)))) continue;
                adults ??= Band.Members.Where(Grown).ToList();
                TakeStock(p, adults);
            }
        }

        /// <summary>
        /// When something brings it home, people take stock of their marriage, as forager couples do: a
        /// hungry season makes a poor provider felt, and a child born to others reminds a childless couple. A
        /// partner who brings home much less than others of their sex, or years together with no
        /// children, wear on a bond (the commonest reasons forager marriages end, along with conflict,
        /// which is already in the bond itself). Whether it ends is weighed against the bond, the
        /// years, and young children at the hearth.
        /// </summary>
        void TakeStock(Person p, List<Person> adults)
        {
            foreach (var partner in p.Spouses.ToList())
            {
                if (!Band.Members.Contains(partner) || !p.Spouses.Contains(partner)) continue;
                var peers = adults.Where(a => a.Sex == partner.Sex && a != partner).ToList();
                double usual = peers.Count == 0 ? partner.Provided : peers.Average(a => a.Provided);
                double provides = usual <= 0 ? 0 : Math.Max(0, Math.Min(1, (usual - partner.Provided) / usual));
                double years = (Clock.Day - p.WedOn[partner]) / (double)SimClock.DaysPerYear;
                var woman = p.Sex == Sex.Female ? p : partner;
                bool childless = years >= 4 && woman.AgeYears(Clock) < 40
                                 && !p.Children.Any(c => c.Alive && (c.Mother == partner || c.Father == partner));
                double strain = provides * 0.8 + (childless ? 0.5 : 0);
                if (strain > 0.1) ConsiderLeaving(p, null, strain, partner);
            }
        }

        /// <summary>
        /// How much someone wants another as a partner: fondness and attraction (judged by their own
        /// band's tastes), and what they'd bring home compared with others of their sex (a good
        /// provider is wanted everywhere; who provides what is up to how each people lives).
        /// </summary>
        double Desire(Person p, Person o, Simulation judgesBy) =>
            p.GetAffinity(o) + judgesBy.Attraction(o) * 0.2 + Math.Max(0, Following(p, o)) * 0.2 - p.GetGrievance(o) * 0.15
            + (o.Knows.Contains("ochre.body") ? 0.05 : 0) + judgesBy.ProviderStanding(o) * 0.2;

        long _standingDay = -1;
        readonly double[] _usualProvided = new double[2];

        /// <summary>How much more (or less) than is usual for their sex someone brings home: -1 to 1.</summary>
        internal double ProviderStanding(Person o)
        {
            if (_standingDay != Clock.Day)
            {
                _standingDay = Clock.Day;
                foreach (Sex s in new[] { Sex.Female, Sex.Male })
                    _usualProvided[(int)s] = Band.Members.Where(m => m.Sex == s && Grown(m)).Select(m => m.Provided).DefaultIfEmpty(0).Average();
            }
            double usual = _usualProvided[(int)o.Sex];
            return usual <= 0 ? 0 : Math.Max(-1, Math.Min(1, (o.Provided - usual) / usual));
        }

        /// <summary>
        /// What someone stands to lose when their spouse takes up with another, from the body, not from
        /// custom: a man can't know a child is his, so sharing a wife risks raising another man's
        /// children (her children are the whole of what the marriage gives him in offspring); a woman's
        /// children are certainly hers, so what she risks is her husband's food and care going
        /// elsewhere, which matters as much as he provides.
        /// </summary>
        double AtStake(Person jealous, Person spouse) =>
            jealous.Sex == Sex.Male ? 1.0 : 0.5 + 0.5 * Math.Max(0, ProviderStanding(spouse) + 0.5);

        /// <summary>The learned custom: what taking another spouse is expected to bring, all told.</summary>
        internal const string PluralTopic = "wed:more";
        static double PluralView(Person p) => p.Expect.Get(PluralTopic, 0);

        /// <summary>Children someone had in the past year with these partners.</summary>
        int KidsLastYear(Person p, IEnumerable<Person> with) =>
            p.Children.Count(c => Clock.Day - c.BirthDay < SimClock.DaysPerYear && with.Any(s => c.Mother == s || c.Father == s));

        /// <summary>
        /// Someone married has grown close to someone unmarried. Taking them as another spouse is a
        /// choice like any other: wanted by both (the newcomer weighing them against everyone free, with
        /// their love shared), weighed against the spouses they have (fondness for them, and what each
        /// is expected to feel), and coloured by what each has come to expect such marriages bring.
        /// Nothing forbids or requires it; peoples come to their own custom by living with the results.
        /// Returns whether it happened.
        /// </summary>
        public bool ConsiderAnotherSpouse(Person p, Person o)
        {
            if (!p.Married || o.Married || o.Sex == p.Sex || p.IsKin(o) || !Grown(p) || !Grown(o) || !o.Alive
                || !Band.Members.Contains(p) || !Band.Members.Contains(o) || p.Home == null) return false;
            if (o.AgeYears(Clock) > (o.Sex == Sex.Female ? 45 : 60) || Math.Min(p.GetAffinity(o), o.GetAffinity(p)) < 0.3) return false;
            // No marrying a spouse's parent or child; sisters (or brothers) may share a spouse.
            if (p.Spouses.Any(s => s.IsKin(o) && !(s.Mother != null && s.Mother == o.Mother))) return false;
            // The newcomer: is this their best choice, with their spouse's love and hearth shared?
            // What sharing costs them is set by the body: a man who'd be one of several husbands can't
            // know which children are his; a woman who'd be one of several wives shares his food and care
            // (which a good provider has more of: that's in wanting him at all).
            double shared = p.Spouses.Count / (p.Spouses.Count + 1.0);
            double theirs = Desire(o, p, this) + PluralView(o) - shared * (o.Sex == Sex.Male ? 0.6 : 0.3);
            foreach (var other in Band.Members)
                if (other != p && CouldPair(o, other) && Desire(o, other, this) > theirs) return false;
            // The one already married: wanting them, against the spouses they'd hurt.
            double mine = Desire(p, o, this) + PluralView(p);
            foreach (var s in p.Spouses)
                mine -= Math.Max(0, p.GetAffinity(s)) * 0.5 * Math.Max(0, 0.5 - PluralView(s)) * (1 - Math.Max(0, s.GetAffinity(o))) * AtStake(s, p);
            if (mine < 0.5) return false;

            var before = p.Spouses.ToList();
            Person.Wed(p, o, Clock.Day);
            MoveInto(o, p.Home!);
            // Each spouse feels it as they've come to expect it: jealousy is innate, but softened by a
            // custom that approves and by fondness for the newcomer (a sister is easier to share with).
            foreach (var s in before)
            {
                double share = 1.0 / before.Count - 1.0 / (before.Count + 1);   // how much of the spouse they lose
                double sting = Math.Max(0, 0.5 - PluralView(s)) * (1 - Math.Max(0, s.GetAffinity(o))) * (0.5 + s.M(MindTrait.Temper))
                               * 8 * share * AtStake(s, p);
                s.AddGrievance(o, sting);
                s.AddGrievance(p, sting * 0.5);
                s.AddAffinity(p, -0.05 * sting);
                if (sting > 0.5) ConsiderLeaving(s, null, sting * 0.2, p);
            }
            string role = o.Sex == Sex.Female ? "wife" : "husband";
            string nth = before.Count == 1 ? "second" : before.Count == 2 ? "third" : "another";
            Chronicle.Write(Clock, Importance.Notable, $"{p.Name} took {o.Name} as a {nth} {role}.");
            return true;
        }

        /// <summary>
        /// Once a year people take stock of the plural marriages they live among: whether such a hearth
        /// is eating better or worse than the camp, how its children are doing, and how much bad blood
        /// runs between its spouses. Those in it learn most; everyone in camp sees it. The view then
        /// spreads by talk like any other belief.
        /// </summary>
        void LearnFromPluralMarriage(Person hub)
        {
            double campFed = Band.Members.Average(m => m.Satiety);
            // Children a married person had in the past year: the usual, to compare with.
            var married = Band.Members.Where(m => m.Married && Grown(m)).ToList();
            double usualKids = married.Count == 0 ? 0 : married.Average(m => KidsLastYear(m, m.Spouses));
            {
                var hearth = hub.Home!.Members;
                double fed = hearth.Average(m => m.Satiety) - campFed;
                double strife = 0;
                foreach (var s in hub.Spouses)
                {
                    strife += s.GetGrievance(hub);
                    foreach (var t in hub.Spouses) if (t != s) strife += s.GetGrievance(t);
                }
                double bad = strife / (strife + 3) * 1.5;
                // What it did for each: the one with several spouses, and each of the spouses. Children
                // count by whose they are, so a man with two wives has more and a woman with two husbands
                // doesn't, while her husbands each have fewer: the body decides that, not the custom.
                double Outcome(Person m) => fed * 2 - bad + (KidsLastYear(m, m == hub ? hub.Spouses : new List<Person> { hub }) - usualKids) * 0.8;
                double hubSide = Outcome(hub);
                double spouseSide = hub.Spouses.Average(Outcome);
                foreach (var m in Band.Members.Where(Grown))
                {
                    bool inIt = m == hub || hub.Spouses.Contains(m);
                    // Onlookers judge by the lot of people like themselves: men by how the man in it fares, women by the woman.
                    double seen = m == hub ? hubSide : hub.Spouses.Contains(m) ? Outcome(m) : m.Sex == hub.Sex ? hubSide : spouseSide;
                    m.Expect.Absorb(PluralTopic, seen, inIt ? 0.3 : 0.03 + 0.05 * Math.Max(0, m.GetAffinity(hub)), 0);
                }
            }
        }

        /// <summary>
        /// Two unpaired people pair when each is the other's best choice among the free people they
        /// know (within a little), and they know each other well enough to have a bond at all.
        /// </summary>
        public bool WouldPair(Person a, Person b, Simulation aBand, Simulation bBand)
        {
            if (!CouldPair(a, b)) return false;
            
            double nudgeA = (!Off("god") && !Off("nudge")) ? a.ConsumeNudgeWeight("marry", 0.5, "yes", "pair") : 0.0;
            double nudgeB = (!Off("god") && !Off("nudge")) ? b.ConsumeNudgeWeight("marry", 0.5, "yes", "pair") : 0.0;

            if (Math.Min(a.GetAffinity(b), b.GetAffinity(a)) < 0.2 && nudgeA <= 0 && nudgeB <= 0) return false;

            bool Best(Person p, Person o, Simulation pb, double nudge)
            {
                double mine = Desire(p, o, pb) + nudge;
                foreach (var other in pb.Band.Members)
                    if (other != o && CouldPair(p, other) && Desire(p, other, pb) > mine + 0.05) return false;
                return true;
            }
            bool willPair = Best(a, b, aBand, nudgeA) && Best(b, a, bBand, nudgeB);
            if (willPair && !Off("god"))
            {
                Region.GodEvents.Add(new GodEvent(Clock.Tick, "ForkPair", $"{a.Name} and {b.Name} are considering pairing", (int)Band.CampX, (int)Band.CampY));
            }
            return willPair;
        }

        /// <summary>
        /// An occasion to think about leaving one's partner: growing close to someone free, or a fight
        /// with the partner. Weighed against the bond, young children, warmth and how much family matters.
        /// </summary>
        public void ConsiderLeaving(Person p, Person? drawnTo, double strain = 0, Person? partner = null)
        {
            partner ??= p.Partner;
            if (partner == null || !p.Spouses.Contains(partner) || !partner.Alive || !Band.Members.Contains(p) || !Band.Members.Contains(partner) || !Grown(p)) return;

            bool free = drawnTo != null && !drawnTo.Married && drawnTo.Sex != p.Sex && !p.IsKin(drawnTo) && Grown(drawnTo)
                        && Band.Members.Contains(drawnTo)
                        && !Band.Members.Any(o => o != p && CouldPair(drawnTo, o) && Desire(drawnTo, o, this) > Desire(drawnTo, p, this));

            double nudgeGo = (!Off("god") && !Off("nudge")) ? p.ConsumeNudgeWeight("leave", 0.5, "leave", "yes") : 0.0;

            int young = p.Children.Count(c => c.Alive && c.AgeYears(Clock) < 10);
            // A pair bond runs deeper than friendship, and deepens with the years together.
            double years = Math.Min(10, (Clock.Day - p.WedOn[partner]) / (double)SimClock.DaysPerYear);
            double stay = p.GetAffinity(partner) * (1.2 + years * 0.05) + young * 0.3 + p.M(MindTrait.Warmth) * 0.2 * p.W(Drive.Family) - p.GetGrievance(partner) * 0.1
                          - strain;
            double go = free ? Desire(p, drawnTo!, this) : 0;
            go += nudgeGo;
            if (go <= stay) return;
            if (!Off("god"))
                Region.GodEvents.Add(new GodEvent(Clock.Tick, "ForkLeave", $"{p.Name} is leaving {partner.Name}", (int)Band.CampX, (int)Band.CampY));
            Separate(p, partner, free ? drawnTo : null);
        }

        void Separate(Person leaver, Person partner, Person? rival)
        {
            Person.Unwed(leaver, partner);
            // In a plural marriage, the one who goes is whoever has no other spouse at the hearth.
            if (rival == null && leaver.Spouses.Any(s => s.Home == leaver.Home) && !partner.Spouses.Any(s => s.Home == partner.Home))
            {
                var stays = leaver; leaver = partner; partner = stays;
            }
            // Parting hurts the bond on both sides.
            leaver.AddAffinity(partner, -0.3); partner.AddAffinity(leaver, -0.4);
            partner.AddGrievance(leaver, 3);
            if (rival != null) partner.AddGrievance(rival, 3);
            // The one leaving makes a new hearth (with the new partner if there is one). The young children
            // go with whichever parent is closer to them, pulled by the band's custom; the custom follows
            // what parents actually do.
            var young = leaver.Children.Where(c => c.Alive && c.AgeYears(Clock) < 14 && c.Home == leaver.Home).ToList();
            double leaverClaim = young.Sum(c => c.GetAffinity(leaver)) + (leaver.Sex == Sex.Female ? 1 : -1) * Band.ChildrenFollowMother;
            double partnerClaim = young.Sum(c => c.GetAffinity(partner)) + (partner.Sex == Sex.Female ? 1 : -1) * Band.ChildrenFollowMother;
            var kids = leaverClaim > partnerClaim ? young : new List<Person>();
            if (young.Count > 0)
            {
                bool withMother = (kids.Count > 0) == (leaver.Sex == Sex.Female);
                Band.ChildrenFollowMother += ((withMother ? 1 : -1) - Band.ChildrenFollowMother) * 0.1;
            }
            if (rival != null)
            {
                Person.Wed(leaver, rival, Clock.Day);
                NewHousehold(new[] { leaver, rival }.Concat(kids).ToArray());
            }
            else NewHousehold(new[] { leaver }.Concat(kids).ToArray());
            Chronicle.Write(Clock, Importance.Notable, rival != null
                ? $"{leaver.Name} left {partner.Name} for {rival.Name}."
                : $"{leaver.Name} and {partner.Name} parted.");
        }

        // ---- Following ----------------------------------------------------------------------------

        static readonly Dictionary<int, string> _followTopics = new Dictionary<int, string>();

        static string FollowTopic(Person leader)
        {
            lock (_followTopics)
            {
                if (!_followTopics.TryGetValue(leader.Id, out var t)) _followTopics[leader.Id] = t = "follow:" + leader.Id;
                return t;
            }
        }

        /// <summary>What someone has come to expect from following this person's lead (0 = no opinion).</summary>
        public static double Following(Person p, Person leader) => p.Expect.Get(FollowTopic(leader), 0);

        /// <summary>Following someone turned out well or badly: the follower's expectation of them moves.</summary>
        public void LearnFollowing(Person follower, Person leader, double outcome)
        {
            double current = Following(follower, leader);
            follower.Expect.Absorb(FollowTopic(leader), Math.Max(-1, Math.Min(1, outcome)), 0.2 + 0.2 * follower.M(MindTrait.Openness), current);
        }

        /// <summary>On the first trips after a move, people judge whether moving on that person's word paid off.</summary>
        public void JudgeMove(Person p, double perHour)
        {
            if (p.MovedBy == null || Clock.Day - p.MovedDay > 30) { p.MovedBy = null; return; }
            double outcome = Math.Max(-1, Math.Min(1, (perHour - p.YieldBeforeMove) * 3));
            LearnFollowing(p, p.MovedBy, outcome);
            LearnElders(p, outcome);
            p.MovedBy = null;
        }
    }
}
