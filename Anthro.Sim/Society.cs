using System;
using System.Collections.Generic;
using System.Linq;
using Anthro.Sim.Activities;
using Anthro.Sim.Core;
using Anthro.Sim.People;

namespace Anthro.Sim
{
    /// <summary>
    /// Society from people. Social acts are small primitives (ask, give, refuse, take, confront,
    /// attack, give back). What follows an act is only whatever others choose to do with the same
    /// primitives. The authored part is thin biology:
    ///   - perception: who notices a deed, and how news of it spreads through talk;
    ///   - innate feelings: anger at a harm you can pin on someone (stronger when your children are
    ///     hungry), guilt at harming kin or friends, fear of someone stronger;
    ///   - the physics of food, fights and wounds.
    /// People learn what acts lead to, in context (act × relation to the target × whether anyone was
    /// watching), from their own results, from what they see happen to others, and from gossip.
    /// Consequences carry the deed that caused them, so the lesson lands on the right act.
    /// </summary>
    public sealed partial class Simulation
    {
        /// <summary>Rations of harm a wound is felt as, for learning and anger (a bad wound ≈ a week or two of food).</summary>
        const double WoundCost = 20;

        // ---- Relations and lessons ---------------------------------------------------------------

        public Relation RelationTo(Person p, Person o)
        {
            // Someone from another camp of one's own people is judged like anyone one knows (kin,
            // friend, neutral, distrusted); only people of another people are outsiders.
            if (!Band.Members.Contains(o) && !SamePeople(o)) return Relation.Outsider;
            if (p.IsKin(o)) return Relation.Kin;
            if (p.GetTrust(o) < 0.35) return Relation.Distrusted;
            return p.GetAffinity(o) > 0.3 ? Relation.Friend : Relation.Neutral;
        }

        /// <summary>Whether someone belongs to a camp of this camp's people.</summary>
        internal bool SamePeople(Person o)
        {
            if (Tribe == null) return false;
            var theirs = Region.BandOf(o);
            return theirs != null && theirs.Tribe == Tribe;
        }

        /// <summary>How someone stands toward a household: by its adult they're closest to (kin first).</summary>
        public Relation RelationTo(Person p, Household h)
        {
            Person? closest = null;
            double best = double.MinValue;
            foreach (var a in h.Members)
            {
                if (a.AgeYears(Clock) < 14) continue;
                if (p.IsKin(a)) return Relation.Kin;
                double v = p.GetAffinity(a) + p.GetTrust(a);
                if (v > best) { best = v; closest = a; }
            }
            return closest == null ? Relation.Neutral : RelationTo(p, closest);
        }

        long _hourTick = -1;
        readonly List<Person> _sick = new List<Person>(), _working = new List<Person>();

        void RefreshHour()
        {
            if (_hourTick == Clock.Tick) return;
            _hourTick = Clock.Tick;
            _sick.Clear(); _working.Clear();
            foreach (var m in Band.Members)
            {
                if (m.Sickness > 0.25 && m.CareToday < 4) _sick.Add(m);
                if (m.Current is Knap || m.Current is Care || m.Current is Cook || m.Current is Preserve) _working.Add(m);
            }
        }

        /// <summary>Who in camp is sick enough to need looking after this hour.</summary>
        public List<Person> SickThisHour { get { RefreshHour(); return _sick; } }

        /// <summary>Who is at a craft this hour that children could watch and learn from.</summary>
        public List<Person> WorkingThisHour { get { RefreshHour(); return _working; } }

        static readonly string[,,] _lessons = BuildLessons();

        static string[,,] BuildLessons()
        {
            var acts = (Act[])Enum.GetValues(typeof(Act));
            var rels = (Relation[])Enum.GetValues(typeof(Relation));
            var names = new string[acts.Length, rels.Length, 3];
            foreach (var a in acts)
                foreach (var r in rels)
                {
                    string b = $"act:{a.ToString().ToLowerInvariant()}:{r.ToString().ToLowerInvariant()}";
                    names[(int)a, (int)r, 0] = b;
                    names[(int)a, (int)r, 1] = b + ":seen";
                    names[(int)a, (int)r, 2] = b + ":unseen";
                }
            return names;
        }

        /// <summary>The learning topic for an act in context (names built once).</summary>
        public static string Lesson(Act act, Relation rel, bool? seen = null) =>
            _lessons[(int)act, (int)rel, seen == null ? 0 : seen.Value ? 1 : 2];

        /// <summary>What this person expects an act to bring them, in rations (food gained minus what follows).</summary>
        public static double Expects(Person p, Act act, Relation rel, bool? seen = null) =>
            p.Expect.Get(Lesson(act, rel, seen), CulturalDefault(act, rel, seen));

        /// <summary>
        /// Only used when a person somehow has no expectation at all (everyone is born into their
        /// band's expectations): a mild default that taking is risky and giving is a little rewarded.
        /// </summary>
        static double CulturalDefault(Act act, Relation rel, bool? seen) => act switch
        {
            Act.Take => seen == true ? -2 : 0.5,
            Act.Ask => rel == Relation.Kin ? 1.2 : 0.5,
            Act.Give => rel == Relation.Kin ? 0.8 : 0.2,
            Act.Confront => 0.5,
            Act.Attack => -2,
            Act.Refuse => -0.5,
            _ => 0,
        };

        /// <summary>
        /// Rations of expected gain or loss, as a pull on a choice: the same scale as an hour of food
        /// work (an hour's foraging at a typical ~0.4 rations is felt as about 0.25).
        /// </summary>
        public static double Felt(double rations) => rations * 0.6;

        /// <summary>Guilt at harming someone: in rations, by fondness and warmth, more for kin. Innate.</summary>
        internal static double Guilt(Person p, Person o) => (p.GetAffinity(o) * 3 + (p.IsKin(o) ? 3 : 0)) * (0.3 + p.M(MindTrait.Warmth));

        /// <summary>A person learns what one of their acts led to (or a later consequence of it).</summary>
        void Learn(Person p, string lesson, double outcome, double fallbackValue, double strength = 1)
        {
            double current = p.Expect.Get(lesson, fallbackValue);
            double rate = (0.1 + 0.2 * p.M(MindTrait.Openness)) * strength;
            p.Expect.Absorb(lesson, outcome, Math.Min(1, rate), current);
        }

        /// <summary>
        /// A consequence of an earlier deed reaches its doer: the cost is added to what they've come to
        /// expect from that kind of act. Those who see it happen learn it too, more weakly.
        /// </summary>
        /// <param name="by">Who is answering the deed; each person's answer counts against it once.</param>
        void Credit(Deed deed, double outcome, IEnumerable<Person>? watchers = null, Person? by = null)
        {
            if (by != null && !deed.AnsweredBy.Add(by)) return;
            // The doer's expectation moves as if this had been part of the deed's outcome all along
            // (same rate as the first lesson, so a consequence counts once, like the gain did).
            if (deed.Actor.Alive)
            {
                double current = deed.Actor.Expect.Get(deed.Context, 0);
                deed.Actor.Expect.Absorb(deed.Context, current + outcome, 0.1 + 0.2 * deed.Actor.M(MindTrait.Openness), current);
            }
            deed.Payoff += outcome;
            if (watchers == null) return;
            foreach (var w in watchers)
            {
                if (w == deed.Actor || !w.Alive) continue;
                // Watching how it played out for someone else: what they gained, and what it cost them.
                double current = w.Expect.Get(deed.Context, 0);
                w.Expect.Absorb(deed.Context, deed.Payoff, 0.1 * (0.5 + w.M(MindTrait.Openness)), current);
            }
        }

        // ---- Perception -----------------------------------------------------------------------------

        internal static bool AwakeInCamp(Person p) => AtCamp(p) && !(p.Current is Sleep);

        /// <summary>Out of camp on work done alone (gathering, fishing, wood, traps): nobody near to see.</summary>
        internal static bool OutAlone(Person p) => p.Current is Forage || p.Current is Fish || p.Current is GatherWood || p.Current is Trap;

        /// <summary>A deed between people of different bands, witnessed by the band where it happened.</summary>
        public List<Person> HappenHere(Deed deed) => Happen(deed);

        long _awakeTick = -1;
        int _awake;

        /// <summary>How many people (6 and up) are awake and about camp this hour.</summary>
        public int AwakeInCampCount
        {
            get
            {
                if (_awakeTick == Clock.Tick) return _awake;
                _awakeTick = Clock.Tick;
                _awake = 0;
                foreach (var m in Band.Members) if (m.AgeYears(Clock) >= 6 && AwakeInCamp(m)) _awake++;
                return _awake;
            }
        }

        /// <summary>
        /// Who notices a deed done in camp: anyone awake and about, each with some chance; the people
        /// it's done to always know. Everyone who sees it remembers it.
        /// </summary>
        List<Person> Witness(Deed deed)
        {
            var seen = new List<Person>();
            foreach (var m in Band.Members)
            {
                if (m == deed.Actor || m.AgeYears(Clock) < 6) continue;
                // Those an act is done to face-to-face know of it; a take from a hearth is only noticed by
                // whoever happens to be about.
                bool involved = deed.Act != Act.Take && (m == deed.Target || (deed.Hearth != null && m.Home == deed.Hearth && !deed.Far));
                if (involved || !deed.Far && AwakeInCamp(m) && Rng.Chance(0.15 * (0.6 + m.M(MindTrait.Curiosity) * 0.8))) seen.Add(m);
            }
            // Someone an act is done to face to face knows of it, even if they're from another band.
            if (deed.Target != null && deed.Act != Act.Take && deed.Target != deed.Actor && !seen.Contains(deed.Target) && deed.Target.Alive) seen.Add(deed.Target);
            deed.Witnessed = seen.Count > 0;
            if (deed.Act == Act.Take && seen.Count == 0 && deed.Hearth != null) _unseenTakes.Add(deed);
            foreach (var w in seen) Remember(w, deed, true);
            return seen;
        }

        static void Remember(Person p, Deed deed, bool firsthand)
        {
            if (!p.KnownDeeds.Add(deed)) return;
            p.Deeds.Add(new Remembered(deed, firsthand));
            if (p.Deeds.Count > 40) { p.KnownDeeds.Remove(p.Deeds[0].Deed); p.Deeds.RemoveAt(0); }
        }

        /// <summary>
        /// What knowing of a deed does to someone. Their expectation of the doer moves (trust is a
        /// prediction of how that person treats people). A harm to them or their family stirs anger,
        /// stronger when their children are hungry; a harm to someone they love stirs some too.
        /// </summary>
        void Feel(Person p, Deed deed, double strength)
        {
            var doer = deed.Actor;
            if (p == doer) return;
            double trustToward = deed.Act switch { Act.Give => 1, Act.Return => 0.6, Act.Take => 0, Act.Attack => 0, Act.Refuse => 0.3, _ => -1 };
            if (trustToward >= 0)
            {
                double t = p.GetTrust(doer);
                p.AddTrust(doer, (trustToward - t) * 0.25 * strength);
            }
            // A gift from another band's people softens feeling toward that band.
            if (deed.Act == Act.Give && deed.Target == p)
            {
                var giverBand = Band.Members.Contains(doer) ? null : Region.BandOf(doer);
                if (giverBand != null) p.AddBandGrievance(giverBand.Id, -deed.Amount);
            }
            if (deed.Act != Act.Take && deed.Act != Act.Attack && deed.Act != Act.Refuse) return;

            // Whose harm is it: mine, my family's, or someone I'm fond of?
            var harmed = deed.Act == Act.Take && deed.Hearth != null ? deed.Hearth.Members : deed.Target != null ? new List<Person> { deed.Target } : new List<Person>();
            double closeness = harmed.Count == 0 ? 0 : harmed.Max(h => h == p ? 1 : p.Home != null && h.Home == p.Home ? 1 : p.IsKin(h) ? 0.6 : p.GetAffinity(h) * 0.5);
            if (closeness <= 0) return;
            double harm = deed.Act == Act.Attack ? deed.Amount * WoundCost : deed.Amount;
            // Hungry children at home make a loss of food unbearable.
            double hungryKids = p.Home == null ? 0 : p.Home.Members.Where(c => c.AgeYears(Clock) < 14).Select(c => 1 - c.Satiety).DefaultIfEmpty(0).Max();
            double felt = harm * closeness * (1 + hungryKids) * strength;
            p.AddGrievance(doer, felt);
            // A harm from someone of another band is laid at that whole band's door.
            var theirs = Band.Members.Contains(doer) ? null : Region.BandOf(doer);
            if (theirs != null && theirs != Region.BandOf(p)) p.AddBandGrievance(theirs.Id, felt * 0.5);
        }

        /// <summary>A deed happens: witnesses notice and feel it, and the chronicle records what it amounted to.</summary>
        List<Person> Happen(Deed deed)
        {
            Remember(deed.Actor, deed, true);
            var seen = Witness(deed);
            foreach (var w in seen) Feel(w, deed, 1);
            Narrate(deed, seen);
            return seen;
        }

        /// <summary>
        /// Talk passes news: each tells the other about the most striking things they know others have
        /// done. Hearsay counts for less than seeing it yourself.
        /// </summary>
        public void Gossip(Person a, Person b)
        {
            Tell(a, b);
            Tell(b, a);
        }

        void Tell(Person teller, Person listener)
        {
            // The two most striking recent deeds the listener hasn't heard of (an attack before a take
            // before anything else; among equals, the first remembered).
            Deed? first = null, second = null;
            int r1 = 0, r2 = 0;
            foreach (var r in teller.Deeds)
            {
                var d = r.Deed;
                if (d.Actor == listener || Clock.Day - d.Day >= 60) continue;
                int rank = d.Act == Act.Attack ? 3 : d.Act == Act.Take ? 2 : d.Act == Act.Refuse || d.Act == Act.Give ? 1 : 0;
                if (rank == 0 || rank <= r2 || listener.KnownDeeds.Contains(d)) continue;
                if (rank > r1) { second = first; r2 = r1; first = d; r1 = rank; }
                else { second = d; r2 = rank; }
            }
            if (first == null) return;
            double believed = (0.3 + 0.4 * listener.GetTrust(teller)) * (0.5 + 0.5 * listener.M(MindTrait.Credulity));
            foreach (var d in second == null ? new[] { first } : new[] { first, second })
            {
                Remember(listener, d, false);
                Feel(listener, d, believed * 0.5);
            }
        }

        /// <summary>Anger cools over weeks; old deeds fade from talk.</summary>
        readonly List<Deed> _unseenTakes = new List<Deed>();

        /// <summary>
        /// Food that went missing gets noticed. The family suspects someone: whoever they've seen or
        /// heard take before, otherwise whoever they trust least. Suspicion is a grievance like any
        /// other, and gossip carries it. When it lands on the real taker, the take finally costs them;
        /// when it lands on someone innocent, that's a wrong of its own.
        /// </summary>
        void NoticeLosses()
        {
            foreach (var deed in _unseenTakes)
            {
                var hearth = deed.Hearth!;
                if (!Band.Households.Contains(hearth)) continue;
                foreach (var owner in hearth.Adults(Clock).ToList())
                {
                    Person? suspect = null;
                    double worst = 0;
                    foreach (var o in Band.Members)
                    {
                        if (o == owner || o.Home == hearth || o.AgeYears(Clock) < 10) continue;
                        int knownTakes = owner.Deeds.Count(r => r.Deed.Actor == o && r.Deed.Act == Act.Take);
                        double s = knownTakes * 2 + (0.5 - owner.GetTrust(o)) + Rng.Range(0, 0.3);
                        if (s > worst) { worst = s; suspect = o; }
                    }
                    if (suspect == null || worst < 0.3) continue;
                    owner.AddGrievance(suspect, deed.Amount * 0.5);
                    owner.AddTrust(suspect, -0.05);
                    // If they're right, whatever they do about it answers the take.
                    if (suspect == deed.Actor) Remember(owner, deed, false);
                }
            }
            _unseenTakes.Clear();
        }

        void DailySociety()
        {
            NoticeLosses();
            foreach (var p in Band.Members)
            {
                if (p.Grievance.Count > 0)
                    foreach (var id in p.Grievance.Keys.ToList())
                    {
                        double v = p.Grievance[id] * (0.96 + 0.02 * p.M(MindTrait.Temper));
                        if (v < 0.05) p.Grievance.Remove(id); else p.Grievance[id] = v;
                    }
                if (p.BandGrievance.Count > 0)
                    foreach (var id in p.BandGrievance.Keys.ToList())
                    {
                        double v = p.BandGrievance[id] * 0.99;
                        if (v < 0.05) p.BandGrievance.Remove(id); else p.BandGrievance[id] = v;
                    }
                if (p.Deeds.Count > 0 && Clock.Day - p.Deeds[0].Deed.Day > SimClock.DaysPerYear) { p.KnownDeeds.Remove(p.Deeds[0].Deed); p.Deeds.RemoveAt(0); }
            }
        }

        /// <summary>The chronicle reads deeds after the fact: a seen take is a theft, a blow is a fight.</summary>
        void Narrate(Deed deed, List<Person> seen)
        {
            string owner = deed.Target?.Name ?? "another family";
            switch (deed.Act)
            {
                case Act.Take:
                    ThisYear.Thefts++;
                    if (seen.Count > 0)
                        Chronicle.Write(Clock, Importance.Notable, $"{deed.Actor.Name} was seen taking food from {owner}'s hearth.");
                    else
                        Chronicle.Write(Clock, Importance.Detail, $"{deed.Actor.Name} took food from {owner}'s hearth unseen.");
                    break;
                case Act.Refuse:
                    ThisYear.Refusals++;
                    break;
                case Act.Give:
                    ThisYear.Shared += deed.Amount;
                    break;
                case Act.Confront:
                    Chronicle.Write(Clock, Importance.Detail, $"{deed.Actor.Name} confronted {deed.Target?.Name} over {(deed.Answers?.Act == Act.Take ? "the taken food" : "an old wrong")}.");
                    break;
                case Act.Return:
                    Chronicle.Write(Clock, Importance.Detail, $"{deed.Actor.Name} gave back what they'd taken from {deed.Target?.Name}.");
                    break;
            }
        }

        // ---- Asking and giving ------------------------------------------------------------------------

        /// <summary>What a household can spare: food beyond a couple of days for its own people.</summary>
        internal static double Spare(Household h) => Math.Max(0, h.Food.Total - h.Members.Count * 0.9 * 2);

        /// <summary>
        /// What a hearth could hand to someone asking without leaving itself worse off than them: with
        /// plenty, whatever is beyond a couple of days; when everyone is short, down to the asker's own
        /// level (foragers' demand sharing evens out hunger).
        /// </summary>
        internal static double SpareFor(Household h, Household? asker)
        {
            double theirDays = asker == null ? 0 : asker.DaysOfFood;
            double keep = h.Members.Count * 0.9 * Math.Min(2, Math.Max(0.5, theirDays + 0.5));
            return Math.Max(0, h.Food.Total - keep);
        }

        /// <summary>
        /// How much the giver chooses to hand over: moved by the asker's need (and the hunger of
        /// their children, which people can see), warmth, kinship and fondness, the band's sharing
        /// norm, and what giving to people like this has brought them; held back by their own
        /// family's need and by anger at the asker.
        /// </summary>
        public double WillingToGive(Person giver, Person asker, double asked)
        {
            var home = giver.Home;
            if (home == null) return 0;
            double spare = SpareFor(home, asker.Home);
            if (spare <= 0.05) return 0;
            var rel = RelationTo(giver, asker);
            double theirHunger = asker.Home == null ? 1 - asker.Satiety
                : asker.Home.Members.Select(m => 1 - m.Satiety).DefaultIfEmpty(0).Max();
            double pull = theirHunger * (0.4 + giver.M(MindTrait.Warmth))
                          + (rel == Relation.Kin ? 0.6 : 0) + giver.GetAffinity(asker) * 0.5
                          + Expects(giver, Act.Give, rel) - Expects(giver, Act.Refuse, rel)
                          - giver.GetGrievance(asker) * 0.3
                          - Inheritance.Clamp01(1 - home.DaysOfFood / 4) * 0.8
                          + Rng.Normal(0, 0.1);
            return Math.Min(spare, asked * Inheritance.Clamp01(pull));
        }

        /// <summary>
        /// Someone asks a household for food. An adult there answers by choice. Giving is felt as a
        /// cost now and a hope of return later; refusing someone in need, when you could spare it,
        /// may anger them. Returns what came into the asker's hearth.
        /// </summary>
        public double DoAsk(Person asker, Household hearth, double amount, FoodStore? carryHome = null)
        {
            var home = HomeOf(asker);
            var giver = hearth.Adults(Clock).Where(a => AwakeInCamp(a) || a.Current is Sleep).OrderByDescending(a => a.M(MindTrait.Warmth)).FirstOrDefault();
            var rel = RelationTo(asker, hearth);
            if (giver == null) return 0;
            double give = WillingToGive(giver, asker, amount);
            if (give > 0.05)
            {
                if (carryHome == null) MoveFood(hearth, home, give, out _);
                else
                {
                    double taken = hearth.Food.Take(give, out double meat);
                    carryHome.AddMeat(meat);
                    carryHome.AddPlants(taken - meat, keeps: true);
                }
                var deed = new Deed { Day = Clock.Day, Actor = giver, Act = Act.Give, Target = asker, Hearth = home, Amount = give, Context = Lesson(Act.Give, RelationTo(giver, asker)) };
                Happen(deed);
                // The giver feels the cost now; any return comes later.
                // The giver feels the cost now (more when their own hearth is short); seeing their own kin
                // fed is felt as a good in itself (kin altruism, innate). Any return comes later.
                double kinGood = giver.IsKin(asker) ? give * 0.5 * giver.W(Drive.Family) : 0;
                Learn(giver, deed.Context, kinGood - give * Inheritance.Clamp01(1 - hearth.DaysOfFood / 6), CulturalDefault(Act.Give, RelationTo(giver, asker), null), 0.5);
                Reciprocate(asker, giver, give);
                asker.AddAffinity(giver, 0.02);
                Learn(asker, Lesson(Act.Ask, rel), give, CulturalDefault(Act.Ask, rel, null));
                asker.Expect.Absorb(DemandTopic, 1, 0.03, Demand(asker));
                return give;
            }
            var no = new Deed { Day = Clock.Day, Actor = giver, Act = Act.Refuse, Target = asker, Amount = Math.Min(SpareFor(hearth, home), amount) * 0.3,
                                Context = Lesson(Act.Refuse, RelationTo(giver, asker)), Answers = LatestWrong(giver, asker) };
            Happen(no);
            // Refusing keeps the food (worth more when one's own hearth is short); what it costs comes later.
            double kept = Math.Min(SpareFor(hearth, home), amount) * Inheritance.Clamp01(1 - hearth.DaysOfFood / 6);
            no.Payoff = kept;
            Learn(giver, no.Context, kept, CulturalDefault(Act.Refuse, RelationTo(giver, asker), null), 0.5);
            Learn(asker, Lesson(Act.Ask, rel), 0, CulturalDefault(Act.Ask, rel, null));
            asker.Expect.Absorb(DemandTopic, 0, 0.03, Demand(asker));
            // A refusal that answers an old wrong is part of what that wrong cost its doer.
            if (no.Answers != null) Credit(no.Answers, -Math.Min(amount, SpareFor(hearth, home)), null, giver);
            return 0;
        }

        /// <summary>When someone gives to a person who once gave to them, the earlier gift has paid off: the earlier giver learns that.</summary>
        void Reciprocate(Person receiver, Person giver, double amount)
        {
            for (int i = receiver.Deeds.Count - 1; i >= 0; i--)
            {
                var d = receiver.Deeds[i].Deed;
                if (d.Act == Act.Give && d.Actor == receiver && d.Target == giver)
                {
                    Credit(d, amount);
                    return;
                }
            }
        }

        /// <summary>The most recent wrong the person remembers this other doing to them or theirs.</summary>
        internal static Deed? LatestWrong(Person p, Person other)
        {
            for (int i = p.Deeds.Count - 1; i >= 0; i--)
            {
                var d = p.Deeds[i].Deed;
                if (d.Actor == other && (d.Act == Act.Take || d.Act == Act.Attack || d.Act == Act.Refuse)) return d;
            }
            return null;
        }

        /// <summary>
        /// A kill comes home with the hunters' families, who then hand it round as they each choose:
        /// to kin, to friends, to those whose children are hungry, to those who've given to them.
        /// </summary>
        public void ShareKill(double rations, List<Person> party, Person? killer)
        {
            var hunterHomes = party.Where(m => m.Alive && Band.Members.Contains(m)).Select(m => HomeOf(m)).Distinct().ToList();
            if (rations <= 0 || hunterHomes.Count == 0) return;
            foreach (var h in hunterHomes) h.Food.AddMeat(rations / hunterHomes.Count);
            foreach (var h in hunterHomes)
            {
                var giver = h.Adults(Clock).Where(a => party.Contains(a)).OrderByDescending(a => a.M(MindTrait.Warmth)).FirstOrDefault()
                            ?? h.Adults(Clock).FirstOrDefault();
                if (giver == null) continue;
                foreach (var other in Band.Households.Where(o => !hunterHomes.Contains(o)).ToList())
                {
                    var asker = other.Adults(Clock).OrderByDescending(a => giver.GetAffinity(a)).FirstOrDefault();
                    if (asker == null) continue;
                    double portion = rations / Math.Max(1, Band.Members.Count) * other.Members.Count;
                    double give = WillingToGive(giver, asker, portion);
                    if (give <= 0.05) continue;
                    MoveFood(h, other, give, out _);
                    var deed = new Deed { Day = Clock.Day, Actor = giver, Act = Act.Give, Target = asker, Hearth = other, Amount = give, Context = Lesson(Act.Give, RelationTo(giver, asker)) };
                    Happen(deed);
                    Reciprocate(asker, giver, give);
                    foreach (var a in other.Adults(Clock))
                    {
                        a.AddAffinity(giver, 0.01);
                        if (killer != null && killer != a) LearnFollowing(a, killer, Math.Min(1, give / 10));
                    }
                }
            }
        }

        // ---- Taking, confronting, fighting ----------------------------------------------------------

        /// <summary>
        /// Someone takes food from another household's store. They gain it at once; whatever else
        /// follows depends on who saw, and what those people choose to do.
        /// </summary>
        public double DoTake(Person taker, Household hearth, double amount)
        {
            var home = HomeOf(taker);
            // As much as they can carry off at once.
            double took = Math.Min(Math.Min(amount, hearth.Food.Total), Physiology.CarryCapacity(taker, taker.AgeYears(Clock)));
            if (took <= 0.05) return 0;
            MoveFood(hearth, home, took, out _);
            var owner = hearth.Adults(Clock).OrderByDescending(a => a.M(MindTrait.Temper)).FirstOrDefault();
            var rel = RelationTo(taker, hearth);
            var deed = new Deed { Day = Clock.Day, Actor = taker, Act = Act.Take, Target = owner, Hearth = hearth, Amount = took };
            var seen = Happen(deed);
            deed.Context = Lesson(Act.Take, rel, seen.Count > 0);
            deed.Payoff = took;
            Learn(taker, deed.Context, took, CulturalDefault(Act.Take, rel, seen.Count > 0));
            // Seen taking from your own hearth, or from someone you're close to: answer it now, or let it go.
            Person? answer = null;
            double best = 0;
            foreach (var w in seen)
            {
                if (!w.Alive || !AwakeInCamp(w) || w.AgeYears(Clock) < 14 || w.GetGrievance(taker) <= 0) continue;
                double v = ConfrontValue(w, taker);
                if (v > best) { best = v; answer = w; }
            }
            if (answer != null) DoConfront(answer, taker);
            return took;
        }

        /// <summary>
        /// Someone confronts a person they hold a grievance against. The other chooses: give back what
        /// they took (if it was a take), or stand their ground. Standing firm may end in blows, if the
        /// confronter chooses that. Everyone watching learns from how it goes.
        /// </summary>
        /// <summary>
        /// What facing someone over a wrong is worth to a person right now, against letting it go:
        /// their anger and temper, what confronting has brought them before, set against guilt and fear.
        /// </summary>
        public double ConfrontValue(Person p, Person target) => ConfrontValue(p, target, Backing(target, p));

        internal double ConfrontValue(Person p, Person target, double backing)
        {
            double anger = p.GetGrievance(target);
            double heat = anger / (anger + 3);
            double fear = backing / Math.Max(0.1, Strength(p)) * 0.3 + p.Injury;
            return heat * (0.4 + p.M(MindTrait.Temper) * 0.6) * 1.5 + Felt(Expects(p, Act.Confront, RelationTo(p, target)))
                   + GodActValue(p, Act.Confront) - Felt(Guilt(p, target)) * 0.5 - fear * 0.3 * p.W(Drive.Safety);
        }

        public void DoConfront(Person accuser, Person accused)
        {
            var wrong = LatestWrong(accuser, accused);
            var deed = new Deed { Day = Clock.Day, Actor = accuser, Act = Act.Confront, Target = accused, Answers = wrong,
                                  Context = Lesson(Act.Confront, RelationTo(accuser, accused)) };
            var seen = Happen(deed);
            double anger = accuser.GetGrievance(accused);

            // The accused weighs giving back against what refusing might bring: a fight with someone
            // this angry and this strong, and their own guilt.
            double owed = wrong != null && wrong.Act == Act.Take ? Math.Min(wrong.Amount, HomeOf(accused).Food.Total) : 0;
            double fear = Strength(accuser) / Math.Max(0.1, Strength(accused)) * accuser.M(MindTrait.Temper);
            double refuseCost = fear * WoundCost * 0.3 + Guilt(accused, accuser) - accused.M(MindTrait.Temper) * 2;
            if (owed > 0.05 && owed < refuseCost)
            {
                MoveFood(HomeOf(accused), HomeOf(accuser), owed, out _);
                var back = new Deed { Day = Clock.Day, Actor = accused, Act = Act.Return, Target = accuser, Amount = owed, Answers = wrong };
                Happen(back);
                if (wrong != null) Credit(wrong, -owed, seen, accuser);
                accuser.AddGrievance(accused, -owed * 1.5);
                Learn(accuser, deed.Context, owed, CulturalDefault(Act.Confront, RelationTo(accuser, accused), null));
                return;
            }
            // Refused (or nothing to give back): does the accuser take it further?
            // Take it further, or back down: anger and temper and what hitting has brought, against
            // guilt and fear of the other.
            double theirStrength = Strength(accused) / Math.Max(0.1, Strength(accuser));
            double attack = anger / (anger + 3) * (0.3 + accuser.M(MindTrait.Temper)) * 1.5 + Felt(Expects(accuser, Act.Attack, RelationTo(accuser, accused)))
                            - Felt(Guilt(accuser, accused)) * 0.5 - theirStrength * 0.3 * accuser.W(Drive.Safety) + Rng.Normal(0, 0.1);
            if (attack > 0) DoAttack(accuser, accused, wrong);
            else
            {
                // Backing down: nothing gained, and the anger stays.
                Learn(accuser, deed.Context, 0, CulturalDefault(Act.Confront, RelationTo(accuser, accused), null));
            }
        }

        /// <summary>
        /// How much force would come down on someone who hits this person: their own strength, and the
        /// kin and close friends around camp who'd stand with them (minus anyone who'd stand with the attacker).
        /// </summary>
        public double Backing(Person target, Person attacker)
        {
            double b = Strength(target);
            foreach (var o in Band.Members)
            {
                if (o == target || o == attacker || !Grown(o) || !AwakeInCamp(o)) continue;
                double forThem = (o.IsKin(target) ? 0.8 : 0) + o.GetAffinity(target);
                double forMe = (o.IsKin(attacker) ? 0.8 : 0) + o.GetAffinity(attacker);
                if (forThem > forMe + 0.3) b += Strength(o) * 0.5;
            }
            return b;
        }

        double Strength(Person p) => p.B(BodyTrait.Strength) * p.Health * (1 - p.Injury) * Physiology.Maturity(p.AgeYears(Clock));

        /// <summary>
        /// One person attacks another. The fight itself is physical (strength, temper, health, luck).
        /// Being hit makes the loser angry at the winner; family and friends feel it too. What each
        /// learns: the attacker, what hitting got them; the one who'd done wrong, what it cost them.
        /// </summary>
        public void DoAttack(Person attacker, Person target, Deed? answers)
        {
            if (attacker == target || !attacker.Alive || !target.Alive) return;
            bool overFood = answers?.Act == Act.Take;
            bool here = Band.Members.Contains(attacker) && Band.Members.Contains(target);
            // Where a people settles grudges one on one, the two fight it out and the rest watch.
            bool duel = here && Rng.Chance((Duel(attacker) + Duel(target)) / 2);
            if (!Off("god") && !Off("nudge"))
            {
                if (attacker.TryConsumeNudge("duel", out var nA))
                {
                    if (nA == "peace" || nA == "no") duel = false;
                    else if (nA == "fight" || nA == "duel" || nA == "yes") duel = true;
                }
                else if (target.TryConsumeNudge("duel", out var nT))
                {
                    if (nT == "peace" || nT == "no") duel = false;
                    else if (nT == "fight" || nT == "duel" || nT == "yes") duel = true;
                }
            }
            if (duel && !Off("god"))
            {
                Region.GodEvents.Add(new GodEvent(Clock.Tick, "ForkDuel", $"{attacker.Name} challenged {target.Name} to a duel", (int)Band.CampX, (int)Band.CampY));
            }
            Fight(new[] { attacker }, new[] { target }, answers, overFood, inCamp: here && !duel, kind: duel ? "duel" : "quarrel");
        }

        // ---- Culture at the start ---------------------------------------------------------------------

        /// <summary>
        /// A band starts out with its own expectations about taking, asking, giving and hitting back,
        /// held by its people with personal variation. These are where learning starts from, not rules:
        /// they move with everything that happens and everything people tell each other.
        /// </summary>
        void SeedSocialCulture(List<Person> group)
        {
            double taboo = Math.Max(0.5, Rng.Normal(3, 1));        // how badly a seen take is expected to go
            double generosity = Math.Max(0.3, Rng.Normal(1, 0.25)); // how much asking and giving are expected to bring
            double retaliation = Rng.Normal(0.8, 0.5);              // what confronting a wrong is expected to bring
            // What hitting someone is expected to bring: costly everywhere inside the band (bands differ in
            // how costly), and less costly toward outsiders, as among real foragers.
            // Peoples differ widely here: for some a punch-up is an ordinary way to settle things, for
            // others any blow is a disgrace. (How far a fight goes is a separate matter: see Fights.cs.)
            double violence = Rng.Normal(-2.5, 1.0);
            double violenceOut = violence + Math.Max(0, Rng.Normal(1.5, 0.5));
            // What taking another spouse is expected to bring, all told (more food and children against
            // jealousy and strife): peoples start anywhere from wary to approving, and learn from there.
            double plural = Rng.Normal(0, 0.3);
            // A seen take costs much the same whoever it's from (the whole camp hears), more from kin and friends.
            var weight = new Dictionary<Relation, double> { [Relation.Kin] = 2, [Relation.Friend] = 1.5, [Relation.Neutral] = 1.2, [Relation.Distrusted] = 1.0, [Relation.Outsider] = 0.6 };
            var askWeight = new Dictionary<Relation, double> { [Relation.Kin] = 1.5, [Relation.Friend] = 1, [Relation.Neutral] = 0.6, [Relation.Distrusted] = 0.2, [Relation.Outsider] = 0.2 };
            var refuseWeight = new Dictionary<Relation, double> { [Relation.Kin] = 1.5, [Relation.Friend] = 1, [Relation.Neutral] = 0.6, [Relation.Distrusted] = 0.1, [Relation.Outsider] = 0.3 };
            var giveWeight = new Dictionary<Relation, double> { [Relation.Kin] = 1.2, [Relation.Friend] = 0.8, [Relation.Neutral] = 0.5, [Relation.Distrusted] = 0.1, [Relation.Outsider] = 0.2 };
            // Hunting habits: how well each way of closing in is expected to go, per animal.
            var habit = new Dictionary<Tactic, double>();
            // Generations of hunting here have taught what works with thrusting spears: creeping close
            // rarely does; waiting at water and driving into wet ground do. Bands differ around that.
            var known = new Dictionary<Tactic, double> { [Tactic.Stalk] = 0.05, [Tactic.Ambush] = 0.3, [Tactic.Drive] = 0.35,
                                                         [Tactic.Surround] = 0.3, [Tactic.Persistence] = 0.25, [Tactic.FireDrive] = 0.35 };
            foreach (Tactic t in Enum.GetValues(typeof(Tactic))) habit[t] = known[t] * Math.Max(0.3, Rng.Normal(1, 0.25));
            foreach (var p in group)
                foreach (var sp in Anthro.Sim.Ecology.Species.All)
                    if (sp.Huntable)
                        foreach (var kv in habit) p.Expect.Absorb(HuntParty.TacticTopic(kv.Key, sp), kv.Value * Math.Max(0.3, Rng.Normal(1, 0.1)), 1, kv.Value);
            foreach (var p in group)
            {
                double Vary(double v) => v + Rng.Normal(0, 0.3);
                foreach (var rel in weight.Keys)
                {
                    p.Expect.Absorb(Lesson(Act.Take, rel, true), Vary(2 - taboo * weight[rel]), 1, 0);
                    p.Expect.Absorb(Lesson(Act.Take, rel, false), Vary(2 - taboo * weight[rel] * 0.3), 1, 0);
                    p.Expect.Absorb(Lesson(Act.Ask, rel), Vary(generosity * askWeight[rel]), 1, 0);
                    p.Expect.Absorb(Lesson(Act.Give, rel), Vary(generosity * giveWeight[rel]), 1, 0);
                    // Turning someone away is expected to cost: they and others remember it.
                    p.Expect.Absorb(Lesson(Act.Refuse, rel), Vary(-generosity * refuseWeight[rel]), 1, 0);
                    p.Expect.Absorb(Lesson(Act.Confront, rel), Vary(retaliation), 1, 0);
                    p.Expect.Absorb(Lesson(Act.Attack, rel), Vary(rel == Relation.Outsider ? violenceOut : violence), 1, 0);
                    p.Expect.Absorb(Lesson(Act.Attack, rel, false), Vary((rel == Relation.Outsider ? violenceOut : violence) + 1), 1, 0);
                }
                p.Expect.Absorb(PluralTopic, plural + Rng.Normal(0, 0.1), 1, 0);
            }
            SeedViolenceCulture(group);
            SeedCustoms(group);
            SeedBelief(group);
        }

        /// <summary>A newborn takes on its mother's expectations about how people treat each other (it grows up among them).</summary>
        static void InheritSocialExpectations(Person child, Person mother)
        {
            foreach (var kv in mother.Expect.Snapshot())
                if (kv.Key.StartsWith("act:") || kv.Key.StartsWith("wed:") || kv.Key.StartsWith("bound:") || kv.Key.StartsWith("norm:") || kv.Key.StartsWith("god:"))
                    child.Expect.Absorb(kv.Key, kv.Value, 1, kv.Value);
        }
    }
}
