using System;
using System.Collections.Generic;
using System.Linq;
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
        public const string GodOfferTopic = "god:offer";
        public const string GodThankTopic = "god:thank";

        public static string GodActTopic(string act) => $"god:{act.ToLowerInvariant()}";
        public static string GodActTopic(Act act) => $"god:{act.ToString().ToLowerInvariant()}";

        public static double GodReal(Person p) => p.Expect.Get(GodRealTopic, 0.2);
        public static double GodAnswers(Person p) => p.Expect.Get(GodAnswersTopic, 0.1);
        public static double GodKind(Person p) => p.Expect.Get(GodKindTopic, 0.0);
        public static double GodOffer(Person p) => GodAct(p, Act.Offer);
        public static double GodThank(Person p) => p.Expect.Get(GodThankTopic, 0.0);
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

            // B4 (owner decision 2026-10-12): Seed god:offer per people with small variation
            // (some start with a weak offering habit, most with none), the way customs are seeded in Customs.SeedCustoms.
            // From GodRng.
            double baseOffer = rng.Range(0.0, 1.0) < 0.25 ? rng.Range(0.05, 0.15) : 0.0;
            if (baseOffer > 0.0)
            {
                Region.OfferingsFromSeedCount++;
                foreach (var p in group)
                {
                    double indOffer = Math.Max(0.0, baseOffer + rng.Normal(0, 0.02));
                    p.Expect.Absorb(GodOfferTopic, indOffer, 1, 0);
                }
            }
            // D1 (owner decision 2026-10-13): Seed god:thank per people with variation
            double baseThank = rng.Range(0.0, 1.0) < 0.35 ? rng.Range(0.04, 0.12) : 0.01;
            foreach (var p in group)
            {
                double indThank = Math.Max(0.0, baseThank + rng.Normal(0, 0.02));
                p.Expect.Absorb(GodThankTopic, indThank, 1, 0);
            }
        }

        /// <summary>
        /// Faith fades without signs: god:real relaxes slowly toward its baseline seed (~0.2).
        /// Offerings that go unanswered erode slowly, like prayers.
        /// Tiny daily adjustment in the daily person update.
        /// </summary>
        public void FadeBelief(Person p)
        {
            if (Off("god") || Off("belief")) return;
            p.Expect.Absorb(GodRealTopic, 0.2, 0.001, 0.2);

            // Offerings that go unanswered erode slowly, like prayers (T10 A3: no 14-day window)
            double offerBelief = GodOffer(p);
            if (offerBelief > 0)
            {
                // Baseline slow decay if unfed by signs
                double decay = 0.005;
                // Faster erosion if an offering was made and went unanswered (no sign has arrived since)
                if (p.OwnDeeds.Any(d => d.Deed.Act == Act.Offer && d.Deed.Day > p.LastGodSignDay && Clock.Day > d.Deed.Day))
                    decay = 0.02;
                else if (p.Deeds.Any(d => d.Deed.Act == Act.Offer && d.Deed.Day > p.LastGodSignDay && Clock.Day > d.Deed.Day))
                    decay = 0.01;

                p.Expect.Absorb(GodOfferTopic, 0.0, decay, offerBelief);
            }

            // D1: Everyday devotion decays slowly if unfed by signs/fortune
            double thankBelief = GodThank(p);
            if (thankBelief > 0)
            {
                p.Expect.Absorb(GodThankTopic, 0.0, 0.005, thankBelief);
            }

            // Learning from relief (T10 A2): dread fades continuously; easing dread credits appeasement
            if (!Off("relief") && p.Dread > 0)
            {
                double prevDread = p.Dread;
                double dreadDecay = (p.Health >= 0.7 && p.Satiety >= 0.5) ? 0.05 : 0.02;
                p.Dread = Math.Max(0.0, p.Dread - dreadDecay);
                double drop = prevDread - p.Dread;

                if (drop > 0 && !string.IsNullOrEmpty(p.AppeasementAct))
                {
                    double cred = p.M(MindTrait.Credulity);
                    double real = GodReal(p);
                    double reliefWeight = 0.20 * (0.4 + cred * 0.8) * Math.Max(0.2, real) * drop;

                    if (p.AppeasementAct == "offer")
                    {
                        p.Expect.Absorb(GodOfferTopic, 1.0, reliefWeight, p.Expect.Get(GodOfferTopic, 0.0));
                        string actTopic = GodActTopic(Act.Offer);
                        p.Expect.Absorb(actTopic, 1.0, reliefWeight, p.Expect.Get(actTopic, 0.0));
                    }
                    else if (p.AppeasementAct == "pray")
                    {
                        p.Expect.Absorb(GodAnswersTopic, 1.0, reliefWeight, GodAnswers(p));
                    }
                    else
                    {
                        string topic = $"god:{p.AppeasementAct.ToLowerInvariant()}";
                        p.Expect.Absorb(topic, 1.0, reliefWeight, p.Expect.Get(topic, 0.0));
                    }
                }

                if (p.Dread <= 0.01)
                {
                    p.Dread = 0.0;
                    p.DreadPeak = 0.0;
                    p.AppeasementAct = null;
                    p.AppeasementDread = 0.0;
                }
            }
        }
    }
}
