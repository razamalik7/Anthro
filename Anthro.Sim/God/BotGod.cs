using System;
using System.Linq;
using Anthro.Sim.Core;
using Anthro.Sim.People;

namespace Anthro.Sim
{
    public enum GodPolicy
    {
        None,
        Kind,
        Cruel,
        Random,
        Skilful,
        Quiet,
        Charity,
        Demanding,
        Malevolent,
        Stern,
        Capricious
    }

    /// <summary>
    /// Autonomous bot god policies for headless simulation, testing, and comparative analysis (T7, T9C, T10C).
    /// </summary>
    public static class BotGod
    {
        public static GodPolicy PolicyFromEnv()
        {
            string? env = Environment.GetEnvironmentVariable("ANTHRO_GOD");
            return FromString(env);
        }

        public static GodPolicy FromString(string? str) => str?.Trim().ToLowerInvariant() switch
        {
            "kind" => GodPolicy.Kind,
            "cruel" => GodPolicy.Cruel,
            "random" => GodPolicy.Random,
            "skilful" or "skillful" => GodPolicy.Skilful,
            "quiet" => GodPolicy.Quiet,
            "charity" => GodPolicy.Charity,
            "demanding" => GodPolicy.Demanding,
            "malevolent" => GodPolicy.Malevolent,
            "stern" => GodPolicy.Stern,
            "capricious" => GodPolicy.Capricious,
            _ => GodPolicy.None
        };

        public static void Step(Region region)
        {
            if (region == null || Simulation.Off("god") || region.GodFaded) return;
            var policy = region.Policy;
            if (policy == GodPolicy.None) return;

            switch (policy)
            {
                case GodPolicy.Kind:
                    StepKind(region);
                    break;
                case GodPolicy.Cruel:
                    StepCruel(region);
                    break;
                case GodPolicy.Random:
                    StepRandom(region);
                    break;
                case GodPolicy.Skilful:
                    StepSkilful(region);
                    break;
                case GodPolicy.Quiet:
                    if (region.Clock.Year < 25)
                        StepSkilful(region);
                    break;
                case GodPolicy.Charity:
                    StepCharity(region);
                    break;
                case GodPolicy.Demanding:
                    StepDemanding(region);
                    break;
                case GodPolicy.Malevolent:
                    StepMalevolent(region);
                    break;
                case GodPolicy.Stern:
                    StepStern(region);
                    break;
                case GodPolicy.Capricious:
                    StepCapricious(region);
                    break;
            }
        }

        static void StepKind(Region region)
        {
            // Kind god answers unanswered prayers
            var unhelped = region.ActivePrayers.FirstOrDefault(p => !p.Resolved && !p.PowerCasted);
            if (unhelped != null)
            {
                if (unhelped.Need == PrayerNeed.Hunger)
                {
                    bool isWinter = region.Clock.Season == Season.Winter;
                    if (isWinter && region.Favor >= 25)
                    {
                        region.CastBounty(unhelped.CampX, unhelped.CampY, animals: true);
                        unhelped.PowerCasted = true;
                    }
                    else if (region.Favor >= 15)
                    {
                        region.CastBounty(unhelped.CampX, unhelped.CampY, animals: false);
                        unhelped.PowerCasted = true;
                    }
                    else if (region.Favor >= 5)
                    {
                        region.CastOmen(unhelped.CampX, unhelped.CampY, "A protective luminescence warms the camp", isGood: true);
                        unhelped.PowerCasted = true;
                    }
                }
                else if (unhelped.Need == PrayerNeed.Sickness)
                {
                    if (region.Favor >= 15)
                    {
                        region.CastBounty(unhelped.CampX, unhelped.CampY, animals: false);
                        unhelped.PowerCasted = true;
                    }
                    else if (region.Favor >= 5)
                    {
                        region.CastOmen(unhelped.CampX, unhelped.CampY, "A gentle sign touches the sick", isGood: true);
                        unhelped.PowerCasted = true;
                    }
                }
                else if (unhelped.Need == PrayerNeed.Danger || unhelped.Need == PrayerNeed.Grief)
                {
                    if (region.Favor >= 5)
                    {
                        region.CastOmen(unhelped.CampX, unhelped.CampY, "A protective luminescence calms the camp", isGood: true);
                        unhelped.PowerCasted = true;
                    }
                }
            }

            // Periodic signs of divine blessing to nurture faith (every 12 days at noon)
            if (region.Clock.Hour == 12 && region.Clock.Day % 12 == 0 && region.Favor >= 15)
            {
                if (region.Bands.Count > 0)
                {
                    var camp = region.Bands[region.GodRng.Range(0, region.Bands.Count)];
                    region.CastOmen((int)camp.Band.CampX, (int)camp.Band.CampY, "A radiant sunbeam of warmth descends on the camp", isGood: true);
                }
            }

            // Unprompted winter relief if favor is high
            if (region.Clock.Season == Season.Winter && region.Clock.Hour == 12 && region.Clock.DayOfSeason == 15)
            {
                if (region.World.Climate.WinterSeverity > 0.8 && region.Favor >= 60)
                {
                    region.CastWeather(Season.Winter, -0.4);
                }
            }

            // Heal severe sickness or wound if unlocked (C3)
            if (region.Favor >= 50)
            {
                var sick = region.People.FirstOrDefault(p => p.Alive && (p.Illness != null || p.Injury > 0.4) && region.CanCastHeal(p));
                if (sick != null) region.CastHeal(sick);
            }
        }

        static void StepCruel(Region region)
        {
            // Cruel god ignores pleas for food/relief.
            // When people plead, the cruel god may mock them with terrifying omens.
            var unhelped = region.ActivePrayers.FirstOrDefault(p => !p.Resolved && !p.PowerCasted);
            if (unhelped != null)
            {
                unhelped.PowerCasted = true;
                if (region.Favor >= 5 && region.GodRng.Chance(0.5))
                {
                    region.CastOmen(unhelped.CampX, unhelped.CampY, "The heavens split with wrathful thunder");
                }
            }

            // Periodic displays of divine terror (every 10 days) to keep fear alive
            if (region.Clock.Hour == 18 && region.Clock.Day % 10 == 0 && region.Favor >= 5)
            {
                if (region.Bands.Count > 0)
                {
                    var camp = region.Bands[region.GodRng.Range(0, region.Bands.Count)];
                    region.CastOmen((int)camp.Band.CampX, (int)camp.Band.CampY, "A terrifying roar shakes the hearth");
                }
            }

            // Cruel god harshens winter if high favor allows
            if (region.Clock.Season == Season.Winter && region.Clock.Hour == 12 && region.Clock.DayOfSeason == 10)
            {
                if (region.World.Climate.WinterSeverity < 1.3 && region.Favor >= 60)
                {
                    region.CastWeather(Season.Winter, +0.4);
                }
            }

            // Strike lightning when unlocked (C3)
            if (region.Favor >= 50)
            {
                var victim = region.People.FirstOrDefault(p => p.Alive && region.CanCastLightning(p));
                if (victim != null) region.CastLightning(victim);
            }
        }

        static void StepRandom(Region region)
        {
            var unhelped = region.ActivePrayers.FirstOrDefault(p => !p.Resolved && !p.PowerCasted);
            if (unhelped != null)
            {
                unhelped.PowerCasted = true;
                if (region.GodRng.Chance(0.5))
                {
                    // Kind
                    if (region.Favor >= 20)
                        region.CastBounty(unhelped.CampX, unhelped.CampY, animals: region.GodRng.Chance(0.5));
                }
                else
                {
                    // Cruel
                    if (region.Favor >= 5 && region.GodRng.Chance(0.5))
                        region.CastOmen(unhelped.CampX, unhelped.CampY, "A strange, eerie glow crosses the heavens");
                }
            }
        }

        static void StepSkilful(Region region)
        {
            if (Simulation.Off("skilful")) return;

            var adults = region.People.Where(p => p.Alive && p.AgeYears(region.Clock) >= 15).ToList();
            int adultCount = adults.Count;
            int believers = adults.Count(Simulation.IsBeliever);
            double meanReal = adultCount > 0 ? adults.Average(Simulation.GodReal) : 0.20;
            bool isFaithfulBand = adultCount >= 15 && (believers / (double)adultCount >= 0.5);

            // 1. Food & Sickness when a camp prays or is in acute famine (medium act, 20 plant / 30 animal favor, 1/season/camp limit):
            var unhelped = region.ActivePrayers.FirstOrDefault(p => !p.Resolved && !p.PowerCasted && p.LastSignDay < p.StartDay);
            if (unhelped != null)
            {
                if (unhelped.Need == PrayerNeed.Hunger)
                {
                    var camp = region.Bands.FirstOrDefault(b => b.Band.CampX == unhelped.CampX && b.Band.CampY == unhelped.CampY);
                    bool campStarving = unhelped.Asker != null && unhelped.Asker.Satiety < 0.45 && (camp == null || camp.Band.FoodTotal < camp.Band.Members.Count * 0.8);
                    bool canBounty = camp == null || (region.Clock.Day - camp.LastBountyDay >= 90);

                    if (campStarving && canBounty)
                    {
                        bool isWinter = region.Clock.Season == Season.Winter;
                        if (isWinter && region.Favor >= 35.0)
                        {
                            region.CastBounty(unhelped.CampX, unhelped.CampY, animals: true);
                            foreach (var pr in region.ActivePrayers.Where(pr => !pr.Resolved && pr.CampX == unhelped.CampX && pr.CampY == unhelped.CampY))
                            {
                                pr.PowerCasted = true;
                                pr.LastSignDay = region.Clock.Day;
                            }
                            if (camp != null) camp.LastBountyDay = region.Clock.Day;
                        }
                        else if (!isWinter && region.Favor >= 25.0)
                        {
                            region.CastBounty(unhelped.CampX, unhelped.CampY, animals: false);
                            foreach (var pr in region.ActivePrayers.Where(pr => !pr.Resolved && pr.CampX == unhelped.CampX && pr.CampY == unhelped.CampY))
                            {
                                pr.PowerCasted = true;
                                pr.LastSignDay = region.Clock.Day;
                            }
                            if (camp != null) camp.LastBountyDay = region.Clock.Day;
                        }
                        else if (region.Favor >= 15.0 && region.Clock.Day - region.LastOmenSignDay >= 12)
                        {
                            region.CastOmen(unhelped.CampX, unhelped.CampY, "A comforting sign answered the hungry camp's plea", isGood: true);
                            foreach (var pr in region.ActivePrayers.Where(pr => !pr.Resolved && pr.CampX == unhelped.CampX && pr.CampY == unhelped.CampY))
                                pr.LastSignDay = region.Clock.Day;
                            region.LastOmenSignDay = region.Clock.Day;
                        }
                    }
                    else if (region.Favor >= 15.0 && region.Clock.Day - region.LastOmenSignDay >= 12)
                    {
                        region.CastOmen(unhelped.CampX, unhelped.CampY, "A comforting sign answered the camp's plea", isGood: true);
                        foreach (var pr in region.ActivePrayers.Where(pr => !pr.Resolved && pr.CampX == unhelped.CampX && pr.CampY == unhelped.CampY))
                            pr.LastSignDay = region.Clock.Day;
                        region.LastOmenSignDay = region.Clock.Day;
                    }
                }
                else if (unhelped.Need == PrayerNeed.Sickness)
                {
                    if (region.CanCastHeal(unhelped.Asker) && region.Favor >= 65.0)
                    {
                        region.CastHeal(unhelped.Asker);
                        unhelped.PowerCasted = true;
                        unhelped.LastSignDay = region.Clock.Day;
                    }
                    else if (region.Favor >= 15.0 && region.Clock.Day - region.LastOmenSignDay >= 12)
                    {
                        region.CastOmen(unhelped.CampX, unhelped.CampY, "A gentle sign touches the sick", isGood: true);
                        foreach (var pr in region.ActivePrayers.Where(pr => !pr.Resolved && pr.CampX == unhelped.CampX && pr.CampY == unhelped.CampY))
                            pr.LastSignDay = region.Clock.Day;
                        unhelped.PowerCasted = true;
                        region.LastOmenSignDay = region.Clock.Day;
                    }
                }
            }

            // Also check camps in severe acute famine without open prayers (1/season/camp limit), keeping a 15 favor reserve for omens
            if (region.Clock.Hour == 12)
            {
                bool isWinter = region.Clock.Season == Season.Winter;
                double cost = isWinter ? 30.0 : 20.0;
                if (region.Favor >= cost + 15.0)
                {
                    var acuteCamp = region.Bands.FirstOrDefault(b => b.Band.Members.Count > 0
                        && b.Band.FoodTotal < b.Band.Members.Count * 0.3
                        && (b.Band.Members.Average(m => m.Satiety) < 0.50 || b.Band.Members.Any(m => m.Reserve < 0.35))
                        && (region.Clock.Day - b.LastBountyDay >= 90));
                    if (acuteCamp != null)
                    {
                        region.CastBounty((int)acuteCamp.Band.CampX, (int)acuteCamp.Band.CampY, animals: isWinter);
                        acuteCamp.LastBountyDay = region.Clock.Day;
                    }
                }
            }

            // 2. Heal critically sick / dying when unlocked (large act, 50 favor, generation limit in CanCastHeal):
            if (region.Favor >= 65.0)
            {
                var dying = region.People.FirstOrDefault(p => p.Alive && p.Illness != null && p.Health < 0.50 && region.CanCastHeal(p));
                if (dying != null)
                {
                    region.CastHeal(dying);
                }
            }

            // 3. Weather before a hard season (large act, 60 favor, 1/year limit):
            // In Autumn or early Winter, soften harsh winter before freeze-up.
            if ((region.Clock.Season == Season.Autumn || (region.Clock.Season == Season.Winter && region.Clock.DayOfSeason < 30)) && region.Clock.Hour == 12)
            {
                if (region.Clock.Day - region.LastWeatherDay >= 360 && region.World.Climate.WinterSeverity > 0.0 && region.Favor >= 80.0)
                {
                    region.CastWeather(Season.Winter, -0.4);
                    region.LastWeatherDay = region.Clock.Day;
                }
            }

            // 4. Small acts: theft omens & periodic signs scaled by faith stage (D4 owner decision 2026-10-13):
            // - Stage 1 (new god, few believers): ~1 a month (~30 days).
            // - Stage 2 (faithful band, believers >= 50%): ~24/yr across the region (~14 days).
            // Pacing scales smoothly from 30 days down to 14 days as mean adult belief grows.
            double t = System.Math.Clamp((meanReal - 0.25) / (0.60 - 0.25), 0.0, 1.0);
            long omenInterval = (long)System.Math.Round(30.0 - t * (30.0 - 14.0));
            if (region.Favor < 25.0) omenInterval = System.Math.Max(omenInterval, 20);
            if (region.Favor < 15.0) omenInterval = System.Math.Max(omenInterval, 30);

            foreach (var b in region.Bands)
            {
                if (region.Clock.Tick - b.LastWitnessedTheftTick <= 12)
                {
                    if (region.Favor >= 15.0 && region.Clock.Day - region.LastOmenSignDay >= 10)
                    {
                        region.CastOmen((int)b.Band.CampX, (int)b.Band.CampY, "A low rumble of celestial anger shook the stolen hearth", isGood: false);
                        region.LastOmenSignDay = region.Clock.Day;
                    }
                    b.LastWitnessedTheftTick = -99999;
                }
            }

            // Proactive sign if god has been quiet for longer than the pacing interval.
            // Cost is 5.0 favor. Keep a crisis reserve during Autumn/Winter (20.0) or Spring/Summer (10.0),
            // but if the god has been silent >= 30 days, cast whenever Favor >= 5.0 so faith never starves.
            double minFavorForProactiveOmen = (region.Clock.Day - region.LastOmenSignDay >= 30)
                ? 5.0
                : ((region.Clock.Season == Season.Autumn || region.Clock.Season == Season.Winter) ? 20.0 : 10.0);
            if (region.Clock.Hour == 12 && region.Clock.Day - region.LastOmenSignDay >= omenInterval && region.Favor >= minFavorForProactiveOmen)
            {
                var liveCamps = region.Bands.Where(b => b.Band.Members.Any(m => m.Alive)).ToList();
                if (liveCamps.Count > 0)
                {
                    // God favors camps that actively pray/thank/offer (creating natural variation in divine attention across camps)
                    Simulation camp = liveCamps[0];
                    double totalWeight = 0.0;
                    var weights = new double[liveCamps.Count];
                    for (int i = 0; i < liveCamps.Count; i++)
                    {
                        var c = liveCamps[i];
                        double w = 0.5 + c.ThisYear.PrayersAsked * 1.5 + c.ThisYear.FavorGain * 2.0;
                        weights[i] = w;
                        totalWeight += w;
                    }
                    double roll = region.GodRng.Range(0.0, totalWeight);
                    double acc = 0.0;
                    for (int i = 0; i < liveCamps.Count; i++)
                    {
                        acc += weights[i];
                        if (roll <= acc) { camp = liveCamps[i]; break; }
                    }

                    region.CastOmen((int)camp.Band.CampX, (int)camp.Band.CampY, isFaithfulBand ? "A celestial blessing bathed the camp" : "A radiant glow illuminated the morning sky", isGood: true);
                    region.LastOmenSignDay = region.Clock.Day;
                }
            }

            // 5. Blessed Birth when demographic support needed and band is established (large act, 40 favor, 1/generation limit):
            if (isFaithfulBand && region.Favor >= 65.0 && adultCount < 45 && region.Clock.Hour == 12)
            {
                var mother = region.People.FirstOrDefault(p => p.Alive && p.Sex == Sex.Female && p.Spouses.Count > 0 && p.AgeYears(region.Clock) >= 18 && p.AgeYears(region.Clock) <= 35 && region.CanCastBlessedBirth(p));
                if (mother != null)
                {
                    region.CastBlessedBirth(mother);
                }
            }
        }

        static void StepCharity(Region region)
        {
            // Responds to witnessed sharing within ~1 day (24 ticks)
            foreach (var b in region.Bands)
            {
                if (b.Band.Members.Count == 0) continue;
                if (region.Clock.Tick - b.LastWitnessedGiveTick <= 24)
                {
                    if (region.Favor >= 25.0 && region.Clock.Day - b.LastBountyDay >= 60)
                    {
                        bool isWinter = region.Clock.Season == Season.Winter;
                        region.CastBounty((int)b.Band.CampX, (int)b.Band.CampY, animals: isWinter);
                        b.LastBountyDay = region.Clock.Day;
                        b.LastWitnessedGiveTick = -99999;
                    }
                    else if (region.Favor >= 5.0 && region.Clock.Day - region.LastOmenSignDay >= 6)
                    {
                        region.CastOmen((int)b.Band.CampX, (int)b.Band.CampY, "A warm light settled over the sharing hearth", isGood: true);
                        region.LastOmenSignDay = region.Clock.Day;
                        b.LastWitnessedGiveTick = -99999;
                    }
                }
            }

            // Answers open prayers with signs
            var unhelped = region.ActivePrayers.FirstOrDefault(p => !p.Resolved && !p.PowerCasted);
            if (unhelped != null && region.Favor >= 15.0 && region.Clock.Day - region.LastOmenSignDay >= 8)
            {
                region.CastOmen(unhelped.CampX, unhelped.CampY, "A gentle sign answered the plea of generous hands", isGood: true);
                unhelped.LastSignDay = region.Clock.Day;
                region.LastOmenSignDay = region.Clock.Day;
            }

            // Soften harsh winter
            if ((region.Clock.Season == Season.Autumn || (region.Clock.Season == Season.Winter && region.Clock.DayOfSeason < 30)) && region.Clock.Hour == 12)
            {
                if (region.Clock.Day - region.LastWeatherDay >= 360 && region.World.Climate.WinterSeverity > 0.0 && region.Favor >= 80.0)
                {
                    region.CastWeather(Season.Winter, -0.4);
                    region.LastWeatherDay = region.Clock.Day;
                }
            }

            // Heal when unlocked (GodHelp >= 4.0)
            if (region.GodHelp >= 4.0 && region.Favor >= 65.0)
            {
                var sick = region.People.FirstOrDefault(p => p.Alive && p.Illness != null && p.Health < 0.55 && region.CanCastHeal(p));
                if (sick != null)
                {
                    region.CastHeal(sick);
                }
            }
        }

        static void StepDemanding(Region region)
        {
            // Responds to witnessed offerings within ~2 days (48 ticks)
            foreach (var b in region.Bands)
            {
                if (b.Band.Members.Count == 0) continue;
                if (region.Clock.Tick - b.LastWitnessedOfferTick <= 48)
                {
                    if (region.Favor >= 25.0 && region.Clock.Day - b.LastBountyDay >= 60)
                    {
                        bool isWinter = region.Clock.Season == Season.Winter;
                        region.CastBounty((int)b.Band.CampX, (int)b.Band.CampY, animals: isWinter);
                        b.LastBountyDay = region.Clock.Day;
                        b.LastWitnessedOfferTick = -99999;
                    }
                    else if (region.Favor >= 5.0 && region.Clock.Day - region.LastOmenSignDay >= 4)
                    {
                        region.CastOmen((int)b.Band.CampX, (int)b.Band.CampY, "Sacred smoke rose sweet into the calm sky", isGood: true);
                        region.LastOmenSignDay = region.Clock.Day;
                        b.LastWitnessedOfferTick = -99999;
                    }
                }
            }

            // Answers prayers if accompanied by offering
            var unhelped = region.ActivePrayers.FirstOrDefault(p => !p.Resolved && !p.PowerCasted && p.HasOffering);
            if (unhelped != null && region.Favor >= 15.0 && region.Clock.Day - region.LastOmenSignDay >= 6)
            {
                region.CastOmen(unhelped.CampX, unhelped.CampY, "The god accepted the offering and answered the plea", isGood: true);
                unhelped.LastSignDay = region.Clock.Day;
                region.LastOmenSignDay = region.Clock.Day;
            }

            // Hardship when offerings lapse (> 35 days without offering)
            if (region.Clock.Hour == 12)
            {
                foreach (var b in region.Bands)
                {
                    if (b.Band.Members.Count == 0) continue;
                    long lapse = b.LastOfferDay < 0 ? region.Clock.Day : (region.Clock.Day - b.LastOfferDay);
                    if (lapse > 35)
                    {
                        if (region.Favor >= 5.0 && region.Clock.Day - region.LastOmenSignDay >= 14)
                        {
                            region.CastOmen((int)b.Band.CampX, (int)b.Band.CampY, "A shadow fell over the silent altar; the god hungers for tribute", isGood: false);
                            region.LastOmenSignDay = region.Clock.Day;
                            break;
                        }
                    }
                }

                bool anyLapsed = region.Bands.Any(b => b.Band.Members.Count > 0 && (b.LastOfferDay < 0 ? region.Clock.Day : region.Clock.Day - b.LastOfferDay) > 35);
                if (anyLapsed && (region.Clock.Season == Season.Autumn || (region.Clock.Season == Season.Winter && region.Clock.DayOfSeason < 30)))
                {
                    if (region.Clock.Day - region.LastWeatherDay >= 360 && region.Favor >= 80.0)
                    {
                        region.CastWeather(Season.Winter, +0.3);
                        region.LastWeatherDay = region.Clock.Day;
                    }
                }
                else if (!anyLapsed && (region.Clock.Season == Season.Autumn || (region.Clock.Season == Season.Winter && region.Clock.DayOfSeason < 30)))
                {
                    if (region.Clock.Day - region.LastWeatherDay >= 360 && region.Favor >= 80.0 && region.World.Climate.WinterSeverity > 0.0)
                    {
                        region.CastWeather(Season.Winter, -0.3);
                        region.LastWeatherDay = region.Clock.Day;
                    }
                }
            }
        }

        static void StepMalevolent(Region region)
        {
            // Sends periodic wrathful omens, harsh seasons, and Lightning (when unlocked),
            // but holds back its wrath for ~30 days for any camp that makes an offering.
            if (region.Clock.Hour == 12)
            {
                var unappeasedBands = region.Bands
                    .Where(b => b.Band.Members.Count > 0 && (b.LastOfferDay < 0 || region.Clock.Day - b.LastOfferDay > 30))
                    .ToList();

                if (unappeasedBands.Count > 0)
                {
                    // Lightning when unlocked (GodHarm >= 4.0 and Favor >= 40.0)
                    if (region.GodHarm >= 4.0 && region.Favor >= 40.0 && region.Clock.Day - region.LastLightningDay >= 90)
                    {
                        var target = unappeasedBands
                            .SelectMany(b => b.Band.Members)
                            .Where(m => m.Alive && region.CanCastLightning(m))
                            .OrderByDescending(m => m.AgeYears(region.Clock))
                            .FirstOrDefault();
                        if (target != null)
                        {
                            region.CastLightning(target);
                        }
                    }

                    // Wrathful omens
                    if (region.Favor >= 5.0 && region.Clock.Day - region.LastOmenSignDay >= 16)
                    {
                        var camp = unappeasedBands[region.GodRng.Range(0, unappeasedBands.Count)];
                        region.CastOmen((int)camp.Band.CampX, (int)camp.Band.CampY, "Dark clouds gathered in malice over the negligent camp", isGood: false);
                        region.LastOmenSignDay = region.Clock.Day;
                    }

                    // Harsh winter
                    if ((region.Clock.Season == Season.Autumn || (region.Clock.Season == Season.Winter && region.Clock.DayOfSeason < 30))
                        && region.Clock.Day - region.LastWeatherDay >= 360 && region.Favor >= 80.0)
                    {
                        region.CastWeather(Season.Winter, +0.4);
                        region.LastWeatherDay = region.Clock.Day;
                    }
                }
            }

            // If an unhelped prayer included an offering, the malevolent god relents with a sign
            var unhelped = region.ActivePrayers.FirstOrDefault(p => !p.Resolved && !p.PowerCasted && p.HasOffering);
            if (unhelped != null && region.Favor >= 5.0 && region.Clock.Day - region.LastOmenSignDay >= 8)
            {
                region.CastOmen(unhelped.CampX, unhelped.CampY, "The offering placated the dark presence", isGood: true);
                unhelped.LastSignDay = region.Clock.Day;
                region.LastOmenSignDay = region.Clock.Day;
            }
        }

        static void StepStern(Region region)
        {
            // Strikes witnessed theft and violence within ~1 day (24 ticks)
            foreach (var b in region.Bands)
            {
                if (b.Band.Members.Count == 0) continue;
                bool recentAttack = region.Clock.Tick - b.LastWitnessedAttackTick <= 24;
                bool recentTheft = region.Clock.Tick - b.LastWitnessedTheftTick <= 24;

                if (recentAttack || recentTheft)
                {
                    var wrongdoer = b.LastWitnessedWrongdoer;
                    if (wrongdoer != null && wrongdoer.Alive && region.GodHarm >= 4.0 && region.CanCastLightning(wrongdoer) && region.Favor >= 40.0)
                    {
                        region.CastLightning(wrongdoer);
                        b.LastWitnessedAttackTick = -99999;
                        b.LastWitnessedTheftTick = -99999;
                        b.LastWitnessedWrongdoer = null;
                    }
                    else if (region.Favor >= 5.0 && region.Clock.Day - region.LastOmenSignDay >= 6)
                    {
                        string desc = recentAttack
                            ? "The skies roared in fury at bloodshed among kin"
                            : "A chill tremor shook the stolen hearth in divine anger";
                        region.CastOmen((int)b.Band.CampX, (int)b.Band.CampY, desc, isGood: false);
                        region.LastOmenSignDay = region.Clock.Day;
                        b.LastWitnessedAttackTick = -99999;
                        b.LastWitnessedTheftTick = -99999;
                        b.LastWitnessedWrongdoer = null;
                    }
                }
            }

            // Affirming omens when camps have kept the peace (no witnessed violence/theft for >= 60 days)
            if (region.Clock.Hour == 12 && region.Clock.Day - region.LastOmenSignDay >= 30 && region.Favor >= 15.0)
            {
                var peacefulBands = region.Bands
                    .Where(b => b.Band.Members.Count > 0 &&
                                (region.Clock.Tick - b.LastWitnessedAttackTick > 60 * 24) &&
                                (region.Clock.Tick - b.LastWitnessedTheftTick > 60 * 24))
                    .ToList();
                if (peacefulBands.Count > 0)
                {
                    var camp = peacefulBands[region.GodRng.Range(0, peacefulBands.Count)];
                    region.CastOmen((int)camp.Band.CampX, (int)camp.Band.CampY, "A serene sky watched peacefully over the tranquil camp", isGood: true);
                    region.LastOmenSignDay = region.Clock.Day;
                }
            }
        }

        static void StepCapricious(Region region)
        {
            // Acts at random intervals (GodRng) with random good/bad omens, bounties, and weather
            if (region.Clock.Hour == 12 && region.GodRng.Chance(0.06))
            {
                var liveBands = region.Bands.Where(b => b.Band.Members.Count > 0).ToList();
                if (liveBands.Count == 0) return;
                var camp = liveBands[region.GodRng.Range(0, liveBands.Count)];

                double roll = region.GodRng.Range(0.0, 1.0);
                if (roll < 0.20 && region.Favor >= 25.0 && region.Clock.Day - camp.LastBountyDay >= 45)
                {
                    bool animals = region.GodRng.Chance(0.5);
                    region.CastBounty((int)camp.Band.CampX, (int)camp.Band.CampY, animals: animals);
                    camp.LastBountyDay = region.Clock.Day;
                }
                else if (roll < 0.35 && region.Favor >= 80.0 && region.Clock.Day - region.LastWeatherDay >= 180)
                {
                    double delta = region.GodRng.Chance(0.5) ? 0.3 : -0.3;
                    region.CastWeather(region.Clock.Season, delta);
                    region.LastWeatherDay = region.Clock.Day;
                }
                else if (region.Favor >= 5.0 && region.Clock.Day - region.LastOmenSignDay >= 6)
                {
                    bool isGood = region.GodRng.Chance(0.5);
                    string desc = isGood
                        ? "A strange radiant glimmer danced across the noon sky"
                        : "A sudden eerie shadow darkened the noon sun";
                    region.CastOmen((int)camp.Band.CampX, (int)camp.Band.CampY, desc, isGood: isGood);
                    region.LastOmenSignDay = region.Clock.Day;
                }
            }
        }
    }
}
