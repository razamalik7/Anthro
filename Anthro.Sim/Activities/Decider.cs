using System;
using System.Collections.Generic;
using System.Linq;
using Anthro.Sim.Core;
using Anthro.Sim.People;
using Anthro.Sim.World;

namespace Anthro.Sim.Activities
{
    public readonly struct Option
    {
        public readonly Verb Verb;
        public readonly double Score;
        public readonly Func<Activity> Make;

        public Option(Verb verb, double score, Func<Activity> make) { Verb = verb; Score = score; Make = make; }
    }

    /// <summary>
    /// What a person could do right now, and how much each pulls on them:
    ///   need it meets × expected result + interest + trait pulls + social pull.
    /// The same scores answer "what would I rather be doing?" when someone asks them to join a hunt.
    /// </summary>
    public static class Decider
    {
        public static Activity Choose(Simulation sim, Person p)
        {
            var clock = sim.Clock;
            if (clock.IsNight)
            {
                int hour = clock.Hour;
                return new Sleep(p, Math.Max(1, hour >= clock.Sunrise ? 24 - hour + clock.Sunrise : clock.Sunrise - hour));
            }
            double age = p.AgeYears(clock);
            if (age < 3)
                return age >= 1 && p.Stomach < 0.4 && sim.Band.FoodTotal > 0 ? new Eat(p) : (Activity)new Play(p);

            // Noise before taking the max: a softmax in disguise, so people aren't perfectly predictable.
            Func<Activity>? best = null;
            double bestScore = double.NegativeInfinity;
            foreach (var o in Options(sim, p))
            {
                double score = o.Score + sim.Rng.Normal(0, 0.15);
                if (score > bestScore) { bestScore = score; best = o.Make; }
            }
            return best!();
        }

        /// <summary>The strongest pull other than <paramref name="except"/>: what this person gives up to do it.</summary>
        public static double BestAlternative(Simulation sim, Person p, Verb except)
        {
            var others = Options(sim, p).Where(o => o.Verb != except).ToList();
            return others.Count == 0 ? 0 : others.Max(o => o.Score);
        }

        public static double ScoreOf(Simulation sim, Person p, Verb verb)
        {
            foreach (var o in Options(sim, p)) if (o.Verb == verb) return o.Score;
            return double.NegativeInfinity;
        }

        /// <summary>
        /// How strongly someone feels the need to put food by: how lean they have learned the coming two
        /// seasons get (the nearer one counting more), how patient they are, and how far short of
        /// enough their hearth's keeping-food is. No calendar: it rises as seasons they know to be lean
        /// draw near, so a band that has never gone hungry doesn't bother, and one that has learned
        /// its springs are hard starts storing in the autumn.
        /// </summary>
        public static double Foresight(Simulation sim, Person p)
        {
            var (now, next, after) = LeanAhead(sim, p);
            double ahead = Math.Max(now, next * 0.6 + after * 0.4);
            var home = sim.HomeOf(p);
            double enough = home.Members.Count * 0.9 * LeanDays(sim, p);
            double shortfall = enough <= 0 ? 0 : Inheritance.Clamp01(1 - Keeping(sim, home) / enough);
            return ahead * (0.5 + p.M(MindTrait.Patience) * 0.5) * shortfall;
        }

        /// <summary>
        /// How many days of food someone thinks their hearth should have put by: the rest of this season
        /// and the two after, each counted by how lean they've learned it gets. Nobody is told how long
        /// winter is; people who have been thin in late winter come to want more laid by.
        /// </summary>
        public static double LeanDays(Simulation sim, Person p)
        {
            var (now, next, after) = LeanAhead(sim, p);
            double rest = SimClock.DaysPerSeason - sim.Clock.DayOfSeason;
            return rest * now + SimClock.DaysPerSeason * (next + after);
        }

        /// <summary>Food at the hearth that will keep through a lean stretch: nuts, grain, dried food, and meat while it's freezing.</summary>
        public static double Keeping(Simulation sim, Household home) =>
            home.Food.Stored + home.Food.Dried + (sim.MeatKeepsDays >= 20 ? home.Food.Meat : 0);

        /// <summary>Whether meat brought in now will keep: dried, or frozen in the cold.</summary>
        static bool MeatWillKeep(Simulation sim, Person p) => Preserve.CanPreserve(p) || sim.MeatKeepsDays >= 20;

        /// <summary>How lean this person expects this season, the next and the one after to be.</summary>
        static (double now, double next, double after) LeanAhead(Simulation sim, Person p)
        {
            int s = (int)sim.Clock.Season;
            return (p.Expect.Get(Simulation.LeanTopic((Season)s), 0.2),
                    p.Expect.Get(Simulation.LeanTopic((Season)((s + 1) % 4)), 0.2),
                    p.Expect.Get(Simulation.LeanTopic((Season)((s + 2) % 4)), 0.2));
        }

        /// <summary>The rations of keeping-food someone's hearth is short for the lean seasons they expect.</summary>
        public static double StoreShortfallRations(Simulation sim, Person p)
        {
            var home = sim.HomeOf(p);
            return Math.Max(0, home.Members.Count * 0.9 * LeanDays(sim, p) - Keeping(sim, home));
        }

        static double MaxGrievance(Person p)
        {
            double m = 0;
            foreach (var v in p.Grievance.Values) if (v > m) m = v;
            return m;
        }

        public static List<Option> Options(Simulation sim, Person p)
        {
            var clock = sim.Clock;
            int hour = clock.Hour;
            double age = p.AgeYears(clock);
            var band = sim.Band;
            int people = Math.Max(1, band.Members.Count);

            // Need: mostly how long their own hearth's food would last (with an eye on the camp's),
            // and meat on its own (it rots, and people crave it). Hunger is also an empty belly and
            // the body thinning.
            var home = sim.HomeOf(p);
            double foodDays = home.DaysOfFood * 0.7 + band.FoodTotal / (people * 0.9) * 0.3;
            double hunger = Math.Max(Inheritance.Clamp01(1 - foodDays / 6),
                            Math.Max(1 - p.Satiety, Inheritance.Clamp01((0.5 - p.Stomach) * 2) * 0.6));
            // Watching the stores go down with the lean time not yet over is a worry long before the
            // pot is empty: it drives people out to look for food and leaves less time for talk.
            double stretch = LeanDays(sim, p);
            if (stretch > 6 && home.Income < home.Members.Count * 0.9)
                hunger = Math.Max(hunger, Inheritance.Clamp01(1 - home.DaysOfFood / stretch) * 0.6);
            // Craving meat: people need some animal food (protein, fat), and miss it when it's been a
            // small part of what the family's been living on; past about 40% of the diet more lean meat
            // adds little (the protein ceiling), and the craving is gone.
            double meatHunger = Inheritance.Clamp01((0.4 - home.Food.AnimalShare) / 0.3);
            // More food is only worth what will be eaten before it spoils. With fresh food piled up
            // at home (or meat hanging all over camp), going out for more is worth little, and people
            // rest, talk and make things instead, as real foragers do in good times.
            double freshDays = (home.Food.Meat + home.Food.Plants + home.Food.Cooked) / (home.Members.Count * 0.9);
            double room = Inheritance.Clamp01(1 - freshDays / 6);
            double meatRoom = Inheritance.Clamp01(1 - band.MeatTotal / (people * 0.9) / sim.MeatKeepsDays);
            // Going hunting just to have more is only worth it while the camp couldn't eat everything it
            // already has in the time meat keeps (nuts, roots and dried food count too).
            double huntRoom = Inheritance.Clamp01(1 - band.FoodTotal / (people * 0.9) / sim.MeatKeepsDays);
            // A trip out needs enough light left to get there, work, and get back.
            bool daylightAhead = clock.DaylightLeft >= 6;
            // Gathering and fishing fit into a short winter day (walking out, a few hours' work, back by
            // dark); a hunt needs longer; a long trip (flint, visiting) a full day.
            bool lightForGathering = clock.DaylightLeft >= 3, lightForHunting = clock.DaylightLeft >= 4;
            double maturity = Physiology.Maturity(age);
            var options = new List<Option>();

            options.Add(new Option(Verb.Rest, p.Fatigue * 1.5 + (1 - p.Health) + p.Injury + p.Sickness * 3 + 0.05, () => new Rest(p)));
            // Hunger is the belly and, more slowly, the body's reserves running down.
            // Eating is from one's own hearth; with nothing there, the choices are to go and get food or ask for it.
            if (home.Food.Total > 0.05) options.Add(new Option(Verb.Eat, (0.6 - p.Stomach) * 4 + (1 - p.Satiety) * 1.5, () => new Eat(p)));
            // Hungry people don't sit around talking.
            options.Add(new Option(Verb.Socialize, (0.2 + p.M(MindTrait.Sociability) * 0.5 + (clock.DaylightLeft <= 1 ? 0.7 : 0)) * (1 - hunger * 0.7), () => new Socialize(p)));

            // Play fades as children grow.
            if (maturity < 0.8) options.Add(new Option(Verb.Play, 1.0 - maturity, () => new Play(p)));

            // Food work is weighed the way real foragers weigh it: by the food an hour of it has
            // actually been bringing in (learned), scaled by need, with interest, nerve and the hope
            // of standing as smaller pulls.
            const double Reference = 0.4;
            if (maturity > 0.2 && lightForGathering)
            {
                // Nuts and grain keep: the urge to store pulls toward gathering them.
                double drive = 0.25 * room + hunger * 1.5 + Foresight(sim, p);
                double yield = p.Expect.Get(Verb.Forage, Forage.FallbackPerHour) / Reference;
                options.Add(new Option(Verb.Forage, drive * yield * (0.7 + p.I(Interest.Plants) * 0.3 + p.M(MindTrait.Patience) * 0.1),
                    () => new Forage(p)));
            }

            if (maturity > 0.3 && lightForGathering && p.Map.KnowsFishing && FishingMethods.CanFish(p))
            {
                double drive = 0.25 * room + Math.Max(hunger, meatHunger * 0.5 * meatRoom) * 1.5 + (MeatWillKeep(sim, p) ? Foresight(sim, p) : 0);   // fish keeps if dried or frozen
                double yield = p.Expect.Get(Verb.Fish, Fish.FallbackPerHour) / Reference;
                options.Add(new Option(Verb.Fish, drive * yield * (0.7 + p.I(Interest.Water) * 0.3 + p.M(MindTrait.Patience) * 0.1),
                    () => new Fish(p)));
            }

            // Setting and checking traps: where small game has been seen, for those who know how.
            if (maturity > 0.4 && clock.DaylightLeft >= 3 && TrappingMethods.Efficiency(p) > 0 && p.Map.Game.Values.Any(s => s.Species.Size < 1 && clock.Day - s.Day < 14))
            {
                double drive = 0.25 * huntRoom + Math.Max(hunger, meatHunger * 0.7 * meatRoom) * 1.5;
                double yield = p.Expect.Get(Verb.Trap, Trap.FallbackPerHour) / Reference;
                options.Add(new Option(Verb.Trap, drive * yield * (0.7 + p.I(Interest.Animals) * 0.2 + p.M(MindTrait.Patience) * 0.2), () => new Trap(p)));
            }

            if (lightForHunting && HuntParty.CanHunt(sim, p))
            {
                // Meat hanging all over camp (shared out after a big kill) takes away the craving too.
                // Meat keeps only for those who can dry it: for them, the urge to store counts here too.
                double drive = 0.25 * huntRoom + Math.Max(hunger, meatHunger * 0.7 * meatRoom) * 1.5
                               + (MeatWillKeep(sim, p) ? Foresight(sim, p) : 0);
                double yield = Math.Max(0, p.Expect.Get(Verb.Hunt, HuntParty.FallbackPerHour)) / Reference;
                double standing = p.M(MindTrait.Boldness) * p.Drives[(int)Drive.Standing] * 0.4;
                // Late pregnancy and nursing make a long, rough day away from camp costly.
                double tied = p.DueDay >= 0 || p.Sex == Sex.Female && p.Children.Any(c => c.Alive && c.AgeYears(clock) < 2) ? 0.6 : 0;
                options.Add(new Option(Verb.Hunt, drive * yield * (0.7 + p.I(Interest.Animals) * 0.3 + p.B(BodyTrait.Strength) * 0.1) + standing - tied,
                    () => new Hunt(p, new HuntParty(p))));
            }

            if (Physiology.CraftCapacity(p, age) > 0.6)
            {
                int adults = Math.Max(1, band.Adults(clock).Count());
                double toolNeed = Inheritance.Clamp01(1 - band.Materials.Tools / (adults * 1.5));
                if (band.Materials.Flint > 0)
                    options.Add(new Option(Verb.Knap, toolNeed * 1.3 + p.I(Interest.StoneEarth) * 0.4 + p.S(Skill.Knapping) * 0.2, () => new Knap(p)));
                else if (daylightAhead)
                    options.Add(new Option(Verb.GatherFlint, toolNeed * 1.2 + p.I(Interest.StoneEarth) * 0.3, () => new GatherFlint(p)));
            }

            // The hearth: cooking what's raw, keeping the fire fed, preserving surplus.
            if (maturity > 0.5)
            {
                double mouths = home.Members.Count * 0.9;
                double rawToCook = Math.Min(home.Food.Raw, mouths * 1.5) - home.Food.Cooked;
                if (rawToCook > 0.5 && home.FireLit)
                    options.Add(new Option(Verb.Cook, Inheritance.Clamp01(rawToCook / mouths) * (0.5 + p.I(Interest.Hearth) * 0.5 + p.S(Skill.Cooking) * 0.2) * (0.5 + hunger),
                        () => new Cook(p)));

                double fuelDays = home.Fuel / Cuisine.DailyBurn(sim.CampCell.Temperature);
                if (clock.DaylightLeft >= 5)
                    options.Add(new Option(Verb.GatherWood, Inheritance.Clamp01(1 - fuelDays / 4) * 1.4 * (0.7 + p.I(Interest.Hearth) * 0.3 + p.B(BodyTrait.Endurance) * 0.2),
                        () => new GatherWood(p)));

                if (Preserve.CanPreserve(p))
                {
                    // Fresh food beyond what the hearth will eat in a couple of days will rot unless it's dried.
                    double surplus = Math.Max(0, home.Food.Perishable - mouths * 2);
                    if (surplus > 2)
                        // Having watched food rot lately makes the work feel worth it.
                        options.Add(new Option(Verb.Preserve, Inheritance.Clamp01(surplus / (mouths * 2))
                                                              * (0.6 + Foresight(sim, p) * 1.5 + p.I(Interest.Hearth) * 0.3 + Inheritance.Clamp01(home.SpoiledRecent / (mouths * 3))),
                            () => new Preserve(p)));
                }
            }

            // When the hearth is running short: ask another hearth, or take from one. Each is weighed by
            // what this person has come to expect it brings (their own experience, what they've seen and
            // been told, the culture they grew up in), by guilt toward the people it would hurt, and,
            // for taking, by how likely they are to be seen.
            // Asking is for a family that's running out. How hungry one has to be before asking is custom:
            // among demand-sharing peoples the short ask freely, elsewhere only the desperate.
            double demand = Simulation.Off("demand") ? 0 : Simulation.Demand(p);
            if (maturity > 0.6 && hunger > 0.3 * (1 - 0.6 * demand) && home.DaysOfFood < 2)
            {
                double amount = home.Members.Count * 0.9;
                int watchers = Math.Max(0, sim.AwakeInCampCount - 1);
                double pSeen = 1 - Math.Pow(0.85, watchers);
                Household? askAt = null, takeAt = null;
                double bestAsk = 0, bestTake = 0;
                foreach (var h in band.Households)
                {
                    if (h == home || h.Food.Total < 1) continue;
                    var rel = sim.RelationTo(p, h);
                    var owner = h.Adults(clock).OrderByDescending(a => p.GetAffinity(a)).FirstOrDefault();
                    double guilt = owner == null ? 0 : Simulation.Guilt(p, owner);
                    double plenty = Math.Min(1, Simulation.SpareFor(h, home) / amount);
                    double godAsk = Simulation.GodActValue(p, Act.Ask);
                    double ask = (Simulation.Expects(p, Act.Ask, rel) + godAsk) * plenty;
                    double godTake = Simulation.GodActValue(p, Act.Take);
                    double take = pSeen * Simulation.Expects(p, Act.Take, rel, true) + (1 - pSeen) * Simulation.Expects(p, Act.Take, rel, false) - guilt + godTake;
                    if (ask > bestAsk) { bestAsk = ask; askAt = h; }
                    if (take > bestTake) { bestTake = take; takeAt = h; }
                }
                // Asking and taking move food around; they don't make any. Only need drives them (unlike
                // getting food, there's no pull to pile up more): a family that's fine doesn't spend its day asking.
                double drive = hunger * 1.5;
                // Taking is only worth something to people who are actually going short: how thin
                // anyone at their hearth is getting, not just how low the store is.
                double thinning = home.Members.Select(m => 1 - m.Satiety).DefaultIfEmpty(0).Max();
                bestTake *= Math.Min(1, thinning * 2);
                // An hour spent asking can bring in a day's food: weighed like any other hour of getting food.
                if (askAt != null) { var at = askAt; options.Add(new Option(Verb.Ask, drive * bestAsk / Reference, () => new AskFor(p, at, amount))); }
                if (takeAt != null) { var at = takeAt; options.Add(new Option(Verb.Take, drive * bestTake / Reference * 0.5 * (1.3 - p.M(MindTrait.Warmth) * 0.6), () => new TakeFrom(p, at, amount))); }
            }

            // Anger at someone in camp: face them over it, or go for them. Anger is innate; what
            // confronting or hitting brings is learned.
            if (maturity > 0.7 && MaxGrievance(p) > 0.5)
            {
                Person? target = null;
                double anger = 0;
                foreach (var kv in p.Grievance)
                {
                    if (kv.Value <= anger) continue;
                    var o = sim.MemberById(kv.Key);
                    if (o == null || o == p) continue;
                    double g = kv.Value * (1 - o.Injury);   // a beaten man is no longer worth fighting, unless the anger is deep
                    if (g > anger && Simulation.AwakeInCamp(o)) { anger = g; target = o; }
                }
                if (target != null && anger > 0.5)
                {
                    var rel = sim.RelationTo(p, target);
                    double heat = anger / (anger + 3);
                    double guilt = Simulation.Felt(Simulation.Guilt(p, target)) * 0.5;
                    double backing = sim.Backing(target, p);
                    double confront = sim.ConfrontValue(p, target, backing);
                    // Fear of them, and of the kin and friends who'd stand with them.
                    double fear = backing / Math.Max(0.1, p.B(BodyTrait.Strength) * p.Health) * 0.3 + p.Injury;
                    double godAttack = Simulation.GodActValue(p, Act.Attack);
                    double attack = heat * p.M(MindTrait.Temper) * 1.5 + Simulation.Felt(Simulation.Expects(p, Act.Attack, rel)) - guilt - fear * p.W(Drive.Safety) + godAttack;
                    var t = target;
                    if (confront > 0) options.Add(new Option(Verb.Confront, confront, () => new Confront(p, t)));
                    if (attack > 0) options.Add(new Option(Verb.Attack, attack, () => new Assault(p, t)));
                }
                // Or wait for them to be alone, away from camp: no kin to stand with them, nobody to see.
                if (lightForHunting && Simulation.AwakeInCamp(p))
                {
                    Person? alone = null;
                    double hate = 0;
                    foreach (var kv in p.Grievance)
                    {
                        if (kv.Value <= hate) continue;
                        var o = sim.MemberById(kv.Key);
                        if (o == null || o == p || !Simulation.OutAlone(o)) continue;
                        hate = kv.Value; alone = o;
                    }
                    if (alone != null && hate > 1)
                    {
                        var rel = sim.RelationTo(p, alone);
                        double heat = hate / (hate + 3);
                        double guilt = Simulation.Felt(Simulation.Guilt(p, alone)) * 0.5;
                        double fear = alone.B(BodyTrait.Strength) * alone.Health / Math.Max(0.1, p.B(BodyTrait.Strength) * p.Health) * 0.3 + p.Injury;
                        double godAmbush = Simulation.GodActValue(p, Act.Attack);
                        double ambush = heat * p.M(MindTrait.Temper) * 1.5 + Simulation.Felt(Simulation.Expects(p, Act.Attack, rel, false)) - guilt - fear * p.W(Drive.Safety) + godAmbush;
                        var t = alone;
                        if (ambush > 0) options.Add(new Option(Verb.Attack, ambush, () => new Ambush(p, t)));
                    }
                }
            }

            // Calling others to take a hearth's food by force: when angry at a household, or hungry
            // while it sits on plenty, and enough people are about to come along.
            if (maturity > 0.8 && (MaxGrievance(p) > 1.5 || hunger > 0.6))
            {
                int about = sim.AwakeInCampCount;
                if (about >= 2)
                {
                    Household? target = null;
                    double best = 0;
                    double famHunger = Simulation.FamilyHunger(p);
                    foreach (var h in band.Households)
                    {
                        if (h == home || Simulation.Spare(h) < 1) continue;
                        double v = sim.RallyValue(p, h, famHunger);
                        if (v > best) { best = v; target = h; }
                    }
                    if (target != null) { var t = target; options.Add(new Option(Verb.Rally, best, () => new Rally(p, t))); }
                }
            }

            // Raiding another band: anger at that band (from harms done by any of its people, and what
            // others have told), set against fear, a culture of peace, and what fighting has brought.
            if (maturity > 0.8 && p.BandGrievance.Count > 0 && p.Health > 0.6)
            {
                Simulation? foe = null;
                double anger = 0;
                foreach (var b in sim.Region.Bands)
                {
                    if (b == sim || b.Extinct) continue;
                    double g = p.GetBandGrievance(b.Id);
                    // Only a band whose camp they know of.
                    if (g > anger && p.Map.Camps.TryGetValue(b.Id, out var where) && clock.Day - where.day < 60
                        && WorldMap.Distance(where.x, where.y, band.CampX, band.CampY) < Region.KnownRange) { anger = g; foe = b; }
                }
                if (foe != null && anger > 1)
                {
                    double v = sim.RaidValue(p, foe);
                    var f = foe;
                    if (v > 0) options.Add(new Option(Verb.Raid, v, () => new Raid(p, f)));
                }
            }

            // Going to visit another band: missing people there, or hoping to find a partner.
            if (daylightAhead && sim.VisitPulls.TryGetValue(p, out var visit))
                options.Add(new Option(Verb.Visit, visit.pull, () => new Visit(p, visit.band) { ForFood = sim.VisitForFood.Contains(p) }));

            var child = p.Children.Where(c => c.Alive && c.AgeYears(clock) < 4).OrderBy(c => c.AgeYears(clock)).FirstOrDefault();
            if (child != null && child.CareToday < 6)
                options.Add(new Option(Verb.Care, 0.5 + p.M(MindTrait.Warmth) * 0.6 + (6 - child.CareToday) / 6 * 0.6, () => new Care(p, child)));

            if (maturity > 0.6 && p.Sickness < 0.2)
            {
                Person? sick = null;
                double worst = 0;
                foreach (var o in sim.SickThisHour)
                {
                    if (o == p || o.CareToday >= 4) continue;
                    double v = p.GetAffinity(o) + o.Sickness;
                    if (sick == null || v > worst) { worst = v; sick = o; }
                }
                if (sick != null && (child == null || child.CareToday >= 6))
                    options.Add(new Option(Verb.Care, p.M(MindTrait.Warmth) * 0.8 + p.S(Skill.Caring) * 0.3 + p.I(Interest.Healing) * 0.5 + p.GetAffinity(sick) * 0.6 + sick.Sickness * 0.5,
                        () => new Care(p, sick)));
            }

            if (maturity > 0.25 && maturity < 1)
            {
                Person? teacher = null;
                double fondest = -1;
                foreach (var o in sim.WorkingThisHour)
                {
                    if (o == p || !(o.Current is Knap || o.Current is Care || o.Current is Cook || o.Current is Preserve)) continue;
                    double v = p.GetAffinity(o);
                    if (v > fondest) { fondest = v; teacher = o; }
                }
                if (teacher != null)
                {
                    var (skill, interest) = teacher.Current is Knap ? (Skill.Knapping, Interest.StoneEarth)
                        : teacher.Current is Care ? (Skill.Caring, Interest.People) : (Skill.Cooking, Interest.Hearth);
                    options.Add(new Option(Verb.WatchAndLearn, 0.5 + p.M(MindTrait.Curiosity) * 0.6, () => new WatchAndLearn(p, teacher, skill, interest)));
                }
            }

            // Praying: a plea for a need beyond one's means (hunger with little food, own or a child's sickness, fear/injury)
            // value = need_severity * god:real * god:answers.
            // Praying: a plea for a need beyond one's means (starvation crisis, severe illness of self/child, severe wound).
            // Value = need_severity * god:real * god:answers. Only when no prayer is already open.
            if (!Simulation.Off("god") && !Simulation.Off("pray") && maturity > 0.3 && !sim.Region.HasOpenPrayer(p))
            {
                double real = Simulation.GodReal(p);
                double answers = Simulation.GodAnswers(p);
                if (real > 0.05 && answers > 0.02)
                {
                    double hungerNeed = 0;
                    double foodPerPerson = band.FoodTotal / Math.Max(1, people);
                    if (p.Satiety < 0.8)
                    {
                        double desperation = (1.0 - p.Satiety) * Math.Max(0, 1.0 - foodPerPerson / 3.0);
                        hungerNeed = desperation * 1.5;
                    }

                    double sickNeed = p.Sickness > 0.3 ? p.Sickness : 0;
                    Person? sickTarget = null;
                    var sickKid = home.Members.Where(m => m != p && m.AgeYears(clock) < 10 && m.Sickness > 0.3)
                                              .OrderByDescending(m => m.Sickness).FirstOrDefault();
                    if (sickKid != null && sickKid.Sickness > sickNeed)
                    {
                        sickNeed = sickKid.Sickness;
                        sickTarget = sickKid;
                    }

                    double fearNeed = p.Injury > 0.4 ? p.Injury : 0;

                    PrayerNeed bestNeed = PrayerNeed.Hunger;
                    double maxNeed = hungerNeed;
                    if (sickNeed > maxNeed) { maxNeed = sickNeed; bestNeed = PrayerNeed.Sickness; }
                    if (fearNeed > maxNeed) { maxNeed = fearNeed; bestNeed = PrayerNeed.Danger; }

                    if (maxNeed > 0.1)
                    {
                        double score = maxNeed * real * answers;
                        options.Add(new Option(Verb.Pray, score, () => new Pray(p, bestNeed, sickTarget)));
                    }
                }
            }

            // Offer to the god: give up food/goods at a sacred spot or hearth
            // Value = belief * Felt(learned return) * scale; sized by giver's means.
            // Gratitude (kind) and dread (cruel) scale that learned value (higher stakes), never add to it.
            // With no learned return, nobody offers, whatever the god's temper.
            if (!Simulation.Off("god") && !Simulation.Off("offer") && maturity > 0.6 && Simulation.AtCamp(p) && home.Food.Total >= 2.0 && home.DaysOfFood >= 2.0)
            {
                double real = Simulation.GodReal(p);
                if (real > 0.05)
                {
                    double learned = Simulation.GodOffer(p);
                    if (learned > 0.0)
                    {
                        double feltReturn = Simulation.Felt(learned);
                        double kind = Simulation.GodKind(p);
                        double scale = 1.0;
                        if (kind > 0)
                            scale += kind * (0.5 + p.M(MindTrait.Warmth));
                        else if (kind < 0)
                            scale += Math.Abs(kind) * p.W(Drive.Safety);

                        double offerScore = real * feltReturn * scale * (home.DaysOfFood > 4.0 ? 1.0 : 0.5);
                        if (offerScore > 0.05)
                        {
                            double offerAmount = Math.Min(2.0, home.Food.Total * 0.15);
                            options.Add(new Option(Verb.Offer, offerScore, () => new Offer(p, offerAmount)));
                        }
                    }
                }
            }

            // What this person cares about weighs their options: their own drive weights.
            for (int i = 0; i < options.Count; i++)
            {
                var o = options[i];
                double w = o.Verb switch
                {
                    Verb.Forage or Verb.Fish or Verb.Trap or Verb.Ask or Verb.Take or Verb.Eat or Verb.Preserve => p.W(Drive.Food),
                    Verb.Hunt => p.W(Drive.Food) * (1 - Simulation.HuntingPrestige(p)) + p.W(Drive.Standing) * Simulation.HuntingPrestige(p),
                    Verb.Rest or Verb.GatherWood or Verb.Cook => p.W(Drive.Comfort),
                    Verb.Socialize or Verb.Visit or Verb.Play => p.W(Drive.Company),
                    Verb.Care => p.W(Drive.Family),
                    Verb.Confront or Verb.Attack or Verb.Rally or Verb.Raid => p.W(Drive.Standing) / p.W(Drive.Safety),
                    Verb.Pray or Verb.Offer => p.W(Drive.Safety),
                    _ => 1,
                };
                if (w != 1) options[i] = new Option(o.Verb, o.Score * w, o.Make);
            }

            // When people are hungry, everything that doesn't get food or keep them alive fades.
            for (int i = 0; i < options.Count; i++)
            {
                var o = options[i];
                bool essential = o.Verb == Verb.Forage || o.Verb == Verb.Hunt || o.Verb == Verb.Fish || o.Verb == Verb.Trap || o.Verb == Verb.Eat || o.Verb == Verb.Rest
                                 || o.Verb == Verb.Care || o.Verb == Verb.Sleep || o.Verb == Verb.Socialize
                                 || o.Verb == Verb.Cook || o.Verb == Verb.GatherWood || o.Verb == Verb.Preserve
                                 || o.Verb == Verb.Ask || o.Verb == Verb.Take || o.Verb == Verb.Pray;
                if (!essential) options[i] = new Option(o.Verb, o.Score * (1 - hunger * 0.7), o.Make);
            }
            return options;
        }
    }
}
