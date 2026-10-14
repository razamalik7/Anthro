using System;
using System.Linq;
using Anthro.Sim.People;

namespace Anthro.Sim.Activities
{
    public sealed class Sleep : TimedActivity
    {
        public Sleep(Person p, int hours) : base(p, hours) { Step = "sleeping"; }
        public override Verb Verb => Verb.Sleep;
        public override bool Interruptible => false;
        protected override double FatiguePerHour => -0.12;
        protected override void OnHour(Simulation sim) { }
    }

    /// <summary>
    /// Eating from the camp's food. When food is short, people hold back to a fair share; how much
    /// they hold back depends on their warmth and on how strongly their band values sharing.
    /// </summary>
    public sealed class Eat : TimedActivity
    {
        public Eat(Person p) : base(p, 1) { Step = "eating"; }
        public override Verb Verb => Verb.Eat;
        protected override double FatiguePerHour => -0.02;

        /// <summary>
        /// A meal from the family's own store. Hungry children of the household are fed first:
        /// warm-hearted adults give them nearly everything if need be. (Asking other hearths, or
        /// taking from them, are choices of their own.)
        /// </summary>
        protected override void OnHour(Simulation sim)
        {
            var home = sim.HomeOf(P);
            double need = sim.DailyNeed(P);
            if (need <= 0) return;
            double want = Math.Max(0, 1.0 - P.Stomach) * need;
            // How long what's at home has to last: the lean stretch they expect, while the land isn't
            // keeping up. With plenty beyond that, people eat more and put on reserves; short of it, they
            // go on short rations, spreading what there is (with what's still coming in) over the stretch.
            double stretch = Decider.LeanDays(sim, P);
            int mouths = Math.Max(1, home.Members.Count);
            bool landKeepsUp = home.Income >= mouths * 0.9;
            // Eating past need is for food that would otherwise go to waste: fresh meat and greens beyond
            // what the family can eat before they spoil (a couple of days), or keeping food beyond what
            // the lean stretch calls for. Nobody gorges on the winter nuts.
            double perishableDays = home.Food.Perishable / (mouths * 0.9);
            if (perishableDays > 2 || home.DaysOfFood > stretch + 3) want *= 1.5;
            else if (!landKeepsUp && stretch > 6 && home.DaysOfFood < stretch)
            {
                double ration = (home.Income + home.Food.Total / stretch) / (mouths * 0.9);
                // How hard they budget is their people's custom (and their own lessons).
                double strict = Simulation.Off("rationing") ? 1 : Simulation.Off("norationing") ? 0 : Simulation.Rationing(P);
                double before = want;
                want *= 1 - strict + strict * Math.Max(0.5, Math.Min(1, ration));
                if (!home.HadToBudget) home.BudgetStart = Decider.Keeping(sim, home);
                home.HadToBudget = true;
                home.HeldBack += before - want;
            }

            var kids = home.Members.Where(c => c != P && c.AgeYears(sim.Clock) >= 1 && c.AgeYears(sim.Clock) < 14 && c.Stomach < 0.6).ToList();
            double kidsWant = kids.Sum(c => Math.Max(0, 1.0 - c.Stomach) * sim.DailyNeed(c));


            double available = home.Food.Total;
            double forKids = Math.Min(kidsWant, Math.Max(available - want, available * (0.4 + 0.6 * P.M(MindTrait.Warmth))));
            foreach (var child in kids)
            {
                double childNeed = sim.DailyNeed(child);
                double childWant = Math.Max(0, 1.0 - child.Stomach) * childNeed;
                double gave = home.Food.Take(kidsWant <= 0 ? 0 : forKids * childWant / kidsWant, out double childMeat);
                if (childMeat > 0 && home.Food.MeatAge > 3) child.AteRotten = true;
                child.Stomach += gave / childNeed;
                sim.ThisYear.Eaten += gave;
            }
            double eaten = home.Food.Take(Math.Min(want, home.Food.Total), out double meat, out double raw);
            sim.ThisYear.Eaten += eaten;
            if (meat > 0 && home.Food.MeatAge > 3) P.AteRotten = true;
            // Old teeth and guts get less from raw food; cooked food is easy.
            double age = P.AgeYears(sim.Clock);
            double value = eaten - (age > 60 ? raw * 0.2 : 0);
            P.Stomach += value / need;
        }
    }

    public sealed class Rest : TimedActivity
    {
        public Rest(Person p) : base(p, 2) { Step = "resting"; }
        public override Verb Verb => Verb.Rest;
        protected override double FatiguePerHour => -0.08;
        protected override void OnHour(Simulation sim) => P.Health = Inheritance.Clamp01(P.Health + 0.002);
    }

    /// <summary>Talking by the fire: friendships grow and people copy each other's expectations.</summary>
    public sealed class Socialize : TimedActivity
    {
        Person? _with;

        public Socialize(Person p) : base(p, 2) { }
        public override Verb Verb => Verb.Socialize;
        protected override double FatiguePerHour => 0;

        public override void Begin(Simulation sim)
        {
            var others = sim.Band.Members.Where(o => o != P && (o.Current is Socialize || o.Current is Rest)).Concat(sim.Visitors).ToList();
            if (others.Count == 0) others = sim.Band.Members.Where(o => o != P && o.IsAdult(sim.Clock)).ToList();
            if (others.Count == 0) { Step = "sitting alone"; return; }
            // Unpaired adults drift toward people they're drawn to; that's how couples form.
            // People drift toward those they like and away from those they're angry with.
            _with = others.OrderByDescending(o => P.GetAffinity(o) - P.GetGrievance(o) * 0.1 + sim.Rng.Range(0, 0.5)
                                                  + (sim.CouldPair(P, o) ? 0.3 + sim.Attraction(o) * 0.5 : 0)).First();
            Step = $"talking with {_with.Name}";
        }

        protected override void OnHour(Simulation sim)
        {
            if (_with == null) return;
            P.AddAffinity(_with, 0.01 + 0.02 * P.M(MindTrait.Sociability));
            _with.AddAffinity(P, 0.01 + 0.02 * _with.M(MindTrait.Sociability));
            sim.Contact(P, _with, 1);
            using (Core.Profiler.Measure("talk jealousy")) sim.Jealousy(P, _with);
        }

        protected override void OnEnd(Simulation sim)
        {
            if (_with == null) return;
            using (Core.Profiler.Measure("talk exchange")) SocialLearning.Exchange(P, _with, sim.Rng);
            using (Core.Profiler.Measure("talk gossip")) sim.Gossip(P, _with);
            using (Core.Profiler.Measure("talk passon")) { sim.PassOn(P, _with); sim.PassOn(_with, P); }
            using (Core.Profiler.Measure("talk places")) { sim.TellPlaces(P, _with); sim.TellPlaces(_with, P); }
            for (int i = 0; i < P.Drives.Length; i++)
            {
                double mid = (P.Drives[i] + _with.Drives[i]) / 2;
                P.Drives[i] += (mid - P.Drives[i]) * 0.002 * (0.5 + P.M(MindTrait.Openness));
            }
            // When fondness has become mutual, two unpaired people pair up.
            var withBand = sim.Region.BandOf(_with) ?? sim;
            using (Core.Profiler.Measure("talk pairing")) if (sim.WouldPair(P, _with, sim, withBand)) sim.Pair(P, _with);
            else using (Core.Profiler.Measure("talk leaving")) if (P.Married && !_with.Married) { if (!sim.ConsiderAnotherSpouse(P, _with)) sim.ConsiderLeaving(P, _with); }
            if (P.Restless) using (Core.Profiler.Measure("consider moving")) sim.ConsiderMoving(P);
        }
    }

    public sealed class Knap : TimedActivity
    {
        public Knap(Person p) : base(p, 3) { Step = "knapping"; }
        public override Verb Verb => Verb.Knap;

        public override void Begin(Simulation sim)
        {
            if (sim.Band.Materials.Flint > 0) sim.Band.Materials.Flint--;
            else { Step = "looking for flint"; }
        }

        protected override void OnHour(Simulation sim) => P.Practice(Skill.Knapping, 1);

        protected override void OnEnd(Simulation sim)
        {
            if (Step != "knapping") return;
            double made = (1.0 + 1.5 * P.S(Skill.Knapping) + 0.5 * P.B(BodyTrait.Dexterity)) * (P.Knows.Contains("knap.heat") ? 1.3 : 1);
            sim.Band.Materials.Tools += made;
            P.Nudge(Interest.StoneEarth, 0.01);
        }
    }

    /// <summary>Looking after a small child or a sick person. Care speeds recovery and keeps infants healthy.</summary>
    public sealed class Care : TimedActivity
    {
        readonly Person _child;

        public Care(Person p, Person who) : base(p, 2) { _child = who; Step = who.Sickness > 0 ? $"tending {who.Name}" : $"caring for {who.Name}"; }
        public Person Who => _child;
        public override Verb Verb => Verb.Care;
        protected override double FatiguePerHour => 0.02;

        protected override void OnHour(Simulation sim)
        {
            _child.CareToday += 1;
            P.Practice(Skill.Caring, 1);
            // Tending the sick is close contact: healers catch what they treat.
            sim.Contact(P, _child, 2);
            if (_child.Sickness > 0 || _child.Injury > 0.2) P.Nudge(Interest.Healing, 0.003);
            P.AddAffinity(_child, 0.01);
            _child.AddAffinity(P, 0.02);
        }
    }

    /// <summary>Children playing: random small pulls on their interests.</summary>
    public sealed class Play : TimedActivity
    {
        public Play(Person p) : base(p, 2) { Step = "playing"; }
        public override Verb Verb => Verb.Play;
        protected override void OnHour(Simulation sim) =>
            P.Nudge((Interest)sim.Rng.Range(0, TraitCounts.Interests), 0.004);
    }

    /// <summary>A child watching an adult work: the main way skills and interests pass down.</summary>
    public sealed class WatchAndLearn : TimedActivity
    {
        readonly Person _teacher;
        readonly Skill _skill;
        readonly Interest _interest;

        public WatchAndLearn(Person p, Person teacher, Skill skill, Interest interest) : base(p, 2)
        {
            _teacher = teacher; _skill = skill; _interest = interest;
            Step = $"watching {teacher.Name}";
        }

        public override Verb Verb => Verb.WatchAndLearn;

        protected override void OnHour(Simulation sim)
        {
            double teaching = 0.5 + _teacher.S(Skill.Teaching) * 0.5 + _teacher.M(MindTrait.Warmth) * 0.3;
            if (P.S(_skill) < _teacher.S(_skill)) P.Practice(_skill, teaching, 0.003);
            P.Interests[(int)_interest] += (_teacher.I(_interest) - P.I(_interest)) * 0.01;
            _teacher.Practice(Skill.Teaching, 0.5);
            // Watching someone work is how children pick up what they know.
            sim.PassOn(_teacher, P, 0.5, shown: true);
            // A child's character forms from the adults they learn from.
            for (int i = 0; i < P.Drives.Length; i++)
            {
                P.BaseDrives[i] += (_teacher.BaseDrives[i] - P.BaseDrives[i]) * 0.003;
                P.Drives[i] += (_teacher.Drives[i] - P.Drives[i]) * 0.003;
            }
            sim.Contact(P, _teacher, 0.5);
            P.AddAffinity(_teacher, 0.01);
            // Children soak up what the adults around them believe, along with the skill.
            foreach (var kv in _teacher.Expect.Snapshot())
                P.Expect.Absorb(kv.Key, kv.Value, 0.05 * (0.5 + P.M(MindTrait.Openness)), kv.Value);
        }
    }

    /// <summary>
    /// A plea to the unseen power for help with a need beyond one's means.
    /// Done at camp, seen by others. Creates an open request with the God layer.
    /// </summary>
    public sealed class Pray : TimedActivity
    {
        readonly PrayerNeed _need;
        readonly Person? _target;

        public Pray(Person p, PrayerNeed need, Person? target = null) : base(p, 1)
        {
            Step = need == PrayerNeed.Thanks ? "giving thanks" : "praying";
            _need = need;
            _target = target;
        }

        public override Verb Verb => Verb.Pray;
        protected override double FatiguePerHour => 0.01;

        protected override void OnHour(Simulation sim)
        {
            sim.RegisterPrayer(P, _need, _target);
        }
    }

    /// <summary>
    /// Giving up food or goods at a sacred spot or hearth for the god.
    /// Valued by belief and expected favor return; size scaled by the giver's means.
    /// Generates divine favor; fear offerings (for a cruel god) provide heightened favor.
    /// </summary>
    public sealed class Offer : TimedActivity
    {
        readonly double _amount;

        public Offer(Person p, double amount) : base(p, 1)
        {
            Step = "offering";
            _amount = amount;
        }

        public override Verb Verb => Verb.Offer;
        protected override double FatiguePerHour => 0.01;

        protected override void OnHour(Simulation sim)
        {
            if (Simulation.Off("god") || Simulation.Off("offer")) return;
            var home = sim.HomeOf(P);
            if (home == null || home.Food.Total <= 0) return;

            double actual = home.Food.Take(Math.Min(_amount, home.Food.Total), out _);
            if (actual <= 0) return;

            // A4a (owner decision 2026-10-12): An offering is a meal shared with the god.
            // Only 15% is burned/sacrificed for the god; 85% is eaten at a shared feast by those in camp,
            // nourishing them and bonding the group like a gathering.
            double burned = actual * 0.15;
            double shared = actual - burned;

            var attendees = sim.Band.Members.Where(m => !(m.Current is Visit) && m.AgeYears(sim.Clock) >= 1).ToList();
            if (attendees.Count > 0)
            {
                double portion = shared / attendees.Count;
                foreach (var m in attendees)
                {
                    m.Stomach = Math.Min(1.2, m.Stomach + portion / Math.Max(0.1, sim.DailyNeed(m)));
                }
                sim.ThisYear.Eaten += shared;

                // Shared sacred feast bonds attendees (like Gatherings.cs)
                for (int i = 0; i < attendees.Count; i++)
                {
                    for (int j = i + 1; j < Math.Min(attendees.Count, i + 6); j++)
                    {
                        var a = attendees[i]; var b = attendees[j];
                        if (a.GetAffinity(b) < 0.3) a.AddAffinity(b, 0.01);
                        if (b.GetAffinity(a) < 0.3) b.AddAffinity(a, 0.01);
                    }
                }
                foreach (var m in attendees) m.Discontent *= 0.95;
            }

            double real = Simulation.GodReal(P);
            double kind = Simulation.GodKind(P);
            // Disposition factor: fear appeasement when cruel, devotion/gratitude when kind
            double dispositionFactor = 1.0 + Math.Abs(kind) * 1.5;
            // Favor comes strictly from what is given up (the burned share)
            double gain = burned * real * 8.0 * dispositionFactor;

            sim.Region.FavorEarnedOffer += gain;
            sim.Region.AddFavor(gain);
            sim.ThisYear.FavorGain += gain;
            sim.ThisYear.FoodOffered += burned;
            var oSeason = sim.Clock.Season;
            sim.ThisYear.DevotionBySeason[oSeason] = sim.ThisYear.DevotionBySeason.TryGetValue(oSeason, out var oDv) ? oDv + 1 : 1;
            if (P.Satiety >= 0.6) sim.ThisYear.DevotionFed++; else sim.ThisYear.DevotionHungry++;

            if (!Simulation.Off("relief") && P.Dread > 0.05)
            {
                P.AppeasementAct = "offer";
                P.AppeasementDread = P.Dread;
            }

            var openPrayer = sim.Region.ActivePrayers.FirstOrDefault(pr => pr.Asker == P && !pr.Resolved);
            if (openPrayer != null && !openPrayer.HasOffering)
            {
                openPrayer.HasOffering = true;
                openPrayer.OfferingDay = sim.Clock.Day;
                sim.Region.OfferingsFromPleaCount++;
            }

            sim.Chronicle.Write(sim.Clock, Anthro.Sim.Core.Importance.Detail, $"{P.Name} held an offering feast of {actual:F1} rations ({burned:F1} burned for the god).");
            sim.Region.GodEvents.Add(new GodEvent(sim.Clock.Tick, "Offer", $"{P.Name} held an offering feast ({burned:F1} burned, +{gain:F1} favor)", (int)sim.Band.CampX, (int)sim.Band.CampY));
            
            sim.HappenHere(new People.Deed { Day = sim.Clock.Day, Actor = P, Act = People.Act.Offer, Amount = burned, Hearth = home });
        }
    }
}
