using System;
using System.Collections.Generic;
using System.Linq;
using Anthro.Sim.Core;
using Anthro.Sim.Ecology;
using Anthro.Sim.People;
using Anthro.Sim.World;

namespace Anthro.Sim.Activities
{
    /// <summary>
    /// A group hunt. The party has no rules of its own: every choice (who comes, which animal,
    /// whether to keep going, whether to chase) is each member weighing gain, danger, need and
    /// tiredness for themselves, then the group following influence-weighted opinion. Members who
    /// disagree strongly and don't defer to the leader walk home.
    ///
    /// Outcomes are physical: get within weapon range before the animals bolt (flight distance
    /// grows with fear), land enough hits to match the animal's size, and face a charge from
    /// aggressive animals you wound.
    /// </summary>
    /// <summary>Ways a party closes in on its quarry.</summary>
    public enum Tactic { Stalk, Ambush, Drive, Surround, Persistence, FireDrive }

    public sealed class HuntParty
    {
        /// <summary>
        /// What people who've always lived here expect an hour of hunting to bring, before their own
        /// experience moves it: the measured return of thrusting-spear hunting on this land (~0.25 rations
        /// per person-hour in calibrated runs).
        /// </summary>
        public const double FallbackPerHour = 0.25;


        public enum Phase { Gather, Out, Track, Stalk, Strike, Chase, Butcher, Back, Done }

        public readonly List<Person> Members = new List<Person>();
        public readonly Person Leader;
        public Phase Current { get; private set; } = Phase.Gather;
        public AnimalGroup? Target { get; private set; }
        public Weapon? Weapon { get; private set; }
        public Person? Killer { get; private set; }

        int _left, _hours, _travel, _hurt, _closeCalls, _waited;
        /// <summary>Where the party thinks its quarry is (from a sighting, then from its tracks).</summary>
        double _believedX, _believedY;
        bool _searching;
        int _searchHours;
        public Tactic Tactic { get; private set; } = Tactic.Stalk;

        /// <summary>Where this person thinks a herd is: their latest sighting of it, or the party's tracking once they're on it.</summary>
        (double x, double y) Believed(Person m, AnimalGroup g)
        {
            if (Target == g && Current != Phase.Gather) return (_believedX, _believedY);
            if (m.Map.Game.TryGetValue(g.Id, out var s)) return (s.X, s.Y);
            return (g.X, g.Y);
        }
        double _damage, _rations, _eatenAtKill;
        /// <summary>How hurt each member already was when they joined, so a hunt is judged only by the wounds it caused.</summary>
        readonly Dictionary<Person, double> _injuryBefore = new Dictionary<Person, double>();
        Species? _killed;
        long _lastTick = -1;

        public HuntParty(Person leader) { Leader = leader; Members.Add(leader); _injuryBefore[leader] = leader.Injury; Count("hunts started"); }

        public bool IsDone => Current == Phase.Done;

        public string StepName => Current switch
        {
            Phase.Gather => "gathering the party",
            Phase.Out => "walking out",
            Phase.Track => Target == null ? "tracking" : $"tracking {Target.Species.Plural}",
            Phase.Stalk => "stalking",
            Phase.Strike => "striking",
            Phase.Chase => "chasing a wounded animal",
            Phase.Butcher => "butchering",
            Phase.Back => _rations > 0 ? "carrying meat home" : "walking back",
            _ => "home",
        };

        /// <summary>
        /// Fit enough to hunt. Desperation bends the limits: a starving band sends out people who'd
        /// normally stay home hurt or tired.
        /// </summary>
        public static bool CanHunt(Simulation sim, Person p)
        {
            double age = p.AgeYears(sim.Clock);
            int people = Math.Max(1, sim.Band.Members.Count);
            double desperate = Inheritance.Clamp01(1 - sim.Band.FoodTotal / (people * 0.9) / 6) * (1 - p.Satiety * 0.5);
            return Physiology.HuntCapacity(p, age) > 0.75
                   && p.Health > 0.6 - 0.3 * desperate && p.Injury < 0.3 + 0.2 * desperate
                   && p.Sickness < 0.2 + 0.2 * desperate && p.Fatigue < 0.6 + 0.2 * desperate;
        }

        /// <summary>
        /// Before any experience: big, aggressive animals look dangerous. The belief is "how likely
        /// is it that someone gets hurt when we go after this animal".
        /// </summary>
        public static double InstinctiveDanger(Species sp) => Math.Min(1, sp.Aggression * sp.Size / 4);

        /// <summary>How many people a charging animal can reach at once.</summary>
        static int ChargeTargets(Species sp) => sp.Size >= 4 ? 2 : 1;

        // ---- How individuals weigh things ------------------------------------------------------

        /// <summary>How much a person's opinion carries in the group.</summary>
        /// <summary>How much a person's opinion carries in the party: their charisma and skill, and how much the others have come to follow them.</summary>
        double Influence(Person m) => 0.5 + m.M(MindTrait.Charisma) + Members.Where(o => o != m).Select(o => Simulation.Following(o, m)).DefaultIfEmpty(0).Average() * 0.5
                                      + m.S(Skill.Hunting) * 0.5 + (m == Leader ? 0.5 : 0);

        /// <summary>How readily someone goes along with the leader against their own judgment.</summary>
        double Deference(Person m) =>
            m.GetAffinity(Leader) * (0.5 + Leader.M(MindTrait.Charisma)) + m.M(MindTrait.Sociability) * 0.3 + (1 - m.M(MindTrait.Temper)) * 0.2;

        /// <summary>What this person believes the odds are that someone gets hurt going after this animal.</summary>
        static double Danger(Person m, Species sp) => m.Expect.Get(Expectations.Danger(sp.Name), InstinctiveDanger(sp));

        static double Norm(Person m, string name, double fallback) => m.Expect.Get(Expectations.Norm(name), fallback);

        /// <summary>
        /// How much meat this person would give to avoid a typical injury: the resilient shrug off
        /// pain more easily; a cautious culture (low valor) makes everyone more careful.
        /// </summary>
        static double InjuryCost(Person m) =>
            60 * (1.3 - m.M(MindTrait.Resilience) * 0.6) * m.W(Drive.Safety);

        /// <summary>
        /// How much this person's own life weighs against meat: far more for a parent of small
        /// children, less for the bold, less in a culture that prizes valor, less in old age.
        /// </summary>
        double LifeValue(Simulation sim, Person m)
        {
            int youngKids = m.Children.Count(c => c.Alive && c.AgeYears(sim.Clock) < 10);
            double age = m.AgeYears(sim.Clock);
            return 400 * (1 + 0.4 * youngKids) * (age > 50 ? 0.6 : 1)
                   * (1.4 - m.M(MindTrait.Boldness) * 0.8) * m.W(Drive.Safety) * (0.7 + 0.3 * m.W(Drive.Family));
        }

        /// <summary>
        /// A person's own sense of what going after this animal is worth: their share of meat the
        /// band can eat before it rots, scaled by how hungry the band is, minus how afraid they are
        /// of it and how far they'd have to walk on tired legs. In rations.
        /// </summary>
        /// <summary>What a hunter brings to every judgment of a hunt, worked out once (it doesn't depend on the animal).</summary>
        readonly struct Outlook
        {
            public readonly double CanEat, Hunger, InjuryCost, LifeValue;
            public Outlook(double canEat, double hunger, double injuryCost, double lifeValue) { CanEat = canEat; Hunger = hunger; InjuryCost = injuryCost; LifeValue = lifeValue; }
        }

        Outlook OutlookOf(Simulation sim, Person m)
        {
            // What the band could still eat before it spoils, beyond the meat already in camp; and, for
            // those who can dry it, what their hearth is short of for the lean seasons ahead.
            double canEat = Math.Max(0, sim.Band.Members.Count * 0.9 * sim.MeatKeepsDays - sim.Band.MeatTotal)
                            + (Preserve.CanPreserve(m) || sim.MeatKeepsDays >= 20 ? Decider.StoreShortfallRations(sim, m) : 0);
            int people = Math.Max(1, sim.Band.Members.Count);
            double hunger = Inheritance.Clamp01(1 - sim.Band.FoodTotal / (people * 0.9) / 6);
            return new Outlook(canEat, hunger, InjuryCost(m), LifeValue(sim, m));
        }

        double Value(Simulation sim, Person m, AnimalGroup g, int partySize, double hope) => Value(sim, m, g, partySize, hope, OutlookOf(sim, m));

        double Value(Simulation sim, Person m, AnimalGroup g, int partySize, double hope, Outlook o)
        {
            var sp = g.Species;
            double canEat = o.CanEat, hunger = o.Hunger;
            double feasible = Math.Min(1, partySize / (sp.Size * 0.7));
            double gain = Math.Min(sp.Meat, canEat) * feasible * hope * (0.5 + hunger) / partySize * 4;
            // Bringing down something big earns standing; the bold, and a culture that prizes valor, care more.
            double glory = m.M(MindTrait.Boldness) * m.Drives[(int)Drive.Standing] * sp.Meat / 40 * hope;
            // Fear is weighed by how bad a wound from this animal would be and how likely it is to kill,
            // and shared out: a charge reaches only one or two of the party.
            double severity = 0.5 + sp.Size / 4;
            double lethal = Math.Max(0, Math.Min(1, (severity - 1) * 0.35));
            double myShare = Math.Min(1, (double)ChargeTargets(sp) / partySize);
            double harm = Danger(m, sp) * myShare * (o.InjuryCost * severity + o.LifeValue * lethal);
            var (gx, gy) = Believed(m, g);
            double distance = WorldMap.Distance(gx, gy, sim.Band.CampX, sim.Band.CampY);
            return gain + glory - harm - distance * WorldMap.CellMeters / 1000 * 1.5 * (0.5 + m.Fatigue);
        }

        /// <summary>What going home is worth to someone: rest for the tired, safety for the hurt.</summary>
        static double HomeValue(Person m) => 2 + m.Fatigue * 15 + m.Injury * 30;

        // ---- Forming the party -----------------------------------------------------------------

        /// <summary>
        /// Each candidate weighs the hunt against the best thing they'd otherwise do, plus the pull
        /// of being asked by this leader.
        /// </summary>
        public void Recruit(Simulation sim)
        {
            foreach (var p in sim.Band.Members.ToList())
            {
                if (p == Leader || !p.Alive || !CanHunt(sim, p) || p.Current is Hunt || (p.Current != null && !p.Current.Interruptible)) continue;
                double want = Decider.ScoreOf(sim, p, Verb.Hunt);
                if (double.IsNegativeInfinity(want)) continue;
                // Being asked by someone you like and trust to lead a hunt pulls you along.
                double pull = (p.GetAffinity(Leader) * 0.5 + (p.GetTrust(Leader) - 0.5)) * (0.3 + Leader.M(MindTrait.Charisma) * 0.5)
                              + Simulation.Following(p, Leader) * 0.3;
                if (want + pull + sim.Rng.Normal(0, 0.1) <= Decider.BestAlternative(sim, p, Verb.Hunt)) continue;
                Members.Add(p);
                _injuryBefore[p] = p.Injury;
                p.Current = new Hunt(p, this);
            }
        }

        public void Advance(Simulation sim)
        {
            if (_lastTick == sim.Clock.Tick || IsDone) return;
            _lastTick = sim.Clock.Tick;
            _hours++;
            if (_hours == 100) Count("~stuck at 100h in " + Current + (_searching ? " (searching)" : "") + " tactic " + Tactic);
            var alive = Members.Where(m => m.Alive).ToList();
            if (alive.Count == 0) { Current = Phase.Done; return; }
            foreach (var m in alive)
            {
                m.Fatigue = Inheritance.Clamp01(m.Fatigue + 0.05);
                m.Practice(Skill.Hunting, 1);
            }
            for (int i = 0; i < alive.Count; i++)
                for (int j = i + 1; j < alive.Count; j++) sim.Contact(alive[i], alive[j], 0.3);

            using (Core.Profiler.Measure("hunt phase ", Current))
            switch (Current)
            {
                case Phase.Gather: ChooseTarget(sim, alive); break;
                case Phase.Out:
                    if (--_left > 0) break;
                    foreach (var m in alive) sim.LookAround(m, _believedX, _believedY);
                    if (_searching) { Current = Phase.Gather; ChooseTarget(sim, alive); }
                    else Current = Phase.Track;
                    break;
                case Phase.Track: Track(sim, alive); break;
                case Phase.Stalk: Stalk(sim, alive); break;
                case Phase.Strike: Strike(sim, alive); break;
                case Phase.Chase: Chase(sim, alive); break;
                case Phase.Butcher: if (--_left <= 0) GoBack(); break;
                case Phase.Back: if (--_left <= 0) Finish(sim, alive); break;
            }
        }

        /// <summary>
        /// Everyone names the animal they'd go after, or home if nothing seems worth it. The group
        /// follows the option with the most influence behind it. Those who think it's a bad idea and
        /// don't defer to the leader leave.
        /// </summary>
        void ChooseTarget(Simulation sim, List<Person> alive)
        {
            // Herds someone in the party has seen in the last few days, within a day's walk.
            var groups = new List<AnimalGroup>();
            var seen = new HashSet<int>();
            foreach (var m in alive)
                foreach (var s in m.Map.Game.Values)
                {
                    if (sim.Clock.Day - s.Day > 5 || !s.Species.Huntable || seen.Contains(s.GroupId)) continue;
                    if (WorldMap.Distance(s.X, s.Y, sim.Band.CampX, sim.Band.CampY) > WorldMap.FromMeters(10000)) continue;
                    var g = sim.Ecology.ById(s.GroupId);
                    if (g != null) { groups.Add(g); seen.Add(g.Id); }
                }
            if (groups.Count == 0)
            {
                if (!_searching) { GoLooking(sim, alive); return; }
                // Nothing here: walk on through new country, scanning as they go, until they find
                // something or must turn for home before dark. Most of real hunting is this; where game
                // is scarce, whole days come back empty.
                int home = sim.TravelHours(WorldMap.Distance(_believedX, _believedY, sim.Band.CampX, sim.Band.CampY), Leader);
                if (_searchHours >= 8 || sim.Clock.DaylightLeft <= home + 1) { _travel = Math.Max(1, home); GoBack(); return; }
                _searchHours++;
                double angle = sim.Rng.Range(0, Math.PI * 2), step = WorldMap.FromMeters(2500);
                _believedX = Math.Max(0, Math.Min(sim.World.Size - 1, _believedX + Math.Cos(angle) * step));
                _believedY = Math.Max(0, Math.Min(sim.World.Size - 1, _believedY + Math.Sin(angle) * step));
                foreach (var m in alive) { m.X = _believedX; m.Y = _believedY; sim.LookAround(m, _believedX, _believedY); }
                return;
            }
            var support = new Dictionary<int, double>();
            foreach (var m in alive)
            {
                int pick = -1;
                double best = HomeValue(m);
                var outlook = OutlookOf(sim, m);
                foreach (var g in groups)
                {
                    double v = Value(sim, m, g, alive.Count, 1, outlook) + sim.Rng.Normal(0, 3);
                    if (v > best) { best = v; pick = g.Id; }
                }
                support[pick] = (support.TryGetValue(pick, out var s) ? s : 0) + Influence(m);
            }
            int winner = support.OrderByDescending(kv => kv.Value).First().Key;
            if (winner < 0) { GoBack(); return; }

            Target = groups.First(g => g.Id == winner);
            _searching = false;
            (_believedX, _believedY) = alive.Where(m => m.Map.Game.ContainsKey(winner)).Select(m => m.Map.Game[winner]).OrderByDescending(s => s.Day)
                                            .Select(s => (s.X, s.Y)).DefaultIfEmpty((Target.X, Target.Y)).First();
            foreach (var m in alive.Where(m => m != Leader))
                if (Value(sim, m, Target, alive.Count, 1) < HomeValue(m) && Deference(m) < 0.5) Leave(sim, m);

            Weapon = Weapon.Best(sim.Band, Target);
            _travel = Math.Max(1, sim.TravelHours(WorldMap.Distance(_believedX, _believedY, sim.Band.CampX, sim.Band.CampY), Leader));
            _left = _travel;
            Current = Phase.Out;
        }

        /// <summary>
        /// Nobody has seen game lately: the party goes to look where game has been seen before (or
        /// open ground they know), and decides what to go after once they're there.
        /// </summary>
        void GoLooking(Simulation sim, List<Person> alive)
        {
            _searching = true;
            var old = alive.SelectMany(m => m.Map.Game.Values).Where(s => s.Species.Huntable && sim.Clock.Day - s.Day <= 14)
                .OrderByDescending(s => s.Day).Select(s => ((double, double)?)(s.X, s.Y)).FirstOrDefault();
            (double x, double y) spot;
            if (old != null) spot = old.Value;
            else
            {
                double r = WorldMap.FromMeters(4000);
                spot = (sim.Band.CampX + sim.Rng.Range(-r, r), sim.Band.CampY + sim.Rng.Range(-r, r));
            }
            _believedX = Math.Max(0, Math.Min(sim.World.Size - 1, spot.x));
            _believedY = Math.Max(0, Math.Min(sim.World.Size - 1, spot.y));
            _travel = Math.Max(1, sim.TravelHours(WorldMap.Distance(_believedX, _believedY, sim.Band.CampX, sim.Band.CampY), Leader));
            _left = _travel;
            Current = Phase.Out;
        }

        // ---- Tactics ----------------------------------------------------------------------------

        static readonly Dictionary<(Tactic, string), string> _tacticTopics = new Dictionary<(Tactic, string), string>();

        public static string TacticTopic(Tactic t, Species sp)
        {
            lock (_tacticTopics)
            {
                if (!_tacticTopics.TryGetValue((t, sp.Name), out var s)) _tacticTopics[(t, sp.Name)] = s = $"tactic:{t.ToString().ToLowerInvariant()}:{sp.Name}";
                return s;
            }
        }

        /// <summary>Which ways of closing in are open to this party, here, now.</summary>
        IEnumerable<Tactic> Feasible(Simulation sim, List<Person> alive)
        {
            yield return Tactic.Stalk;
            var t = Target!;
            var around = sim.World.Around(t.X, t.Y, WorldMap.FromMeters(1000)).ToList();
            bool wet = around.Any(c => c.Terrain == Terrain.Marsh || c.Terrain == Terrain.River);
            bool open = around.Count(c => c.Terrain == Terrain.Grassland) > around.Count / 2;
            // Animals come to water to drink: a place to wait for them.
            if (wet) yield return Tactic.Ambush;
            if (wet && alive.Count >= 4) yield return Tactic.Drive;
            if (open && alive.Count >= 6 && alive.Any(m => m.Knows.Contains("hunt.surround"))) yield return Tactic.Surround;
            if (alive.Any(m => m.Knows.Contains("hunt.persist"))) yield return Tactic.Persistence;
            if (open && sim.Clock.Season != Core.Season.Spring && alive.Any(m => m.Knows.Contains("fire.land"))) yield return Tactic.FireDrive;
        }

        /// <summary>
        /// The party settles on how to close in: each member favors the way that has brought them the
        /// most against this kind of animal (what they've learned, their band's habits, their own
        /// patience or boldness), and the group follows influence.
        /// </summary>
        void ChooseTactic(Simulation sim, List<Person> alive)
        {
            var options = Feasible(sim, alive).ToList();
            var support = new Dictionary<Tactic, double>();
            foreach (var m in alive)
            {
                Tactic pick = Tactic.Stalk;
                double best = double.MinValue;
                foreach (var t in options)
                {
                    double v = m.Expect.Get(TacticTopic(t, Target!.Species), FallbackPerHour)
                               + (t == Tactic.Ambush ? m.M(MindTrait.Patience) * 0.1 : t == Tactic.Stalk ? m.M(MindTrait.Boldness) * 0.05 : 0)
                               // The curious sometimes favor something they haven't been doing.
                               + sim.Rng.Normal(0, 0.03 + 0.1 * m.M(MindTrait.Curiosity));
                    if (v > best) { best = v; pick = t; }
                }
                support[pick] = (support.TryGetValue(pick, out var s) ? s : 0) + Influence(m);
            }
            Tactic = support.OrderByDescending(kv => kv.Value).First().Key;
            _waited = 0;
        }

        void Leave(Simulation sim, Person m)
        {
            Members.Remove(m);
            m.Current = null;
            m.LastHuntDay = sim.Clock.Day;
        }

        /// <summary>
        /// Keep going? Each member weighs the remaining chance of meat against their fear (sharpened by
        /// anything that's gone wrong today) and their tiredness. Darkness ends any hunt.
        /// </summary>
        bool KeepGoing(Simulation sim, List<Person> alive, double hope)
        {
            if (!sim.Clock.IsDaylight || Target == null || Target.Gone) return false;
            double go = 0, stop = 0;
            foreach (var m in alive)
            {
                if (Value(sim, m, Target, alive.Count, hope) > HomeValue(m)) go += Influence(m);
                else stop += Influence(m);
            }
            return go > stop;
        }

        void Track(Simulation sim, List<Person> alive)
        {
            if (Target == null || Target.Gone) { GoBack(); return; }
            // Hunting dogs follow scent where people lose the trail: as much help as there are dogs to go round.
            double skill = alive.Average(m => m.S(Skill.Hunting)) + (sim.Band.AnyoneKnows("dog.hunt") ? 0.2 * Math.Min(1, sim.Band.Dogs / (double)alive.Count) : 0)
                           + (alive.Any(m => m.Knows.Contains("trap.trail")) ? 0.1 : 0);   // knowing the game trails
            // In sight of them: on to the approach.
            if (WorldMap.Distance(_believedX, _believedY, Target.X, Target.Y) < WorldMap.FromMeters(500))
            {
                _believedX = Target.X; _believedY = Target.Y;
                foreach (var m in alive) m.Map.Sight(Target, sim.Clock.Day);
                ChooseTactic(sim, alive);
                Current = Phase.Stalk;
                return;
            }
            // Find their trail near where the animals were last seen, and read how old it is.
            var trail = Target.Trail;
            int at = -1; double nearest = WorldMap.FromMeters(1500);
            for (int i = 0; i < trail.Count; i++)
            {
                double d = WorldMap.Distance(trail[i].X, trail[i].Y, _believedX, _believedY);
                if (d < nearest) { nearest = d; at = i; }
            }
            long day = sim.Clock.Day;
            bool readable = at >= 0 && Ecology.EcologySystem.Legible(sim.World[(int)trail[at].X, (int)trail[at].Y], day - trail[at].Day);
            if (!readable && !sim.Rng.Chance(0.15 * skill))
            {
                // The trail's gone cold: all they can do is cast about for fresh sign.
                Count("trail cold (" + Target.Species.Name + ")");
                // That lead is spent: nobody here will go looking there again on the strength of it.
                foreach (var m in alive) m.Map.Game.Remove(Target.Id);
                Target = null;
                _searching = true;
                Current = Phase.Gather;
                return;
            }
            // Follow it toward newer sign: a skilled tracker reads it at a walk; fresh snow makes it easy.
            bool snow = sim.World[(int)_believedX, (int)_believedY].Snow > 3;
            double step = WorldMap.FromMeters(1000 + 2000 * Math.Min(1, skill)) * (snow ? 1.3 : 1);
            int next = Math.Max(0, at);
            while (step > 0)
            {
                (double x, double y) goal = next + 1 < trail.Count ? (trail[next + 1].X, trail[next + 1].Y) : (Target.X, Target.Y);
                double d = WorldMap.Distance(_believedX, _believedY, goal.x, goal.y);
                if (d <= step) { _believedX = goal.x; _believedY = goal.y; step -= d; if (next + 1 >= trail.Count) break; next++; }
                else { _believedX += (goal.x - _believedX) * step / d; _believedY += (goal.y - _believedY) * step / d; step = 0; }
            }
            foreach (var m in alive) { m.X = _believedX; m.Y = _believedY; }
            if (!KeepGoing(sim, alive, 0.5)) GoBack();
        }

        /// <summary>
        /// Get within striking range. Two ways: creep up before the animals notice (hopeless with a
        /// thrusting spear unless very skilled, easy with weapons that reach far), or, where there's
        /// marsh or river nearby, spread out and drive the herd into it, where the animals flounder
        /// and hunters can close in. Driving needs enough people to turn a herd. The party uses
        /// whichever the ground and their numbers allow best.
        /// </summary>
        void Stalk(Simulation sim, List<Person> alive)
        {
            if (Target == null || Target.Gone) { GoBack(); return; }
            double skill = alive.Average(m => m.S(Skill.Hunting));
            double stealth = alive.Average(m => (m.S(Skill.Hunting) + m.M(MindTrait.Patience)) / 2)
                             * Math.Max(0.5, 1 - 0.05 * (alive.Count - 2));
            double creep = Math.Min(0.95, stealth * Weapon!.Range / Target.FlightDistance * 1.5);
            bool boggy = sim.World.Around(Target.X, Target.Y, WorldMap.FromMeters(1000)).Any(c => c.Terrain == Terrain.Marsh || c.Terrain == Terrain.River);
            double drive = boggy ? 0.35 * Math.Min(1, (alive.Count - 1) / 5.0) * (0.5 + skill) * (1 - 0.5 * Target.Species.Speed) : 0;
            double fear = Target.Fear;
            double patience = alive.Average(m => m.M(MindTrait.Patience));
            double approach = Tactic switch
            {
                Tactic.Drive => drive,
                Tactic.Ambush => 0.25 * patience * (1 - 0.6 * fear),
                Tactic.Surround => 0.35 * Math.Min(1, (alive.Count - 2) / 6.0) * (0.5 + skill) * (1 - 0.3 * fear),
                Tactic.Persistence => 0.3 * alive.Average(m => m.B(BodyTrait.Endurance)) * (0.4 + 0.6 * Math.Max(0, Math.Min(1, (sim.CampCell.Temperature - 8) / 12))),
                Tactic.FireDrive => 0.5,
                _ => creep,
            };
            // Herds that have been hunted a lot don't let people near.
            approach *= 1 - 0.6 * Target.Shy;
            if (sim.Rng.Chance(Math.Max(0.02, approach))) { Current = Phase.Strike; return; }
            // Waiting in ambush doesn't spook them; they can wait some hours yet.
            if (Tactic == Tactic.Ambush && ++_waited < 5) return;
            if (Tactic == Tactic.FireDrive)
                foreach (var g in sim.Ecology.Near(Target.X, Target.Y, WorldMap.FromMeters(3000))) sim.Ecology.Startle(g, 0.5);

            sim.Ecology.Startle(Target, 0.3);
            Target.Shy = Math.Min(1, Target.Shy + 0.05);
            sim.Band.StalkFailures += 1;
            if (KeepGoing(sim, alive, 0.4)) Current = Phase.Track;
            else GoBack();
        }

        /// <summary>
        /// Everyone throws or thrusts. Hits add up against the animal's size. A wounded aggressive
        /// animal may charge; at close range that reaches the hunters.
        /// </summary>
        void Strike(Simulation sim, List<Person> alive)
        {
            if (Target == null || Target.Gone) { GoBack(); return; }
            var sp = Target.Species;
            var weapon = Weapon!;
            double toolFactor = (sim.Band.Materials.Tools >= alive.Count ? 1 : 0.7) * (sim.Band.AnyoneKnows("bone.point") ? 1.1 : 1);
            sim.Band.Materials.Tools = Math.Max(0, sim.Band.Materials.Tools - 0.2 * alive.Count);

            foreach (var m in alive)
            {
                double hit = 0.3 + 0.5 * m.S(Skill.Hunting) + (m.B(BodyTrait.Dexterity) - 0.5) * 0.2;
                if (sim.Rng.Chance(hit)) _damage += weapon.Power * toolFactor * sim.Rng.Range(0.6, 1.4) * (0.8 + m.B(BodyTrait.Strength) * 0.4);
            }
            sim.Ecology.Startle(Target, 0.5);

            if (_damage >= sp.Size) { Kill(sim, alive); return; }

            bool wounded = _damage > 0;
            if (wounded && sim.Rng.Chance(sp.Aggression)) Charge(sim, alive.Where(m => m.Alive).ToList(), sp, weapon);
            if (_damage >= sp.Size) { Kill(sim, alive.Where(m => m.Alive).ToList()); return; }

            var standing = alive.Where(m => m.Alive).ToList();
            if (standing.Count == 0) { Current = Phase.Done; return; }
            // A wounded animal is worth more effort the closer it is to dropping.
            if (wounded && KeepGoing(sim, standing, 0.3 + 0.6 * _damage / sp.Size)) { Current = Phase.Chase; _left = 2; return; }
            _damage = 0;
            if (!wounded && KeepGoing(sim, standing, 0.4)) Current = Phase.Track;
            else GoBack();
        }

        /// <summary>
        /// The animal goes for one or two people, usually the nearest and boldest. Everyone else
        /// gets a strike in as it comes; enough of them can drop it before it reaches anyone.
        /// </summary>
        void Charge(Simulation sim, List<Person> alive, Species sp, Weapon weapon)
        {
            _closeCalls++;
            var targets = alive.OrderByDescending(m => m.M(MindTrait.Boldness) + sim.Rng.Range(0, 0.5)).Take(ChargeTargets(sp)).ToList();
            foreach (var m in alive.Except(targets))
            {
                double hit = 0.3 + 0.5 * m.S(Skill.Hunting);
                if (sim.Rng.Chance(hit)) _damage += weapon.Power * sim.Rng.Range(0.6, 1.4) * (0.8 + m.B(BodyTrait.Strength) * 0.4);
            }
            if (_damage >= sp.Size) return;

            double exposure = weapon.CloseRange ? 1 : 0.4;
            foreach (var m in targets)
            {
                if (!sim.Rng.Chance(0.7 * exposure)) continue;
                _hurt++;
                double wound = sim.Rng.Range(0.15, 0.6) * (0.5 + sp.Size / 4) * (1.2 - m.B(BodyTrait.Endurance) * 0.4);
                m.Injury = Inheritance.Clamp01(m.Injury + wound);
                bool trample = sp.Size >= 4;
                if (m.Injury >= 0.95) sim.Kill(m, trample ? $"trampled by a {sp.Name}" : $"gored by a {sp.Name}");
                else sim.Chronicle.Write(sim.Clock, Importance.Notable, $"A wounded {sp.Name} turned on the hunters and hurt {m.Name}.");
            }
            // Everyone saw it: fear sharpens on the spot, and shapes whether they keep going.
            if (_hurt > 0)
                foreach (var m in alive.Where(m => m.Alive))
                    m.Expect.LearnDanger(m, sp.Name, 1, InstinctiveDanger(sp), 0.5);
        }

        /// <summary>Run down a wounded animal: endurance and nerve against its speed and how badly it's hurt.</summary>
        void Chase(Simulation sim, List<Person> alive)
        {
            if (--_left > 0) return;
            if (Target == null || Target.Gone) { GoBack(); return; }
            double runners = alive.Average(m => (m.B(BodyTrait.Endurance) + m.M(MindTrait.Boldness)) / 2 * (1 - m.Fatigue * 0.5));
            double caught = runners * (1 - Target.Species.Speed) + 0.4 * Math.Min(1, _damage / Target.Species.Size);
            // Deep snow: animals flounder but so do hunters, unless they have snowshoes, which turns the snow to their advantage.
            if (sim.World[(int)Target.X, (int)Target.Y].Snow > 25)
                caught *= alive.All(m => m.Knows.Contains("hide.snowshoe")) ? 1.4 : 0.7;
            if (sim.Rng.Chance(caught)) { Current = Phase.Strike; return; }
            _damage = 0;
            GoBack();
        }

        void Kill(Simulation sim, List<Person> alive)
        {
            var sp = Target!.Species;
            Count("killed " + sp.Name);
            Target.Count--; sim.Ecology.Count(Target.Species, "hunted", 1);
            // The rest of the herd won't forget it.
            Target.Shy = Math.Min(1, Target.Shy + 0.1);
            _killed = sp;
            _rations = sp.Meat;
            _damage = 0;
            foreach (var m in alive)
                if (sim.Rng.Chance(0.004)) m.Catch(Disease.Fever, 0.2);
            Killer = alive.OrderByDescending(m => m.S(Skill.Hunting) + m.M(MindTrait.Boldness) * 0.5 + sim.Rng.Range(0, 0.5)).First();
            // The hunters eat their fill at the kill before carrying the rest home.
            foreach (var m in alive)
            {
                double need = sim.DailyNeed(m);
                if (need <= 0) continue;
                // Parents with hungry children at home eat less and carry more back.
                double fill = m.HasHungryDependents(sim.Clock) ? 0.5 - 0.4 * m.M(MindTrait.Warmth) : 1.0;
                double eat = Math.Min(_rations, Math.Max(0, fill - m.Stomach) * need);
                _rations -= eat;
                _eatenAtKill += eat;
                m.Stomach += eat / need;
            }
            // They carry home what they can; the rest of the carcass is left.
            double carry = alive.Sum(m => Physiology.CarryCapacity(m, m.AgeYears(sim.Clock)));
            _rations = Math.Min(_rations, carry);
            Current = Phase.Butcher;
            _left = Math.Max(1, (int)Math.Round(sp.Size));
        }

        public static readonly Dictionary<string, int> DebugEnds = new Dictionary<string, int>();
        static void Count(string k) { lock (DebugEnds) DebugEnds[k] = DebugEnds.TryGetValue(k, out var n) ? n + 1 : 1; }

        void GoBack()
        {
            if (_killed == null) Count("gave up at " + Current + (Target == null ? "" : " (" + Target.Species.Name + ")"));
            Current = Phase.Back;
            _left = Math.Max(1, _travel);
        }

        void Finish(Simulation sim, List<Person> alive)
        {
            Current = Phase.Done;
            var band = sim.Band;
            if (_killed != null)
            {
                sim.ShareKill(_rations, alive, Killer);
                sim.ThisYear.Hunted += _rations + _eatenAtKill;
                foreach (var m in alive) m.ProvidedToday += _rations / alive.Count;
                band.Materials.Hides += _killed.Hides;
                band.Materials.Sinew += _killed.Sinew;
                band.Materials.Bone += _killed.Bone;
                sim.RecordKill(_killed, Killer!, alive.Count);
                if (Killer != null && Killer.Alive)
                {
                    if (_killed.Meat >= 30) Killer.BigKills++;
                    Killer.Nudge(Interest.Animals, 0.02);
                }
            }
            double perHour = (_rations + _eatenAtKill) / Math.Max(1, alive.Count * _hours);
            Count(_hours > 72 ? "~over 3 days" : _hours > 24 ? "~1-3 days" : "~under a day");
            lock (DebugEnds)
            {
                DebugEnds["~person-hours"] = DebugEnds.TryGetValue("~person-hours", out var ph) ? ph + alive.Count * _hours : alive.Count * _hours;
                DebugEnds["~rations"] = DebugEnds.TryGetValue("~rations", out var r) ? r + (int)(_rations + _eatenAtKill) : (int)(_rations + _eatenAtKill);
            }
            foreach (var m in alive)
            {
                m.LastHuntDay = sim.Clock.Day;
                m.X = band.CampX; m.Y = band.CampY;
                if (Target != null && (_closeCalls > 0 || _killed != null))
                    m.Expect.LearnDanger(m, Target.Species.Name, _hurt > 0 ? 1 : _closeCalls > 0 ? 0.3 : 0, InstinctiveDanger(Target.Species), 1);
                // Did daring pay? A clean kill of something dangerous says yes; someone hurt says no.
                if (Target != null && Target.Species.Aggression > 0.3)
                    m.Shift(Drive.Standing, _hurt > 0 ? -0.003 : _killed != null ? 0.003 : 0);
                // A hunt that hurt you feels worse than its meat.
                double wounded = Math.Max(0, m.Injury - (_injuryBefore.TryGetValue(m, out var before) ? before : m.Injury));
                // Was following this leader worth it, against what a hunt usually brings them?
                if (m != Leader) sim.LearnFollowing(m, Leader, (perHour - wounded * 0.5 - m.Expect.Get(Verb.Hunt, FallbackPerHour)) * 5);
                m.Expect.Learn(m, Verb.Hunt, perHour - wounded * 0.5, FallbackPerHour);
                if (Target != null)
                {
                    string topic = TacticTopic(Tactic, Target.Species);
                    double current = m.Expect.Get(topic, FallbackPerHour);
                    m.Expect.Absorb(topic, perHour - wounded * 0.5, 0.05 + 0.1 * m.M(MindTrait.Openness), current);
                }
            }
        }
    }

    public sealed class Hunt : Activity
    {
        public readonly HuntParty Party;

        public Hunt(Person p, HuntParty party) : base(p) { Party = party; }

        public override Verb Verb => Verb.Hunt;
        public override bool Interruptible => false;

        public override void Begin(Simulation sim)
        {
            if (Party.Leader == P) Party.Recruit(sim);
        }

        public override void Hour(Simulation sim)
        {
            Party.Advance(sim);
            Step = Party.StepName;
            Done = Party.IsDone || !Party.Members.Contains(P);
        }
    }
}
