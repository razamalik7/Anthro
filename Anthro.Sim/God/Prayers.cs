using System;
using System.Collections.Generic;
using System.Linq;
using Anthro.Sim.Core;
using Anthro.Sim.People;

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

            var toResolve = new List<(Prayer prayer, bool answered)>();

            foreach (var prayer in ActivePrayers)
            {
                var asker = prayer.Asker;
                var target = prayer.SickTarget ?? asker;
                long daysOpen = Clock.Day - prayer.StartDay;

                if (prayer.Need == PrayerNeed.Hunger)
                {
                    if (asker.Health <= 0)
                    {
                        toResolve.Add((prayer, false));
                    }
                    else if (asker.Satiety >= 0.7 && (asker.Home?.DaysOfFood ?? 0) >= 3.0)
                    {
                        toResolve.Add((prayer, true));
                    }
                    else if (daysOpen >= 5)
                    {
                        // Starved on without divine answer: gives up
                        toResolve.Add((prayer, false));
                    }
                }
                else if (prayer.Need == PrayerNeed.Sickness)
                {
                    if (target.Health <= 0)
                    {
                        toResolve.Add((prayer, false));
                    }
                    else if (target.Sickness <= 0 && target.Health >= 0.75)
                    {
                        toResolve.Add((prayer, true));
                    }
                    else if (daysOpen >= 7)
                    {
                        // Sickness dragged on without relief: gives up
                        toResolve.Add((prayer, false));
                    }
                }
                else if (prayer.Need == PrayerNeed.Danger)
                {
                    if (asker.Health <= 0)
                    {
                        toResolve.Add((prayer, false));
                    }
                    else if (asker.Injury <= 0.05 && daysOpen >= 2)
                    {
                        toResolve.Add((prayer, true));
                    }
                    else if (daysOpen >= 5)
                    {
                        toResolve.Add((prayer, false));
                    }
                }
                else if (prayer.Need == PrayerNeed.Grief)
                {
                    if (asker.Health <= 0)
                    {
                        toResolve.Add((prayer, false));
                    }
                    else if (daysOpen >= 15)
                    {
                        toResolve.Add((prayer, true));
                    }
                }
            }

            foreach (var (p, answered) in toResolve)
            {
                ResolvePrayer(p, answered);
            }
        }

        public void ResolvePrayer(Prayer p, bool answered)
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
                        w.Expect.Absorb(Simulation.GodAnswersTopic, 1.0, 0.12, Simulation.GodAnswers(w));
                        w.Expect.Absorb(Simulation.GodRealTopic, 1.0, 0.08, Simulation.GodReal(w));
                    }
                }

                // Asker learns answered plea
                p.Asker.Expect.Absorb(Simulation.GodAnswersTopic, 1.0, 0.25, Simulation.GodAnswers(p.Asker));
                p.Asker.Expect.Absorb(Simulation.GodRealTopic, 1.0, 0.20, Simulation.GodReal(p.Asker));

                GodEvents.Add(new GodEvent(Clock.Tick, "PrayerAnswered", $"Plea by {p.Asker.Name} for {p.Need} was met", p.CampX, p.CampY));
                InterpretStrikingEvent(p.CampX, p.CampY, isGood: true, "PrayerAnswered");

                if (p.IsEmber)
                {
                    IgnoredEmbers = 0;
                    p.Asker.Expect.Absorb(Simulation.GodRealTopic, 1.0, 0.40, Simulation.GodReal(p.Asker));
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
                InterpretStrikingEvent(p.CampX, p.CampY, isGood: false, "PrayerFailed");

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
            Region.Favor = Math.Min(Region.FavorCap, Region.Favor + 1.0 * GodReal(p));
            ThisYear.PrayersAsked++;
            ThisYear.FavorGain += 1.0 * GodReal(p);
        }
    }
}
