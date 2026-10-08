using System;
using System.Linq;
using Anthro.Sim.Core;
using Anthro.Sim.People;
using Anthro.Sim.World;

namespace Anthro.Sim.Activities
{
    /// <summary>
    /// Walking to another band's camp to spend some days there: seeing kin and friends, and for the
    /// unpaired, courting. Hosts feed their guests from their own hearths. When the visit is over the
    /// visitor walks home, unless they've paired with someone there, or they decide they'd rather
    /// live with these people (the same weighing anyone does about where to live).
    /// </summary>
    public sealed class Visit : Activity
    {
        public readonly Simulation Host;
        int _travel, _stayHours, _talkHours;
        bool _arrived, _homeward;
        Person? _with;
        /// <summary>Gone to ask for food for the family at home.</summary>
        public bool ForFood;
        /// <summary>What they've been given to carry home.</summary>
        readonly FoodStore _pack = new FoodStore();
        readonly System.Collections.Generic.HashSet<Household> _asked = new System.Collections.Generic.HashSet<Household>();

        public Visit(Person p, Simulation host) : base(p) { Host = host; }
        public override Verb Verb => Verb.Visit;
        public override bool Interruptible => false;

        /// <summary>At the host camp now (not on the road).</summary>
        public bool Arrived => _arrived && !_homeward;

        double Distance(Simulation sim) => WorldMap.Distance(sim.Band.CampX, sim.Band.CampY, Host.Band.CampX, Host.Band.CampY);

        public override void Begin(Simulation sim)
        {
            P.LastVisitDay = sim.Clock.Day;
            _travel = Math.Max(1, sim.TravelHours(Distance(sim), P));
            _stayHours = (ForFood ? sim.Rng.Range(1, 3) : sim.Rng.Range(3, 11)) * SimClock.HoursPerDay;
            Step = $"walking to the {Host.Band.Name} band";
        }

        public override void Hour(Simulation sim)
        {
            if (_travel > 0)
            {
                P.Fatigue = Inheritance.Clamp01(P.Fatigue + 0.04);
                if (--_travel > 0) return;
                if (_homeward)
                {
                    P.X = sim.Band.CampX; P.Y = sim.Band.CampY; Done = true;
                    if (_pack.Total > 0 && sim.Band.Members.Contains(P)) Simulation.Unpack(_pack, sim.HomeOf(P).Food);
                    return;
                }
                // They went where they thought the camp was; if it has moved on, they find it empty and go home.
                if (P.Map.Camps.TryGetValue(Host.Id, out var thought) && WorldMap.Distance(thought.x, thought.y, Host.Band.CampX, Host.Band.CampY) > WorldMap.FromMeters(3000))
                {
                    P.Map.Camps.Remove(Host.Id);
                    sim.LookAround(P, thought.x, thought.y);
                    _homeward = true;
                    _travel = Math.Max(1, sim.TravelHours(WorldMap.Distance(sim.Band.CampX, sim.Band.CampY, thought.x, thought.y), P));
                    Step = "walking home (the camp had moved)";
                    return;
                }
                _arrived = true;
                P.X = Host.Band.CampX; P.Y = Host.Band.CampY;
                P.Map.Camps[Host.Id] = (Host.Band.CampX, Host.Band.CampY, sim.Clock.Day);
                if (Host.Tribe == sim.Tribe) sim.LastKinContact = Host.LastKinContact = sim.Clock.Day;
                Step = $"visiting the {Host.Band.Name} band";
                return;
            }

            // The hosts' band may have died out, or the guest may be going hungry there (unless they came
            // hungry, to ask).
            if (Host.Extinct || (!ForFood && P.Satiety < 0.4) || --_stayHours <= 0) { GoHome(sim); return; }
            if (sim.Clock.IsNight) { P.Fatigue *= 0.9; return; }

            // Asking the hearths here for food to take home, one at a time, fondest first; each answers by
            // their own lights (and their people's ways with guests). Once their hands are full, or
            // nobody's left to ask, they set off home.
            if (ForFood && sim.Clock.Hour % 3 == 0)
            {
                double carry = Physiology.CarryCapacity(P, P.AgeYears(sim.Clock));
                var family = sim.HomeOf(P);
                double want = Math.Min(carry - _pack.Total, family.Members.Count * 0.9 * 7);
                var hearth = Host.Band.Households.Where(h => !_asked.Contains(h) && h.Food.Total > 1)
                    .OrderByDescending(h => h.Members.Sum(m => m.GetAffinity(P))).FirstOrDefault();
                if (want <= 0.5 || hearth == null) { _stayHours = Math.Min(_stayHours, 1); }
                else
                {
                    _asked.Add(hearth);
                    double got = Host.DoAsk(P, hearth, want, _pack);
                    sim.ThisYear.GuestAsks++;
                    if (got > 0) { sim.ThisYear.GuestGifts++; sim.ThisYear.GuestFood += got; }
                    Step = _pack.Total > 0 ? $"carrying food given by the {Host.Band.Name} band" : $"asking the {Host.Band.Name} band for food";
                }
            }

            // Guests eat at their hosts' hearths: the hearth fondest of them that has food to share.
            if (P.Stomach < 0.4)
            {
                var hearth = Host.Band.Households.Where(h => h.Food.Total > 1)
                    .OrderByDescending(h => h.Members.Sum(m => m.GetAffinity(P))).FirstOrDefault();
                if (hearth != null)
                {
                    double need = sim.DailyNeed(P);
                    double eaten = hearth.Food.Take(Math.Min(need * 0.5, hearth.Food.Total), out _, out _);
                    P.Stomach += eaten / need;
                    var host = hearth.Adults(sim.Clock).OrderByDescending(a => a.GetAffinity(P)).FirstOrDefault();
                    if (host != null && eaten > 0.05)
                        Host.HappenHere(new Deed { Day = sim.Clock.Day, Actor = host, Act = Act.Give, Target = P, Hearth = hearth, Amount = eaten,
                                                   Context = Simulation.Lesson(Act.Give, Host.RelationTo(host, P)) });
                }
            }

            // Spend the day talking with the hosts, drawn to friends, kin and (for the unpaired) someone to court.
            if (_with == null || _talkHours <= 0 || !Host.Band.Members.Contains(_with))
            {
                var around = Host.Band.Members.Where(o => o.Alive && o.AgeYears(sim.Clock) >= 14
                                                          && (o.Current is Socialize || o.Current is Rest || o.Current is Eat || o.Current is Cook)).ToList();
                if (around.Count == 0) { _with = null; Step = $"waiting about the {Host.Band.Name} camp"; return; }
                _with = around.OrderByDescending(o => P.GetAffinity(o) + sim.Rng.Range(0, 0.5)
                                                      + (sim.CouldPair(P, o) ? 0.3 + sim.Attraction(o) * 0.5 : 0)).First();
                _talkHours = 2;
                Step = $"talking with {_with.Name} of the {Host.Band.Name} band";
            }
            P.AddAffinity(_with, 0.01 + 0.02 * P.M(MindTrait.Sociability));
            _with.AddAffinity(P, 0.01 + 0.02 * _with.M(MindTrait.Sociability));
            sim.Contact(P, _with, 1);
            if (--_talkHours > 0) return;

            SocialLearning.Exchange(P, _with, sim.Rng);
            sim.Gossip(P, _with);
            sim.PassOn(P, _with);
            sim.PassOn(_with, P);
            sim.TellPlaces(P, _with);
            sim.TellPlaces(_with, P);
            // Each judges the other by their own people's tastes.
            if (sim.WouldPair(P, _with, sim, Host))
            {
                sim.Pair(P, _with);
                Done = true;
            }
        }

        void GoHome(Simulation sim)
        {
            _homeward = true;
            // Having spent these days here, would they rather live with these people?
            if (!Host.Extinct && sim.Band.Members.Contains(P))
            {
                double here = Simulation.Ties(P, sim.Band.Members);
                double there = Simulation.Ties(P, Host.Band.Members) + (Host.Prospects - sim.Prospects) * 4;
                if (there > here * 1.3 + 0.5)
                {
                    var host = Host;
                    sim.Region.Move(new System.Collections.Generic.List<Person> { P }, sim, host);
                    sim.Chronicle.Write(sim.Clock, Importance.Notable, $"{P.Name} stayed on with the {host.Band.Name} band.");
                    host.Chronicle.Write(sim.Clock, Importance.Notable, $"{P.Name} of the {sim.Band.Name} band came visiting and stayed.");
                    Done = true;
                    return;
                }
            }
            _travel = Math.Max(1, sim.TravelHours(Distance(sim), P));
            Step = "walking home";
        }
    }
}
