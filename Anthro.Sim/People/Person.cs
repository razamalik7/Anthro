using System.Collections.Generic;
using System.Linq;
using Anthro.Sim.Activities;
using Anthro.Sim.Core;

namespace Anthro.Sim.People
{
    public enum Sex { Female, Male }

    public sealed class Person
    {
        public readonly int Id;
        public readonly string Name;
        public readonly Sex Sex;
        public readonly long BirthDay;
        /// <summary>The day they died, or -1.</summary>
        public long DeathDay = -1;

        public Person? Mother, Father;
        /// <summary>
        /// Everyone they're married to. One spouse is usual; more than one happens where a people has
        /// come to think well of it (a learned custom, see Simulation.ConsiderAnotherSpouse), never by rule.
        /// </summary>
        public readonly List<Person> Spouses = new List<Person>();
        /// <summary>The day each marriage began.</summary>
        public readonly Dictionary<Person, long> WedOn = new Dictionary<Person, long>();
        /// <summary>Their first spouse (for the many things only one spouse matters to), or null if unmarried.</summary>
        public Person? Partner => Spouses.Count > 0 ? Spouses[0] : null;
        public bool Married => Spouses.Count > 0;

        public static void Wed(Person a, Person b, long day)
        {
            if (a.Spouses.Contains(b)) return;
            a.Spouses.Add(b); b.Spouses.Add(a);
            a.WedOn[b] = day; b.WedOn[a] = day;
            a.PairedDay = b.PairedDay = day;
        }

        public static void Unwed(Person a, Person b)
        {
            a.Spouses.Remove(b); b.Spouses.Remove(a);
            a.WedOn.Remove(b); b.WedOn.Remove(a);
        }
        public readonly List<Person> Children = new List<Person>();

        // Inherited, fixed for life.
        public readonly double[] Mind = new double[TraitCounts.Mind];
        public readonly double[] Body = new double[TraitCounts.Body];

        // Shift over a lifetime.
        public readonly double[] Interests = new double[TraitCounts.Interests];
        public readonly double[] Skills = new double[TraitCounts.Skills];
        /// <summary>How much this person cares about each drive (0-1, 0.5 typical).</summary>
        public readonly double[] Drives = { 0.5, 0.5, 0.5, 0.5, 0.5, 0.5 };
        /// <summary>
        /// Their settled character: formed in childhood (from their parents, and the adults they
        /// learned from). What happens in adult life moves their drives away from it for a while;
        /// they relax back.
        /// </summary>
        public readonly double[] BaseDrives = { 0.5, 0.5, 0.5, 0.5, 0.5, 0.5 };
        public readonly HashSet<string> Knows = new HashSet<string>();
        /// <summary>
        /// Things someone has partly learned (0-1): heard about, or watched a few times. A craft can only
        /// be got so far by being told; the rest comes from watching it done. Having heard of something
        /// also makes working it out oneself likelier.
        /// </summary>
        public readonly Dictionary<string, double> Learning = new Dictionary<string, double>();
        public readonly Expectations Expect = new Expectations();
        /// <summary>Active divine nudges at life branch points (T8): fork -> choice.</summary>
        public readonly Dictionary<string, string> Nudges = new Dictionary<string, string>();

        /// <summary>Per-person dread after a striking bad event (T10 A2). Fades gradually unless renewed.</summary>
        public double Dread { get; set; } = 0.0;
        public double DreadPeak { get; set; } = 0.0;
        public string? AppeasementAct { get; set; } = null;
        public double AppeasementDread { get; set; } = 0.0;
        public long LastGodSignDay { get; set; } = -1;
        public long LastThanksDay { get; set; } = -99999;
        public double ThanksUrge { get; set; } = 0.0;
        public long LastPrayDay { get; set; } = -99999;

        public double ConsumeNudgeWeight(string fork, double basePush = 0.5, params string[] targetChoices)
        {
            if (Nudges.TryGetValue(fork, out var pushedChoice))
            {
                Nudges.Remove(fork);
                double weight = basePush * Simulation.GodReal(this) * (0.5 + M(MindTrait.Credulity));
                return targetChoices.Contains(pushedChoice) ? weight : -weight;
            }
            return 0.0;
        }

        public string NudgeOutlook(string fork, string choice, double basePush = 0.5)
        {
            double weight = basePush * Simulation.GodReal(this) * (0.5 + M(MindTrait.Credulity));
            if (weight < 0.1) return "set on it, a whisper won't do it";
            if (weight < 0.3) return "torn, might listen";
            return "torn, likely to listen";
        }
        /// <summary>Fondness for each person (0 to 1). Grows with time together; fades without it; drops with conflict.</summary>
        public readonly Dictionary<int, double> Affinity = new Dictionary<int, double>();
        /// <summary>
        /// Trust in each person (0 to 1, 0.5 = no opinion): what they expect of that person, learned
        /// only from what they've seen or heard that person do (giving, refusing, taking, hitting).
        /// </summary>
        public readonly Dictionary<int, double> Trust = new Dictionary<int, double>();
        /// <summary>
        /// Anger at each person for a harm pinned on them (a loss, a blow), in rations of harm felt.
        /// Innate: stronger when one's children are hungry, fading over weeks. It's what moves people
        /// to confront, hit back or shut someone out.
        /// </summary>
        public readonly Dictionary<int, double> Grievance = new Dictionary<int, double>();
        /// <summary>
        /// Anger at another band as a whole (by band id): a harm from one of its people spreads to all
        /// of them. It's why revenge can fall on someone who did nothing.
        /// </summary>
        public readonly Dictionary<int, double> BandGrievance = new Dictionary<int, double>();

        public double GetBandGrievance(int bandId) => BandGrievance.TryGetValue(bandId, out var v) ? v : 0;

        public void AddBandGrievance(int bandId, double amount)
        {
            double v = GetBandGrievance(bandId) + amount;
            if (v <= 0.01) BandGrievance.Remove(bandId); else BandGrievance[bandId] = v;
        }
        /// <summary>Deeds they've seen or heard about, newest last (a few dozen at most).</summary>
        public readonly List<Remembered> Deeds = new List<Remembered>();
        public readonly HashSet<Deed> KnownDeeds = new HashSet<Deed>();
        /// <summary>Their own recent acts (recent season), kept separate to not crowd out memory of others.</summary>
        public readonly List<Remembered> OwnDeeds = new List<Remembered>();
        public readonly HashSet<Deed> KnownOwnDeeds = new HashSet<Deed>();
        /// <summary>What they know of the land: places, game seen lately, other camps.</summary>
        public readonly MentalMap Map = new MentalMap();
        /// <summary>The household this person eats and lives with.</summary>
        public Household? Home;
        /// <summary>When they last left the band (to judge whether coming back is on their mind yet).</summary>
        public long LeftDay = -100000;
        /// <summary>Discontent with life in the band: hardship, crowding and distrust, built up over a season or more.</summary>
        public double Discontent;
        /// <summary>Hours spent getting food and fuel today, and the usual day lately (hours, averaged over a month or two).</summary>
        public double WorkToday, Workload = 4;
        /// <summary>Food this person brought home today, and what they've usually brought lately (rations a day, over a few months).</summary>
        public double ProvidedToday, Provided = 1;
        public readonly List<string> Memories = new List<string>();
        /// <summary>Foraging yield remembered per place (see <see cref="PlaceKey"/>), one memory per season, from their own trips.</summary>
        public readonly Dictionary<int, double>[] Places =
            { new Dictionary<int, double>(), new Dictionary<int, double>(), new Dictionary<int, double>(), new Dictionary<int, double>() };

        /// <summary>Set when foraging has disappointed lately; that's when people start thinking about moving.</summary>
        public bool Restless;

        // Body state.
        public double Health = 1, Fatigue, Injury;
        /// <summary>Food in the belly, in days of this person's own need. Empties hour by hour, faster when working hard.</summary>
        public double Stomach = 0.8;
        /// <summary>Body reserves (fat), in days of this person's own need. Built up when food is plentiful, burned when it isn't.</summary>
        public double Reserve = 4;
        /// <summary>Yesterday's reserve, and the recent trend (reserve gained or lost per day, averaged over about a month).</summary>
        public double LastReserve = 4, WeightTrend;
        /// <summary>The leanest they've been this winter; what they'll remember about it.</summary>
        /// <summary>The leanest this person has been so far this season (fed and fat, 0-1).</summary>
        public double WinterLow = 1;
        /// <summary>Nourishment (0 to 1): about three days of reserves or more is fully nourished.</summary>
        public double Satiety = 1;
        /// <summary>Ate meat that had gone off today.</summary>
        public bool AteRotten;
        /// <summary>How bad a current sickness is, 0 when well.</summary>
        public double Sickness;
        public Disease? Illness;
        public int SickDays;
        /// <summary>Protection left by past sicknesses, per disease. Fades over time.</summary>
        public readonly Dictionary<Disease, double> Immunities = new Dictionary<Disease, double>();
        public bool Alive = true;
        public string? DeathCause;
        public long DueDay = -1;
        /// <summary>Next child born to this mother is blessed by the god.</summary>
        public bool BlessedNextBirth;
        /// <summary>Born under the god's blessing.</summary>
        public bool Blessed;
        /// <summary>Can no longer conceive: most often from an infection after a birth (secondary sterility).</summary>
        public bool Sterile;
        public Person? ExpectingWith;
        /// <summary>When they paired with their current partner.</summary>
        public long PairedDay;
        public double CareToday;
        /// <summary>What has been wearing this person down over the past few weeks, by cause. Fades day by day.</summary>
        public readonly Dictionary<string, double> HarmLedger = new Dictionary<string, double>();

        /// <summary>How chilled they were today (0 = comfortable). Weakens the body's defenses.</summary>
        public double ColdStress;

        /// <summary>Big game this person has brought down: a plain record people can see and tell of.</summary>
        public int BigKills;
        public long LastHuntDay = -10;
        /// <summary>An idea like moving camp comes up at most once a day per person.</summary>
        public long LastProposalDay = -1;
        /// <summary>When they last called others together against a hearth: rounding people up for that takes days of talk.</summary>
        public long LastRallyDay = -1000;
        /// <summary>When they last went to visit another band. Longing to go again grows from then.</summary>
        public long LastVisitDay;
        /// <summary>The hour this person last lived through (so someone moving between bands mid-hour isn't stepped twice).</summary>
        public long SteppedAt = -1;
        /// <summary>Whose urging last moved their camp, when, and how foraging had been going before: judged on the next trips.</summary>
        public Person? MovedBy;
        public long MovedDay;
        public double YieldBeforeMove;
        /// <summary>How much more the old than the young were for the last move (+ the old pushed it, - they were against it).</summary>
        public double EldersTiltOnMove;
        /// <summary>Someone whose call to leave was turned down doesn't raise it again for a season.</summary>
        public long LastSplitProposalDay = -1000;
        public double X, Y;
        public Activity? Current;

        public Person(int id, string name, Sex sex, long birthDay)
        {
            Id = id; Name = name; Sex = sex; BirthDay = birthDay;
        }

        public double M(MindTrait t) => Mind[(int)t];
        public double B(BodyTrait t) => Body[(int)t];
        public double I(Interest i) => Interests[(int)i];
        public double S(Skill s) => Skills[(int)s];

        public double AgeYears(SimClock clock) => (clock.Day - BirthDay) / (double)SimClock.DaysPerYear;

        public bool IsAdult(SimClock clock) => AgeYears(clock) >= 14;

        public bool IsKin(Person other) =>
            other == Mother || other == Father || Children.Contains(other) || other.Children.Contains(this)
            || (Mother != null && (other.Mother == Mother)) || (Father != null && (other.Father == Father));

        /// <summary>A drive as a weight on choices: 0.5 (cares little) to 1.5 (cares a lot).</summary>
        public double W(Drive d) => 0.5 + Drives[(int)d];

        /// <summary>What happens to people shifts what they care about, a little.</summary>
        public void Shift(Drive d, double amount) => Drives[(int)d] = Inheritance.Clamp01(Drives[(int)d] + amount);

        public double GetAffinity(Person other) => Affinity.TryGetValue(other.Id, out var v) ? v : 0;

        public void AddAffinity(Person other, double amount) =>
            Affinity[other.Id] = Inheritance.Clamp01(GetAffinity(other) + amount);

        public double GetTrust(Person other) => Trust.TryGetValue(other.Id, out var v) ? v : 0.5;

        public double GetGrievance(Person other) => Grievance.TryGetValue(other.Id, out var v) ? v : 0;

        public void AddGrievance(Person other, double amount)
        {
            double v = GetGrievance(other) + amount;
            if (v <= 0.01) Grievance.Remove(other.Id); else Grievance[other.Id] = v;
        }

        public void AddTrust(Person other, double amount) =>
            Trust[other.Id] = Inheritance.Clamp01(GetTrust(other) + amount);

        /// <summary>Practice slowly raises a skill; aptitude sets the pace, mastery gets harder near the top.</summary>
        public void Practice(Skill skill, double hours, double rate = 0.002)
        {
            int i = (int)skill;
            Skills[i] = Inheritance.Clamp01(Skills[i] + rate * hours * (0.4 + M(MindTrait.Aptitude)) * (1 - Skills[i]));
        }

        /// <summary>Their own children still dependent on them (under 14) who are hungry right now.</summary>
        public bool HasHungryDependents(SimClock clock) =>
            Children.Any(c => c.Alive && c.AgeYears(clock) < 14 && (c.Stomach < 0.3 || c.Satiety < 0.8));

        public double ImmunityTo(Disease d) => Immunities.TryGetValue(d, out var v) ? v : 0;

        /// <summary>Fall ill with a disease, unless already ill.</summary>
        public bool Catch(Disease d, double severity)
        {
            if (Illness != null || !Alive) return false;
            Illness = d; Sickness = severity; SickDays = 0;
            // Getting sick in camp makes people think about leaving it.
            if (d == Disease.Flux) Restless = true;
            return true;
        }

        public static int PlaceKey(int x, int y) => x * 1000 + y;
        public static (int x, int y) FromPlaceKey(int key) => (key / 1000, key % 1000);

        public void RememberPlace(int x, int y, Core.Season season, double yield)
        {
            var memory = Places[(int)season];
            int key = PlaceKey(x, y);
            memory[key] = memory.TryGetValue(key, out var old) ? old + (yield - old) * 0.5 : yield;
        }

        public void Nudge(Interest interest, double amount)
        {
            int i = (int)interest;
            Interests[i] = Inheritance.Clamp01(Interests[i] + amount);
        }

        public override string ToString() => Name;
    }
}
