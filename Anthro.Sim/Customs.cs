using System;
using System.Collections.Generic;
using System.Linq;
using Anthro.Sim.People;

namespace Anthro.Sim
{
    /// <summary>
    /// Customs a people holds about everyday life, each person's own version of them, learned from what
    /// happens and spread by talk like any belief. They start wherever a people's history left them (a
    /// random seed per people) and drift with experience:
    /// <list type="bullet">
    /// <item>rationing: how hard a family budgets its stores in a lean time (0 not at all, 1 strictly);
    /// running out with stores that could have been spread teaches it; pinching that left a surplus or
    /// watched the store rot unteaches it (see LearnRationing).</item>
    /// <item>demand: how freely people ask others for food (0 only when desperate, 1 any time they're
    /// short, as in demand-sharing peoples); asking that's answered teaches it, refusal unteaches it.</item>
    /// <item>elders: how much the old are listened to in camp decisions (0 not specially, 2 a great deal);
    /// moves the old pushed that paid off teach it (see LearnElders).</item>
    /// <item>hunting: how much standing a hunter wins by hunting (0 none, 1 hunting is what makes a name).
    /// Purely cultural: inherited and spread by talk. (Tying it to the share of food hunting brings was
    /// tried and fed on itself: less prestige, less hunting, less meat, less prestige, down in every world.)</item>
    /// <item>sides: whether people take sides in others' quarrels (0 it's their business, 1 kin and friends
    /// pile in): what turns quarrels into brawls and feuds between families.</item>
    /// <item>duel: whether a grudge is settled one on one, others watching and keeping out (0 never, 1 the
    /// accepted way); how far a duel goes is the people's sense of how far violence goes.</item>
    /// </list>
    /// </summary>
    public sealed partial class Simulation
    {
        static readonly HashSet<string> _off = new HashSet<string>((Environment.GetEnvironmentVariable("ANTHRO_OFF") ?? "").Split(','));
        /// <summary>For experiments only: whether a feature has been switched off (ANTHRO_OFF).</summary>
        internal static bool Off(string feature) => _off.Contains(feature);

        internal const string RationTopic = "norm:rationing", DemandTopic = "norm:demand", EldersTopic = "norm:elders", HuntingTopic = "norm:hunting";
        internal const string SidesTopic = "norm:sides", DuelTopic = "norm:duel";
        internal static double Sides(Person p) => p.Expect.Get(SidesTopic, 0.4);
        internal static double Duel(Person p) => p.Expect.Get(DuelTopic, 0.2);
        internal static double Rationing(Person p) => p.Expect.Get(RationTopic, 0.6);
        internal static double Demand(Person p) => p.Expect.Get(DemandTopic, 0.3);
        internal static double Elders(Person p) => p.Expect.Get(EldersTopic, 1);
        internal static double HuntingPrestige(Person p) => p.Expect.Get(HuntingTopic, 0.4);

        /// <summary>
        /// The rationing custom is judged by the seasons it had a say in, by what a family could see it do:
        /// <list type="bullet">
        /// <item>They had stores to spread, budgeted, and still ran out and went hungry: pinch harder. As
        /// strong as what they had to spread (with next to nothing put by, pinching couldn't have saved
        /// them, and they don't blame it).</item>
        /// <item>They pinched and still have more than the lean time ahead calls for: they went hungry for
        /// nothing, as strong as the surplus is to what they held back. (Autumn food kept for winter is
        /// not a surplus.)</item>
        /// <item>They pinched and watched the store rot: food held back rots like the rest, so as strong as
        /// the share of the store that went bad.</item>
        /// <item>They budgeted and came through without running out or waste: it worked; nothing to change.</item>
        /// </list>
        /// So the custom settles where each world's food puts it: low where there's little to keep, higher
        /// with good storage and hard winters. A season with no budgeting to do teaches nothing about it.
        /// </summary>
        void LearnRationing(Person p)
        {
            var h = p.Home;
            if (Off("rationlearn"))
            {
                // The old rule: any hard season teaches rationing; stores rotting in a good one unteaches it.
                if (p.WinterLow < 0.5) p.Expect.Absorb(RationTopic, 1, 0.1, Rationing(p));
                else if (h != null && p.WinterLow > 0.8 && h.StoresSpoiledRecent > h.Members.Count * 2) p.Expect.Absorb(RationTopic, 0, 0.05, Rationing(p));
                return;
            }
            if (h == null || !h.HadToBudget) { ThisYear.RationNone++; return; }
            int mouths = Math.Max(1, h.Members.Count);
            if (h.RanOut && p.WinterLow < 0.5)
            {
                double hadToSpread = Math.Min(1, h.BudgetStart / (mouths * 0.9 * Core.SimClock.DaysPerSeason));
                if (hadToSpread > 0) { p.Expect.Absorb(RationTopic, 1, 0.1 * hadToSpread, Rationing(p)); ThisYear.RationUp++; }
                else ThisYear.RationHeld++;
                return;
            }
            if (h.HeldBack <= 0) { ThisYear.RationHeld++; return; }
            double surplus = Math.Max(0, Activities.Decider.Keeping(this, h) - mouths * 0.9 * Activities.Decider.LeanDays(this, p));
            double tooStrict = Math.Max(Math.Min(1, surplus / h.HeldBack), Math.Min(1, h.StoresRotted * Core.SimClock.DaysPerSeason / Math.Max(1, h.StoresKept)));
            if (tooStrict > 0) { p.Expect.Absorb(RationTopic, 0, 0.1 * tooStrict, Rationing(p)); ThisYear.RationDown++; }
            else ThisYear.RationHeld++;
        }

        /// <summary>
        /// Whether the old are worth heeding is judged by moves that happened: where the old pushed a move
        /// more than the young did and it paid off (or the young carried one the old were against and it
        /// went badly), people come to give the old more say; the other way round, less. Peoples whose
        /// elders remember the land well learn to defer to them. (A move that was voted down shows nothing.)
        /// </summary>
        static void LearnElders(Person p, double outcome)
        {
            if (Off("elderslearn") || p.EldersTiltOnMove == 0) return;
            double signal = outcome * Math.Max(-1, Math.Min(1, p.EldersTiltOnMove));
            p.Expect.Absorb(EldersTopic, signal > 0 ? 2 : 0, 0.1 * Math.Abs(signal), Elders(p));
        }

        /// <summary>A new season's budgeting starts fresh (after everyone has drawn the last one's lesson).</summary>
        static readonly bool _debugRation = Environment.GetEnvironmentVariable("ANTHRO_DEBUG_RATION") != null;
        void NewBudgetSeason()
        {
            if (_debugRation)
                foreach (var h in Band.Households.Where(h => h.Members.Count > 0))
                    Console.Error.WriteLine($"RATION {(Core.Season)(((int)Clock.Season + 3) % 4)} budget {(h.HadToBudget ? 1 : 0)} ranout {(h.RanOut ? 1 : 0)} held {h.HeldBack / h.Members.Count:F1} rotted {h.StoresRotted / h.Members.Count:F1} kept {h.StoresKept / Core.SimClock.DaysPerSeason / h.Members.Count:F1} daysleft {h.DaysOfFood:F1} fat {h.Members.Average(m => m.Reserve):F2}");
            foreach (var h in Band.Households) { h.HadToBudget = h.RanOut = false; h.BudgetStart = h.HeldBack = h.StoresRotted = h.StoresKept = 0; }
        }

        void SeedCustoms(List<Person> group)
        {
            double ration = Inheritance.Clamp01(Rng.Normal(0.6, 0.2)), demand = Inheritance.Clamp01(Rng.Normal(0.3, 0.2));
            double elders = Math.Max(0, Rng.Normal(1, 0.4)), hunting = Inheritance.Clamp01(Rng.Normal(0.4, 0.2));
            double sides = Inheritance.Clamp01(Rng.Normal(0.4, 0.25)), duel = Inheritance.Clamp01(Rng.Normal(0.2, 0.25));
            foreach (var p in group)
            {
                p.Expect.Absorb(RationTopic, Inheritance.Clamp01(ration + Rng.Normal(0, 0.05)), 1, 0);
                p.Expect.Absorb(DemandTopic, Inheritance.Clamp01(demand + Rng.Normal(0, 0.05)), 1, 0);
                p.Expect.Absorb(EldersTopic, Math.Max(0, elders + Rng.Normal(0, 0.1)), 1, 0);
                p.Expect.Absorb(HuntingTopic, Inheritance.Clamp01(hunting + Rng.Normal(0, 0.05)), 1, 0);
                p.Expect.Absorb(SidesTopic, Inheritance.Clamp01(sides + Rng.Normal(0, 0.05)), 1, 0);
                p.Expect.Absorb(DuelTopic, Inheritance.Clamp01(duel + Rng.Normal(0, 0.05)), 1, 0);
            }
        }
    }
}
