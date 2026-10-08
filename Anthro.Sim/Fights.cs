using System;
using System.Collections.Generic;
using System.Linq;
using Anthro.Sim.Core;
using Anthro.Sim.People;

namespace Anthro.Sim
{
    /// <summary>
    /// Fights, from a shove to a killing. Each fighter means to go only so far: to show anger, to
    /// hurt, or to kill. How far comes from what they feel (rage, desperation, love, guilt, fear) and
    /// from what their people have come to accept toward kin, friends, strangers and outsiders, which
    /// differs from people to people and is learned from what violence brings. A fight runs in
    /// exchanges: after each, everyone decides whether to keep going, give way or run; onlookers may
    /// join their own side or step in to stop it; a side that is losing breaks. Most killings happen
    /// when a fighter who means to kill presses on against someone already beaten or running.
    /// </summary>
    public sealed partial class Simulation
    {
        enum Aim { Display, Hurt, Kill }
        static readonly bool _debugFights = Environment.GetEnvironmentVariable("ANTHRO_DEBUG_FIGHTS") != null;

        sealed class Fighter
        {
            public Person P = null!;
            public int Side;
            public double Severity;
            public Aim Aim;
            public double Desperation;
            public bool Out, Fled;
            public double Dealt;
        }

        /// <summary>How a fight ended.</summary>
        internal sealed class FightResult
        {
            /// <summary>0 if nobody prevailed (stopped, or both gave way), else the side (1 or 2) left standing.</summary>
            public int Winner;
            public bool Stopped;
            public readonly List<Person> Dead = new List<Person>();
        }

        // ---- The learned culture of violence --------------------------------------------------------

        static readonly string[] _boundTopics = { "bound:kin", "bound:friend", "bound:neutral", "bound:distrusted", "bound:outsider" };

        /// <summary>
        /// How far this person's people go, they've come to think, against someone of this relation:
        /// 0 a show of anger, 1 a beating, 2 a killing. Learned and spread like any belief.
        /// </summary>
        static string BoundTopic(Relation r) => _boundTopics[(int)r];
        static double Bound(Person p, Relation r) => p.Expect.Get(BoundTopic(r), 0.8);

        /// <summary>Honour: standing won by fighting on, shame in giving way (-1 to 1). Some peoples prize it; some think it foolish.</summary>
        internal const string HonourTopic = "norm:honour";
        /// <summary>Peacemaking: whether stepping between fighters is what one does (0 to 1).</summary>
        internal const string PeaceTopic = "norm:peacemaking";
        static double Honour(Person p) => p.Expect.Get(HonourTopic, 0);
        static double PeaceView(Person p) => p.Expect.Get(PeaceTopic, 0.5);

        /// <summary>A people's starting culture of violence: where learning starts from.</summary>
        void SeedViolenceCulture(List<Person> group)
        {
            double inside = Rng.Normal(0.5, 0.35);                  // how far a quarrel among one's own goes (most peoples restrain it; some don't)
            double outside = inside + Math.Max(0, Rng.Normal(0.5, 0.3));
            double honour = Rng.Normal(0, 0.35), peace = Inheritance.Clamp01(Rng.Normal(0.5, 0.25));
            var offset = new Dictionary<Relation, double> { [Relation.Kin] = -0.4, [Relation.Friend] = -0.2, [Relation.Neutral] = 0, [Relation.Distrusted] = 0.2 };
            foreach (var p in group)
            {
                foreach (var kv in offset) p.Expect.Absorb(BoundTopic(kv.Key), Math.Max(0, inside + kv.Value + Rng.Normal(0, 0.1)), 1, 0);
                p.Expect.Absorb(BoundTopic(Relation.Outsider), Math.Max(0, outside + Rng.Normal(0, 0.1)), 1, 0);
                p.Expect.Absorb(HonourTopic, honour + Rng.Normal(0, 0.1), 1, 0);
                p.Expect.Absorb(PeaceTopic, Inheritance.Clamp01(peace + Rng.Normal(0, 0.1)), 1, 0);
            }
        }

        // ---- How far someone means to go -----------------------------------------------------------

        /// <summary>
        /// How far p means to go against o (0 show, 1 hurt, 2 kill): what their people accept toward
        /// someone like o, pushed up by rage and desperation, held back by love, kinship and guilt.
        /// </summary>
        double Severity(Person p, Person o, double desperation)
        {
            var rel = RelationTo(p, o);
            var theirs = Region.BandOf(o);
            double anger = p.GetGrievance(o) + (theirs != null && theirs != this ? p.GetBandGrievance(theirs.Id) : 0);
            // Rage that would kill takes a grievance on the scale of a life (a kinsman killed), not a theft
            // or a beating: those make people want to hurt. Where a people already accepts killing, it
            // takes less (that's the bound).
            double rage = anger / (anger + 10) * (0.5 + p.M(MindTrait.Temper));
            double love = Math.Max(0, p.GetAffinity(o)) + (p.IsKin(o) ? 0.5 : 0);
            double guilt = Felt(Guilt(p, o)) * 0.1;
            // Hunger makes people fight harder and longer (see resolve in Fight), but seldom to kill:
            // killing comes from hatred far more than from want.
            return Bound(p, rel) + rage * 0.9 + desperation * 0.2 - love * 0.8 - guilt + Rng.Normal(0, 0.15);
        }

        static Aim AimOf(double severity) => severity < 0.6 ? Aim.Display : severity < 1.5 ? Aim.Hurt : Aim.Kill;

        /// <summary>Whether someone has a weapon at hand: hunters carry their spears; anyone means to kill reaches for one.</summary>
        bool Armed(Person p, Aim aim) =>
            p.Current is Activities.Hunt || aim == Aim.Kill && (Band.AnyoneKnows("spear.stone") || Band.AnyoneKnows("spear.wood"));

        /// <summary>How badly a blow lands, by what the striker means and what they hold.</summary>
        double Wound(Fighter by)
        {
            double w = by.Aim switch
            {
                Aim.Display => Rng.Range(0, 0.04),
                Aim.Hurt => Rng.Range(0.05, 0.2),
                _ => Rng.Range(0.15, 0.4),
            };
            if (by.Aim != Aim.Display && Armed(by.P, by.Aim)) w *= 1.6;
            return w * (0.5 + by.P.B(BodyTrait.Strength));
        }

        // ---- The fight ------------------------------------------------------------------------------

        /// <summary>
        /// A fight between two sides. <paramref name="overFood"/> brings hunger into it; <paramref name="inCamp"/>
        /// lets onlookers take part (join a side, or step in).
        /// </summary>
        internal FightResult Fight(IList<Person> sideA, IList<Person> sideB, Deed? answers, bool overFood, bool inCamp, string kind = "quarrel", bool far = false)
        {
            var result = new FightResult();
            var fighters = new List<Fighter>();
            double Desperation(Person p) => !overFood || p.Home == null ? 0 : p.Home.Members.Select(m => 1 - m.Satiety).DefaultIfEmpty(0).Max();
            void Add(Person p, int side, IList<Person> foes)
            {
                if (fighters.Any(f => f.P == p) || !p.Alive) return;
                double d = Desperation(p);
                double sev = foes.Count == 0 ? 0 : foes.Max(o => Severity(p, o, d));
                // Someone still badly hurt from before can't fight: they're down from the start.
                bool laidUp = p.Injury >= 0.6;
                fighters.Add(new Fighter { P = p, Side = side, Severity = sev, Aim = AimOf(sev), Desperation = d, Out = laidUp, Fled = laidUp });
            }
            foreach (var p in sideA) Add(p, 1, sideB);
            foreach (var p in sideB) Add(p, 2, sideA);
            if (!fighters.Any(f => f.Side == 1) || !fighters.Any(f => f.Side == 2)) return result;

            // Onlookers: stand with their own, or step in between.
            var peacemakers = new List<Person>();
            if (inCamp)
                foreach (var o in Band.Members)
                {
                    if (!Grown(o) || o.Health < 0.4 || o.Injury >= 0.5 || !AwakeInCamp(o) || fighters.Any(f => f.P == o)) continue;
                    double For(int side) => fighters.Where(f => f.Side == side).Select(f => (o.IsKin(f.P) ? 0.8 : 0) + o.GetAffinity(f.P)).DefaultIfEmpty(0).Max();
                    double Against(int side) => fighters.Where(f => f.Side == side).Select(f => o.GetGrievance(f.P)).DefaultIfEmpty(0).Max();
                    double a = For(1) + Against(2) / 3, b = For(2) + Against(1) / 3;
                    double courage = 0.3 + o.M(MindTrait.Boldness) * 0.5 - o.Injury;
                    int pick = a > b ? 1 : 2;
                    // Taking a family's food by force: onlookers stand with the family or step in; nobody joins
                    // the takers who wasn't asked.
                    if (kind == "gang" && pick == 1) { if (PeaceView(o) * (0.5 + o.M(MindTrait.Warmth)) > 0.3) peacemakers.Add(o); continue; }
                    var against = fighters.Where(f => f.Side != pick).Select(f => f.P).ToList();
                    // Joining means being ready to hurt someone on the other side: how far they'd go is
                    // their people's and their own (a peaceful people's onlookers mostly stay out).
                    // Whether one takes sides in others' quarrels at all is a people's custom (and one's own lessons).
                    if (Math.Abs(a - b) > 0.6 && Math.Max(a, b) * courage * (0.3 + Sides(o) * 1.4) > 0.35 && against.Max(x => Severity(o, x, Desperation(o))) >= 0.6)
                        Add(o, pick, against);
                    else if (PeaceView(o) * (0.5 + o.M(MindTrait.Warmth)) + Math.Min(a, b) * 0.5 > 0.5) peacemakers.Add(o);
                }

            double Power(Fighter f) => Strength(f.P) * (0.6 + 0.4 * f.P.M(MindTrait.Temper)) * (Armed(f.P, f.Aim) ? 1.3 : 1) + Rng.Range(0, 0.4);
            var dealt = new Dictionary<(Person, Person), double>();
            var took = new Dictionary<Person, double>();
            string phase = "fight";
            void Hit(Fighter by, Fighter on, double wound)
            {
                if (_debugFights && on.P.Injury < 0.95 && on.P.Injury + wound >= 0.95)
                    Console.Error.WriteLine("KILLBLOW " + kind + " " + phase + " by " + by.Aim + (Armed(by.P, by.Aim) ? " armed" : "") + " before " + on.P.Injury.ToString("F2") + " wound " + wound.ToString("F2"));
                on.P.Injury = Inheritance.Clamp01(on.P.Injury + wound);
                dealt[(by.P, on.P)] = (dealt.TryGetValue((by.P, on.P), out var w) ? w : 0) + wound;
                took[on.P] = (took.TryGetValue(on.P, out var t) ? t : 0) + wound;
                by.Dealt += wound;
                // Down at once: nobody else in the fray keeps hitting someone who's fallen (only those
                // meaning to kill go after the fallen, below).
                if (on.P.Injury >= 0.6) { on.Out = true; on.Fled = true; }
            }

            // Exchange after exchange until it ends of itself: someone gives way, runs or goes down, a side
            // breaks, or someone steps in. Fighting is exhausting, and exhaustion feeds fear, so even two
            // furious people stop before long. (The bound on rounds is only a guard against a loop; fights
            // end well before it.)
            for (int round = 0; round < 40; round++)
            {
                var active = fighters.Where(f => !f.Out && f.P.Alive).ToList();
                if (!active.Any(f => f.Side == 1) || !active.Any(f => f.Side == 2)) break;
                // Someone stepping in: harder the more murderous the fight has become.
                if (peacemakers.Count > 0)
                {
                    double pull = peacemakers.Sum(o => 0.3 + PeaceView(o) * 0.5 + o.M(MindTrait.Charisma) * 0.3);
                    double fury = active.Max(f => f.Severity);
                    if (Rng.Chance(pull / (pull + fury * 2 + 0.5))) { result.Stopped = true; break; }
                }
                foreach (var f in active.OrderBy(_ => Rng.Range(0, 1.0)))
                {
                    if (f.Out || !f.P.Alive) continue;
                    var foes = fighters.Where(o => o.Side != f.Side && !o.Out && o.P.Alive).ToList();
                    if (foes.Count == 0) break;
                    // Whom to face: the one they hate most, whoever is on their kin, and not the strongest if they can help it.
                    Fighter? foe = null; double best = double.NegativeInfinity;
                    foreach (var o in foes)
                    {
                        double v = f.P.GetGrievance(o.P) + (fighters.Any(k => k.Side == f.Side && f.P.IsKin(k.P) && dealt.ContainsKey((o.P, k.P))) ? 1 : 0)
                                   - Strength(o.P) / Math.Max(0.1, Strength(f.P)) * 0.3 * f.P.W(Drive.Safety) + Rng.Range(0, 0.5);
                        if (v > best) { best = v; foe = o; }
                    }
                    Fighter win = f, lose = foe!;
                    if (Power(foe!) > Power(f)) { win = foe!; lose = f; }
                    Hit(win, lose, Wound(win));
                    if (Rng.Chance(0.5)) Hit(lose, win, Wound(lose) * 0.3);
                }
                // The dead are out; everyone else decides whether to keep going.
                foreach (var f in fighters.Where(f => !f.Out))
                {
                    if (f.P.Injury >= 0.95) { f.Out = true; continue; }
                    // Badly hurt, a body can't keep fighting whatever the will: they go down. Only someone who
                    // means to kill will finish them.
                    if (f.P.Injury >= 0.6) { f.Out = true; f.Fled = true; continue; }
                    var mine = fighters.Where(o => o.Side == f.Side && !o.Out).Sum(o => Strength(o.P));
                    var theirs = fighters.Where(o => o.Side != f.Side && !o.Out).Sum(o => Strength(o.P));
                    bool forKin = fighters.Any(k => k.Side == f.Side && k != f && f.P.IsKin(k.P));
                    double resolve = f.Severity * 0.6 + f.Desperation + Honour(f.P) * 0.4 + (forKin ? 0.3 : 0) + f.P.M(MindTrait.Boldness) * 0.3;
                    f.P.Fatigue = Inheritance.Clamp01(f.P.Fatigue + 0.15 * (1.2 - f.P.B(BodyTrait.Endurance) * 0.6));
                    double fear = f.P.Injury * 2 + f.P.Fatigue * 1.5 + Math.Max(0, theirs / Math.Max(0.1, mine) - 1) * 0.5 * f.P.W(Drive.Safety);
                    if (fear > resolve) { f.Out = true; f.Fled = true; }
                }
                // A side that has lost half its people (more, if it prizes standing firm) breaks and runs.
                foreach (int side in new[] { 1, 2 })
                {
                    var all = fighters.Where(f => f.Side == side).ToList();
                    if (all.Count < 3) continue;
                    double honour = all.Average(f => Honour(f.P));
                    if (all.Count(f => f.Out) >= all.Count * Math.Min(0.9, 0.5 + 0.3 * Math.Max(0, honour)))
                        foreach (var f in all.Where(f => !f.Out)) { f.Out = true; f.Fled = true; }
                }
            }

            // Those who mean to kill go after the beaten and the running: this is where most killings happen.
            phase = "pursuit";
            if (!result.Stopped)
                foreach (var f in fighters.Where(f => !f.Out && f.P.Alive && f.Aim == Aim.Kill))
                {
                    var down = fighters.Where(o => o.Side != f.Side && o.Fled && o.P.Alive && o.P.Injury < 0.95)
                        .OrderByDescending(o => f.P.GetGrievance(o.P)).FirstOrDefault();
                    if (down != null && Rng.Chance(0.35)) Hit(f, down, Wound(f));
                }

            // Who carried it.
            var standing1 = fighters.Count(f => f.Side == 1 && !f.Out && f.P.Alive);
            var standing2 = fighters.Count(f => f.Side == 2 && !f.Out && f.P.Alive);
            result.Winner = result.Stopped ? 0 : standing1 > 0 && standing2 == 0 ? 1 : standing2 > 0 && standing1 == 0 ? 2 : 0;

            // Every blow is a deed: felt, remembered, learned from.
            foreach (var kv in dealt)
            {
                var (by, on) = kv.Key;
                double back = dealt.TryGetValue((on, by), out var b) ? b : 0;
                RecordBlow(by, on, kv.Value, back, by == sideA.FirstOrDefault() ? answers : null, far);
            }
            // Having fought it out, the anger is largely spent: a fight often settles a quarrel, and a duel
            // (fought by the rules, before everyone) settles it most.
            double spent = kind == "duel" ? 0.8 : 0.4;
            foreach (var f in fighters)
                foreach (var o in fighters.Where(o => o.Side != f.Side))
                    f.P.AddGrievance(o.P, -f.P.GetGrievance(o.P) * spent);
            // Among peoples who prize standing firm, the winner of a duel is looked up to.
            if (kind == "duel" && result.Winner != 0)
            {
                var victor = fighters.First(f => f.Side == result.Winner).P;
                foreach (var w in Band.Members.Where(m => Grown(m) && m != victor && AwakeInCamp(m)))
                    if (Honour(w) > 0) LearnFollowing(w, victor, Honour(w) * 0.2);
            }
            foreach (var f in fighters)
                if (f.P.Alive && f.P.Injury >= 0.95)
                {
                    var killer = dealt.Where(kv => kv.Key.Item2 == f.P).OrderByDescending(kv => kv.Value).Select(kv => kv.Key.Item1).FirstOrDefault();
                    result.Dead.Add(f.P);
                    (Band.Members.Contains(f.P) ? this : Region.BandOf(f.P) ?? this).Kill(f.P, killer == null ? "wounds" : $"killed by {killer.Name}");
                    if (killer != null) AfterKilling(killer, f.P);
                }
            if (_debugFights) Console.Error.WriteLine(DescribeFight(kind, fighters, result));
            ThisYear.Fights++;
            ThisYear.FightsBy[kind] = (ThisYear.FightsBy.TryGetValue(kind, out var nf) ? nf : 0) + 1;
            ThisYear.FightDeathsBy[kind] = (ThisYear.FightDeathsBy.TryGetValue(kind, out var nd) ? nd : 0) + result.Dead.Count;
            NarrateFight(fighters, peacemakers, result, overFood);
            return result;
        }

        /// <summary>
        /// The bookkeeping of one person's blows on another in a fight: the deed (felt by witnesses and
        /// remembered), what the striker learns hitting got them, and the anger it leaves.
        /// </summary>
        void RecordBlow(Person attacker, Person target, double targetWound, double attackerWound, Deed? answers, bool far = false)
        {
            // An attack where nobody else could see is learned apart: it costs less in others' eyes.
            var deed = new Deed { Day = Clock.Day, Actor = attacker, Act = Act.Attack, Target = target, Amount = targetWound, Answers = answers, Far = far,
                                  Context = far ? Lesson(Act.Attack, RelationTo(attacker, target), false) : Lesson(Act.Attack, RelationTo(attacker, target)) };
            var seen = Happen(deed);
            if (attacker.Spouses.Contains(target)) { ConsiderLeaving(target, null, 0, attacker); ConsiderLeaving(attacker, null, 0, target); }
            if (answers != null) Credit(answers, -targetWound * WoundCost, seen, attacker);
            Learn(attacker, deed.Context, -attackerWound * WoundCost, CulturalDefault(Act.Attack, RelationTo(attacker, target), null));
            attacker.AddGrievance(target, -attacker.GetGrievance(target) * (targetWound > attackerWound ? 0.7 : 0.3));
            if (attackerWound > 0.05) attacker.AddGrievance(target, attackerWound * WoundCost * 0.5);
        }

        /// <summary>
        /// A killing among people who know each other changes what they think acceptable. Those close to
        /// the dead come to think killing goes too far; those who hated the dead think it was right. Where
        /// a people prizes honour, the killer gains standing with those who weren't close to the dead.
        /// </summary>
        void AfterKilling(Person killer, Person dead)
        {
            var rel = RelationTo(killer, dead);
            foreach (var w in Band.Members.Where(Grown))
            {
                if (w == killer) continue;
                double closeToDead = (w.IsKin(dead) ? 0.8 : 0) + Math.Max(0, w.GetAffinity(dead));
                double hatedDead = w.GetGrievance(dead) / (w.GetGrievance(dead) + 3);
                double current = Bound(w, rel);
                double verdict = current - closeToDead * 0.8 + hatedDead * 0.5;
                w.Expect.Absorb(BoundTopic(rel), Math.Max(0, verdict), 0.1 + closeToDead * 0.2, current);
                if (Honour(w) > 0 && closeToDead < 0.3) LearnFollowing(w, killer, Honour(w) * 0.3);
            }
        }

        string DescribeFight(string kind, List<Fighter> fighters, FightResult result)
        {
            var parts = new List<string>();
            foreach (int side in new[] { 1, 2 })
            {
                var other = fighters.First(o => o.Side != side).P;
                var ms = fighters.Where(f => f.Side == side).Select(f =>
                    f.P.Name + "[" + f.Aim.ToString()[0] + " sev" + f.Severity.ToString("F1") + " bnd" + Bound(f.P, RelationTo(f.P, other)).ToString("F1")
                    + " des" + f.Desperation.ToString("F1") + " inj" + f.P.Injury.ToString("F2") + (f.P.Alive && f.P.Injury < 0.95 ? "" : " DEAD") + (f.Fled ? " fled" : "") + "]");
                parts.Add(string.Join(" ", ms));
            }
            return "FIGHT " + kind + " Y" + Clock.Year + " " + string.Join(" | ", parts) + " => winner " + result.Winner + " dead " + result.Dead.Count + (result.Stopped ? " stopped" : "");
        }

        /// <summary>
        /// Someone falls on a person they hate who is out alone, away from anyone who would see. If the
        /// victim dies, nobody knows who did it: the dead one's family suspect someone (whoever the dead
        /// had quarrelled with, or whoever they themselves trust least), rightly or not.
        /// </summary>
        internal void DoAmbush(Person attacker, Person victim)
        {
            var result = Fight(new[] { attacker }, new[] { victim }, LatestWrong(attacker, victim), overFood: false, inCamp: false, kind: "ambush", far: true);
            if (!result.Dead.Contains(victim)) return;
            foreach (var kin in Band.Members.Where(m => Grown(m) && m.IsKin(victim)))
            {
                var suspect = Band.Members.Where(o => o != kin && !o.IsKin(victim) && Grown(o))
                    .OrderByDescending(o => victim.GetGrievance(o) * 0.5 + (1 - kin.GetTrust(o)) + (o == attacker ? kin.GetGrievance(o) * 0.2 : 0))
                    .FirstOrDefault();
                if (suspect == null) continue;
                kin.AddGrievance(suspect, 8);
                if (suspect == attacker) kin.AddGrievance(attacker, 4);
            }
            var blamed = Band.Members.Where(m => m.IsKin(victim)).SelectMany(k => k.Grievance).GroupBy(kv => kv.Key).OrderByDescending(g => g.Sum(kv => kv.Value)).Select(g => MemberById(g.Key)).FirstOrDefault();
            Chronicle.Write(Clock, Importance.Major, $"{victim.Name} was found dead away from camp.{(blamed != null ? $" Their family blame {blamed.Name}{(blamed == attacker ? "" : ", though it was " + attacker.Name)}." : "")}");
        }

        void NarrateFight(List<Fighter> fighters, List<Person> peacemakers, FightResult result, bool overFood)
        {
            var a = fighters.Where(f => f.Side == 1).Select(f => f.P).ToList();
            var b = fighters.Where(f => f.Side == 2).Select(f => f.P).ToList();
            string Names(List<Person> s) => s.Count == 1 ? s[0].Name : $"{s[0].Name} and {s.Count - 1} other{(s.Count > 2 ? "s" : "")}";
            string over = overFood ? " over food" : "";
            string end = result.Stopped ? $"; {peacemakers.OrderByDescending(p => PeaceView(p)).First().Name} stepped in and it ended"
                : result.Winner == 1 ? (b.Count > 1 ? $"; {b[0].Name}'s side broke and ran" : $"; {b[0].Name} gave way")
                : result.Winner == 2 ? (a.Count > 1 ? $"; {a[0].Name}'s side broke and ran" : $"; {a[0].Name} gave way")
                : "; both drew back";
            var importance = result.Dead.Count > 0 || a.Count + b.Count > 3 ? Importance.Major : Importance.Notable;
            Chronicle.Write(Clock, importance, $"{Names(a)} fought {Names(b)}{over}{end}{(result.Dead.Count > 0 ? $" ({result.Dead.Count} dead)" : "")}.");
        }
    }
}
