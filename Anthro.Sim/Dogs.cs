using System;
using System.Linq;
using Anthro.Sim.Core;
using Anthro.Sim.Ecology;
using Anthro.Sim.People;
using Anthro.Sim.World;

namespace Anthro.Sim
{
    /// <summary>
    /// Dogs: real animals a band keeps, not just something it knows. Pups come from wolves living near
    /// camp, so only bands that have learned to raise them (dog.pup) and have wolves about can start;
    /// with camp dogs (dog.camp) they breed. Dogs live on scraps and food that would go bad, go hungry
    /// and die in lean times (or are eaten: a dog is a few days' food), keep wolves off, wake the camp,
    /// and help hunters track (dog.hunt).
    /// </summary>
    public sealed partial class Simulation
    {
        /// <summary>
        /// What a dog eats in a day, in rations: a 15-20 kg dog needs roughly a third of a person's food.
        /// The meat on one if it comes to that: about eight rations.
        /// </summary>
        const double DogNeed = 0.3, DogMeat = 8;

        /// <summary>
        /// A day of the band's dogs. They eat first from what went bad today and the scraps of what
        /// people ate, then (if people can spare it) from the camp's food; a hungry dog weakens and may
        /// die. People starving with dogs about eat them.
        /// </summary>
        void DailyDogs(double spoiledToday)
        {
            if (Band.Dogs == 0)
            {
                // Taking in pups: someone who knows how, and wolves denning nearby.
                // Someone who knows how to raise them and cares for animals takes in pups, if the camp
                // can spare the scraps to keep them.
                var keen = Band.Members.Where(m => m.Knows.Contains("dog.pup")).Select(m => m.I(Interest.Animals)).DefaultIfEmpty(0).Max();
                double spare = Math.Min(1, Band.FoodTotal / Math.Max(1, Band.Members.Count * 0.9) / 4);
                if (Clock.DayOfYear == 60 && keen > 0 && WolvesNear(WorldMap.FromMeters(6000)) && Rng.Chance(keen * spare))
                {
                    Band.Dogs = Rng.Range(2, 5);   // a litter
                    Chronicle.Write(Clock, Importance.Notable, $"Wolf pups were brought into camp to be raised.");
                }
                return;
            }
            double need = Band.Dogs * DogNeed;
            // Scraps: what went bad today, and the bones and offal of what people ate.
            double scraps = spoiledToday * 0.5 + Band.Members.Count * 0.05;
            double fed = Math.Min(need, scraps);
            if (fed < need)
            {
                // Fed from the camp's own food only while people are doing well enough.
                int people = Math.Max(1, Band.Members.Count);
                double days = Band.FoodTotal / (people * 0.9);
                if (days > 4)
                {
                    var richest = Band.Households.OrderByDescending(h => h.Food.Total).First();
                    fed += richest.Food.Take(Math.Min(need - fed, richest.Food.Total * 0.05), out _);
                }
            }
            Band.DogsFed += (fed / need - Band.DogsFed) * 0.1;
            // Hunger and age: a starving dog dies within weeks; a dog lives ten years or so.
            double dying = (Band.DogsFed < 0.4 ? (0.4 - Band.DogsFed) * 0.05 : 0) + 0.1 / SimClock.DaysPerYear;
            int died = Math.Min(Band.Dogs, Rng.Poisson(Band.Dogs * dying));
            Band.Dogs -= died;
            // Camp dogs breed in spring, as many pups surviving as there's food for.
            if (Clock.DayOfYear == 60 && Band.AnyoneKnows("dog.camp") && Band.Dogs >= 2)
                // Half-starved bitches raise few pups or none.
                Band.Dogs += Rng.Poisson(Band.Dogs * 0.6 * Math.Max(0, (Band.DogsFed - 0.4) / 0.6));
            // People starving with dogs about eat them.
            if (Band.Dogs > 0 && _hardship > 0.5 && Band.FoodTotal < Band.Members.Count * 0.3)
            {
                Band.Dogs--;
                var hungriest = Band.Households.Where(h => h.Members.Count > 0).OrderBy(h => h.DaysOfFood).FirstOrDefault();
                hungriest?.Food.AddMeat(DogMeat);
                if (Band.Dogs == 0) Chronicle.Write(Clock, Importance.Notable, "Hunger drove the band to eat the last of its dogs.");
            }
        }

        /// <summary>Whether anyone in camp has seen wolves near it lately.</summary>
        bool WolvesNear(double radius) =>
            Band.Members.Any(m => m.Map.Game.Values.Any(s => s.Species.Predator && Clock.Day - s.Day < 30
                                                             && WorldMap.Distance(s.X, s.Y, Band.CampX, Band.CampY) < radius));

        /// <summary>How much the band's dogs add to keeping a camp safe: each dog is another pair of ears, up to about double.</summary>
        internal double DogGuard => 1 + Math.Min(1, Band.Dogs * 0.08);
    }
}
