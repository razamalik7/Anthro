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
        Random
    }

    /// <summary>
    /// Autonomous bot god policies for headless simulation, testing, and comparative analysis (T7).
    /// Policies:
    /// - None: No divine intervention; baseline prayer fading.
    /// - Kind: Answers prayers with bounties/relief, eases harsh winters, fosters trust and survival.
    /// - Cruel: Ignores pleas for relief, displays terrifying omens and harshens winters, breeds fear and fear offerings.
    /// - Random: Capricious, mixed benevolence and wrath.
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
                    var camp = region.Bands[region.Rng.Range(0, region.Bands.Count)];
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
        }

        static void StepCruel(Region region)
        {
            // Cruel god ignores pleas for food/relief.
            // When people plead, the cruel god may mock them with terrifying omens.
            var unhelped = region.ActivePrayers.FirstOrDefault(p => !p.Resolved && !p.PowerCasted);
            if (unhelped != null)
            {
                unhelped.PowerCasted = true;
                if (region.Favor >= 5 && region.Rng.Chance(0.5))
                {
                    region.CastOmen(unhelped.CampX, unhelped.CampY, "The heavens split with wrathful thunder");
                }
            }

            // Periodic displays of divine terror (every 10 days) to keep fear alive
            if (region.Clock.Hour == 18 && region.Clock.Day % 10 == 0 && region.Favor >= 5)
            {
                if (region.Bands.Count > 0)
                {
                    var camp = region.Bands[region.Rng.Range(0, region.Bands.Count)];
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
        }

        static void StepRandom(Region region)
        {
            var unhelped = region.ActivePrayers.FirstOrDefault(p => !p.Resolved && !p.PowerCasted);
            if (unhelped != null)
            {
                unhelped.PowerCasted = true;
                if (region.Rng.Chance(0.5))
                {
                    // Kind
                    if (region.Favor >= 20)
                        region.CastBounty(unhelped.CampX, unhelped.CampY, animals: region.Rng.Chance(0.5));
                }
                else
                {
                    // Cruel
                    if (region.Favor >= 5 && region.Rng.Chance(0.5))
                        region.CastOmen(unhelped.CampX, unhelped.CampY, "A strange, eerie glow crosses the heavens");
                }
            }
        }
    }
}
