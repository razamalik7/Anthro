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
        Skilful
    }

    /// <summary>
    /// Autonomous bot god policies for headless simulation, testing, and comparative analysis (T7, T9C).
    /// Policies:
    /// - None: No divine intervention; baseline prayer fading.
    /// - Kind: Answers prayers with bounties/relief, eases harsh winters, fosters trust, heals when unlocked.
    /// - Cruel: Ignores pleas for relief, displays terrifying omens and harshens winters, strikes lightning when unlocked.
    /// - Random: Capricious, mixed benevolence and wrath.
    /// - Skilful: Intervenes at the right moments (harsh winter mitigation, starving camp relief, theft omens, sparing blessings, heal).
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

            // 1. Weather before a hard winter (large act: ~once a year or rarer):
            // In late autumn (DayOfSeason >= 75), if winter severity is harsh (> 0.8), soften it once per year.
            // Requires 75 favor to preserve a 15-favor cushion (never zeroing out the pool).
            if (region.Clock.Season == Season.Autumn && region.Clock.DayOfSeason >= 75 && region.Clock.Hour == 12)
            {
                if (region.Clock.Day - region.LastWeatherDay >= 360 && region.World.Climate.WinterSeverity > 0.8 && region.Favor >= 75)
                {
                    region.CastWeather(Season.Winter, -0.4);
                    region.LastWeatherDay = region.Clock.Day;
                }
            }

            // 2. An omen right after a witnessed theft (small act: weekly/biweekly pace):
            // Discourages theft and restores moral order with a stern sign.
            foreach (var b in region.Bands)
            {
                if (region.Clock.Tick - b.LastWitnessedTheftTick <= 12)
                {
                    if (region.Favor >= 5)
                    {
                        region.CastOmen((int)b.Band.CampX, (int)b.Band.CampY, "A low rumble of celestial anger shook the stolen hearth", isGood: false);
                    }
                    b.LastWitnessedTheftTick = -99999;
                }
            }

            // 3. Food when a camp is genuinely starving (medium act: a few times a year):
            var unhelped = region.ActivePrayers.FirstOrDefault(p => !p.Resolved && !p.PowerCasted);
            if (unhelped != null)
            {
                if (unhelped.Need == PrayerNeed.Hunger)
                {
                    bool campStarving = unhelped.Asker != null && (unhelped.Asker.Satiety < 0.25 || (unhelped.Asker.Home?.Food.Total ?? 0) < 1.0);
                    var camp = region.Bands.FirstOrDefault(b => b.Band.CampX == unhelped.CampX && b.Band.CampY == unhelped.CampY);
                    bool canBounty = camp == null || (region.Clock.Day - camp.LastBountyDay >= 90);

                    if (campStarving && canBounty)
                    {
                        bool isWinter = region.Clock.Season == Season.Winter;
                        // Require extra favor cushion (favor >= cost + 15)
                        if (isWinter && region.Favor >= 45)
                        {
                            region.CastBounty(unhelped.CampX, unhelped.CampY, animals: true);
                            unhelped.PowerCasted = true;
                            if (camp != null) camp.LastBountyDay = region.Clock.Day;
                        }
                        else if (!isWinter && region.Favor >= 35)
                        {
                            region.CastBounty(unhelped.CampX, unhelped.CampY, animals: false);
                            unhelped.PowerCasted = true;
                            if (camp != null) camp.LastBountyDay = region.Clock.Day;
                        }
                    }
                }
                else if (unhelped.Need == PrayerNeed.Sickness)
                {
                    if (region.CanCastHeal(unhelped.Asker) && region.Favor >= 65)
                    {
                        region.CastHeal(unhelped.Asker);
                        unhelped.PowerCasted = true;
                    }
                    else if (region.Favor >= 5)
                    {
                        region.CastOmen(unhelped.CampX, unhelped.CampY, "A gentle sign touches the sick", isGood: true);
                        unhelped.PowerCasted = true;
                    }
                }
            }

            // Also check camps without open prayers if in acute famine (medium act, max once/season)
            if (region.Favor >= 45 && region.Clock.Hour == 12)
            {
                var acuteCamp = region.Bands.FirstOrDefault(b => b.Band.FoodTotal < b.Band.Members.Count * 0.5 && b.Band.Members.Any(m => m.Reserve < 0.25) && (region.Clock.Day - b.LastBountyDay >= 90));
                if (acuteCamp != null)
                {
                    region.CastBounty((int)acuteCamp.Band.CampX, (int)acuteCamp.Band.CampY, animals: region.Clock.Season == Season.Winter);
                    acuteCamp.LastBountyDay = region.Clock.Day;
                }
            }

            // 4. Heal directly if someone is dying of sickness and Heal is unlocked (large act: rare)
            if (region.Favor >= 65)
            {
                var dying = region.People.FirstOrDefault(p => p.Alive && p.Illness != null && p.Health < 0.35 && region.CanCastHeal(p));
                if (dying != null)
                {
                    region.CastHeal(dying);
                }
            }

            // 5. Blessings sparingly (large act: once every ~12 years = 4320 days)
            if (region.Favor >= 55 && region.Clock.Day % 4320 == 0 && region.Clock.Hour == 12)
            {
                var mother = region.People.FirstOrDefault(p => p.Alive && p.Sex == Sex.Female && p.Spouses.Count > 0 && !p.BlessedNextBirth && p.AgeYears(region.Clock) >= 18 && p.AgeYears(region.Clock) <= 32);
                if (mother != null)
                {
                    region.CastBlessedBirth(mother);
                }
            }
        }
    }
}
