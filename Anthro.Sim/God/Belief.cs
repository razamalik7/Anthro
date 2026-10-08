using System;
using System.Collections.Generic;
using Anthro.Sim.People;

namespace Anthro.Sim
{
    /// <summary>
    /// Belief state for the god layer (Phase 1, T1). Belief lives in individual people as learned topics
    /// (Expectations), spreads by conversation, is inherited mother -> child and across kin camps,
    /// and slowly decays toward its baseline seed when unfed by signs.
    /// </summary>
    public sealed partial class Simulation
    {
        public const string GodRealTopic = "god:real";
        public const string GodAnswersTopic = "god:answers";
        public const string GodKindTopic = "god:kind";

        public static string GodActTopic(string act) => $"god:{act.ToLowerInvariant()}";
        public static string GodActTopic(Act act) => $"god:{act.ToString().ToLowerInvariant()}";

        public static double GodReal(Person p) => p.Expect.Get(GodRealTopic, 0.2);
        public static double GodAnswers(Person p) => p.Expect.Get(GodAnswersTopic, 0.1);
        public static double GodKind(Person p) => p.Expect.Get(GodKindTopic, 0.0);
        public static double GodAct(Person p, string act) => p.Expect.Get(GodActTopic(act), 0.0);
        public static double GodAct(Person p, Act act) => p.Expect.Get(GodActTopic(act), 0.0);
        public static bool IsBeliever(Person p) => GodReal(p) > 0.5;

        /// <summary>
        /// Seed beliefs for a people (vague spirits, low faith).
        /// </summary>
        void SeedBelief(List<Person> group)
        {
            if (Off("god") || Off("belief")) return;
            var rng = Region.GodRng;
            double baseReal = Inheritance.Clamp01(rng.Normal(0.2, 0.1));
            double baseAnswers = Inheritance.Clamp01(rng.Normal(0.1, 0.05));
            double baseKind = Math.Max(-1, Math.Min(1, rng.Normal(0.0, 0.1)));

            foreach (var p in group)
            {
                p.Expect.Absorb(GodRealTopic, Inheritance.Clamp01(baseReal + rng.Normal(0, 0.05)), 1, 0);
                p.Expect.Absorb(GodAnswersTopic, Inheritance.Clamp01(baseAnswers + rng.Normal(0, 0.02)), 1, 0);
                p.Expect.Absorb(GodKindTopic, Math.Max(-1, Math.Min(1, baseKind + rng.Normal(0, 0.05))), 1, 0);
            }
        }

        /// <summary>
        /// Faith fades without signs: god:real relaxes slowly toward its baseline seed (~0.2).
        /// Tiny daily adjustment (0.001) in the daily person update.
        /// </summary>
        void FadeBelief(Person p)
        {
            if (Off("god") || Off("belief")) return;
            p.Expect.Absorb(GodRealTopic, 0.2, 0.001, 0.2);
        }
    }
}
