using System;
using System.Collections.Generic;
using System.Linq;
using Anthro.Sim.Core;
using Anthro.Sim.People;
using Anthro.Sim.World;

namespace Anthro.Sim
{
    public enum PrayerNeed { Hunger, Sickness, Danger, Grief }

    /// <summary>
    /// A prayer request that stays open while the underlying need lasts.
    /// No timer: resolves when the need is met (by power or chance) or when the petitioner starves on / gives up / dies.
    /// </summary>
    public sealed class Prayer
    {
        public int Id { get; }
        public Person Asker { get; }
        public PrayerNeed Need { get; }
        public Person? SickTarget { get; }
        public int CampX { get; }
        public int CampY { get; }
        public long StartDay { get; }
        public bool Resolved { get; private set; }
        public bool Answered { get; private set; }
        public long ResolvedDay { get; private set; }
        public bool IsEmber { get; set; }
        public bool PowerCasted { get; set; }
        public double InitialDaysFood { get; set; }
        public double InitialPlenty { get; set; }
        public double InitialSeverity { get; set; }
        public long LastSignDay { get; set; } = -1;
        public long LastRefusalDay { get; set; } = -1;

        public Prayer(int id, Person asker, PrayerNeed need, int campX, int campY, long startDay, Person? sickTarget = null)
        {
            Id = id;
            Asker = asker;
            Need = need;
            CampX = campX;
            CampY = campY;
            StartDay = startDay;
            SickTarget = sickTarget;
        }

        public void Resolve(bool answered, long day)
        {
            Resolved = true;
            Answered = answered;
            ResolvedDay = day;
        }
    }

    public sealed partial class Region
    {
        public readonly List<Prayer> ActivePrayers = new List<Prayer>();
        public readonly List<Prayer> ResolvedPrayers = new List<Prayer>();
        int _nextPrayerId = 1;

        public int PrayersAskedYear { get; internal set; }
        public int PrayersResolvedGoodYear { get; internal set; }
        public int PrayersResolvedBadYear { get; internal set; }

        public bool HasOpenPrayer(Person p) => ActivePrayers.Any(pr => pr.Asker == p && !pr.Resolved);

        public Prayer AddPrayer(Person asker, PrayerNeed need, int campX, int campY, Person? sickTarget = null)
        {
            var existing = ActivePrayers.FirstOrDefault(p => p.Asker == asker && p.Need == need && !p.Resolved);
            if (existing != null) return existing;

            var prayer = new Prayer(_nextPrayerId++, asker, need, campX, campY, Clock.Day, sickTarget);
            var band = Bands.FirstOrDefault(b => b.Band.Members.Contains(asker));
            if (need == PrayerNeed.Hunger)
            {
                prayer.InitialDaysFood = asker.Home?.DaysOfFood ?? 0.0;
                prayer.InitialPlenty = (band != null && band.ForagePatches.Count > 0) ? Math.Min(1.0, band.ForagePatches.Sum(c => c.PlantStock) / 50.0) : 0.0;
                prayer.InitialSeverity = Math.Max(0.1, 1.0 - asker.Satiety);
            }
            else if (need == PrayerNeed.Sickness)
            {
                prayer.InitialSeverity = (sickTarget ?? asker).Sickness;
            }
            else if (need == PrayerNeed.Danger)
            {
                prayer.InitialSeverity = asker.Injury;
            }

            if (Favor <= 5.0 && People.Count(Simulation.IsBeliever) <= 2)
            {
                prayer.IsEmber = true;
                GodEvents.Add(new GodEvent(Clock.Tick, "LastEmber", $"{asker.Name} prayed from the last embers of faith", campX, campY));
            }
            ActivePrayers.Add(prayer);
            PrayersAskedYear++;
            GodEvents.Add(new GodEvent(Clock.Tick, "Prayer", $"{asker.Name} prayed for {need}", campX, campY));
            return prayer;
        }

        /// <summary>
        /// Evaluates open prayers. Stays open while need lasts (no timers).
        /// Resolves good when need is met; resolves bad if petitioner dies or starves on / gives up.
        /// </summary>
        public void ResolvePrayers()
        {
            if (Simulation.Off("god") || Simulation.Off("pray")) return;
            if (ActivePrayers.Count == 0) return;

            var toResolve = new List<(Prayer prayer, bool answered, double surprise)>();
            var toGiveUp = new List<Prayer>();

            foreach (var prayer in ActivePrayers)
            {
                var asker = prayer.Asker;
                var target = prayer.SickTarget ?? asker;
                
                // Each day a plea goes unanswered, it erodes belief
                if (Clock.Hour == 0)
                {
                    double desperation = 0.01;
                    if (prayer.Need == PrayerNeed.Hunger) desperation = Math.Max(0.01, (1.0 - asker.Satiety) * 0.05);
                    if (prayer.Need == PrayerNeed.Sickness) desperation = Math.Max(0.01, target.Sickness * 0.05);
                    if (prayer.Need == PrayerNeed.Danger) desperation = Math.Max(0.01, asker.Injury * 0.05);
                    asker.Expect.Absorb(Simulation.GodAnswersTopic, 0.0, desperation, Simulation.GodAnswers(asker));
                    asker.Expect.Absorb(Simulation.GodRealTopic, 0.0, desperation * 0.5, Simulation.GodReal(asker));
                    
                    if (Simulation.GodAnswers(asker) < 0.02)
                    {
                        toGiveUp.Add(prayer);
                        continue;
                    }
                }

                // Signs while a plea is open (brief B1, Fix 3)
                // An omen nearby, a bounty nearby, or a big hunt brought in reads as a sign (belief up)
                // A death in the family or a hard frost reads as a refusal (belief down)
                var band = Bands.FirstOrDefault(b => b.Band.Members.Contains(asker));
                if (band != null && Clock.Hour == 12)
                {
                    bool omenNearby = GodEvents.Any(e => e.Tick >= Clock.Tick - 24 && (e.Kind == "Omen" || e.Kind.StartsWith("Bounty")) && WorldMap.Distance(e.X, e.Y, prayer.CampX, prayer.CampY) <= WorldMap.FromMeters(5000));
                    bool bigHunt = band.ThisYear.Hunted >= 20 && band.Band.Members.Any(m => m.LastHuntDay == Clock.Day && m.BigKills > 0);

                    if ((omenNearby || bigHunt) && prayer.LastSignDay != Clock.Day)
                    {
                        prayer.LastSignDay = Clock.Day;
                        double cred = asker.M(MindTrait.Credulity);
                        asker.Expect.Absorb(Simulation.GodAnswersTopic, 1.0, 0.08 * (0.4 + cred * 0.8), Simulation.GodAnswers(asker));
                        asker.Expect.Absorb(Simulation.GodRealTopic, 1.0, 0.06 * (0.4 + cred * 0.8), Simulation.GodReal(asker));
                        GodEvents.Add(new GodEvent(Clock.Tick, "PrayerSign", $"{asker.Name} saw a sign while their plea for {prayer.Need} was open", prayer.CampX, prayer.CampY));
                    }

                    bool deathInFamily = band.Band.LastDeathDay == Clock.Day;
                    bool hardFrost = band.World[(int)band.Band.CampX, (int)band.Band.CampY].Temperature < -5.0 && Clock.Season == Season.Winter;

                    if ((deathInFamily || hardFrost) && prayer.LastRefusalDay != Clock.Day)
                    {
                        prayer.LastRefusalDay = Clock.Day;
                        asker.Expect.Absorb(Simulation.GodAnswersTopic, 0.0, 0.08, Simulation.GodAnswers(asker));
                        asker.Expect.Absorb(Simulation.GodKindTopic, -1.0, 0.06, Simulation.GodKind(asker));
                        GodEvents.Add(new GodEvent(Clock.Tick, "PrayerRefusal", $"{asker.Name} felt refused while their plea for {prayer.Need} was open", prayer.CampX, prayer.CampY));
                    }
                }

                if (prayer.Need == PrayerNeed.Hunger)
                {
                    if (asker.Health <= 0)
                    {
                        toResolve.Add((prayer, false, 1.0));
                    }
                    else if (asker.Satiety >= 0.7)
                    {
                        // Continuous surprise: food when hearth was empty and land bare counts fully (1.0); ordinary meal barely counts (0.05).
                        double foodUnlikely = Math.Max(0.0, 1.0 - (prayer.InitialDaysFood / 2.0));
                        double landUnlikely = Math.Max(0.0, 1.0 - prayer.InitialPlenty);
                        double surprise = Math.Min(1.0, 0.05 + 0.95 * foodUnlikely * (0.3 + 0.7 * landUnlikely));
                        toResolve.Add((prayer, true, surprise));
                    }
                }
                else if (prayer.Need == PrayerNeed.Sickness)
                {
                    if (target.Health <= 0)
                    {
                        toResolve.Add((prayer, false, 1.0));
                    }
                    else if (target.Sickness <= 0 && target.Health >= 0.7)
                    {
                        // Continuous surprise: severe sickness breaking is a surprise, mild cough is expected
                        double surprise = Math.Max(0.05, Math.Min(1.0, prayer.InitialSeverity));
                        toResolve.Add((prayer, true, surprise));
                    }
                }
                else if (prayer.Need == PrayerNeed.Danger)
                {
                    if (asker.Health <= 0)
                    {
                        toResolve.Add((prayer, false, 1.0));
                    }
                    else if (asker.Injury <= 0.05)
                    {
                        // Continuous surprise: severe injury healing is a surprise, light scratch is expected
                        double surprise = Math.Max(0.05, Math.Min(1.0, prayer.InitialSeverity));
                        toResolve.Add((prayer, true, surprise));
                    }
                }
                else if (prayer.Need == PrayerNeed.Grief)
                {
                    if (asker.Health <= 0)
                    {
                        toResolve.Add((prayer, false, 1.0));
                    }
                    // Grief pleas drop for now, but if they are open, just give up eventually
                }
            }

            foreach (var p in toGiveUp)
            {
                p.Resolve(false, Clock.Day);
                ActivePrayers.Remove(p);
                ResolvedPrayers.Add(p);
                if (ResolvedPrayers.Count > 500) ResolvedPrayers.RemoveAt(0);
                GodEvents.Add(new GodEvent(Clock.Tick, "PrayerFaded", $"Plea by {p.Asker.Name} for {p.Need} was given up", p.CampX, p.CampY));
            }

            foreach (var (p, answered, surprise) in toResolve)
            {
                ResolvePrayer(p, answered, surprise);
            }
        }

        public void ResolvePrayer(Prayer p, bool answered, double surprise = 1.0)
        {
            p.Resolve(answered, Clock.Day);
            ActivePrayers.Remove(p);
            ResolvedPrayers.Add(p);
            if (ResolvedPrayers.Count > 500) ResolvedPrayers.RemoveAt(0);

            if (answered)
            {
                PrayersResolvedGoodYear++;
                var band = Bands.FirstOrDefault(b => b.Band.Members.Contains(p.Asker));
                if (band != null)
                {
                    band.ThisYear.PrayersResolvedGood++;
                    foreach (var w in band.Band.Members.Where(m => m != p.Asker && band.Grown(m)))
                    {
                        w.Expect.Absorb(Simulation.GodAnswersTopic, 1.0, 0.12 * surprise, Simulation.GodAnswers(w));
                        w.Expect.Absorb(Simulation.GodRealTopic, 1.0, 0.08 * surprise, Simulation.GodReal(w));
                    }
                }

                // Asker learns answered plea
                p.Asker.Expect.Absorb(Simulation.GodAnswersTopic, 1.0, 0.25 * surprise, Simulation.GodAnswers(p.Asker));
                p.Asker.Expect.Absorb(Simulation.GodRealTopic, 1.0, 0.20 * surprise, Simulation.GodReal(p.Asker));

                GodEvents.Add(new GodEvent(Clock.Tick, "PrayerAnswered", $"Plea by {p.Asker.Name} for {p.Need} was met (surprise: {surprise:F2})", p.CampX, p.CampY));
                
                if (surprise > 0.5) InterpretStrikingEvent(p.CampX, p.CampY, isGood: true, "PrayerAnswered");

                if (p.IsEmber)
                {
                    IgnoredEmbers = 0;
                    p.Asker.Expect.Absorb(Simulation.GodRealTopic, 1.0, 0.40 * surprise, Simulation.GodReal(p.Asker));
                    GodEvents.Add(new GodEvent(Clock.Tick, "EmberAnswered", $"An ember plea was answered! Faith rekindles in {p.Asker.Name}", p.CampX, p.CampY));
                }
            }
            else
            {
                PrayersResolvedBadYear++;
                var band = Bands.FirstOrDefault(b => b.Band.Members.Contains(p.Asker));
                if (band != null)
                {
                    band.ThisYear.PrayersResolvedBad++;
                    foreach (var w in band.Band.Members.Where(m => m != p.Asker && band.Grown(m)))
                    {
                        w.Expect.Absorb(Simulation.GodAnswersTopic, 0.0, 0.06, Simulation.GodAnswers(w));
                    }
                }

                if (p.Asker.Health > 0)
                {
                    p.Asker.Expect.Absorb(Simulation.GodAnswersTopic, 0.0, 0.15, Simulation.GodAnswers(p.Asker));
                    p.Asker.Expect.Absorb(Simulation.GodRealTopic, 0.0, 0.05, Simulation.GodReal(p.Asker));
                }
                GodEvents.Add(new GodEvent(Clock.Tick, "PrayerFailed", $"Plea by {p.Asker.Name} for {p.Need} failed", p.CampX, p.CampY));

                if (p.IsEmber)
                {
                    IgnoredEmbers++;
                    GodEvents.Add(new GodEvent(Clock.Tick, "EmberIgnored", $"An ember plea went unanswered ({IgnoredEmbers}/3)", p.CampX, p.CampY));
                    if (IgnoredEmbers >= 3)
                    {
                        GodFaded = true;
                        Favor = 0.0;
                        Chronicle.Write(Clock, Importance.Major, "The last embers went cold. The god faded from the world.");
                        GodEvents.Add(new GodEvent(Clock.Tick, "GodFaded", "The god has faded from existence", p.CampX, p.CampY));
                    }
                }
            }
        }
    }

    public sealed partial class Simulation
    {
        public void RegisterPrayer(Person p, PrayerNeed need, Person? target = null)
        {
            if (Off("god") || Off("pray")) return;
            Region.AddPrayer(p, need, (int)Band.CampX, (int)Band.CampY, target);
            double gain = 1.0 * GodReal(p);
            Region.FavorEarnedPrayer += gain;
            Region.Favor = Math.Min(Region.FavorCap, Region.Favor + gain);
            ThisYear.PrayersAsked++;
            ThisYear.FavorGain += gain;

            if (!Off("relief") && p.Dread > 0.05)
            {
                p.AppeasementAct = "pray";
                p.AppeasementDread = p.Dread;
            }
        }
    }
}
